using EwECore.MSE;

namespace SURIMI_fisheries_authority.Models
{
    public class MSEQuotaData : IMSEQuotaData
    {
        public int nGroups { get; }
        public int nLiving { get; }
        public int nFleets { get; }
        public float[] TAC { get; set; }
        public float[] FixedEscapement { get; set; }
        public float[] FixedF { get; set; }
        public float[] Fopt { get; set; }
        public float[] Fmin { get; set; }
        public float[] Bbase { get; set; }
        public float[] Blim { get; set; }
        public float[] Bestimate { get; set; }
        public float[] CVbiomEst { get; set; }
        public float[] FTarget { get; set; }
        public float[,] Quotashare { get; set; }
        public float[,] QuotaTime { get; set; }

        public MSEQuotaData(int nGroups, int nLiving, int nFleets)
        {
            this.nGroups = nGroups;
            this.nLiving = nLiving;
            this.nFleets = nFleets;

            TAC = new float[nGroups];
            FixedEscapement = new float[nGroups];
            FixedF = new float[nGroups];
            Fopt = new float[nGroups];
            Fmin = new float[nGroups];
            Bbase = new float[nGroups];
            Blim = new float[nGroups];
            Bestimate = new float[nGroups];
            CVbiomEst = new float[nGroups];
            FTarget = new float[nGroups];
            Quotashare = new float[nGroups, nFleets];
            QuotaTime = new float[nGroups, nFleets];
        }
    }
}
