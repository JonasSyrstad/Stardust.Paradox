using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the mean() terminal step which calculates the average of all numeric values.
    /// 
    /// Behavior:
    /// - Calculates the average of all values, converting them to double
    /// - Returns a single result with the average
    /// - Returns empty if no values are present
    /// 
    /// Example:
    /// g.V().values('age').mean() - average age across all vertices
    /// </summary>
    [UsedImplicitly]
    public class MeanStepExecutor : StepExecutorBase
    {
        public MeanStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "mean";

        public override string StepDescription => 
            "Terminal step that calculates the average of all numeric values in the traversal stream. " +
            "Returns a single result with the average, or empty if no values exist.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            double sum = 0.0;
            long count = 0;

            foreach (var traverser in context.Traversers)
            {
                for (int i = 0; i < traverser.Bulk; i++)
                {
                    var value = traverser.Value;
                    if (TryConvertToDouble(value, out double doubleValue))
                    {
                        sum += doubleValue;
                        count++;
                    }
                }
            }

            context.Clear();

            // For mean, if no values were found, don't add a result (return empty)
            if (count > 0)
            {
                var mean = sum / count;
                context.Traversers.Add(new Traverser(mean));
            }
        }
    }
}
