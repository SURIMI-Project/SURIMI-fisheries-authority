using EwECore.MSE;
using SURIMI.Datamodel;
using SURIMI_fisheries_authority.Models;

namespace SURIMI_fisheries_authority.Services
{
    public class QuotaCalculationService : IQuotaCalculationService
    {
        private readonly ILogger<QuotaCalculationService> m_logger;
        private readonly IMSEStockRecruitment m_stockRecruitment;
        private readonly IMSEQuotaCalculator m_quotaCalculator;


        public string SimulationId { get; private set; } = string.Empty;
        public SurimiContract? SurimiContract { get; private set; }
        public IMSEQuotaData? MSEQuotaData { get; private set; }
        public SpeciesGroupMap SpeciesGroupMap { get; private set; } = new SpeciesGroupMap();
        public float[] Biomass { get; private set; } = Array.Empty<float>();

        public QuotaCalculationService(ILogger<QuotaCalculationService> logger, IMSEStockRecruitment stockRecruitment, IMSEQuotaCalculator quotaCalculator)
        {
            m_logger = logger;
            m_stockRecruitment = stockRecruitment;
            m_quotaCalculator = quotaCalculator;
        }

        public Task InitialiseSimulationAsync(string simulationId, SurimiContract surimiContract)
        {
            IMSEQuotaData mSEQuotaData = new MSEQuotaData(
                surimiContract.Items.Species.Count,
                surimiContract.Items.FleetSegments.Count 
            );

            m_stockRecruitment.Data = mSEQuotaData;
            m_quotaCalculator.Data = mSEQuotaData;

            var speciesGroupMap = new SpeciesGroupMap();
            for (int iGroup = 0; iGroup < surimiContract.Items.Species.Count; iGroup++)
            {
                var species = surimiContract.Items.Species[iGroup];
                // EwECore uses 1-based group indices, so store iGroup + 1
                if (!speciesGroupMap.Add(species.SpeciesCode, species.LifeStage, iGroup + 1))
                {
                    m_logger.LogWarning($"Duplicate species ({species.SpeciesCode}, {species.LifeStage}) in contract for simulation {simulationId}; keeping first group index");
                }
            }

            SimulationId = simulationId;
            SurimiContract = surimiContract;
            MSEQuotaData = mSEQuotaData;
            SpeciesGroupMap = speciesGroupMap;
            // EwECore VB arrays are 1-based with inclusive sizing. To keep the same indexing, we allocate nGroups + 1 elements and ignore index 0.
            Biomass = new float[mSEQuotaData.nGroups + 1];

            m_logger.LogInformation($"Initialized simulation {simulationId}");
            return Task.CompletedTask;
        }

        public Task SimulateStepAsync()
        {
            m_logger.LogInformation($"Simulated step for simulation {SimulationId}");
            return Task.CompletedTask;
        }

        public Task FinaliseSimulationAsync()
        {
            m_logger.LogInformation($"Finalised simulation {SimulationId}");
            return Task.CompletedTask;
        }

        public Task CancelSimulationAsync()
        {
            m_logger.LogInformation($"Cancelled simulation {SimulationId}");
            return Task.CompletedTask;
        }

        public Task UpdateBiomassAsync(List<BiomassGrid> biomassGrids)
        {
            int applied = 0, skipped = 0;
            foreach (var grid in biomassGrids)
            {
                if (!SpeciesGroupMap.TryGetGroupIndex(grid.Species.SpeciesCode, grid.Species.LifeStage, out int iGroup))
                {
                    m_logger.LogWarning($"No group found for species ({grid.Species.SpeciesCode}, {grid.Species.LifeStage}) in simulation {SimulationId}; skipping biomass grid");
                    skipped++;
                    continue;
                }

                Biomass[iGroup] += (float)grid.BiomassCells.Sum(cell => cell.Biomass);
                applied++;
            }

            m_logger.LogInformation($"Aggregated {applied} biomass grids ({skipped} skipped) for simulation {SimulationId}");
            return Task.CompletedTask;
        }

