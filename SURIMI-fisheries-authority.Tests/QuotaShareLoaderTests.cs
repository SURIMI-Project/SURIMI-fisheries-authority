using Eii.BlobStore;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class QuotaShareLoaderTests
    {
        private const string ScenarioName = "test-scenario";

        private static QuotaShareLoader CreateLoader(string csvContent)
        {
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync($"{ScenarioName}/{ScenarioName}_quotashare.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            blobStore
                .Setup(bs => bs.ReadAllTextAsync($"{ScenarioName}/{ScenarioName}_quotashare.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(csvContent);
            return new QuotaShareLoader(blobStore.Object, NullLogger<QuotaShareLoader>.Instance);
        }

        [Fact]
        public async Task Load_ParsesWideFormatFile()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code;life_stage;ART,ESP;ART,FRA;OTB,ESP;Sum\n" +
                "PIL;;0,5;0,25;0,25;1\n" +
                "KHE;ADULT;0,75;;0,25;1\n");

            // Act
            var map = await loader.LoadAsync(ScenarioName);

            // Assert
            map.Fleets.Should().HaveCount(3);
            map.SpeciesCount.Should().Be(2);

            map.TryGetShares("PIL", "", out var pilShares).Should().BeTrue();
            pilShares.Should().HaveCount(3);
            pilShares[0].Fleet.GearCode.Should().Be("ART");
            pilShares[0].Fleet.CountryCode.Should().Be("ESP");
            pilShares[0].Share.Should().Be(0.5f);
            pilShares[1].Share.Should().Be(0.25f);

            // Empty cell means the fleet has no share
            map.TryGetShares("KHE", "ADULT", out var kheShares).Should().BeTrue();
            kheShares.Should().HaveCount(2);
            kheShares[0].Share.Should().Be(0.75f);
            kheShares[1].Share.Should().Be(0.25f);
        }

        [Fact]
        public async Task Load_ThrowsWhenFileMissing()
        {
            // Arrange
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync(It.IsAny<string>(), PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var loader = new QuotaShareLoader(blobStore.Object, NullLogger<QuotaShareLoader>.Instance);

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task Load_ThrowsWhenSharesDoNotSumToOne()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;0,6;0,3;1\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*PIL*sum*");
        }

        [Fact]
        public async Task Load_ThrowsOnMalformedShareValue()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;abc;0,5;1\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*abc*");
        }

        [Fact]
        public async Task Load_ThrowsOnMissingColumns()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;1\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*columns*");
        }

        [Fact]
        public async Task Load_ThrowsOnDuplicateSpeciesRow()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;0,5;0,5;1\n" +
                "PIL;;1;;1\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*Duplicate species*");
        }

        [Fact]
        public async Task Load_ThrowsOnInvalidFleetColumn()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code;life_stage;ARTESP;Sum\n" +
                "PIL;;1;1\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*fleet column*");
        }
    }
}
