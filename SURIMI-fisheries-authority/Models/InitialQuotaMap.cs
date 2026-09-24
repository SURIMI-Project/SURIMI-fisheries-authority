namespace SURIMI_fisheries_authority.Models
{
    /// <summary>
    /// Maps a species (species code + life stage) to its initial Total Allowable Catch (TAC),
    /// as loaded from the scenario initial quota file.
    /// </summary>
    public class InitialQuotaMap
    {
        private readonly Dictionary<SpeciesKey, float> m_quotas = new();

        /// <summary>
        /// Adds the initial quota (TAC) for a species. Returns false if the key was already present.
        /// </summary>
        public bool Add(SpeciesKey speciesKey, float tac)
            => m_quotas.TryAdd(speciesKey, tac);

        public bool TryGetQuota(string speciesCode, string lifeStage, out float tac)
            => m_quotas.TryGetValue(new SpeciesKey(speciesCode, lifeStage), out tac);

        public IEnumerable<SpeciesKey> SpeciesKeys => m_quotas.Keys;

        public int SpeciesCount => m_quotas.Count;
    }
}
