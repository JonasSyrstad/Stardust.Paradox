using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the coalesce() step which evaluates multiple traversal options
    /// and returns the result of the first one that yields results.
    /// 
    /// Behavior:
    /// - coalesce(trav1, trav2, ...): Returns first non-empty traversal result
    /// 
    /// This is similar to a try-catch for traversals.
    /// </summary>
    [UsedImplicitly]
    public class CoalesceStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public CoalesceStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "coalesce";

        public string StepDescription => 
            "Evaluates multiple traversal options and returns the first non-empty result. " +
            "coalesce(trav1, trav2, ...) acts like a try-catch for traversals.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Coalesce with traversal arguments needs to be handled by parser
            // For now, mark as needing sub-traversal execution
            context.SetMetadata("coalesce_step", step);
            
            // This step requires special handling in the parser to execute
            // sub-traversals and select the first non-empty result
            // For basic implementation, pass through traversers unchanged
        }
    }
}
