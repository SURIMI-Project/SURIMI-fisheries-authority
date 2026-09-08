using FluentAssertions;
using Grpc.Core;
using Grpc.Surimi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI.Common.gRPC.Services;
using SURIMI_fisheries_authority.Services;
using FisheriesAuthorityService = SURIMI_fisheries_authority.Services.FisheriesAuthorityService;

namespace SURIMI_fisheries_authority.Tests
{
    public class FisheriesAuthorityServiceTests
    {
        private const string SimulationId = "test-simulation";

        private sealed class TestServerCallContext : ServerCallContext
        {
            protected override string MethodCore => "test-method";
            protected override string HostCore => "test-host";
            protected override string PeerCore => "test-peer";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore => new();
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
            protected override Metadata ResponseTrailersCore => new();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions? WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());
            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotImplementedException();
            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }

        private static (FisheriesAuthorityService Service, Mock<IQuotaCalculationService> QuotaCalculationService) CreateServiceWithMocks()
        {
            var quotaCalculationService = new Mock<IQuotaCalculationService>();

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(sp => sp.GetService(typeof(IQuotaCalculationService)))
                .Returns(quotaCalculationService.Object);

            var serviceScope = new Mock<IServiceScope>();
            serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

            var serviceScopeFactory = new Mock<IServiceScopeFactory>();
            serviceScopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

            var simulationScopeManager = new SimulationScopeManager(serviceScopeFactory.Object);
            simulationScopeManager.CreateSimulationScope(SimulationId);

            var service = new FisheriesAuthorityService(
                NullLogger<FisheriesAuthorityService>.Instance,
                new ProtocolVersionService(),
                simulationScopeManager);

            return (service, quotaCalculationService);
        }

        [Fact]
        public async Task CreateRegulations_CreatesEmptyTargetFishingMortalitiesWhenRegulationsSummaryIsNull()
        {
            // Arrange
            var (service, quotaCalculationService) = CreateServiceWithMocks();
            SURIMI.Datamodel.RegulationDefinitionsSummary? capturedSummary = null;
            quotaCalculationService
                .Setup(qcs => qcs.CreateRegulationsAsync(It.IsAny<SURIMI.Datamodel.RegulationDefinitionsSummary>(), It.IsAny<CancellationToken>()))
                .Callback<SURIMI.Datamodel.RegulationDefinitionsSummary, CancellationToken>((summary, _) => capturedSummary = summary)
                .Returns(Task.CompletedTask);
            var request = new CreateRegulationsRequest { SimulationId = SimulationId };

            // Act
            var response = await service.CreateRegulations(request, new TestServerCallContext());

            // Assert
            response.SimulationId.Should().Be(SimulationId);
            capturedSummary.Should().NotBeNull();
            capturedSummary!.TargetFishingMortalities.Should().NotBeNull();
            capturedSummary.TargetFishingMortalities.Should().BeEmpty();
        }
    }
}
