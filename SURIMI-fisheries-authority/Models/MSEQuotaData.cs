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
        public float[] CatchYearGroup { get; set; }
        public float[] BestimateLast { get; set; }
        public float[] Fish1 { get; set; }
        public float[] GstockPred { get; set; }
        public float[] RstockRatio { get; set; }
        public float[] RHalfB0Ratio { get; set; }
        public float[] KalmanGain { get; set; }
        public float[] BhalfT { get; set; }
        public float[] Rmax { get; set; }
        public float[] cvRec { get; set; }
        public IMSESummaryStats BioEstStats { get; set; }

        public MSEQuotaData(int nGroups, int nFleets)
        {
            this.nGroups = nGroups;
            this.nLiving = nGroups;     // Assuming all groups are living in SURIMI; 
            this.nFleets = nFleets;

            // because EwECore uses 1-based group indices, so allocate nGroups + 1 elements (indices 0..nGroups)
            TAC = new float[nGroups + 1];
            FixedEscapement = new float[nGroups + 1];
            FixedF = new float[nGroups + 1];
            Fopt = new float[nGroups + 1];
            Fmin = new float[nGroups + 1];
            Bbase = new float[nGroups + 1];
            Blim = new float[nGroups + 1];
            Bestimate = new float[nGroups + 1];
            CVbiomEst = new float[nGroups + 1];
            FTarget = new float[nGroups + 1];
            Quotashare = new float[nFleets + 1, nGroups + 1];
            QuotaTime = new float[nFleets + 1, nGroups + 1];
            CatchYearGroup = new float[nGroups + 1];
            BestimateLast = new float[nGroups + 1];
            Fish1 = new float[nGroups + 1];
            GstockPred = new float[nGroups + 1];
            RstockRatio = new float[nGroups + 1];
            RHalfB0Ratio = new float[nGroups + 1];
            KalmanGain = new float[nGroups + 1];
            BhalfT = new float[nGroups + 1];
            Rmax = new float[nGroups + 1];
            cvRec = new float[nGroups + 1];
            BioEstStats = new MSESummaryStats();
        }
    }
}
