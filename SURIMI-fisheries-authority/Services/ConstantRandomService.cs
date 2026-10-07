using EwECore.MSE;

namespace SURIMI_fisheries_authority.Services
{
    /// <summary>
    /// Deterministic <see cref="IRandomService"/>: no noise is introduced.
    /// Normal draws have mean 0, <see cref="RandNormDist"/> returns its mean, and uniform draws are fixed inside [0, 1).
    /// </summary>
    public sealed class ConstantRandomService : IRandomService
    {
        public const double UniformValue = 0.5;

        public float Normal2() => 1f;

        public float RandNormDist(float stdev, float mean) => mean;

        public float Normal() => 1f;

        public float RandomNormal() => 1f;

        public double NextDouble() => UniformValue;
    }
}
