using System.Globalization;
using System.Text;
using Eii.BlobStore;
using EwECore.MSE;

namespace SURIMI_fisheries_authority.Services
{
    public class CsvMseDiagnosticsRecorder : IMseDiagnosticsRecorder
    {
        private readonly ILogger<CsvMseDiagnosticsRecorder> m_logger;
        private readonly IBlobStore m_blobStore;
        private readonly StringBuilder m_monthlyBiomassRows = new();
        private readonly StringBuilder m_monthlyCatchRows = new();
        private readonly StringBuilder m_yearRows = new();
        private readonly StringBuilder m_tacRows = new();

        public CsvMseDiagnosticsRecorder(ILogger<CsvMseDiagnosticsRecorder> logger, IBlobStore blobStore)
        {
            m_logger = logger;
            m_blobStore = blobStore;
        }

        public void RecordMonthlyBiomass(string simulationId, DateTime periodStart, string speciesCode, string lifeStage, int iGroup, float monthBiomass, float accumulatedBiomass)
        {
            if (m_monthlyBiomassRows.Length == 0)
            {
                m_monthlyBiomassRows.AppendLine("simulation_id,year,month,species_code,life_stage,group,MonthBiomass,AccumulatedBiomass");
            }
            m_monthlyBiomassRows.AppendLine(string.Join(',',
                simulationId,
                periodStart.Year.ToString(CultureInfo.InvariantCulture),
                periodStart.Month.ToString(CultureInfo.InvariantCulture),
                speciesCode,
                lifeStage,
                iGroup.ToString(CultureInfo.InvariantCulture),
                monthBiomass.ToString(CultureInfo.InvariantCulture),
                accumulatedBiomass.ToString(CultureInfo.InvariantCulture)));
        }

        public void RecordMonthlyCatch(string simulationId, DateTime periodStart, string speciesCode, string lifeStage, int iGroup, float monthLandings, float accumulatedCatchYearGroup)
        {
            if (m_monthlyCatchRows.Length == 0)
            {
                m_monthlyCatchRows.AppendLine("simulation_id,year,month,species_code,life_stage,group,MonthLandings,AccumulatedCatchYearGroup");
            }
            m_monthlyCatchRows.AppendLine(string.Join(',',
                simulationId,
                periodStart.Year.ToString(CultureInfo.InvariantCulture),
                periodStart.Month.ToString(CultureInfo.InvariantCulture),
                speciesCode,
                lifeStage,
                iGroup.ToString(CultureInfo.InvariantCulture),
                monthLandings.ToString(CultureInfo.InvariantCulture),
                accumulatedCatchYearGroup.ToString(CultureInfo.InvariantCulture)));
        }

        public void RecordYear(string simulationId, int year, string speciesCode, string lifeStage, int iGroup, float biomass, IMSEQuotaData data, float quota)
        {
            if (m_yearRows.Length == 0)
            {
                m_yearRows.AppendLine("simulation_id,year,species_code,life_stage,group,Biomass,CatchYearGroup,Bestimate,BhalfT,Rmax,Fish1,Blim,Bbase,Fopt,Quota");
            }
            m_yearRows.AppendLine(string.Join(',',
                simulationId,
                year.ToString(CultureInfo.InvariantCulture),
                speciesCode,
                lifeStage,
                iGroup.ToString(CultureInfo.InvariantCulture),
                biomass.ToString(CultureInfo.InvariantCulture),
                data.CatchYearGroup[iGroup].ToString(CultureInfo.InvariantCulture),
                data.Bestimate[iGroup].ToString(CultureInfo.InvariantCulture),
                data.BhalfT[iGroup].ToString(CultureInfo.InvariantCulture),
                data.Rmax[iGroup].ToString(CultureInfo.InvariantCulture),
                data.Fish1[iGroup].ToString(CultureInfo.InvariantCulture),
                data.Blim[iGroup].ToString(CultureInfo.InvariantCulture),
                data.Bbase[iGroup].ToString(CultureInfo.InvariantCulture),
                data.Fopt[iGroup].ToString(CultureInfo.InvariantCulture),
                quota.ToString(CultureInfo.InvariantCulture)));
        }

        public void RecordTac(string simulationId, int year, string speciesCode, string lifeStage, string gearCode, string countryCode, float share, float tac)
        {
            if (m_tacRows.Length == 0)
            {
                m_tacRows.AppendLine("simulation_id,year,species_code,life_stage,gear_code,country_code,share,TAC");
            }
            m_tacRows.AppendLine(string.Join(',',
                simulationId,
                year.ToString(CultureInfo.InvariantCulture),
                speciesCode,
                lifeStage,
                gearCode,
                countryCode,
                share.ToString(CultureInfo.InvariantCulture),
                tac.ToString(CultureInfo.InvariantCulture)));
        }

        public async Task FlushAsync(string simulationId, CancellationToken cancellationToken)
        {
            await UploadIfNotEmptyAsync($"{simulationId}_biomass-monthly.csv", m_monthlyBiomassRows, cancellationToken);
            await UploadIfNotEmptyAsync($"{simulationId}_catch-monthly.csv", m_monthlyCatchRows, cancellationToken);
            await UploadIfNotEmptyAsync($"{simulationId}_mse-assessment.csv", m_yearRows, cancellationToken);
            await UploadIfNotEmptyAsync($"{simulationId}_tac.csv", m_tacRows, cancellationToken);

            m_logger.LogInformation($"Wrote MSE diagnostics for simulation {simulationId} to blob store");

        }

        private async Task UploadIfNotEmptyAsync(string key, StringBuilder rows, CancellationToken cancellationToken)
        {
            if (rows.Length == 0)
            {
                return;
            }
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(rows.ToString()));
            await m_blobStore.UploadAsync(key, PathType.Output, content, "text/csv", ct: cancellationToken);
        }
    }
}
