using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the min() terminal step which finds the minimum numeric value.
    /// 
    /// Behavior:
    /// - Finds the minimum value, converting to double
    /// - Returns a single result with the minimum
    /// - Returns empty if no values are present
    /// 
    /// Example:
    /// g.V().values('age').min() - minimum age across all vertices
    /// </summary>
    [UsedImplicitly]
    public class MinStepExecutor : StepExecutorBase
    {
        public MinStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "min";

        public override string StepDescription => 
            "Terminal step that finds the minimum numeric value in the traversal stream. " +
            "Returns a single result with the minimum, or empty if no values exist.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            double? min = null;

            foreach (var traverser in context.Traversers)
            {
                for (int i = 0; i < traverser.Bulk; i++)
                {
                    var value = traverser.Value;
                    if (TryConvertToDouble(value, out double doubleValue))
                    {
                        if (!min.HasValue || doubleValue < min.Value)
                        {
                            min = doubleValue;
                        }
                    }
                }
            }

            context.Clear();

            // For min, if no values were found, don't add a result (return empty)
            if (min.HasValue)
                context.Traversers.Add(new Traverser(min.Value));
        }
    }
}
