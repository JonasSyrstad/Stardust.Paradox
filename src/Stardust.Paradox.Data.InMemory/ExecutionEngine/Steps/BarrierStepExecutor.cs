using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the barrier() step which is a synchronization step that collects
    /// all traversers before allowing them to proceed.
    /// 
    /// Behavior:
    /// - barrier(): Waits for all traversers to reach this point before continuing
    /// 
    /// This is useful for ensuring certain operations complete before others begin.
    /// </summary>
    [UsedImplicitly]
    public class BarrierStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public BarrierStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "barrier";

        public string StepDescription => 
            "Synchronization step that collects all traversers before proceeding. " +
            "barrier() ensures all upstream operations complete before continuing.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // In an in-memory implementation, this is effectively a no-op
            // since we're not dealing with distributed/async execution.
            // All traversers are already collected in the context.
            
            // Mark that we've passed through a barrier (can be useful for optimization)
            context.SetMetadata("barrier_passed", true);
        }
    }
}
