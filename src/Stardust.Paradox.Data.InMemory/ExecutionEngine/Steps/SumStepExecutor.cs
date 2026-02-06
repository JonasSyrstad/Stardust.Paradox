using Stardust.Paradox.Data.Annotations.Annotations;
using System;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the sum() terminal step which sums all numeric values in the stream.
    /// 
    /// Behavior:
    /// - Sums all values
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
            long sumLong = 0;
            double sumDouble = 0.0;
            decimal sumDecimal = 0m;

            bool allIntegral = true;
            bool hasDecimal = false;
            bool hasFloating = false;

            foreach (var traverser in context.Traversers)
            {
                var bulk = traverser.Bulk <= 0 ? 1 : traverser.Bulk;
                var value = traverser.Value;

                if (value is decimal dec)
                {
                    // Track decimal but also detect if this value likely came from a floating literal (stored as decimal)
                    hasDecimal = true;
                    allIntegral = false;
                    sumDecimal += dec * bulk;
                    sumDouble += (double)dec * bulk;

                    // If it has a fractional part, treat as floating for output type compatibility
                    if (dec != decimal.Truncate(dec))
                        hasFloating = true;
                    continue;
                }

                long longValue;
                if (TryConvertToLong(value, out longValue))
                {
                    checked
                    {
                        sumLong += longValue * bulk;
                    }

                    sumDouble += longValue * (double)bulk;
                    if (hasDecimal)
                        sumDecimal += (decimal)longValue * bulk;

                    continue;
                }

                double doubleValue;
                if (TryConvertToDouble(value, out doubleValue))
                {
                    hasFloating = true;
                    allIntegral = false;
                    sumDouble += doubleValue * bulk;
                    if (hasDecimal)
                        sumDecimal += (decimal)doubleValue * bulk;
                    continue;
                }

                allIntegral = false;
            }

            context.Clear();

            if (allIntegral)
                context.Traversers.Add(new Traverser(sumLong));
            else if (hasDecimal)
                context.Traversers.Add(new Traverser(sumDecimal));
            else if (hasFloating)
                context.Traversers.Add(new Traverser(sumDouble));
            else
                context.Traversers.Add(new Traverser(sumDouble));
        }
    }
}
