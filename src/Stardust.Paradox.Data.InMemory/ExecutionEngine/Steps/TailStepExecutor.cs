using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the tail() step which returns the last n results.
    /// 
    /// Behavior:
    /// - tail(n): Returns the last n results from the stream
    /// 
    /// Example:
    /// g.V().tail(5) - returns the last 5 vertices
    /// </summary>
    [UsedImplicitly]
    public class TailStepExecutor : StepExecutorBase
    {
        public TailStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "tail";

        public override string StepDescription => 
            "Returns the last n results from the traversal stream. " +
            "tail(n) keeps only the final n elements.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var tailSize = step.GetFirstIntArgument();
            if (tailSize > 0)
            {
                var allTraversers = context.Traversers.ToList();
                var tailTraversers = allTraversers.Skip(Math.Max(0, allTraversers.Count - tailSize)).ToList();
                context.Traversers.Clear();
                context.Traversers.AddRange(tailTraversers);
            }
        }
    }
}
