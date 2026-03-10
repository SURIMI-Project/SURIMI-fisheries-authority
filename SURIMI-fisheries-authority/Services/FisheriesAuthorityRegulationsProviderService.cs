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
            var response = new GetRegulationsResponse
            {
                SimulationId = request.SimulationId
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
            m_logger.LogInformation($"Received CreateRegulations request for regulationDefinitionId {request.RegulationDefinitionsId}");
            var response = new CreateRegulationsResponse
            {
                RegulationDefinitionsId = request.RegulationDefinitionsId
            };
            return await Task.FromResult(response);
        }
    }
}
