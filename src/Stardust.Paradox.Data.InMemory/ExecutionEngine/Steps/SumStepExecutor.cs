using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the sum() terminal step which sums all numeric values in the stream.
    /// 
    /// Behavior:
    /// - Sums all values, converting them to double
    /// - Returns a single result with the sum
    /// 
    /// Example:
    /// g.V().values('salary').sum() - sum of all salary values
    /// </summary>
    [UsedImplicitly]
    public class SumStepExecutor : StepExecutorBase
    {
        public SumStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "sum";

        public override string StepDescription => 
            "Terminal step that sums all numeric values in the traversal stream. " +
            "Returns a single result with the sum.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            double sum = 0.0;
            foreach (var traverser in context.Traversers)
            {
                for (int i = 0; i < traverser.Bulk; i++)
                {
                    var value = traverser.Value;
                    if (TryConvertToDouble(value, out double doubleValue))
                    {
                        sum += doubleValue;
                    }
                }
            }

            context.Clear();
            context.Traversers.Add(new Traverser(sum));
        }
    }
}
