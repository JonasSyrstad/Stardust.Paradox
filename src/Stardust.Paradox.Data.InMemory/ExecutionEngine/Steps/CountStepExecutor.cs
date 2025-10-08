using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the count() step which counts the number of traversers.
    /// 
    /// Behavior:
    /// - Counts all current traversers considering their bulk
    /// - Replaces all traversers with a single traverser containing the count
    /// - This is a terminal/barrier step that aggregates all traversers
    /// 
    /// Example:
    /// g.V().count() returns the total number of vertices
    /// g.V().out().count() returns the number of adjacent vertices
    /// </summary>
    [UsedImplicitly]
    public class CountStepExecutor : StepExecutorBase
    {
        public CountStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "count";

        public override string StepDescription => 
            "Counts the number of objects (traversers) in the current traversal stream. " +
            "This is a terminal/barrier step that reduces all traversers to a single count value. " +
            "The count respects traverser bulk (multiplicity).";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var count = context.Count;
            context.Clear();
            context.Traversers.Add(new Traverser(count));
        }
    }
}
