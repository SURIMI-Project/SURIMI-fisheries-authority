using Eii.BlobStore;
using EwECore.MSE;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI.Datamodel;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class QuotaCalculationServiceTests
    {
        private const string ScenarioName = "test-scenario";

        private static IBlobStore CreateQuotaShareBlobStore(string csvContent)
        {
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync($"{ScenarioName}-quotashare.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            blobStore
                .Setup(bs => bs.ReadAllTextAsync($"{ScenarioName}-quotashare.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(csvContent);
            return blobStore.Object;
        }

        private static IBlobStore CreateMatchingQuotaShareBlobStore()
        {
            // Matches the species and fleets of CreateContract exactly
            return CreateQuotaShareBlobStore(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;0,75;0,25;1\n" +
                "KHE;;0,5;0,5;1\n");
        }

        private static IBlobStore CreateRecruitmentBlobStore(string csvContent)
        {
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync($"{ScenarioName}-recruitment.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            blobStore
                .Setup(bs => bs.ReadAllTextAsync($"{ScenarioName}-recruitment.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(csvContent);
            return blobStore.Object;
        }

        private static IBlobStore CreateMatchingRecruitmentBlobStore()
        {
            // Matches the species of CreateContract exactly
            return CreateRecruitmentBlobStore(
                "species_code;life_stage;RstockRatio;RHalfB0Ratio;cvRec\n" +
                "PIL;;0,893;0,2;0,8\n" +
                "KHE;;0,7134952;0,25;0,9\n");
        }

        private static (QuotaCalculationService Service, Mock<IMSEStockRecruitment> StockRecruitment, Mock<IMSEQuotaCalculator> QuotaCalculator) CreateServiceWithMocks()
            => CreateServiceWithMocks(CreateMatchingRecruitmentBlobStore());

        private static (QuotaCalculationService Service, Mock<IMSEStockRecruitment> StockRecruitment, Mock<IMSEQuotaCalculator> QuotaCalculator) CreateServiceWithMocks(IBlobStore recruitmentBlobStore)
        {
            var stockRecruitment = new Mock<IMSEStockRecruitment>();
            var quotaCalculator = new Mock<IMSEQuotaCalculator>();
            // nGroups + 1 elements for 1-based EwECore indexing
            quotaCalculator.Setup(qc => qc.UpdateQuotas()).Returns(new float[3]);

            var service = new QuotaCalculationService(
                NullLogger<QuotaCalculationService>.Instance,
                stockRecruitment.Object,
                quotaCalculator.Object,
                new QuotaShareLoader(CreateMatchingQuotaShareBlobStore(), NullLogger<QuotaShareLoader>.Instance),
                new RecruitmentLoader(recruitmentBlobStore, NullLogger<RecruitmentLoader>.Instance));

            return (service, stockRecruitment, quotaCalculator);
        }

        private static QuotaCalculationService CreateService() => CreateServiceWithMocks().Service;

        private static SurimiContract CreateContract()
        {
            return new SurimiContract
            {
                Items = new Items
                {
                    Species =
                    [
                        new Species { SpeciesCode = "PIL", LifeStage = "" },
                        new Species { SpeciesCode = "KHE", LifeStage = "" }
                    ],
                    FleetSegments =
                    [
                        new FleetSegment { GearCode = "ART", CountryCode = "ESP" },
                        new FleetSegment { GearCode = "OTB", CountryCode = "ESP" }
                    ]
                }
            };
        }

        private static RegulationDefinitionsSummary CreateRegulationSummary()
        {
            return new RegulationDefinitionsSummary
            {
                TargetFishingMortalities =
                [
                    new TargetFishingMortality
                    {
                        Species = new Species { SpeciesCode = "PIL", LifeStage = "" },
                        BiomassLimit = 1.0,
                        BiomassBase = 2.0,
                        FMax = 0.05
                    },
                    new TargetFishingMortality
                    {
                        Species = new Species { SpeciesCode = "KHE", LifeStage = "" },
                        BiomassLimit = 3.0,
                        BiomassBase = 4.0,
                        FMax = 0.1
                    }
                ]
            };
        }

        private static async Task<QuotaCalculationService> CreateInitialisedServiceAsync()
        {
            var (service, _, _) = await CreateInitialisedServiceWithMocksAsync();
            return service;
        }

        private static async Task<(QuotaCalculationService Service, Mock<IMSEStockRecruitment> StockRecruitment, Mock<IMSEQuotaCalculator> QuotaCalculator)> CreateInitialisedServiceWithMocksAsync()
        {
            var (service, stockRecruitment, quotaCalculator) = CreateServiceWithMocks();
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            await service.CreateRegulationsAsync(CreateRegulationSummary());
            return (service, stockRecruitment, quotaCalculator);
        }

        private static BiomassGrid CreateBiomassGrid(string speciesCode, params double[] cellBiomasses)
        {
            return new BiomassGrid
            {
                Species = new Species { SpeciesCode = speciesCode, LifeStage = "" },
                BiomassCells = cellBiomasses.Select(b => new BiomassCell { Biomass = b }).ToList()
            };
        }

        private static DispositionGrid CreateGrid(string speciesCode, string gearCode, params (double Gross, double Live, double Dead)[] cells)
        {
            return new DispositionGrid
            {
                Species = new Species { SpeciesCode = speciesCode, LifeStage = "" },
                FleetSegment = new FleetSegment { GearCode = gearCode, CountryCode = "ESP" },
                DispositionCells = cells.Select(c => new DispositionCell
                {
                    GrossCatchBiomass = c.Gross,
                    LiveDiscardsBiomass = c.Live,
                    DeadDiscardsBiomass = c.Dead
                }).ToList()
            };
        }

        [Fact]
        public async Task UpdateCatchDisposition_AggregatesAcrossFleetsIntoSameGroup()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 100.0)]);
            var summary = new CatchDispositionSummary
            {
                DispositionGrids =
                [
                    CreateGrid("PIL", "ART", (10.0, 0.0, 0.0), (5.0, 0.0, 0.0)),
                    CreateGrid("PIL", "OTB", (7.0, 0.0, 0.0))
                ]
            };

            // Act
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, summary);

            // Assert
            service.m_MSEQuotaData!.CatchYearGroup[1].Should().Be(22.0f);
            service.m_MSEQuotaData.CatchYearGroup[2].Should().Be(0.0f);
        }

        [Fact]
        public async Task UpdateCatchDisposition_SubtractsLiveDiscardsFromGrossCatch()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync([CreateBiomassGrid("KHE", 100.0)]);
            var summary = new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("KHE", "OTB", (10.0, 3.0, 2.0))]
            };

            // Act
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, summary);

            // Assert
            // Dead discards are part of the gross catch and remain removed from the stock; only live discards survive
            service.m_MSEQuotaData!.CatchYearGroup[2].Should().Be(7.0f);
        }

        [Fact]
        public async Task UpdateCatchDisposition_ThrowsWhenSpeciesUnmapped()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var summary = new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("XXX", "ART", (10.0, 0.0, 0.0))]
            };

            // Act & Assert
            var act = () => service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, summary);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No group found*");
        }

        [Fact]
        public async Task UpdateCatchDisposition_AccumulatesAcrossMonthlyCalls()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 100.0)]);

            // Act
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (10.0, 0.0, 0.0))]
            });
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (6.0, 1.0, 0.0))]
            });

            // Assert
            service.m_MSEQuotaData!.CatchYearGroup[1].Should().Be(15.0f);
        }

        [Fact]
        public async Task UpdateCatchDisposition_ThrowsWhenNotInitialised()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var act = () => service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary());
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task GetRegulations_ClearsCatchYearGroup()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 100.0)]);
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (10.0, 0.0, 0.0))]
            });

            // Act
            await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            service.m_MSEQuotaData!.CatchYearGroup.Should().OnlyContain(v => v == 0.0f);
        }

        [Fact]
        public async Task InitialiseSimulation_SetsUpSimulationState()
        {
            // Arrange
            var (service, _, _) = CreateServiceWithMocks();
            var contract = CreateContract();

            // Act
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, contract);

            // Assert
            service.m_SimulationId.Should().Be("sim-1");
            service.m_ScenarioName.Should().Be(ScenarioName);
            service.m_SurimiContract.Should().BeSameAs(contract);
        }

        [Fact]
        public async Task CreateRegulations_SetsUpQuotaData()
        {
            // Arrange
            var (service, stockRecruitment, quotaCalculator) = CreateServiceWithMocks();
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());

            // Act
            await service.CreateRegulationsAsync(CreateRegulationSummary());

            // Assert
            service.m_MSEQuotaData.Should().NotBeNull();
            service.m_MSEQuotaData!.nGroups.Should().Be(2);
            service.m_MSEQuotaData.nFleets.Should().Be(2);
            service.m_Biomass.Should().HaveCount(3); // nGroups + 1 for 1-based EwECore indexing
            stockRecruitment.VerifySet(sr => sr.Data = service.m_MSEQuotaData, Times.Once);
            quotaCalculator.VerifySet(qc => qc.Data = service.m_MSEQuotaData, Times.Once);
        }

        [Fact]
        public async Task CreateRegulations_ThrowsOnDuplicateSpecies()
        {
            // Arrange
            var service = CreateService();
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            var summary = CreateRegulationSummary();
            summary.TargetFishingMortalities!.Add(new TargetFishingMortality
            {
                Species = new Species { SpeciesCode = "PIL", LifeStage = "" },
                BiomassLimit = 1.0,
                BiomassBase = 2.0,
                FMax = 0.05
            });

            // Act & Assert
            var act = () => service.CreateRegulationsAsync(summary);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Duplicate species*");
        }

        [Fact]
        public async Task UpdateBiomass_AggregatesCellsIntoGroups()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var grids = new List<BiomassGrid>
            {
                CreateBiomassGrid("PIL", 10.0, 5.0),
                CreateBiomassGrid("PIL", 2.0),
                CreateBiomassGrid("KHE", 3.0)
            };

            // Act
            await service.UpdateBiomassAsync(grids);

            // Assert
            service.m_Biomass[1].Should().Be(17.0f);
            service.m_Biomass[2].Should().Be(3.0f);
        }

        [Fact]
        public async Task UpdateBiomass_ThrowsWhenSpeciesUnmapped()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var grids = new List<BiomassGrid>
            {
                CreateBiomassGrid("XXX", 10.0)
            };

            // Act & Assert
            var act = () => service.UpdateBiomassAsync(grids);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No group found*");
        }

        [Fact]
        public async Task CreateRegulations_AppliesTargetFishingMortalities()
        {
            // Arrange
            var service = CreateService();
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            var summary = CreateRegulationSummary();

            // Act
            await service.CreateRegulationsAsync(summary);

            // Assert
            service.m_MSEQuotaData!.Blim[1].Should().Be(1.0f);
            service.m_MSEQuotaData.Bbase[1].Should().Be(2.0f);
            service.m_MSEQuotaData.Fopt[1].Should().Be(0.05f);
            service.m_MSEQuotaData.Blim[2].Should().Be(3.0f);
            service.m_MSEQuotaData.Bbase[2].Should().Be(4.0f);
            service.m_MSEQuotaData.Fopt[2].Should().Be(0.1f);
        }

        [Fact]
        public async Task CreateRegulations_ThrowsWhenRecruitmentConfigMissing()
        {
            // Arrange
            var service = CreateService();
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            var summary = new RegulationDefinitionsSummary
            {
                TargetFishingMortalities =
                [
                    new TargetFishingMortality
                    {
                        Species = new Species { SpeciesCode = "XXX", LifeStage = "" },
                        BiomassLimit = 1.0,
                        BiomassBase = 2.0,
                        FMax = 0.05
                    }
                ]
            };

            // Act & Assert
            var act = () => service.CreateRegulationsAsync(summary);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No recruitment configuration found*");
        }

        [Fact]
        public async Task CreateRegulations_ThrowsWhenNotInitialised()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var act = () => service.CreateRegulationsAsync(new RegulationDefinitionsSummary());
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetRegulations_RunsAssessmentAndUpdatesQuotas()
        {
            // Arrange
            var (service, _, quotaCalculator) = CreateServiceWithMocks();
            // Snapshot the biomass at call time, because the service clears the (shared) array afterwards
            float[]? assessedBiomass = null;
            int assessedYear = 0;
            quotaCalculator
                .Setup(qc => qc.DoAssessment(It.IsAny<float[]>(), It.IsAny<int>()))
                .Callback<float[], int>((b, year) => { assessedBiomass = (float[])b.Clone(); assessedYear = year; });
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            await service.CreateRegulationsAsync(CreateRegulationSummary());
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 10.0)]);

            // Act
            await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            assessedYear.Should().Be(2024);
            assessedBiomass.Should().NotBeNull();
            assessedBiomass![1].Should().Be(10.0f);
            quotaCalculator.Verify(qc => qc.UpdateQuotas(), Times.Once);
        }

        [Fact]
        public async Task GetRegulations_ClearsBiomass()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 10.0)]);

            // Act
            await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            service.m_Biomass.Should().OnlyContain(v => v == 0.0f);
        }

        [Fact]
        public async Task GetRegulations_ThrowsWhenNotInitialised()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var act = () => service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        private static QuotaCalculationService CreateServiceWithQuotaShareCsv(string csvContent)
        {
            var stockRecruitment = new Mock<IMSEStockRecruitment>();
            var quotaCalculator = new Mock<IMSEQuotaCalculator>();
            // nGroups + 1 elements for 1-based EwECore indexing
            quotaCalculator.Setup(qc => qc.UpdateQuotas()).Returns(new float[3]);
            return new QuotaCalculationService(
                NullLogger<QuotaCalculationService>.Instance,
                stockRecruitment.Object,
                quotaCalculator.Object,
                new QuotaShareLoader(CreateQuotaShareBlobStore(csvContent), NullLogger<QuotaShareLoader>.Instance),
                new RecruitmentLoader(CreateMatchingRecruitmentBlobStore(), NullLogger<RecruitmentLoader>.Instance));
        }

        [Fact]
        public async Task GetRegulations_ThrowsWhenSpeciesNotInShareFile()
        {
            // Arrange: CSV lacks species KHE while a quota is defined for it
            var service = CreateServiceWithQuotaShareCsv(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;0,75;0,25;1\n");
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            await service.CreateRegulationsAsync(CreateRegulationSummary());

            // Act & Assert
            var act = () => service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No quota shares configured*");
        }

        [Fact]
        public async Task GetRegulations_ThrowsWhenQuotaShareSpeciesNotInContract()
        {
            // Arrange: CSV contains species XXX that is not present in the contract
            var service = CreateServiceWithQuotaShareCsv(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;0,75;0,25;1\n" +
                "KHE;;0,5;0,5;1\n" +
                "XXX;;1;;1\n");
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            await service.CreateRegulationsAsync(CreateRegulationSummary());

            // Act & Assert
            var act = () => service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*XXX*not in contract*");
        }

        [Fact]
        public async Task GetRegulations_ThrowsWhenQuotaShareFleetNotInContract()
        {
            // Arrange: CSV contains fleet (TM, FRA) that is not present in the contract
            var service = CreateServiceWithQuotaShareCsv(
                "species_code;life_stage;ART,ESP;OTB,ESP;TM,FRA;Sum\n" +
                "PIL;;0,75;0,25;;1\n" +
                "KHE;;0,5;0,5;;1\n");
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            await service.CreateRegulationsAsync(CreateRegulationSummary());

            // Act & Assert
            var act = () => service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TM*not in contract*");
        }

        [Fact]
        public async Task GetRegulations_LoadsQuotaSharesOnExactMatch()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();

            // Act
            await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            service.m_QuotaShares.Should().NotBeNull();
            service.m_QuotaShares!.SpeciesCount.Should().Be(2);
            service.m_QuotaShares.Fleets.Should().HaveCount(2);
        }

        [Fact]
        public async Task CreateRegulations_AssignsRecruitmentIntoMSEQuotaData()
        {
            // Arrange
            var service = CreateService();
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());

            // Act
            await service.CreateRegulationsAsync(CreateRegulationSummary());

            // Assert
            // 1-based EwECore indexing: PIL -> group 1, KHE -> group 2
            service.m_MSEQuotaData!.RstockRatio[1].Should().Be(0.893f);
            service.m_MSEQuotaData.cvRec[1].Should().Be(0.8f);
            service.m_MSEQuotaData.RstockRatio[2].Should().Be(0.7134952f);
            service.m_MSEQuotaData.cvRec[2].Should().Be(0.9f);
        }

        [Fact]
        public async Task CreateRegulations_ThrowsWhenRecruitmentSpeciesNotInContract()
        {
            // Arrange: recruitment file contains species XXX that is not present in the contract
            var service = CreateServiceWithMocks(CreateRecruitmentBlobStore(
                "species_code;life_stage;RstockRatio;RHalfB0Ratio;cvRec\n" +
                "PIL;;0,893;0,2;0,8\n" +
                "KHE;;0,7134952;0,25;0,9\n" +
                "XXX;;0,5;0,2;0,8\n")).Service;
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());

            // Act & Assert
            var act = () => service.CreateRegulationsAsync(CreateRegulationSummary());
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*XXX*recruitment file*not in contract*");
        }

        [Fact]
        public async Task GetRegulations_SplitsQuotaAcrossFleets()
        {
            // Arrange: quota shares are PIL: 0.75/0.25 and KHE: 0.5/0.5 for fleets (ART, ESP) and (OTB, ESP)
            var (service, _, quotaCalculator) = CreateServiceWithMocks();
            quotaCalculator.Setup(qc => qc.UpdateQuotas()).Returns(new float[] { 0f, 100f, 40f });
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            await service.CreateRegulationsAsync(CreateRegulationSummary());

            // Act
            var regulations = await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            regulations.TotalAllowableCatches.Should().HaveCount(4);

            var pilArt = regulations.TotalAllowableCatches!.Single(tac => tac.Species!.SpeciesCode == "PIL" && tac.FleetSegment!.GearCode == "ART");
            pilArt.FleetSegment!.CountryCode.Should().Be("ESP");
            pilArt.Catch.Should().Be(75.0);

            var pilOtb = regulations.TotalAllowableCatches!.Single(tac => tac.Species!.SpeciesCode == "PIL" && tac.FleetSegment!.GearCode == "OTB");
            pilOtb.Catch.Should().Be(25.0);

            var kheArt = regulations.TotalAllowableCatches!.Single(tac => tac.Species!.SpeciesCode == "KHE" && tac.FleetSegment!.GearCode == "ART");
            kheArt.Catch.Should().Be(20.0);

            var kheOtb = regulations.TotalAllowableCatches!.Single(tac => tac.Species!.SpeciesCode == "KHE" && tac.FleetSegment!.GearCode == "OTB");
            kheOtb.Catch.Should().Be(20.0);
        }

        [Fact]
        public async Task GetRegulations_EmitsQuotaPerMappedSpeciesWithUppercasedCodes()
        {
            // Arrange: regulation summary uses lowercase codes; the species group map normalises them to uppercase
            var (service, _, quotaCalculator) = CreateServiceWithMocks();
            quotaCalculator.Setup(qc => qc.UpdateQuotas()).Returns(new float[] { 0f, 100f, 40f });
            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            var summary = CreateRegulationSummary();
            foreach (var tfm in summary.TargetFishingMortalities!)
            {
                tfm.Species!.SpeciesCode = tfm.Species.SpeciesCode!.ToLowerInvariant();
            }
            await service.CreateRegulationsAsync(summary);

            // Act
            var regulations = await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            regulations.TotalAllowableCatches.Should().HaveCount(4);
            regulations.TotalAllowableCatches!.Select(tac => tac.Species!.SpeciesCode).Distinct().Should().BeEquivalentTo("PIL", "KHE");
        }

        [Fact]
        public async Task CreateRegulations_InitialisesCVbiomEstTo0Point2()
        {
            // Arrange & Act
            var service = await CreateInitialisedServiceAsync();

            // Assert
            service.m_MSEQuotaData!.CVbiomEst.Should().OnlyContain(v => v == 0.2f);
        }

        [Fact]
        public async Task UpdateBiomass_SeedsBestimateBhalfTAndRmaxOnFirstCall()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();

            // Act
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 10.0)]);
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 5.0)]);

            // Assert
            // Recruitment CSV: PIL RstockRatio = 0.893, RHalfB0Ratio = 0.2; seeded values are based on the first biomass only
            service.m_Biomass[1].Should().Be(15.0f);
            service.m_MSEQuotaData!.Bestimate[1].Should().Be(10.0f);
            service.m_MSEQuotaData.BhalfT[1].Should().Be(0.2f * 10.0f);
            service.m_MSEQuotaData.Rmax[1].Should().Be(0.893f * 10.0f * (0.2f + 1.0f));
        }

        [Fact]
        public async Task UpdateCatchDisposition_CalculatesFish1OnFirstCall()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 10.0)]);

            // Act
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (4.0, 0.0, 0.0))]
            });
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (6.0, 0.0, 0.0))]
            });

            // Assert
            // Fish1 = landings / biomass from the first call only; subsequent calls do not recompute it
            service.m_MSEQuotaData!.Fish1[1].Should().Be(0.4f);
            service.m_MSEQuotaData.CatchYearGroup[1].Should().Be(10.0f);
        }

        [Fact]
        public async Task UpdateCatchDisposition_ThrowsWhenBiomassNotSetBeforeCatch()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var summary = new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (4.0, 0.0, 0.0))]
            };

            // Act & Assert
            var act = () => service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, summary);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cannot calculate Fish1 factor*");
        }

        [Fact]
        public async Task UpdateBiomass_ThrowsWhenNotInitialised()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var act = () => service.UpdateBiomassAsync([CreateBiomassGrid("PIL", 10.0)]);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
