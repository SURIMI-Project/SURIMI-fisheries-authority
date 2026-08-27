using EwECore.MSE;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI.Datamodel;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class QuotaCalculationServiceTests
    {
        private static (QuotaCalculationService Service, Mock<IMSEStockRecruitment> StockRecruitment, Mock<IMSEQuotaCalculator> QuotaCalculator) CreateServiceWithMocks()
        {
            var stockRecruitment = new Mock<IMSEStockRecruitment>();
            var quotaCalculator = new Mock<IMSEQuotaCalculator>();
            quotaCalculator.Setup(qc => qc.UpdateQuotas()).Returns(Array.Empty<float>());

            var service = new QuotaCalculationService(
                NullLogger<QuotaCalculationService>.Instance,
                stockRecruitment.Object,
                quotaCalculator.Object);

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

        private static async Task<QuotaCalculationService> CreateInitialisedServiceAsync()
        {
            var service = CreateService();
            await service.InitialiseSimulationAsync("sim-1", CreateContract());
            return service;
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
            service.MSEQuotaData!.CatchYearGroup[1].Should().Be(22.0f);
            service.MSEQuotaData.CatchYearGroup[2].Should().Be(0.0f);
        }

        [Fact]
        public async Task UpdateCatchDisposition_SubtractsLiveDiscardsFromGrossCatch()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var summary = new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("KHE", "OTB", (10.0, 3.0, 2.0))]
            };

            // Act
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, summary);

            // Assert
            // Dead discards are part of the gross catch and remain removed from the stock; only live discards survive
            service.MSEQuotaData!.CatchYearGroup[2].Should().Be(7.0f);
        }

        [Fact]
        public async Task UpdateCatchDisposition_SkipsUnmappedSpeciesWithoutThrowing()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var summary = new CatchDispositionSummary
            {
                DispositionGrids =
                [
                    CreateGrid("XXX", "ART", (10.0, 0.0, 0.0)),
                    CreateGrid("PIL", "ART", (4.0, 0.0, 0.0))
                ]
            };

            // Act
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, summary);

            // Assert
            service.MSEQuotaData!.CatchYearGroup[1].Should().Be(4.0f);
        }

        [Fact]
        public async Task UpdateCatchDisposition_AccumulatesAcrossMonthlyCalls()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();

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
            service.MSEQuotaData!.CatchYearGroup[1].Should().Be(15.0f);
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
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (10.0, 0.0, 0.0))]
            });

            // Act
            await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            service.MSEQuotaData!.CatchYearGroup.Should().OnlyContain(v => v == 0.0f);
        }

        [Fact]
        public async Task InitialiseSimulation_SetsUpSimulationState()
        {
            // Arrange
            var (service, stockRecruitment, quotaCalculator) = CreateServiceWithMocks();

            // Act
            await service.InitialiseSimulationAsync("sim-1", CreateContract());

            // Assert
            service.SimulationId.Should().Be("sim-1");
            service.MSEQuotaData.Should().NotBeNull();
            service.MSEQuotaData!.nGroups.Should().Be(2);
            service.MSEQuotaData.nFleets.Should().Be(2);
            service.Biomass.Should().HaveCount(3); // nGroups + 1 for 1-based EwECore indexing
            stockRecruitment.VerifySet(sr => sr.Data = service.MSEQuotaData, Times.Once);
            quotaCalculator.VerifySet(qc => qc.Data = service.MSEQuotaData, Times.Once);
        }

        [Fact]
        public async Task InitialiseSimulation_KeepsFirstGroupIndexForDuplicateSpecies()
        {
            // Arrange
            var service = CreateService();
            var contract = CreateContract();
            contract.Items!.Species!.Add(new Species { SpeciesCode = "PIL", LifeStage = "" });

            // Act
            await service.InitialiseSimulationAsync("sim-1", contract);

            // Assert
            service.SpeciesGroupMap.TryGetGroupIndex("PIL", "", out int iGroup).Should().BeTrue();
            iGroup.Should().Be(1);
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
            service.Biomass[1].Should().Be(17.0f);
            service.Biomass[2].Should().Be(3.0f);
        }

        [Fact]
        public async Task UpdateBiomass_SkipsUnmappedSpeciesWithoutThrowing()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var grids = new List<BiomassGrid>
            {
                CreateBiomassGrid("XXX", 10.0),
                CreateBiomassGrid("PIL", 4.0)
            };

            // Act
            await service.UpdateBiomassAsync(grids);

            // Assert
            service.Biomass[1].Should().Be(4.0f);
            service.Biomass[2].Should().Be(0.0f);
        }

        [Fact]
        public async Task CreateRegulations_AppliesTargetFishingMortalities()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
            var summary = new RegulationDefinitionsSummary
            {
                TargetFishingMortalities =
                [
                    new TargetFishingMortality
                    {
                        Species = new Species { SpeciesCode = "PIL", LifeStage = "" },
                        BiomassLimit = 1.0,
                        BiomassBase = 2.0,
                        FMax = 0.05
                    }
                ]
            };

            // Act
            await service.CreateRegulationsAsync(summary);

            // Assert
            service.MSEQuotaData!.Blim[1].Should().Be(1.0f);
            service.MSEQuotaData.Bbase[1].Should().Be(2.0f);
            service.MSEQuotaData.Fopt[1].Should().Be(0.05f);
        }

        [Fact]
        public async Task CreateRegulations_SkipsUnmappedSpeciesWithoutThrowing()
        {
            // Arrange
            var service = await CreateInitialisedServiceAsync();
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

            // Act
            await service.CreateRegulationsAsync(summary);

            // Assert
            service.MSEQuotaData!.Blim.Should().OnlyContain(v => v == 0.0f);
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
            await service.InitialiseSimulationAsync("sim-1", CreateContract());
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
            service.Biomass.Should().OnlyContain(v => v == 0.0f);
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
    }
}
