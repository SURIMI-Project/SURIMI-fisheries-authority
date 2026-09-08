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
        private readonly QuotaShareLoader m_quotaShareLoader;
        private readonly RecruitmentLoader m_recruitmentLoader;


        public string m_SimulationId { get; private set; } = string.Empty;
        public SurimiContract? m_SurimiContract { get; private set; }
        public string m_ScenarioName { get; private set; } = string.Empty;
        public IMSEQuotaData? m_MSEQuotaData { get; private set; }
        public SpeciesGroupMap m_QuotaSpeciesGroupMap { get; private set; } = new SpeciesGroupMap();
        public FleetQuotaShareMap? m_QuotaShares { get; private set; }
        public StockRecruitmentMap? m_StockRecruitment { get; private set; }
        public float[] m_Biomass { get; private set; }
        private bool[] m_IsBiomassAlreadyAssigned;
        private bool[] m_IsCatchYearGroupAlreadyAssigned;

        public QuotaCalculationService(ILogger<QuotaCalculationService> logger, IMSEStockRecruitment stockRecruitment, IMSEQuotaCalculator quotaCalculator, QuotaShareLoader quotaShareLoader, RecruitmentLoader recruitmentLoader)
        {
            m_logger = logger;
            m_stockRecruitment = stockRecruitment;
            m_quotaCalculator = quotaCalculator;
            m_quotaShareLoader = quotaShareLoader;
            m_recruitmentLoader = recruitmentLoader;
        }

        public Task InitialiseSimulationAsync(string simulationId, string scenarioName, SurimiContract surimiContract)
        {
            m_SimulationId = simulationId;
            m_SurimiContract = surimiContract;
            m_ScenarioName = scenarioName;
            // EwECore VB arrays are 1-based with inclusive sizing. To keep the same indexing, we allocate nGroups + 1 elements and ignore index 0.
            
            m_logger.LogInformation($"Initialized simulation {simulationId} ");
            return Task.CompletedTask;
        }

        public async Task CreateRegulationsAsync(RegulationDefinitionsSummary regulationDefinitionsSummary, CancellationToken cancellationToken)
        {
            if(m_MSEQuotaData != null)
            {
                throw new InvalidOperationException($"Simulation with Id {m_SimulationId} already has regulations");
            }
            m_MSEQuotaData = new MSEQuotaData(
                regulationDefinitionsSummary.TargetFishingMortalities.Count,
                m_SurimiContract.Items.FleetSegments.Count
            );
            Array.Fill(m_MSEQuotaData.CVbiomEst, 0.2f); // Initialise the Coefficient of Variation for biomass estimates to 0.2 for all species
            m_stockRecruitment.Data = m_MSEQuotaData;
            m_quotaCalculator.Data = m_MSEQuotaData;

            m_QuotaShares = await m_quotaShareLoader.LoadAsync(m_ScenarioName, cancellationToken);
            ValidateQuotaSharesMatchContract(m_QuotaShares, m_SurimiContract, m_ScenarioName);

            var recruitmentMap = await m_recruitmentLoader.LoadAsync(m_ScenarioName, cancellationToken);
            ValidateRecruitmentMatchContract(recruitmentMap, m_SurimiContract, m_ScenarioName);

            foreach (var tfm in regulationDefinitionsSummary.TargetFishingMortalities ?? Enumerable.Empty<TargetFishingMortality>())
            {
                // EwECore uses 1-based group indices, so store iGroup + 1
                if (!m_QuotaSpeciesGroupMap.Add(tfm.Species.SpeciesCode, tfm.Species.LifeStage, out var iGroup))
                {
                    throw new InvalidOperationException($"Duplicate species ({tfm.Species.SpeciesCode}, {tfm.Species.LifeStage}) in RegulationDefinitionsSummary for simulation {m_SimulationId}");
                }

                if (recruitmentMap.TryGetConfiguration(tfm.Species.SpeciesCode, tfm.Species.LifeStage, out var recruitmentConfig))
                {
                    m_MSEQuotaData.RstockRatio[iGroup] = recruitmentConfig.RstockRatio;
                    m_MSEQuotaData.RHalfB0Ratio[iGroup] = recruitmentConfig.RHalfB0Ratio;
                    m_MSEQuotaData.cvRec[iGroup] = recruitmentConfig.cvRec;
                }
                else
                {
                    throw new InvalidOperationException($"No recruitment configuration found for species ({tfm.Species.SpeciesCode}, {tfm.Species.LifeStage}) in simulation {m_SimulationId}");
                }

                m_MSEQuotaData.Blim[iGroup] = (float)tfm.BiomassLimit;
                m_MSEQuotaData.Bbase[iGroup] = (float)tfm.BiomassBase;
                m_MSEQuotaData.Fopt[iGroup] = (float)tfm.FMax;

                m_logger.LogInformation($"Regulation definition for species ({tfm.Species.SpeciesCode}, {tfm.Species.LifeStage}) group {iGroup} in simulation {m_SimulationId}: Blim={tfm.BiomassLimit}, Bbase={tfm.BiomassBase}, Fopt={tfm.FMax}, RstockRatio={recruitmentConfig.RstockRatio}, RHalfB0Ratio={recruitmentConfig.RHalfB0Ratio}, cvRec={recruitmentConfig.cvRec}");
            }

            m_Biomass = new float[m_MSEQuotaData.nGroups + 1];
            m_IsBiomassAlreadyAssigned = new bool[m_MSEQuotaData.nGroups + 1];
            m_IsCatchYearGroupAlreadyAssigned = new bool[m_MSEQuotaData.nGroups + 1];
        }

        public Task UpdateBiomassAsync(List<BiomassGrid> biomassGrids)
        {
            var mseQuotaData = m_MSEQuotaData ?? throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");
            foreach (var grid in biomassGrids)
            {
                if (!m_QuotaSpeciesGroupMap.TryGetGroupIndex(grid.Species.SpeciesCode, grid.Species.LifeStage, out int iGroup))
                {
                    continue;   // we are only interested in species that are part of the quota calculation, so we can skip any other species
                }

                m_Biomass[iGroup] += (float)grid.BiomassCells.Sum(cell => cell.Biomass);

                // only the first time Biomass is assigned, we also fill the Bestimate array, which is used in the EwECore MSE model to calculate the fishing mortality rate
                if (!m_IsBiomassAlreadyAssigned[iGroup])
                {
                    mseQuotaData.Bestimate[iGroup] = m_Biomass[iGroup];
                    var RStock0 = mseQuotaData.RstockRatio[iGroup] * m_Biomass[iGroup];
                    mseQuotaData.BhalfT[iGroup] = mseQuotaData.RHalfB0Ratio[iGroup] * m_Biomass[iGroup];
                    mseQuotaData.Rmax[iGroup] = RStock0 * (mseQuotaData.RHalfB0Ratio[iGroup] + 1);
                }
                 m_IsBiomassAlreadyAssigned[iGroup] = true;
            }

            return Task.CompletedTask;
        }

        public Task UpdateCatchDispositionAsync(DateTime startDateTime, DateTime endDateTime, CatchDispositionSummary catchDispositionSummary)
        {
            var mseQuotaData = m_MSEQuotaData ?? throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");

            foreach (var grid in catchDispositionSummary.DispositionGrids ?? Enumerable.Empty<DispositionGrid>())
            {
                if (!m_QuotaSpeciesGroupMap.TryGetGroupIndex(grid.Species.SpeciesCode, grid.Species.LifeStage, out int iGroup))
                {
                    continue;   // we are only interested in species that are part of the quota calculation, so we can skip any other species
                }

                var landings = (float)grid.DispositionCells.Sum(cell => cell.GrossCatchBiomass - cell.LiveDiscardsBiomass);

                // if this is the first catch disposition, calulate the Fish1 factor for the species group, which is used in the EwECore MSE model to calculate the fishing mortality rate
                if (!m_IsCatchYearGroupAlreadyAssigned[iGroup])
                {
                    if(!m_IsBiomassAlreadyAssigned[iGroup])
                    {
                        throw new InvalidOperationException($"Biomass for species group {iGroup} is not set before catch disposition update in simulation {m_SimulationId}. Cannot calculate Fish1 factor.");
                    }
                    mseQuotaData.Fish1[iGroup] = landings / m_Biomass[iGroup];
                }
                // Aggregate the biomass removed from the stock: gross catch minus live discards (live discards survive)
                mseQuotaData.CatchYearGroup[iGroup] += landings;
                m_IsCatchYearGroupAlreadyAssigned[iGroup] = true;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called (By the controller) when the new year starts. It assumes the Biomass, catches etc from last year have been updated and it will calculate the new regulations for the next year. 
        /// </summary>
        /// <param name="startDateTime"></param>
        /// <param name="endDateTime"></param>
        /// <returns></returns>
        public async Task<RegulationsSummary> GetRegulationsAsync(DateTime startDateTime, DateTime endDateTime)
        {
            m_logger.LogInformation($"Calculating regulations for simulation {m_SimulationId} ({startDateTime} - {endDateTime})");

            if (m_MSEQuotaData is null || m_Biomass is null || m_Biomass.Length <= m_MSEQuotaData.nLiving)
            {
                throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");
            }

            m_quotaCalculator.DoAssessment(m_Biomass, startDateTime.Year);

            var quotas = m_quotaCalculator.UpdateQuotas();

            Array.Clear(m_Biomass);
            Array.Clear(m_MSEQuotaData.CatchYearGroup);

            // Split each species quota across the fleets according to the scenario quota shares
            var totalAllowableCatches = new List<TotalAllowableCatch>();
            foreach (var (speciesCode, lifeStage, iGroup) in m_QuotaSpeciesGroupMap.Entries)
            {
                if(m_IsBiomassAlreadyAssigned[iGroup] == false || m_IsCatchYearGroupAlreadyAssigned[iGroup] == false)
                {
                    m_logger.LogInformation($"Skipping quota calculation for species ({speciesCode}, {lifeStage}) in simulation {m_SimulationId} as biomass or catch has not been assigned");
                    continue; // skip species that have not been assigned biomass and catch, as they are not part of the current simulation
                }
                if (!m_QuotaShares.TryGetShares(speciesCode, lifeStage, out var shares))
                {
                    throw new InvalidOperationException($"No quota shares configured for species ({speciesCode}, {lifeStage}) while a quota is defined in simulation {m_SimulationId}");
                }

                foreach (var (fleet, share) in shares)
                {
                    var tac = quotas[iGroup] * share;
                    m_logger.LogInformation($"TAC for species ({speciesCode}, {lifeStage}) fleet ({fleet.GearCode}, {fleet.CountryCode}) in simulation {m_SimulationId}: {tac}");
                    totalAllowableCatches.Add(new TotalAllowableCatch
                    {
                        Species = new Species()
                        {
                            SpeciesCode = speciesCode,
                            LifeStage = lifeStage
                        },
                        FleetSegment = new FleetSegment()
                        {
                            GearCode = fleet.GearCode,
                            CountryCode = fleet.CountryCode
                        },
                        Catch = tac
                    });
                }
            }

            var regulationsSummary = new RegulationsSummary
            {
                TotalAllowableCatches = totalAllowableCatches
            };

            return  regulationsSummary;
        }

        public Task UpdateFishingActivityAsync(DateTime startDateTime, DateTime endDateTime, FishingActivitySummary fishingActivitySummary)
        {
            m_logger.LogInformation($"Received {fishingActivitySummary.FishingActivities?.Count ?? 0} fishing activities for simulation {m_SimulationId} ({startDateTime} - {endDateTime})");
            return Task.CompletedTask;
        }

        public Task FinaliseSimulationAsync()
        {
            m_logger.LogInformation($"Finalised simulation {m_SimulationId}");
            return Task.CompletedTask;
        }

        public Task CancelSimulationAsync()
        {
            m_logger.LogInformation($"Cancelled simulation {m_SimulationId}");
            return Task.CompletedTask;
        }
        public Task SimulateStepAsync()
        {
            m_logger.LogInformation($"Simulated step for simulation {m_SimulationId}");
            return Task.CompletedTask;
        }

        private static void ValidateQuotaSharesMatchContract(FleetQuotaShareMap quotaShares, SurimiContract surimiContract, string scenarioName)
        {
            var contractSpecies = surimiContract.Items.Species
                .Select(s => new SpeciesKey(s.SpeciesCode, s.LifeStage))
                .ToHashSet();
            var csvSpecies = quotaShares.SpeciesKeys.ToHashSet();

            var contractFleets = surimiContract.Items.FleetSegments
                .Select(f => new FleetKey(f.GearCode, f.CountryCode))
                .ToHashSet();
            var csvFleets = quotaShares.Fleets.ToHashSet();

            var problems = new List<string>();
            problems.AddRange(csvSpecies.Except(contractSpecies).Select(s => $"species ({s.SpeciesCode}, {s.LifeStage}) in quota share file but not in contract"));
            problems.AddRange(csvFleets.Except(contractFleets).Select(f => $"fleet ({f.GearCode}, {f.CountryCode}) in quota share file but not in contract"));

            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"Quota share file for scenario {scenarioName} does not match the simulation contract: {string.Join("; ", problems)}");
            }
        }

        private static void ValidateRecruitmentMatchContract(StockRecruitmentMap recruitment, SurimiContract surimiContract, string scenarioName)
        {
            var contractSpecies = surimiContract.Items.Species
                .Select(s => new SpeciesKey(s.SpeciesCode, s.LifeStage))
                .ToHashSet();
            var csvSpecies = recruitment.SpeciesKeys.ToHashSet();

            var problems = new List<string>();
            problems.AddRange(csvSpecies.Except(contractSpecies).Select(s => $"species ({s.SpeciesCode}, {s.LifeStage}) in recruitment file but not in contract"));

            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"Recruitment file for scenario {scenarioName} does not match the simulation contract: {string.Join("; ", problems)}");
            }
        }
    }
}
