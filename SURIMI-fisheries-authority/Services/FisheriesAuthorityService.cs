using Grpc.Core;
using Grpc.Surimi;
using SURIMI.Common.gRPC;
using SURIMI.Common.gRPC.Services;

namespace SURIMI_fisheries_authority.Services
{
    public class FisheriesAuthorityService : Grpc.Surimi.FisheriesAuthorityService.FisheriesAuthorityServiceBase
    {
        private readonly ILogger<FisheriesAuthorityService> m_logger;
        private readonly string _version;

        public FisheriesAuthorityService(ILogger<FisheriesAuthorityService> logger, ProtocolVersionService protocolVersionService)
        {
            m_logger = logger;
            _version = protocolVersionService.LoadVersion();
        }

        public override async Task<UpdateCatchDispositionResponse> UpdateCatchDisposition(UpdateCatchDispositionRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received ConsumeCatch request for simulation {request.SimulationId}");
            // Here you would implement the logic to process the catch data based on the SimulationId
            // For demonstration purposes, we'll return a dummy response
            var response = new UpdateCatchDispositionResponse
            {
                SimulationId = request.SimulationId,
            };
            return await Task.FromResult(response);
        }

        public override async Task<InitialiseSimulationResponse> InitialiseSimulation(InitialiseSimulationRequest request, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioName);

            m_logger.LogInformation($"Initializing simulation {request.SimulationId}, with scenario {request.ScenarioName}...");

            try
            {
                var surimiContract = GetSurimiContract(request.Simulation);

                //var result = await m_controller.StartAsync();
                //if (result != 1)
                //{
                //    throw new RpcException(new Status(StatusCode.Internal, "Failed to initialise Fisheries Authority"));
                //}
                return new InitialiseSimulationResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error during InitialiseSimulation");
                throw;
            }
        }

        public override async Task<FinaliseSimulationResponse> FinaliseSimulation(FinaliseSimulationRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Finalizing simulation {request.SimulationId}");

            try
            {
                //var result = await m_controller.StopAsync();
                //if (result == false)
                //{
                //    throw new RpcException(new Status(StatusCode.Internal, "Failed to finalise Fisheries Authority"));
                //}
                return new FinaliseSimulationResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error during Finalise");
                throw;
            }
        }

        public override async Task<CancelSimulationResponse> CancelSimulation(CancelSimulationRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Cancel simulation {request.SimulationId}");

            try
            {
                //var result = await m_controller.StopAsync();
                //if (result == false)
                //{
                //    throw new RpcException(new Status(StatusCode.Internal, "Failed to cancel Fisheries Authority"));
                //}
                return new CancelSimulationResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error during CancelSimulation");
                throw;
            }
        }

        public override async Task<SimulateStepResponse> SimulateStep(SimulateStepRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Simulate step for simulation {request.SimulationId}");

            //var res = await m_controller.ContinueAsync();

            return new SimulateStepResponse() { SimulationId = request.SimulationId };
        }

        public override Task<GetProtocolVersionResponse> GetProtocolVersion(GetProtocolVersionRequest request, ServerCallContext context)
        {
            return Task.FromResult(new GetProtocolVersionResponse() { ProtocolVersion = _version });
        }

        public override async Task<GetRegulationsResponse> GetRegulations(GetRegulationsRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received GetRegulations request for simulation {request.SimulationId}");
            // Here you would implement the logic to retrieve the regulations based on the SimulationId
            // For demonstration purposes, we'll return a dummy response


            SURIMI.Datamodel.RegulationsSummary regulationsSummary = new SURIMI.Datamodel.RegulationsSummary
            {
                TotalAllowableCatches = new List<SURIMI.Datamodel.TotalAllowableCatch>
                {
                    new SURIMI.Datamodel.TotalAllowableCatch
                    {
                        Species = new SURIMI.Datamodel.Species()
                        {
                            SpeciesCode = "PIL",
                        },
                        FleetSegment = new SURIMI.Datamodel.FleetSegment()
                        {
                            GearCode = "ART",
                            CountryCode = "ESP"
                        },
                        Catch = 2323.34
                    },
                    new SURIMI.Datamodel.TotalAllowableCatch
                    {
                        Species = new SURIMI.Datamodel.Species()
                        {
                            SpeciesCode = "KHE",
                        },
                        FleetSegment = new SURIMI.Datamodel.FleetSegment()
                        {
                            GearCode = "OTB",
                            CountryCode = "ESP"
                        },
                        Catch = 500.0005
                    }
                }
            };

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
                                SpeciesCode = tac.Species.SpeciesCode
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
            return await Task.FromResult(response);
        }

        public override async Task<UpdateFishingActivityResponse> UpdateFishingActivity(UpdateFishingActivityRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received UpdateFishingActivity request for simulation {request.SimulationId}");
            var response = new UpdateFishingActivityResponse
            {
                SimulationId = request.SimulationId
            };
            return await Task.FromResult(response);
        }

        public override async Task<CreateRegulationsResponse> CreateRegulations(CreateRegulationsRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Received CreateRegulations request for simulation {request.SimulationId}");
            var response = new CreateRegulationsResponse
            {
                SimulationId = request.SimulationId
            };
            return await Task.FromResult(response);
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
                }
            };
        }
    }
}
