using SURIMI.Datamodel;

namespace SURIMI_fisheries_authority.Services
{
    public interface IQuotaCalculationService
    {
        Task InitialiseSimulationAsync(string simulationId, SurimiContract surimiContract);
        Task SimulateStepAsync(string simulationId);
        Task FinaliseSimulationAsync(string simulationId);
        Task CancelSimulationAsync(string simulationId);
        Task UpdateBiomassAsync(string simulationId, List<BiomassGrid> biomassGrids);
        Task UpdateCatchDispositionAsync(string simulationId, DateTime startDateTime, DateTime endDateTime, CatchDispositionSummary catchDispositionSummary);
        Task UpdateFishingActivityAsync(string simulationId, DateTime startDateTime, DateTime endDateTime, FishingActivitySummary fishingActivitySummary);
        Task CreateRegulationsAsync(string simulationId, RegulationDefinitionsSummary regulationDefinitionsSummary);
        Task<RegulationsSummary> GetRegulationsAsync(string simulationId, DateTime startDateTime, DateTime endDateTime);
    }
}
