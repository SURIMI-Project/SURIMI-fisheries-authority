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
        private readonly InitialQuotaLoader m_initialQuotaLoader;
        private readonly IMseDiagnosticsRecorder m_diagnostics;
        private readonly SwitchableRandomService m_randomService;


        public string m_SimulationId { get; private set; } = string.Empty;
        public SurimiContract? m_SurimiContract { get; private set; }
        public string m_ScenarioName { get; private set; } = string.Empty;
        public IMSEQuotaData? m_MSEQuotaData { get; private set; }
        public SpeciesGroupMap m_QuotaSpeciesGroupMap { get; private set; } = new SpeciesGroupMap();
        public FleetQuotaShareMap? m_QuotaShares { get; private set; }
        public InitialQuotaMap? m_InitialQuotaMap { get; private set; }
        public StockRecruitmentMap? m_RecruitmentMap { get; private set; }
        public StockRecruitmentMap? m_StockRecruitment { get; private set; }
        public float[] Biomass { get; private set; } = Array.Empty<float>();
        public float[] Quotas { get; private set; } = Array.Empty<float>();
        private bool m_IsBiomassAllreadyAssigned = false;       // if biomass in not assigned, MSE is not initialised yet , so we cannot calculate Fish1 factor for catch disposition. 


        public QuotaCalculationService(ILogger<QuotaCalculationService> logger, IMSEStockRecruitment stockRecruitment, IMSEQuotaCalculator quotaCalculator, QuotaShareLoader quotaShareLoader, RecruitmentLoader recruitmentLoader, InitialQuotaLoader initialQuotaLoader, IMseDiagnosticsRecorder diagnostics, SwitchableRandomService randomService)
        {
            m_logger = logger;
            m_stockRecruitment = stockRecruitment;
            m_quotaCalculator = quotaCalculator;
            m_quotaShareLoader = quotaShareLoader;
            m_recruitmentLoader = recruitmentLoader;
            m_initialQuotaLoader = initialQuotaLoader;
            m_diagnostics = diagnostics;
            m_randomService = randomService;
        }

        public async Task InitialiseSimulationAsync(string simulationId, string scenarioName, SurimiContract surimiContract, CancellationToken cancellationToken = default, bool isMseRun = true)
        {
            m_SimulationId = simulationId;
            m_SurimiContract = surimiContract;
            m_ScenarioName = scenarioName;
            // EwECore VB arrays are 1-based with inclusive sizing. To keep the same indexing, we allocate nGroups + 1 elements and ignore index 0.
            
            m_randomService.Mode = isMseRun ? RandomMode.Random : RandomMode.Constant;
            m_logger.LogInformation("Initialized simulation {SimulationId} (IsMseRun={IsMseRun}, RandomMode={RandomMode})", simulationId, isMseRun, m_randomService.Mode);

            m_QuotaShares = await m_quotaShareLoader.LoadAsync(m_ScenarioName, cancellationToken);
            ValidateQuotaSharesMatchContract(m_QuotaShares, surimiContract, m_ScenarioName);

            m_RecruitmentMap = await m_recruitmentLoader.LoadAsync(m_ScenarioName, cancellationToken);
            ValidateRecruitmentMatchContract(m_RecruitmentMap, surimiContract, m_ScenarioName);

            m_InitialQuotaMap = await m_initialQuotaLoader.LoadAsync(m_ScenarioName, cancellationToken);
            ValidateInitialQuotaMatchContract(m_InitialQuotaMap, surimiContract, m_ScenarioName);
        }

        public async Task CreateRegulationsAsync(RegulationDefinitionsSummary regulationDefinitionsSummary, CancellationToken cancellationToken = default)
        {
            if(m_MSEQuotaData != null)
            {
                throw new InvalidOperationException($"Simulation with Id {m_SimulationId} already has regulations");
            }
            if(m_RecruitmentMap == null)
            {
                throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");
            }

            var surimiContract = m_SurimiContract ?? throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");

            m_MSEQuotaData = new MSEQuotaData(
                regulationDefinitionsSummary.TargetFishingMortalities.Count,
                surimiContract.Items.FleetSegments.Count
            );
            Array.Fill(m_MSEQuotaData.CVbiomEst, 0.2f); // Initialise the Coefficient of Variation for biomass estimates to 0.2 for all species
            m_stockRecruitment.Data = m_MSEQuotaData;
            m_quotaCalculator.Data = m_MSEQuotaData;

            foreach (var tfm in regulationDefinitionsSummary.TargetFishingMortalities ?? Enumerable.Empty<TargetFishingMortality>())
            {
                // EwECore uses 1-based group indices, so store iGroup + 1
                if (!m_QuotaSpeciesGroupMap.Add(tfm.Species.SpeciesCode, tfm.Species.LifeStage, out var iGroup))
                {
                    throw new InvalidOperationException($"Duplicate species ({tfm.Species.SpeciesCode}, {tfm.Species.LifeStage}) in RegulationDefinitionsSummary for simulation {m_SimulationId}");
                }

                if (m_RecruitmentMap.TryGetConfiguration(tfm.Species.SpeciesCode, tfm.Species.LifeStage, out var recruitmentConfig))
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

                m_logger.LogInformation("Regulation definition for species ({SpeciesCode}, {LifeStage}) group {GroupIndex} in simulation {SimulationId}: Blim={BiomassLimit}, Bbase={BiomassBase}, Fopt={FMax}, RstockRatio={RstockRatio}, RHalfB0Ratio={RHalfB0Ratio}, cvRec={CvRec}", tfm.Species.SpeciesCode, tfm.Species.LifeStage, iGroup, m_SimulationId, tfm.BiomassLimit, tfm.BiomassBase, tfm.FMax, recruitmentConfig.RstockRatio, recruitmentConfig.RHalfB0Ratio, recruitmentConfig.cvRec);
            }

            Biomass = new float[m_MSEQuotaData.nGroups + 1];
            Quotas = new float[m_MSEQuotaData.nGroups + 1];
        }

        /// <summary>
        /// This method is called (By the controller) when the biomass is updated. 
        /// </summary>
        public Task UpdateBiomassAsync(DateTime dateTime, List<BiomassGrid> biomassGrids, CancellationToken cancellationToken = default)
        {
            var mseQuotaData = m_MSEQuotaData ?? throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");
            foreach (var grid in biomassGrids)
            {
                if (!m_QuotaSpeciesGroupMap.TryGetGroupIndex(grid.Species.SpeciesCode, grid.Species.LifeStage, out int iGroup))
                {
                    continue;   // we are only interested in species that are part of the quota calculation, so we can skip any other species
                }

                var monthBiomass = (float)grid.BiomassCells.Sum(cell => cell.Biomass);
                Biomass[iGroup] = monthBiomass;

                // only the first time Biomass is assigned, we also fill the Bestimate array, which is used in the EwECore MSE model to calculate the fishing mortality rate
                // UpdateBiomass is called at the start of the simulation, before GetRegulations. So we can use the Biomass as the initial Bestimate for the first year of the simulation.
                if (!m_IsBiomassAllreadyAssigned)
                {
                    InitForRun(monthBiomass, iGroup);
                }

                m_diagnostics.RecordMonthlyBiomass(m_SimulationId, dateTime, grid.Species.SpeciesCode, grid.Species.LifeStage, iGroup, monthBiomass);
            }

            m_IsBiomassAllreadyAssigned = true;     // So next time, we can just update the Biomass array
            return Task.CompletedTask;
        }

        public Task UpdateCatchDispositionAsync(DateTime startDateTime, DateTime endDateTime, CatchDispositionSummary catchDispositionSummary, CancellationToken cancellationToken = default)
        {
            var mseQuotaData = m_MSEQuotaData ?? throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");

            foreach (var grid in catchDispositionSummary.DispositionGrids ?? Enumerable.Empty<DispositionGrid>())
            {
                if (!m_QuotaSpeciesGroupMap.TryGetGroupIndex(grid.Species.SpeciesCode, grid.Species.LifeStage, out int iGroup))
                {
                    continue;   // we are only interested in species that are part of the quota calculation, so we can skip any other species
                }

                var landings = (float)grid.DispositionCells.Sum(cell => cell.GrossCatchBiomass - cell.LiveDiscardsBiomass);

                mseQuotaData.Fish1[iGroup] = landings / Biomass[iGroup];
                // Aggregate the biomass removed from the stock: gross catch minus live discards (live discards survive)
                mseQuotaData.CatchYearGroup[iGroup] += landings;
                m_diagnostics.RecordMonthlyCatch(m_SimulationId, startDateTime, grid.Species.SpeciesCode, grid.Species.LifeStage, iGroup, landings, mseQuotaData.CatchYearGroup[iGroup]);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called (By the controller) when the new year starts. It assumes the Biomass, catches etc from last year have been updated and it will calculate the new regulations for the next year. 
        /// </summary>
        /// <param name="startDateTime"></param>
        /// <param name="endDateTime"></param>
        /// <returns></returns>
        public async Task<RegulationsSummary> GetRegulationsAsync(DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default)
        {
            m_logger.LogInformation("Calculating regulations for simulation {SimulationId} ({StartDateTime} - {EndDateTime})", m_SimulationId, startDateTime, endDateTime);

            if (m_MSEQuotaData is null || Biomass is null || Biomass.Length <= m_MSEQuotaData.nLiving || m_QuotaShares is null)
            {
                throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");
            }

            m_quotaCalculator.DoAssessment(Biomass, startDateTime.Year);

            // check if biomass is already assigned, if not, we need to assign the initial quotas from the InitialQuotaMap, otherwise we can update the quotas using the EwECore MSE model
            if (!m_IsBiomassAllreadyAssigned)
            {
                var initialQuotaMap = m_InitialQuotaMap ?? throw new InvalidOperationException($"Simulation with Id {m_SimulationId}, InitialQuotaMap is not initialised");

                foreach (var (speciesCode, lifeStage, iGroup) in m_QuotaSpeciesGroupMap.Entries)
                {
                    if (!initialQuotaMap.TryGetQuota(speciesCode, lifeStage, out var tac))
                    {
                        throw new InvalidOperationException($"No initial quota configured for species ({speciesCode}, {lifeStage}) group {iGroup} in simulation {m_SimulationId}");
                    }

                    Quotas[iGroup] = tac;
                }
            }
            else
            {
                Quotas = m_quotaCalculator.UpdateQuotas();
            }

            // Record the assessment snapshot before the yearly accumulators are cleared, so it reflects exactly the regulatory year just assessed
            foreach (var (speciesCode, lifeStage, iGroup) in m_QuotaSpeciesGroupMap.Entries)
            {
                m_diagnostics.RecordYear(m_SimulationId, startDateTime.Year, speciesCode, lifeStage, iGroup, Biomass[iGroup], m_MSEQuotaData, Quotas[iGroup]);
            }

            Array.Clear(m_MSEQuotaData.CatchYearGroup);

            // Split each species quota across the fleets according to the scenario quota shares
            var totalAllowableCatches = new List<TotalAllowableCatch>();
            foreach (var (speciesCode, lifeStage, iGroup) in m_QuotaSpeciesGroupMap.Entries)
            {
                if (!m_QuotaShares.TryGetShares(speciesCode, lifeStage, out var shares))
                {
                    throw new InvalidOperationException($"No quota shares configured for species ({speciesCode}, {lifeStage}) while a quota is defined in simulation {m_SimulationId}");
                }

                foreach (var (fleet, share) in shares)
                {
                    var tac = Quotas[iGroup] * share;
                    m_logger.LogInformation("TAC for species ({SpeciesCode}, {LifeStage}) fleet ({GearCode}, {CountryCode}) in simulation {SimulationId}: {Tac}", speciesCode, lifeStage, fleet.GearCode, fleet.CountryCode, m_SimulationId, tac);
                    m_diagnostics.RecordTac(m_SimulationId, startDateTime.Year, speciesCode, lifeStage, fleet.GearCode, fleet.CountryCode, share, tac);
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

        public async Task FinaliseSimulationAsync(CancellationToken cancellationToken = default)
        {
            await m_diagnostics.FlushAsync(m_SimulationId, cancellationToken);
            m_logger.LogInformation("Finalised simulation {SimulationId}", m_SimulationId);
        }

        public async Task CancelSimulationAsync(CancellationToken cancellationToken = default)
        {
            await m_diagnostics.FlushAsync(m_SimulationId, cancellationToken);
            m_logger.LogInformation("Cancelled simulation {SimulationId}", m_SimulationId);
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

        private static void ValidateInitialQuotaMatchContract(InitialQuotaMap initialQuotas, SurimiContract surimiContract, string scenarioName)
        {
            var contractSpecies = surimiContract.Items.Species
                .Select(s => new SpeciesKey(s.SpeciesCode, s.LifeStage))
                .ToHashSet();
            var csvSpecies = initialQuotas.SpeciesKeys.ToHashSet();

            var problems = new List<string>();
            problems.AddRange(csvSpecies.Except(contractSpecies).Select(s => $"species ({s.SpeciesCode}, {s.LifeStage}) in initial quota file but not in contract"));

            if (problems.Count > 0)
            {
                throw new InvalidOperationException($"Initial quota file for scenario {scenarioName} does not match the simulation contract: {string.Join("; ", problems)}");
            }
        }

        private void InitForRun(float biomass, int iGroup)
        {
            var mseQuotaData = m_MSEQuotaData ?? throw new InvalidOperationException($"Simulation with Id {m_SimulationId} is not initialised");
            mseQuotaData.Bestimate[iGroup] = biomass;
            var RStock0 = mseQuotaData.RstockRatio[iGroup] * biomass;
            mseQuotaData.BhalfT[iGroup] = mseQuotaData.RHalfB0Ratio[iGroup] * biomass;
            mseQuotaData.Rmax[iGroup] = RStock0 * (mseQuotaData.RHalfB0Ratio[iGroup] + 1);  
        }
    }
}
