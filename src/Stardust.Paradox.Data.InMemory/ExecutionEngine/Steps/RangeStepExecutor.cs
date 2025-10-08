using Stardust.Paradox.Data.Annotations.Annotations;
using System;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the range() step which returns a subset of results.
    /// 
    /// Behavior:
    /// - range(low, high): Returns results from index low (inclusive) to high (exclusive)
    /// 
    /// Example:
    /// g.V().range(5, 10) - returns elements at indices 5, 6, 7, 8, 9
    /// </summary>
    [UsedImplicitly]
    public class RangeStepExecutor : StepExecutorBase
    {
        public RangeStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "range";

        public override string StepDescription => 
            "Returns a subset of results from index low (inclusive) to high (exclusive). " +
            "range(low, high) filters to keep only elements within the specified index range.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count >= 2)
            {
                // Safely convert arguments to integers with better error handling
                if (TryConvertToInt(step.Arguments[0], out int low) &&
                    TryConvertToInt(step.Arguments[1], out int high))
                {
                    context.Range(low, high);
                }
                else
                {
                    throw new InvalidOperationException($"Range step requires integer arguments, but got: {step.Arguments[0]} and {step.Arguments[1]}");
                }
            }
        }
    }
}
