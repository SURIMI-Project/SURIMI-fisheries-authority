using SURIMI.Datamodel;

namespace SURIMI_fisheries_authority.Services
{
    public interface IQuotaCalculationService
    {
        Task InitialiseSimulationAsync(string simulationId, string scenarioName, SurimiContract surimiContract);
        Task FinaliseSimulationAsync(CancellationToken cancellationToken);
        Task CancelSimulationAsync(CancellationToken cancellationToken);
        Task UpdateBiomassAsync(DateTime dateTime, List<BiomassGrid> biomassGrids);
        Task UpdateCatchDispositionAsync(DateTime startDateTime, DateTime endDateTime, CatchDispositionSummary catchDispositionSummary);
        Task CreateRegulationsAsync(RegulationDefinitionsSummary regulationDefinitionsSummary, CancellationToken cancellationToken);
        Task<RegulationsSummary> GetRegulationsAsync(DateTime startDateTime, DateTime endDateTime);
    }
}
