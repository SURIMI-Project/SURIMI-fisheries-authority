using Eii.BlobStore;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class RecruitmentLoaderTests
    {
        private const string ScenarioName = "test-scenario";

        private static RecruitmentLoader CreateLoader(string csvContent)
        {
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync($"{ScenarioName}/{ScenarioName}_recruitment.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            blobStore
                .Setup(bs => bs.ReadAllTextAsync($"{ScenarioName}/{ScenarioName}_recruitment.csv", PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(csvContent);
            return new RecruitmentLoader(blobStore.Object, NullLogger<RecruitmentLoader>.Instance);
        }

        [Fact]
        public async Task Load_ParsesNarrowFormatFile()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec\n" +
                "BOG,,0.893,0.2,0.8\n" +
                "HKE,ADULT,0.7134952,0.25,0.8\n");

            // Act
            var map = await loader.LoadAsync(ScenarioName, CancellationToken.None);

            // Assert
            map.SpeciesCount.Should().Be(2);

            map.TryGetConfiguration("BOG", "", out var bog).Should().BeTrue();
            bog.RstockRatio.Should().Be(0.893f);
            bog.RHalfB0Ratio.Should().Be(0.2f);
            bog.cvRec.Should().Be(0.8f);

            map.TryGetConfiguration("HKE", "ADULT", out var hke).Should().BeTrue();
            hke.RstockRatio.Should().Be(0.7134952f);
            hke.RHalfB0Ratio.Should().Be(0.25f);
            hke.cvRec.Should().Be(0.8f);
        }

        [Fact]
        public async Task Load_ThrowsWhenFileMissing()
        {
            // Arrange
            var blobStore = new Mock<IBlobStore>();
            blobStore
                .Setup(bs => bs.ExistsAsync(It.IsAny<string>(), PathType.Input, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var loader = new RecruitmentLoader(blobStore.Object, NullLogger<RecruitmentLoader>.Instance);

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName, CancellationToken.None);
            await act.Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task Load_ThrowsOnMalformedValue()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec\n" +
                "BOG,,abc,0.2,0.8\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*RstockRatio*abc*");
        }

        [Fact]
        public async Task Load_ThrowsOnMissingColumns()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec\n" +
                "BOG,,0.893,0.2\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*columns*");
        }

        [Fact]
        public async Task Load_ThrowsOnDuplicateSpeciesRow()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec\n" +
                "BOG,,0.893,0.2,0.8\n" +
                "BOG,,0.5,0.2,0.8\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*Duplicate species*");
        }

        [Fact]
        public async Task Load_ThrowsOnInvalidHeader()
        {
            // Arrange
            var loader = CreateLoader(
                "species_code,life_stage,RstockRatio,cvRec\n" +
                "BOG,,0.893,0.8\n");

            // Act & Assert
            var act = () => loader.LoadAsync(ScenarioName, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*invalid header*");
        }
    }
}
