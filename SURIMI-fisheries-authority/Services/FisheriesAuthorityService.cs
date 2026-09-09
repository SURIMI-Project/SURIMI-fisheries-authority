using Grpc.Core;
using Grpc.Surimi;
using SURIMI.Common.gRPC;
using SURIMI.Common.gRPC.Services;

namespace SURIMI_fisheries_authority.Services
{
    public class FisheriesAuthorityService : Grpc.Surimi.FisheriesAuthorityService.FisheriesAuthorityServiceBase
    {
        private readonly ILogger<FisheriesAuthorityService> m_logger;
        private readonly SimulationScopeManager m_simulationScopeManager;
        private readonly string _version;

        public FisheriesAuthorityService(ILogger<FisheriesAuthorityService> logger, ProtocolVersionService protocolVersionService, SimulationScopeManager simulationScopeManager)
        {
            m_logger = logger;
            m_simulationScopeManager = simulationScopeManager;
            _version = protocolVersionService.LoadVersion();
        }

        public override async Task<UpdateCatchDispositionResponse> UpdateCatchDisposition(UpdateCatchDispositionRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received UpdateCatchDisposition request for simulation {request.SimulationId}");
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);

            try
            {
                var catchDispositionSummary = GetCatchDispositionSummary(request.CatchDispositionSummary);
                await m_simulationScopeManager.GetService(request.SimulationId).UpdateCatchDispositionAsync(request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime(), catchDispositionSummary);

                return new UpdateCatchDispositionResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during UpdateCatchDisposition for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<InitialiseSimulationResponse> InitialiseSimulation(InitialiseSimulationRequest request, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioName);

            m_logger.LogInformation($"Initializing simulation {request.SimulationId}, with scenario {request.ScenarioName}...");

            try
            {
                var surimiContract = GetSurimiContract(request.Simulation);
                var quotaCalculationService = m_simulationScopeManager.CreateSimulationScope(request.SimulationId);
                try
                {
                    await quotaCalculationService.InitialiseSimulationAsync(request.SimulationId, request.ScenarioName, surimiContract);
                }
                catch
                {
                    m_simulationScopeManager.RemoveSimulationScope(request.SimulationId);
                    throw;
                }

                return new InitialiseSimulationResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during InitialiseSimulation for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<FinaliseSimulationResponse> FinaliseSimulation(FinaliseSimulationRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Finalizing simulation {request.SimulationId}");

            try
            {
                var quotaCalculationService = m_simulationScopeManager.GetService(request.SimulationId);
                try
                {
                    await quotaCalculationService.FinaliseSimulationAsync(context.CancellationToken);
                }
                finally
                {
                    m_simulationScopeManager.RemoveSimulationScope(request.SimulationId);
                }
                return new FinaliseSimulationResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during FinaliseSimulation for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<CancelSimulationResponse> CancelSimulation(CancelSimulationRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Cancel simulation {request.SimulationId}");

            try
            {
                var quotaCalculationService = m_simulationScopeManager.GetService(request.SimulationId);
                try
                {
                    await quotaCalculationService.CancelSimulationAsync(context.CancellationToken);
                }
                finally
                {
                    m_simulationScopeManager.RemoveSimulationScope(request.SimulationId);
                }
                return new CancelSimulationResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during CancelSimulation for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<SimulateStepResponse> SimulateStep(SimulateStepRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Simulate step for simulation {request.SimulationId}");

            try
            {
                await m_simulationScopeManager.GetService(request.SimulationId).SimulateStepAsync();
                return new SimulateStepResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during SimulateStep for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override Task<GetProtocolVersionResponse> GetProtocolVersion(GetProtocolVersionRequest request, ServerCallContext context)
        {
            return Task.FromResult(new GetProtocolVersionResponse() { ProtocolVersion = _version });
        }

        public override async Task<GetRegulationsResponse> GetRegulations(GetRegulationsRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received GetRegulations request for simulation {request.SimulationId}");
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);

            try
            {
                var regulationsSummary = await m_simulationScopeManager.GetService(request.SimulationId).GetRegulationsAsync(request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime());

                var response = new GetRegulationsResponse
                {
                    SimulationId = request.SimulationId,
                    StartDateTime = request.StartDateTime,
                    EndDateTime = request.EndDateTime,
                    RegulationsSummary = new RegulationsSummary
                    {
                        TotalAllowableCatches =
                        {
                            regulationsSummary.TotalAllowableCatches.Select(tac => new TotalAllowableCatch
                            {
                                Species = new Species
                                {
                                    SpeciesCode = tac.Species.SpeciesCode,
                                    LifeStage = tac.Species.LifeStage
                                },
                                FleetSegment = new FleetSegment
                                {
                                    GearCode = tac.FleetSegment.GearCode,
                                    CountryCode = tac.FleetSegment.CountryCode
                                },
                                Catch = tac.Catch
                            })
                        }
                    }
                };
                return response;
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during GetRegulations for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<UpdateFishingActivityResponse> UpdateFishingActivity(UpdateFishingActivityRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received UpdateFishingActivity request for simulation {request.SimulationId}");
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);

            try
            {
                var fishingActivitySummary = GetFishingActivitySummary(request.FishingActivitySummary);
                await m_simulationScopeManager.GetService(request.SimulationId).UpdateFishingActivityAsync(request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime(), fishingActivitySummary);
                return new UpdateFishingActivityResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during UpdateFishingActivity for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<CreateRegulationsResponse> CreateRegulations(CreateRegulationsRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received CreateRegulations request for simulation {request.SimulationId}");
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);

            try
            {
                var regulationDefinitionsSummary = GetRegulationDefinitionsSummary(request.RegulationsSummary);
                await m_simulationScopeManager.GetService(request.SimulationId).CreateRegulationsAsync(regulationDefinitionsSummary, context.CancellationToken);
                return new CreateRegulationsResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during CreateRegulations for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<UpdateBiomassResponse> UpdateBiomass(UpdateBiomassRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received UpdateBiomass request for simulation {request.SimulationId}");
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);

            try
            {
                var biomassGrids = GetBiomassGrids(request.BiomassSummary);
                await m_simulationScopeManager.GetService(request.SimulationId).UpdateBiomassAsync(request.DateTime.ToDateTime(), biomassGrids);
                return new UpdateBiomassResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, $"Error during UpdateBiomass for simulation {request.SimulationId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        /// <summary>
        /// Mapping method from gRPC Surimi BiomassSummary to SURIMI Datamodel BiomassGrids
        /// </summary>
        /// <param name="biomassSummary"></param>
        /// <returns></returns>
        private static List<SURIMI.Datamodel.BiomassGrid> GetBiomassGrids(BiomassSummary biomassSummary)
        {
            return biomassSummary.BiomassGrids
                .Select(grid => new SURIMI.Datamodel.BiomassGrid
                {
                    Species = GetSpecies(grid.Species),
                    BiomassCells = grid.BiomassCells
                        .Select(grpcCell => new SURIMI.Datamodel.BiomassCell
                        {
                            Biomass = grpcCell.Biomass,
                            Latitude = grpcCell.Latitude,
                            Longitude = grpcCell.Longitude
                        })
                        .ToList()
                })
                .ToList();
        }

        /// <summary>
        /// Mapping method from gRPC Surimi CatchDispositionSummary to SURIMI Datamodel CatchDispositionSummary
        /// </summary>
        /// <param name="catchDispositionSummary"></param>
        /// <returns></returns>
        private static SURIMI.Datamodel.CatchDispositionSummary GetCatchDispositionSummary(CatchDispositionSummary catchDispositionSummary)
        {
            return new SURIMI.Datamodel.CatchDispositionSummary
            {
                DispositionGrids = catchDispositionSummary.DispositionGrids
                    .Select(grid => new SURIMI.Datamodel.DispositionGrid
                    {
                        Species = GetSpecies(grid.Species),
                        FleetSegment = GetFleetSegment(grid.FleetSegment),
                        DispositionCells = grid.DispositionCells
                            .Select(grpcCell => new SURIMI.Datamodel.DispositionCell
                            {
                                Longitude = grpcCell.Longitude,
                                Latitude = grpcCell.Latitude,
                                GrossCatchBiomass = grpcCell.GrossCatch,
                                LiveDiscardsBiomass = grpcCell.LiveDiscards,
                                DeadDiscardsBiomass = grpcCell.DeadDiscards
                            })
                            .ToList()
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Mapping method from gRPC Surimi FishingActivitySummary to SURIMI Datamodel FishingActivitySummary
        /// </summary>
        /// <param name="fishingActivitySummary"></param>
        /// <returns></returns>
        private static SURIMI.Datamodel.FishingActivitySummary GetFishingActivitySummary(FishingActivitySummary fishingActivitySummary)
        {
            return new SURIMI.Datamodel.FishingActivitySummary
            {
                FishingActivities = fishingActivitySummary.FishingActivities
                    .Select(activity => new SURIMI.Datamodel.FishingActivity
                    {
                        FleetSegment = GetFleetSegment(activity.FleetSegment),
                        FishingActivityRatio = activity.FishingActivityRatio
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Mapping method from gRPC Surimi RegulationDefinitionsSummary to SURIMI Datamodel RegulationDefinitionsSummary
        /// </summary>
        /// <param name="regulationDefinitionsSummary"></param>
        /// <returns></returns>
        private static SURIMI.Datamodel.RegulationDefinitionsSummary GetRegulationDefinitionsSummary(RegulationDefinitionsSummary? regulationDefinitionsSummary)
        {
            return new SURIMI.Datamodel.RegulationDefinitionsSummary
            {
                TargetFishingMortalities = regulationDefinitionsSummary?.TargetFishingMortalities?
                    .Select(tfm => new SURIMI.Datamodel.TargetFishingMortality
                    {
                        Species = GetSpecies(tfm.Species),
                        BiomassLimit = tfm.BiomassLimit,
                        BiomassBase = tfm.BiomassBase,
                        FMax = tfm.FMax
                    })
                    .ToList() ?? new List<SURIMI.Datamodel.TargetFishingMortality>()    // if no TargetFishingMortalities are provided, return an empty list
            };
        }

        /// <summary>
        /// Mapping method from gRPC Surimi Species to SURIMI Datamodel Species
        /// </summary>
        /// <param name="species"></param>
        /// <returns></returns>
        private static SURIMI.Datamodel.Species GetSpecies(Species species)
        {
            return new SURIMI.Datamodel.Species
            {
                SpeciesCode = species.SpeciesCode,
                LengthClass = species.LengthClass ?? string.Empty,
                Age = species.Age ?? string.Empty,
                LifeStage = species.LifeStage ?? string.Empty
            };
        }

        /// <summary>
        /// Mapping method from gRPC Surimi FleetSegment to SURIMI Datamodel FleetSegment
        /// </summary>
        /// <param name="fleetSegment"></param>
        /// <returns></returns>
        private static SURIMI.Datamodel.FleetSegment GetFleetSegment(FleetSegment fleetSegment)
        {
            return new SURIMI.Datamodel.FleetSegment
            {
                GearCode = fleetSegment.GearCode,
                VesselLengthClass = fleetSegment.VesselLengthClass,
                Scale = fleetSegment.Scale,
                CountryCode = fleetSegment.CountryCode
            };
        }

        /// <summary>
        /// Mapping method from gRPC Surimi Simulation to SURIMI Datamodel SurimiContract
        /// </summary>
        /// <param name="simulation"></param>
        /// <returns></returns>
        private SURIMI.Datamodel.SurimiContract GetSurimiContract(Grpc.Surimi.Simulation simulation)
        {
            return new SURIMI.Datamodel.SurimiContract
            {
                Simulation = new SURIMI.Datamodel.Simulation()
                {
                    CaseStudyName = simulation.CaseStudyName,
                    StartDateTime = simulation.StartDateTime.ToDateTime(),
                    MaximumEndDateTime = simulation.MaximumEndDateTime.ToDateTime(),
                    TimeStep = simulation.TimeStep,
                    Geography = new SURIMI.Datamodel.Geography()
                    {
                        Crs = new SURIMI.Datamodel.CoordinateReferenceSystem()
                        {
                            Authority = simulation.Geography.Crs.Authority,
                            Code = simulation.Geography.Crs.Code,
                            Name = simulation.Geography.Crs.Name
                        },
                        RasterCellOrigin = Enum.Parse<SURIMI.Datamodel.RasterCellOrigin>(simulation.Geography.RasterCellOrigin.ToString()),
                        Xres = simulation.Geography.Xres,
                        Yres = simulation.Geography.Yres,
                        Ncol = simulation.Geography.Ncol,
                        Nrow = simulation.Geography.Nrow,
                        Xmin = simulation.Geography.Xmin,
                        Xmax = simulation.Geography.Xmax,
                        Ymin = simulation.Geography.Ymin,
                        Ymax = simulation.Geography.Ymax
                    }
                },
                Standards = new SURIMI.Datamodel.Standards()
                {
                    Currency = simulation.Standards.Currency,
                    CountryCode = simulation.Standards.CountryCode,
                    //CategoryCode = surimiContract.Standards?.CategoryCode ?? string.Empty,        TODO
                    DateAndTime = simulation.Standards.DateAndTime,
                    GearCode = simulation.Standards.GearCode,
                    LifeStage = simulation.Standards.LifeStage,
                    MarketCode = simulation.Standards.MarketCode,
                    SpeciesCode = simulation.Standards.SpeciesCode,
                    Measurements = new SURIMI.Datamodel.Measurement()
                    {
                        System = simulation.Standards.Measurements.System,
                        Units = simulation.Standards.Measurements.Units
                            .Select(u => new SURIMI.Datamodel.UnitType
                            {
                                Quantity = u.Quantity ?? string.Empty,
                                Unit = u.Unit_ ?? string.Empty, // Unit_ because 'unit' may be reserved in proto
                            })
                            .ToList()
                    }
                },
                Items = new SURIMI.Datamodel.Items()
                {
                    Species = simulation.Items.Species
                        .Select(s => new SURIMI.Datamodel.Species
                        {
                            SpeciesCode = s.SpeciesCode,
                            LengthClass = s.LengthClass,
                            Age = s.Age,
                            LifeStage = s.LifeStage
                        })
                        .ToList(),
                    FleetSegments = simulation.Items.FleetSegments
                        .Select(f => new SURIMI.Datamodel.FleetSegment
                        {
                            GearCode = f.GearCode,
                            VesselLengthClass = f.VesselLengthClass,
                            Scale = f.Scale,
                            CountryCode = f.CountryCode,
                        })
                        .ToList(),
                    Currencies = simulation.Items.Currencies
                        .Select(c => new SURIMI.Datamodel.Currency
                        {
                            CurrencyCode = c.Code
                        })
                        .ToList(),
                    Markets = simulation.Items.Markets
                        .Select(c => new SURIMI.Datamodel.Market
                        {
                            MarketCode = c.MarketCode,
                        })
                        .ToList(),
                    Price_Categories = simulation.Items.PriceCategories
                        .Select(c => new SURIMI.Datamodel.PriceCategory
                        {
                            CategoryCode = c.CategoryCode,
                        })
                        .ToList()
                }
            };
        }
    }
}