        public Task UpdateCatchDispositionAsync(DateTime startDateTime, DateTime endDateTime, CatchDispositionSummary catchDispositionSummary)
        {
            var mseQuotaData = MSEQuotaData ?? throw new InvalidOperationException($"Simulation with Id {SimulationId} is not initialised");

            int applied = 0, skipped = 0;
            foreach (var grid in catchDispositionSummary.DispositionGrids ?? Enumerable.Empty<DispositionGrid>())
            {
                if (!SpeciesGroupMap.TryGetGroupIndex(grid.Species.SpeciesCode, grid.Species.LifeStage, out int iGroup))
                {
                    m_logger.LogWarning($"No group found for species ({grid.Species.SpeciesCode}, {grid.Species.LifeStage}) in simulation {SimulationId}; skipping disposition grid");
                    skipped++;
                    continue;
                }

                // Aggregate the biomass removed from the stock: gross catch minus live discards (live discards survive)
                mseQuotaData.CatchYearGroup[iGroup] += (float)grid.DispositionCells.Sum(cell => cell.GrossCatchBiomass - cell.LiveDiscardsBiomass);
                applied++;
            }

            m_logger.LogInformation($"Aggregated {applied} disposition grids ({skipped} skipped) for simulation {SimulationId} ({startDateTime} - {endDateTime})");
            return Task.CompletedTask;
        }

        public Task UpdateFishingActivityAsync(DateTime startDateTime, DateTime endDateTime, FishingActivitySummary fishingActivitySummary)
        {
            m_logger.LogInformation($"Received {fishingActivitySummary.FishingActivities?.Count ?? 0} fishing activities for simulation {SimulationId} ({startDateTime} - {endDateTime})");
            return Task.CompletedTask;
        }

        public Task CreateRegulationsAsync(RegulationDefinitionsSummary regulationDefinitionsSummary)
        {
            var mseQuotaData = MSEQuotaData ?? throw new Exception($"Simulation with Id {SimulationId} is not initialised");

            int applied = 0, skipped = 0;
            foreach (var tfm in regulationDefinitionsSummary.TargetFishingMortalities ?? Enumerable.Empty<TargetFishingMortality>())
            {
                if (!SpeciesGroupMap.TryGetGroupIndex(tfm.Species.SpeciesCode, tfm.Species.LifeStage, out int iGroup))
                {
                    m_logger.LogWarning($"No group found for species ({tfm.Species.SpeciesCode}, {tfm.Species.LifeStage}) in simulation {SimulationId}; skipping regulation");
                    skipped++;
                    continue;
                }

                mseQuotaData.Blim[iGroup] = (float)tfm.BiomassLimit;
                mseQuotaData.Bbase[iGroup] = (float)tfm.BiomassBase;
                mseQuotaData.Fopt[iGroup] = (float)tfm.FMax;
                applied++;
            }

            m_logger.LogInformation($"Applied {applied} target fishing mortalities ({skipped} skipped) for simulation {SimulationId}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called (By the controller) when the new year starts. It assumes the Biomass, catches etc from last year have been updated and it will calculate the new regulations for the next year. 
        /// </summary>
        /// <param name="startDateTime"></param>
        /// <param name="endDateTime"></param>
        /// <returns></returns>
        public Task<RegulationsSummary> GetRegulationsAsync(DateTime startDateTime, DateTime endDateTime)
        {
            m_logger.LogInformation($"Calculating regulations for simulation {SimulationId} ({startDateTime} - {endDateTime})");

            if (MSEQuotaData is null || Biomass.Length <= MSEQuotaData.nLiving)
            {
                throw new InvalidOperationException($"Simulation with Id {SimulationId} is not initialised");
            }

            m_quotaCalculator.DoAssessment(Biomass, startDateTime.Year);

            var quotas = m_quotaCalculator.UpdateQuotas();

            Array.Clear(Biomass);
            Array.Clear(MSEQuotaData.CatchYearGroup);


            // Dummy Total Allowable Catches. Here you would implement the quota calculation logic (e.g. harvest control rules).
            var regulationsSummary = new RegulationsSummary
            {
                TotalAllowableCatches = new List<TotalAllowableCatch>
                {
                    new TotalAllowableCatch
                    {
                        Species = new Species()
                        {
                            SpeciesCode = "PIL",
                        },
                        FleetSegment = new FleetSegment()
                        {
                            GearCode = "ART",
                            CountryCode = "ESP"
                        },
                        Catch = 2323.34
                    },
                    new TotalAllowableCatch
                    {
                        Species = new Species()
                        {
                            SpeciesCode = "KHE",
                        },
                        FleetSegment = new FleetSegment()
                        {
                            GearCode = "OTB",
                            CountryCode = "ESP"
                        },
                        Catch = 500.0005
                    }
                }
            };

            return Task.FromResult(regulationsSummary);
        }
    }
}
