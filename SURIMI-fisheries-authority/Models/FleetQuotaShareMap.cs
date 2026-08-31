namespace SURIMI_fisheries_authority.Models
{
    public readonly record struct FleetKey
    {
        public string GearCode { get; }
        public string CountryCode { get; }

        public FleetKey(string gearCode, string countryCode)
        {
            GearCode = gearCode?.ToUpperInvariant() ?? string.Empty;
            CountryCode = countryCode?.ToUpperInvariant() ?? string.Empty;
        }
    }

    /// <summary>
    /// Maps a species (species code + life stage) to the fraction of its quota allocated to each fleet.
    /// The shares of a species always sum to exactly 1.
    /// </summary>
    public class FleetQuotaShareMap
    {
        private readonly Dictionary<SpeciesKey, IReadOnlyList<(FleetKey Fleet, float Share)>> m_shares = new();

        public FleetQuotaShareMap(IReadOnlyList<FleetKey> fleets)
        {
            Fleets = fleets;
        }

        /// <summary>
        /// All fleets defined in the quota share configuration, including fleets without any share.
        /// </summary>
        public IReadOnlyList<FleetKey> Fleets { get; }

        /// <summary>
        /// Adds the fleet shares for a species. Returns false if the key was already present.
        /// </summary>
        public bool Add(SpeciesKey speciesKey, IReadOnlyList<(FleetKey Fleet, float Share)> shares)
            => m_shares.TryAdd(speciesKey, shares);

        public bool TryGetShares(string speciesCode, string lifeStage, out IReadOnlyList<(FleetKey Fleet, float Share)> shares)
            => m_shares.TryGetValue(new SpeciesKey(speciesCode, lifeStage), out shares!);

        public IEnumerable<SpeciesKey> SpeciesKeys => m_shares.Keys;

        public int SpeciesCount => m_shares.Count;
    }
}
