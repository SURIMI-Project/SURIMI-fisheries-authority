using System.Collections.Concurrent;

namespace SURIMI_fisheries_authority.Services
{
    /// <summary>
    /// Singleton that manages one dependency injection scope per simulation.
    /// Each scope owns a scoped <see cref="IQuotaCalculationService"/> instance that holds the state of a single simulation.
    /// </summary>
    public class SimulationScopeManager
    {
        private readonly IServiceScopeFactory m_serviceScopeFactory;
        private readonly ConcurrentDictionary<string, IServiceScope> _scopes = new ConcurrentDictionary<string, IServiceScope>();

        public SimulationScopeManager(IServiceScopeFactory serviceScopeFactory)
        {
            m_serviceScopeFactory = serviceScopeFactory;
        }

        public IQuotaCalculationService CreateSimulationScope(string simulationId)
        {
            var scope = m_serviceScopeFactory.CreateScope();
            if (!_scopes.TryAdd(simulationId, scope))
            {
                scope.Dispose();
                throw new Exception($"Simulation with Id {simulationId} is already running");
            }

            return scope.ServiceProvider.GetRequiredService<IQuotaCalculationService>();
        }

        public IQuotaCalculationService GetService(string simulationId)
        {
            if (!_scopes.TryGetValue(simulationId, out var scope))
            {
                throw new Exception($"Simulation with Id {simulationId} is not running");
            }

            return scope.ServiceProvider.GetRequiredService<IQuotaCalculationService>();
        }

        public void RemoveSimulationScope(string simulationId)
        {
            if (!_scopes.TryRemove(simulationId, out var scope))
            {
                throw new Exception($"Simulation with Id {simulationId} is not running");
            }

            scope.Dispose();
        }
    }
}
