namespace SURIMI_fisheries_authority.Models
{
    public readonly record struct SpeciesKey
    {
        public string SpeciesCode { get; }
        public string LifeStage { get; }

        public SpeciesKey(string speciesCode, string lifeStage)
        {
            SpeciesCode = speciesCode?.ToUpperInvariant() ?? string.Empty;
            LifeStage = lifeStage?.ToUpperInvariant() ?? string.Empty;
        }
    }

    /// <summary>
    /// Maps a species (species code + life stage) to its group index in the MSEQuotaData arrays.
    /// </summary>
    public class SpeciesGroupMap
    {
        private readonly Dictionary<SpeciesKey, int> m_groupIndices = new();

        /// <summary>
        /// Adds a mapping. Returns false if the key was already present.
        /// </summary>
        public bool Add(string speciesCode, string lifeStage, out int groupIndex)
        {
            groupIndex = m_groupIndices.Count + 1;
            return m_groupIndices.TryAdd(new SpeciesKey(speciesCode, lifeStage), groupIndex);
        }

        public bool TryGetGroupIndex(string speciesCode, string lifeStage, out int groupIndex)
            => m_groupIndices.TryGetValue(new SpeciesKey(speciesCode, lifeStage), out groupIndex);
    }
}
