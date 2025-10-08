using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the max() terminal step which finds the maximum numeric value.
    /// 
    /// Behavior:
    /// - Finds the maximum value, converting to double
    /// - Returns a single result with the maximum
    /// - Returns empty if no values are present
    /// 
    /// Example:
    /// g.V().values('age').max() - maximum age across all vertices
    /// </summary>
    [UsedImplicitly]
    public class MaxStepExecutor : StepExecutorBase
    {
        public MaxStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "max";

        public override string StepDescription => 
            "Terminal step that finds the maximum numeric value in the traversal stream. " +
            "Returns a single result with the maximum, or empty if no values exist.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            double? max = null;

            foreach (var traverser in context.Traversers)
            {
                for (int i = 0; i < traverser.Bulk; i++)
                {
                    var value = traverser.Value;
                    if (TryConvertToDouble(value, out double doubleValue))
                    {
                        if (!max.HasValue || doubleValue > max.Value)
                        {
                            max = doubleValue;
                        }
                    }
                }
            }

            context.Clear();

            // For max, if no values were found, don't add a result (return empty)
            if (max.HasValue)
            {
                context.Traversers.Add(new Traverser(max.Value));
            }
        }
    }
}
