using SURIMI.Datamodel;

namespace SURIMI_fisheries_authority.Services
{
    public interface IQuotaCalculationService
    {
        Task InitialiseSimulationAsync(string simulationId, string scenarioName, SurimiContract surimiContract);
        Task SimulateStepAsync();
        Task FinaliseSimulationAsync();
        Task CancelSimulationAsync();
        Task UpdateBiomassAsync(List<BiomassGrid> biomassGrids);
        Task UpdateCatchDispositionAsync(DateTime startDateTime, DateTime endDateTime, CatchDispositionSummary catchDispositionSummary);
        Task UpdateFishingActivityAsync(DateTime startDateTime, DateTime endDateTime, FishingActivitySummary fishingActivitySummary);
        Task CreateRegulationsAsync(RegulationDefinitionsSummary regulationDefinitionsSummary, CancellationToken cancellationToken);
        Task<RegulationsSummary> GetRegulationsAsync(DateTime startDateTime, DateTime endDateTime);
    }
}
