using Grpc.Core;
using Grpc.Surimi;

namespace SURIMI_fisheries_authority.Services
{
    public class FisheriesAuthorityWorkflowService : WorkflowService.WorkflowServiceBase
    {
        private readonly ILogger<FisheriesAuthorityWorkflowService> m_logger;

        public FisheriesAuthorityWorkflowService(ILogger<FisheriesAuthorityWorkflowService> logger)
        {
            m_logger = logger;
        }

        public override async Task<InitialiseResponse> Initialise(InitialiseRequest request, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);


            m_logger.LogInformation($"Initializing simulation {request.SimulationId}, with scenario {request.ScenarioId}...");

            try
            {
                var surimiConfiguration = GetSurimiConfiguration(request.Simulation);

                //var result = await m_controller.StartAsync();
                //if (result != 1)
                //{
                //    throw new RpcException(new Status(StatusCode.Internal, "Failed to initialise Value Chain"));
                //}
                return new InitialiseResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error during Initialise");
                throw;
            }
        }

        public override async Task<FinaliseResponse> Finalise(FinaliseRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Finalizing simulation {request.SimulationId}");

            try
            {
                //var result = await m_controller.StopAsync();
                //if (result == false)
                //{
                //    throw new RpcException(new Status(StatusCode.Internal, "Failed to finalise Value Chain"));
                //}
                return new FinaliseResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error during Finalise");
                throw;
            }
        }

        public override async Task<CancelResponse> Cancel(CancelRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Cancel simulation {request.SimulationId}");

            try
            {
                //var result = await m_controller.StopAsync();
                //if (result == false)
                //{
                //    throw new RpcException(new Status(StatusCode.Internal, "Failed to cancel Value Chain"));
                //}
                return new CancelResponse() { SimulationId = request.SimulationId };
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error during Cancel");
                throw;
            }
        }

        public override async Task<SimulateStepResponse> SimulateStep(SimulateStepRequest request, ServerCallContext context)
        {
            m_logger.LogInformation($"Simulate step for simulation {request.SimulationId}");

            //var res = await m_controller.ContinueAsync();

            return new SimulateStepResponse() { SimulationId = request.SimulationId };
        }

        /// <summary>
        /// Mapping method from gRPC Surimi Simulation to SURIMI Datamodel SurimiConfiguration
        /// </summary>
        /// <param name="simulation"></param>
        /// <returns></returns>
        private SURIMI.Datamodel.SurimiConfiguration GetSurimiConfiguration(Grpc.Surimi.Simulation simulation)
        {
            return new SURIMI.Datamodel.SurimiConfiguration
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
