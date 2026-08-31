using FluentAssertions;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class QuotaShareLoaderTests
    {
        private const string ScenarioName = "test-scenario";

        private static string CreateQuotaShareFolder(string csvContent)
        {
            var folder = Path.Combine(Path.GetTempPath(), $"quota-shares-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"{ScenarioName}-quotashare.csv"), csvContent);
            return folder;
        }

        [Fact]
        public void Load_ParsesWideFormatFile()
        {
            // Arrange
            var folder = CreateQuotaShareFolder(
                "species_code;life_stage;ART,ESP;ART,FRA;OTB,ESP;Sum\n" +
                "PIL;;0,5;0,25;0,25;1\n" +
                "KHE;ADULT;0,75;;0,25;1\n");
            var loader = new QuotaShareLoader(folder);

            // Act
            var map = loader.Load(ScenarioName);

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
        public void Load_ThrowsWhenFileMissing()
        {
            // Arrange
            var folder = Path.Combine(Path.GetTempPath(), $"quota-shares-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            var loader = new QuotaShareLoader(folder);

            // Act & Assert
            var act = () => loader.Load(ScenarioName);
            act.Should().Throw<FileNotFoundException>();
        }

        [Fact]
        public void Load_ThrowsWhenSharesDoNotSumToOne()
        {
            // Arrange
            var folder = CreateQuotaShareFolder(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;0,6;0,3;1\n");
            var loader = new QuotaShareLoader(folder);

            // Act & Assert
            var act = () => loader.Load(ScenarioName);
            act.Should().Throw<InvalidDataException>().WithMessage("*PIL*sum*");
        }

        [Fact]
        public void Load_ThrowsOnMalformedShareValue()
        {
            // Arrange
            var folder = CreateQuotaShareFolder(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;abc;0,5;1\n");
            var loader = new QuotaShareLoader(folder);

            // Act & Assert
            var act = () => loader.Load(ScenarioName);
            act.Should().Throw<InvalidDataException>().WithMessage("*abc*");
        }

        [Fact]
        public void Load_ThrowsOnMissingColumns()
        {
            // Arrange
            var folder = CreateQuotaShareFolder(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;1\n");
            var loader = new QuotaShareLoader(folder);

            // Act & Assert
            var act = () => loader.Load(ScenarioName);
            act.Should().Throw<InvalidDataException>().WithMessage("*columns*");
        }

        [Fact]
        public void Load_ThrowsOnDuplicateSpeciesRow()
        {
            // Arrange
            var folder = CreateQuotaShareFolder(
                "species_code;life_stage;ART,ESP;OTB,ESP;Sum\n" +
                "PIL;;0,5;0,5;1\n" +
                "PIL;;1;;1\n");
            var loader = new QuotaShareLoader(folder);

            // Act & Assert
            var act = () => loader.Load(ScenarioName);
            act.Should().Throw<InvalidDataException>().WithMessage("*Duplicate species*");
        }

        [Fact]
        public void Load_ThrowsOnInvalidFleetColumn()
        {
            // Arrange
            var folder = CreateQuotaShareFolder(
                "species_code;life_stage;ARTESP;Sum\n" +
                "PIL;;1;1\n");
            var loader = new QuotaShareLoader(folder);

            // Act & Assert
            var act = () => loader.Load(ScenarioName);
            act.Should().Throw<InvalidDataException>().WithMessage("*fleet column*");
        }
    }
}
