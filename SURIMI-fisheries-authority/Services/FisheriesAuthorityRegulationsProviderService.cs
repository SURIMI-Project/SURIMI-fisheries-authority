using Grpc.Core;
using Grpc.Surimi;

namespace SURIMI_fisheries_authority.Services
{
    public class FisheriesAuthorityRegulationsProviderService : RegulationsProviderService.RegulationsProviderServiceBase
    {
        private readonly ILogger<FisheriesAuthorityRegulationsProviderService> m_logger;

        public FisheriesAuthorityRegulationsProviderService(ILogger<FisheriesAuthorityRegulationsProviderService> logger)
        {
            m_logger = logger;
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
    }
}
