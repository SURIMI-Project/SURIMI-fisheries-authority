using FluentAssertions;
using SURIMI_fisheries_authority.Services;

namespace SURIMI_fisheries_authority.Tests
{
    public class SwitchableRandomServiceTests
    {
        [Fact]
        public void Mode_ConstantReturnsDeterministicValues()
        {
            // Arrange
            var service = new SwitchableRandomService { Mode = RandomMode.Constant };

            // Act
            var normal = service.Normal();
            var normal2 = service.Normal2();
            var randomNormal = service.RandomNormal();
            var randNormDist = service.RandNormDist(2f, 3f);
            var nextDouble = service.NextDouble();

            // Assert
            normal.Should().Be(1f);
            normal2.Should().Be(1f);
            randomNormal.Should().Be(1f);
            randNormDist.Should().Be(3f);
            nextDouble.Should().Be(ConstantRandomService.UniformValue);
        }

        [Fact]
        public void Mode_RandomReturnsUniformValuesInRange()
        {
            // Arrange
            var service = new SwitchableRandomService { Mode = RandomMode.Random };

            // Act
            var values = Enumerable.Range(0, 100).Select(_ => service.NextDouble()).ToList();

            // Assert
            values.Should().OnlyContain(v => v >= 0.0 && v < 1.0);
            values.Distinct().Count().Should().BeGreaterThan(1);
        }
    }
}
