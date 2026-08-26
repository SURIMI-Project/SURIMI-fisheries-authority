using EwECore.MSE;
using SURIMI.Datamodel;

namespace SURIMI_fisheries_authority.Models
{
    public class Simulation
    {
        public string SimulationId { get; set; }
        public SurimiContract SurimiContract { get; set; }
        public IMSEQuotaData MSEQuotaData { get; set; }
        public SpeciesGroupMap SpeciesGroupMap { get; }
        public float[] Biomass { get; }
        public Simulation(string simulationId, SurimiContract surimiContract, IMSEQuotaData mSEQuotaData, SpeciesGroupMap speciesGroupMap)
        {
            SimulationId = simulationId;
            SurimiContract = surimiContract;
            MSEQuotaData = mSEQuotaData;
            SpeciesGroupMap = speciesGroupMap;
            Biomass = new float[mSEQuotaData.nGroups];
        }
    }
}
