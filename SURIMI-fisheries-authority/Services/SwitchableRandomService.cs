using EwECore.MSE;

namespace SURIMI_fisheries_authority.Services
{
    public enum RandomMode
    {
        Random,
        Constant
    }

    /// <summary>
    /// Delegating <see cref="IRandomService"/> whose behaviour can be switched after the MSE components have been constructed.
    /// </summary>
    public sealed class SwitchableRandomService : IRandomService
    {
        private readonly IRandomService m_random = new cRandomService();
        private readonly IRandomService m_constant = new ConstantRandomService();

        public RandomMode Mode { get; set; } = RandomMode.Random;

        private IRandomService Current => Mode == RandomMode.Constant ? m_constant : m_random;

        public float Normal2() => Current.Normal2();

        public float RandNormDist(float stdev, float mean) => Current.RandNormDist(stdev, mean);

        public float Normal() => Current.Normal();

        public float RandomNormal() => Current.RandomNormal();

        public double NextDouble() => Current.NextDouble();
    }
}
