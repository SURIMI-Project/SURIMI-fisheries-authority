using EwECore.MSE;

namespace SURIMI_fisheries_authority.Services
{
    public interface IMseDiagnosticsRecorder
    {
        void RecordMonthlyBiomass(string simulationId, DateTime periodStart, string speciesCode, string lifeStage, int iGroup, float monthBiomass);

        void RecordMonthlyCatch(string simulationId, DateTime periodStart, string speciesCode, string lifeStage, int iGroup, float monthLandings, float accumulatedCatchYearGroup);

        void RecordYear(string simulationId, int year, string speciesCode, string lifeStage, int iGroup, float biomass, IMSEQuotaData data, float quota);

        void RecordTac(string simulationId, int year, string speciesCode, string lifeStage, string gearCode, string countryCode, float share, float tac);

        Task FlushAsync(string simulationId, CancellationToken cancellationToken = default);
    }
}
