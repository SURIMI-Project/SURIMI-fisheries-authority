using Grpc.Core;
using Grpc.Surimi;

namespace SURIMI_fisheries_authority.Services
{
    public class FisheriesAuthorityCatchConsumerService : CatchConsumerService.CatchConsumerServiceBase
    {
        private readonly ILogger<FisheriesAuthorityCatchConsumerService> m_logger;

        public FisheriesAuthorityCatchConsumerService(ILogger<FisheriesAuthorityCatchConsumerService> logger)
        {
            m_logger = logger;
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
    }
}
