using FluentAssertions;
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
                .Setup(qcs => qcs.CreateRegulationsAsync(It.IsAny<SURIMI.Datamodel.RegulationDefinitionsSummary>()))
                .Callback<SURIMI.Datamodel.RegulationDefinitionsSummary>(summary => capturedSummary = summary)
                .Returns(Task.CompletedTask);
            var request = new CreateRegulationsRequest { SimulationId = SimulationId };

            // Act
            var response = await service.CreateRegulations(request, null!);

            // Assert
            response.SimulationId.Should().Be(SimulationId);
            capturedSummary.Should().NotBeNull();
            capturedSummary!.TargetFishingMortalities.Should().NotBeNull();
            capturedSummary.TargetFishingMortalities.Should().BeEmpty();
        }
    }
}
