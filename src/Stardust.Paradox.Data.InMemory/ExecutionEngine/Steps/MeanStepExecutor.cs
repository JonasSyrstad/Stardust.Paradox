using Stardust.Paradox.Data.Annotations.Annotations;
using System;

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
            long count = 0;
            double sumDouble = 0.0;
            decimal sumDecimal = 0m;
            bool hasDecimal = false;

            foreach (var traverser in context.Traversers)
            {
                var bulk = traverser.Bulk <= 0 ? 1 : traverser.Bulk;
                var value = traverser.Value;

                if (value is decimal dec)
                {
                    hasDecimal = true;
                    sumDecimal += dec * bulk;
                    count += bulk;
                    continue;
                }

                double doubleValue;
                if (TryConvertToDouble(value, out doubleValue))
                {
                    sumDouble += doubleValue * bulk;
                    if (hasDecimal)
                        sumDecimal += (decimal)doubleValue * bulk;
                    count += bulk;
                }
            }

            context.Clear();

            if (count <= 0)
                return;

            if (hasDecimal)
            {
                context.Traversers.Add(new Traverser(sumDecimal / count));
            }
            else
            {
                context.Traversers.Add(new Traverser(sumDouble / count));
            }
        }
    }
}
