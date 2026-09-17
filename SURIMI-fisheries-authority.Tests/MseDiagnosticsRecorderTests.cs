using Eii.BlobStore;
using EwECore.MSE;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI.Datamodel;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class MseDiagnosticsRecorderTests
    {
        private const string ScenarioName = "test-scenario";

        [Fact]
        public async Task UpdateBiomass_RecordsMonthlyDiagnostics()
        {
            // Arrange
            var (service, diagnostics) = await CreateInitialisedServiceAsync();
            var period = new DateTime(2024, 3, 1);
            // The first call seeds Bestimate/BhalfT/Rmax and is not recorded as a monthly diagnostic
            await service.UpdateBiomassAsync(DateTime.MinValue, [CreateBiomassGrid("PIL", 0.0)]);

            // Act
            await service.UpdateBiomassAsync(period, [CreateBiomassGrid("PIL", 10.0, 5.0)]);

            // Assert
            diagnostics.Verify(d => d.RecordMonthlyBiomass("sim-1", period, "PIL", "", 1, 15.0f), Times.Once);
        }

        [Fact]
        public async Task UpdateCatchDisposition_RecordsMonthlyLandings()
        {
            // Arrange
            var (service, diagnostics) = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync(DateTime.MinValue, [CreateBiomassGrid("PIL", 100.0)]);
            var period = new DateTime(2024, 3, 1);

            // Act
            await service.UpdateCatchDispositionAsync(period, new DateTime(2024, 3, 31), new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (10.0, 3.0, 0.0))]
            });

            // Assert
            // Landings removed from the stock = gross catch minus live discards
            diagnostics.Verify(d => d.RecordMonthlyCatch("sim-1", period, "PIL", "", 1, 7.0f, 7.0f), Times.Once);
        }

        [Fact]
        public async Task GetRegulations_RecordsYearlyAssessmentBeforeClearingAccumulators()
        {
            // Arrange
            var (service, diagnostics) = await CreateInitialisedServiceAsync();
            await service.UpdateBiomassAsync(DateTime.MinValue, [CreateBiomassGrid("PIL", 100.0)]);
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (10.0, 0.0, 0.0))]
            });
            float recordedBiomass = -1f;
            float recordedCatch = -1f;
            diagnostics
                .Setup(d => d.RecordYear("sim-1", 2024, "PIL", "", 1, It.IsAny<float>(), It.IsAny<IMSEQuotaData>(), It.IsAny<float>()))
                .Callback<string, int, string, string, int, float, IMSEQuotaData, float>((_, _, _, _, iGroup, biomass, data, _) =>
                {
                    recordedBiomass = biomass;
                    recordedCatch = data.CatchYearGroup[iGroup];
                });

            // Act
            await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            recordedBiomass.Should().Be(100.0f);
            recordedCatch.Should().Be(10.0f);
        }

        [Fact]
        public async Task GetRegulations_RecordsTacPerFleet()
        {
            // Arrange
            var (service, diagnostics) = await CreateInitialisedServiceAsync(quotas: [0f, 100f, 0f]);
            await service.UpdateBiomassAsync(DateTime.MinValue, [CreateBiomassGrid("PIL", 100.0)]);
            await service.UpdateCatchDispositionAsync(DateTime.MinValue, DateTime.MaxValue, new CatchDispositionSummary
            {
                DispositionGrids = [CreateGrid("PIL", "ART", (10.0, 0.0, 0.0))]
            });

            // Act
            await service.GetRegulationsAsync(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));

            // Assert
            diagnostics.Verify(d => d.RecordTac("sim-1", 2024, "PIL", "", "ART", "ESP", 0.75f, 75.0f), Times.Once);
            diagnostics.Verify(d => d.RecordTac("sim-1", 2024, "PIL", "", "OTB", "ESP", 0.25f, 25.0f), Times.Once);
        }

        [Fact]
        public async Task FinaliseSimulation_FlushesDiagnostics()
        {
            // Arrange
            var (service, diagnostics) = await CreateInitialisedServiceAsync();

            // Act
            await service.FinaliseSimulationAsync(CancellationToken.None);

            // Assert
            diagnostics.Verify(d => d.FlushAsync("sim-1", CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task CancelSimulation_FlushesDiagnostics()
        {
            // Arrange
            var (service, diagnostics) = await CreateInitialisedServiceAsync();

            // Act
            await service.CancelSimulationAsync(CancellationToken.None);

            // Assert
            diagnostics.Verify(d => d.FlushAsync("sim-1", CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task FlushAsync_UploadsCsvFilesToBlobStoreWithInvariantCultureDecimals()
        {
            // Arrange
            var uploads = new Dictionary<string, string>();
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.UploadAsync(It.IsAny<string>(), PathType.Output, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<string, PathType, Stream, string, IReadOnlyDictionary<string, string>, CancellationToken>((key, _, content, _, _, _) =>
                {
                    using var reader = new StreamReader(content, leaveOpen: true);
                    uploads[key] = reader.ReadToEnd();
                })
                .Returns(Task.CompletedTask);
            var recorder = new CsvMseDiagnosticsRecorder(NullLogger<CsvMseDiagnosticsRecorder>.Instance, blobStore.Object);
            recorder.RecordMonthlyBiomass("sim-1", new DateTime(2024, 3, 1), "PIL", "", 1, 10.5f);
            recorder.RecordMonthlyCatch("sim-1", new DateTime(2024, 3, 1), "PIL", "", 1, 2.5f, 7.5f);
            recorder.RecordTac("sim-1", 2024, "PIL", "", "ART", "ESP", 0.75f, 75.5f);

            // Act
            await recorder.FlushAsync("sim-1", CancellationToken.None);

            // Assert
            var biomassLines = uploads["sim-1/sim-1_biomass-monthly.csv"].Split(Environment.NewLine);
            biomassLines[0].Should().Be("simulation_id,year,month,species_code,life_stage,group,MonthBiomass");
            biomassLines[1].Should().Be("sim-1,2024,3,PIL,,1,10.5");

            var catchLines = uploads["sim-1/sim-1_catch-monthly.csv"].Split(Environment.NewLine);
            catchLines[0].Should().Be("simulation_id,year,month,species_code,life_stage,group,MonthLandings,AccumulatedCatchYearGroup");
            catchLines[1].Should().Be("sim-1,2024,3,PIL,,1,2.5,7.5");

            var tacLines = uploads["sim-1/sim-1_tac.csv"].Split(Environment.NewLine);
            tacLines[0].Should().Be("simulation_id,year,species_code,life_stage,gear_code,country_code,share,TAC");
            tacLines[1].Should().Be("sim-1,2024,PIL,,ART,ESP,0.75,75.5");

            // No yearly rows were recorded, so no assessment file is uploaded
            uploads.Should().NotContainKey("sim-1/sim-1_mse-assessment.csv");
        }

        private static async Task<(QuotaCalculationService Service, Mock<IMseDiagnosticsRecorder> Diagnostics)> CreateInitialisedServiceAsync(float[]? quotas = null)
        {
            var stockRecruitment = new Mock<IMSEStockRecruitment>();
            var quotaCalculator = new Mock<IMSEQuotaCalculator>();
            // nGroups + 1 elements for 1-based EwECore indexing
            quotaCalculator.Setup(qc => qc.UpdateQuotas()).Returns(quotas ?? new float[3]);
            var diagnostics = new Mock<IMseDiagnosticsRecorder>();

            var service = new QuotaCalculationService(
                NullLogger<QuotaCalculationService>.Instance,
                stockRecruitment.Object,
                quotaCalculator.Object,
                new QuotaShareLoader(CreateQuotaShareBlobStore(), NullLogger<QuotaShareLoader>.Instance),
                new RecruitmentLoader(CreateRecruitmentBlobStore(), NullLogger<RecruitmentLoader>.Instance),
                diagnostics.Object);

            await service.InitialiseSimulationAsync("sim-1", ScenarioName, CreateContract());
            await service.CreateRegulationsAsync(CreateRegulationSummary(), CancellationToken.None);
            return (service, diagnostics);
        }

        private static IBlobStore CreateQuotaShareBlobStore()
        {
            var csvContent =
                "species_code,life_stage,ART|ESP,OTB|ESP\n" +
                "PIL,,0.75,0.25\n" +
                "KHE,,0.5,0.5\n";
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync($"{ScenarioName}/{ScenarioName}_quotashare.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            blobStore
                .Setup(bs => bs.ReadAllTextAsync($"{ScenarioName}/{ScenarioName}_quotashare.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(csvContent);
            return blobStore.Object;
        }

        private static IBlobStore CreateRecruitmentBlobStore()
        {
            var csvContent =
                "species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec\n" +
                "PIL,,0.893,0.2,0.8\n" +
                "KHE,,0.7134952,0.25,0.9\n";
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync($"{ScenarioName}/{ScenarioName}_recruitment.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            blobStore
                .Setup(bs => bs.ReadAllTextAsync($"{ScenarioName}/{ScenarioName}_recruitment.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(csvContent);
            return blobStore.Object;
        }

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
                },
                Standards = new Standards(),
                Simulation = new Simulation() 
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
    }
}
