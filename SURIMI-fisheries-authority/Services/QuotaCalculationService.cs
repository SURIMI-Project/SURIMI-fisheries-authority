using SURIMI.Datamodel;
using SURIMI_fisheries_authority.Models;
using System.Collections.Concurrent;

namespace SURIMI_fisheries_authority.Services
{
    public class QuotaCalculationService : IQuotaCalculationService
    {
        private readonly ILogger<QuotaCalculationService> m_logger;
        private readonly ConcurrentDictionary<string, Models.Simulation> _simulations = new ConcurrentDictionary<string, Models.Simulation>();

        public QuotaCalculationService(ILogger<QuotaCalculationService> logger)
        {
            m_logger = logger;
        }

        public Task InitialiseSimulationAsync(string simulationId, SurimiContract surimiContract)
        {
            MSEQuotaData mSEQuotaData = new MSEQuotaData(
                surimiContract.Items.Species.Count,
                surimiContract.Items.Species.Count,
                surimiContract.Items.FleetSegments.Count
            );

            var speciesGroupMap = new SpeciesGroupMap();
            for (int iGroup = 0; iGroup < surimiContract.Items.Species.Count; iGroup++)
            {
                var species = surimiContract.Items.Species[iGroup];
                if (!speciesGroupMap.Add(species.SpeciesCode, species.LifeStage, iGroup))
                {
                    m_logger.LogWarning($"Duplicate species ({species.SpeciesCode}, {species.LifeStage}) in contract for simulation {simulationId}; keeping first group index");
                }
            }

            if (!_simulations.TryAdd(simulationId, new Models.Simulation(simulationId, surimiContract, mSEQuotaData, speciesGroupMap)))
            {
                throw new Exception($"Simulation with Id {simulationId} is already running");
            }

            m_logger.LogInformation($"Initialized simulation {simulationId}");
            return Task.CompletedTask;
        }

        public Task SimulateStepAsync(string simulationId)
        {
            GetSimulation(simulationId);
            m_logger.LogInformation($"Simulated step for simulation {simulationId}");
            return Task.CompletedTask;
        }

        public Task FinaliseSimulationAsync(string simulationId)
        {
            if (!_simulations.TryRemove(simulationId, out _))
            {
                throw new Exception($"Simulation with Id {simulationId} is not running");
            }

            m_logger.LogInformation($"Finalised simulation {simulationId}");
            return Task.CompletedTask;
        }

        public Task CancelSimulationAsync(string simulationId)
        {
            if (!_simulations.TryRemove(simulationId, out _))
            {
                throw new Exception($"Simulation with Id {simulationId} is not running");
            }

            m_logger.LogInformation($"Cancelled simulation {simulationId}");
            return Task.CompletedTask;
        }

        public Task UpdateBiomassAsync(string simulationId, List<BiomassGrid> biomassGrids)
        {
            var simulation = GetSimulation(simulationId);

            int applied = 0, skipped = 0;
            foreach (var grid in biomassGrids)
            {
                if (!simulation.SpeciesGroupMap.TryGetGroupIndex(grid.Species.SpeciesCode, grid.Species.LifeStage, out int iGroup))
                {
                    m_logger.LogWarning($"No group found for species ({grid.Species.SpeciesCode}, {grid.Species.LifeStage}) in simulation {simulationId}; skipping biomass grid");
                    skipped++;
                    continue;
                }

                simulation.Biomass[iGroup] += (float)grid.BiomassCells.Sum(cell => cell.Biomass);
                applied++;
            }

            m_logger.LogInformation($"Aggregated {applied} biomass grids ({skipped} skipped) for simulation {simulationId}");
            return Task.CompletedTask;
        }

        public Task UpdateCatchDispositionAsync(string simulationId, DateTime startDateTime, DateTime endDateTime, CatchDispositionSummary catchDispositionSummary)
        {
            GetSimulation(simulationId);

            m_logger.LogInformation($"Received {catchDispositionSummary.DispositionGrids?.Count ?? 0} disposition grids for simulation {simulationId} ({startDateTime} - {endDateTime})");
            return Task.CompletedTask;
        }

        public Task UpdateFishingActivityAsync(string simulationId, DateTime startDateTime, DateTime endDateTime, FishingActivitySummary fishingActivitySummary)
        {
            GetSimulation(simulationId);

            m_logger.LogInformation($"Received {fishingActivitySummary.FishingActivities?.Count ?? 0} fishing activities for simulation {simulationId} ({startDateTime} - {endDateTime})");
            return Task.CompletedTask;
        }

        public Task CreateRegulationsAsync(string simulationId, RegulationDefinitionsSummary regulationDefinitionsSummary)
        {
            var simulation = GetSimulation(simulationId);

            int applied = 0, skipped = 0;
            foreach (var tfm in regulationDefinitionsSummary.TargetFishingMortalities ?? Enumerable.Empty<TargetFishingMortality>())
            {
                if (!simulation.SpeciesGroupMap.TryGetGroupIndex(tfm.Species.SpeciesCode, tfm.Species.LifeStage, out int iGroup))
                {
                    m_logger.LogWarning($"No group found for species ({tfm.Species.SpeciesCode}, {tfm.Species.LifeStage}) in simulation {simulationId}; skipping regulation");
                    skipped++;
                    continue;
                }

                simulation.MSEQuotaData.Blim[iGroup] = (float)tfm.BiomassLimit;
                simulation.MSEQuotaData.Bbase[iGroup] = (float)tfm.BiomassBase;
                simulation.MSEQuotaData.Fopt[iGroup] = (float)tfm.FMax;
                applied++;
            }

            m_logger.LogInformation($"Applied {applied} target fishing mortalities ({skipped} skipped) for simulation {simulationId}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called (By the controller) when the new year starts. It assumes the Biomass, catches etc from last year have been updated and it will calculate the new regulations for the next year. 
        /// </summary>
        /// <param name="simulationId"></param>
        /// <param name="startDateTime"></param>
        /// <param name="endDateTime"></param>
        /// <returns></returns>
        public Task<RegulationsSummary> GetRegulationsAsync(string simulationId, DateTime startDateTime, DateTime endDateTime)
        {
            var simulation = GetSimulation(simulationId);

            m_logger.LogInformation($"Calculating regulations for simulation {simulationId} ({startDateTime} - {endDateTime})");



            Array.Clear(simulation.Biomass);


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

        private Models.Simulation GetSimulation(string simulationId)
        {
            if (!_simulations.TryGetValue(simulationId, out var simulation))
            {
                throw new Exception($"Simulation with Id {simulationId} is not running");
            }

            return simulation;
        }
    }
}
