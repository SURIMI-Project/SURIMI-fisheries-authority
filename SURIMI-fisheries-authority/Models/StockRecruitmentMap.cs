namespace SURIMI_fisheries_authority.Models
{
    /// <summary>
    /// The stock recruitment configuration for a single species, as loaded from the
    /// scenario recruitment file. Property names match the recruitment file columns.
    /// </summary>
    public class StockRecruitmentConfiguration
    {
        public float RstockRatio { get; }
        public float RHalfB0Ratio { get; }
        public float cvRec { get; }

        public StockRecruitmentConfiguration(float rstockRatio, float rHalfB0Ratio, float cvRec)
        {
            RstockRatio = rstockRatio;
            RHalfB0Ratio = rHalfB0Ratio;
            this.cvRec = cvRec;
        }
    }

    /// <summary>
    /// Maps a species (species code + life stage) to its stock recruitment configuration.
    /// </summary>
    public class StockRecruitmentMap
    {
        private readonly Dictionary<SpeciesKey, StockRecruitmentConfiguration> m_configurations = new();

        /// <summary>
        /// Adds the recruitment configuration for a species. Returns false if the key was already present.
        /// </summary>
        public bool Add(SpeciesKey speciesKey, StockRecruitmentConfiguration configuration)
            => m_configurations.TryAdd(speciesKey, configuration);

        public bool TryGetConfiguration(string speciesCode, string lifeStage, out StockRecruitmentConfiguration configuration)
            => m_configurations.TryGetValue(new SpeciesKey(speciesCode, lifeStage), out configuration!);

        public IEnumerable<SpeciesKey> SpeciesKeys => m_configurations.Keys;

        public int SpeciesCount => m_configurations.Count;
    }
}
