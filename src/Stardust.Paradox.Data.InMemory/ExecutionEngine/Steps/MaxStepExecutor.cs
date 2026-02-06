using Stardust.Paradox.Data.Annotations.Annotations;
using System;

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
            double? maxDouble = null;
            long? maxLong = null;
            bool allIntegral = true;

            foreach (var traverser in context.Traversers)
            {
                var value = traverser.Value;

                if (TryConvertToLong(value, out long longValue))
                {
                    if (!maxLong.HasValue || longValue > maxLong.Value)
                        maxLong = longValue;

                    if (!maxDouble.HasValue || longValue > maxDouble.Value)
                        maxDouble = longValue;

                    continue;
                }

                if (TryConvertToDouble(value, out double doubleValue))
                {
                    allIntegral = false;
                    if (!maxDouble.HasValue || doubleValue > maxDouble.Value)
                        maxDouble = doubleValue;
                    continue;
                }

                allIntegral = false;
            }

            context.Clear();

            if (allIntegral)
            {
                if (maxLong.HasValue)
                    context.Traversers.Add(new Traverser(maxLong.Value));
                return;
            }

            if (maxDouble.HasValue)
                context.Traversers.Add(new Traverser(maxDouble.Value));
        }
    }
}
