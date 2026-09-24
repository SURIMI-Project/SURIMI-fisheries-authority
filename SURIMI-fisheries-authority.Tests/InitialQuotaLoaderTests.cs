using Eii.BlobStore;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class InitialQuotaLoaderTests
    {
        private const string ScenarioName = "test-scenario";

        private static InitialQuotaLoader CreateLoader(string csvContent)
        {
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync($"{ScenarioName}/{ScenarioName}_initial_quota.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            blobStore
                .Setup(bs => bs.ReadAllTextAsync($"{ScenarioName}/{ScenarioName}_initial_quota.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(csvContent);
            return new InitialQuotaLoader(blobStore.Object, NullLogger<InitialQuotaLoader>.Instance);
        }

        [Fact]
        public async Task Load_ParsesNarrowFormatFile()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,TAC\n" +
                "BOG,,25000\n" +
                "HKE,ADULT,40000\n");

            // Act
            var map = await loader.LoadAsync(ScenarioName);

            // Assert
            map.SpeciesCount.Should().Be(2);
            map.TryGetQuota("BOG", "", out var bogTac).Should().BeTrue();
            bogTac.Should().Be(25000f);
            map.TryGetQuota("HKE", "ADULT", out var hkeTac).Should().BeTrue();
            hkeTac.Should().Be(40000f);
        }

        [Fact]
        public async Task Load_ThrowsWhenFileMissing()
        {
            // Arrange
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync(It.IsAny<string>(), PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var loader = new InitialQuotaLoader(blobStore.Object, NullLogger<InitialQuotaLoader>.Instance);

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task Load_ThrowsOnInvalidHeader()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,quota\n" +
                "BOG,,25000\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>();
        }

        [Fact]
        public async Task Load_ThrowsOnMalformedTacValue()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,TAC\n" +
                "BOG,,abc\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>();
        }

        [Fact]
        public async Task Load_ThrowsOnDuplicateSpecies()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,TAC\n" +
                "BOG,,25000\n" +
                "BOG,,30000\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>();
        }
    }
}
