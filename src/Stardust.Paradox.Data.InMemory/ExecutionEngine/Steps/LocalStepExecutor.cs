using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the local() step which executes a traversal locally
    /// on each element rather than across the entire traversal stream.
    /// 
    /// Behavior:
    /// - local(traversal): Executes traversal locally per element
    /// 
    /// This is useful for operations that should be scoped to individual elements.
    /// </summary>
    [UsedImplicitly]
    public class LocalStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public LocalStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "local";

        public string StepDescription => 
            "Executes a traversal locally on each element. " +
            "local(traversal) scopes operations to individual elements.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Local with traversal argument needs to be handled by parser
            // For now, mark as needing sub-traversal execution
            context.SetMetadata("local_step", step);
            
            // This step requires special handling in the parser to execute
            // the sub-traversal locally on each element
            // For basic implementation, pass through traversers unchanged
        }
    }
}
