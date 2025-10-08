using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the union() step which merges the results of multiple traversals.
    /// 
    /// Behavior:
    /// - union(trav1, trav2, ...): Merges results from all traversals
    /// 
    /// This is like a SQL UNION, combining results from multiple paths.
    /// </summary>
    [UsedImplicitly]
    public class UnionStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public UnionStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "union";

        public string StepDescription => 
            "Merges results from multiple traversals. " +
            "union(trav1, trav2, ...) combines all traversal results.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Union with traversal arguments needs to be handled by parser
            // For now, mark as needing sub-traversal execution
            context.SetMetadata("union_step", step);
            
            // This step requires special handling in the parser to execute
            // multiple sub-traversals and merge their results
            // For basic implementation, pass through traversers unchanged
        }
    }
}
