using Stardust.Paradox.Data.Annotations.Annotations;
using System;

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
            double? minDouble = null;
            long? minLong = null;
            bool allIntegral = true;

            foreach (var traverser in context.Traversers)
            {
                var value = traverser.Value;

                if (TryConvertToLong(value, out long longValue))
                {
                    if (!minLong.HasValue || longValue < minLong.Value)
                        minLong = longValue;

                    if (!minDouble.HasValue || longValue < minDouble.Value)
                        minDouble = longValue;

                    continue;
                }

                if (TryConvertToDouble(value, out double doubleValue))
                {
                    allIntegral = false;
                    if (!minDouble.HasValue || doubleValue < minDouble.Value)
                        minDouble = doubleValue;
                    continue;
                }

                allIntegral = false;
            }

            context.Clear();

            if (allIntegral)
            {
                if (minLong.HasValue)
                    context.Traversers.Add(new Traverser(minLong.Value));
                return;
            }

            if (minDouble.HasValue)
                context.Traversers.Add(new Traverser(minDouble.Value));
        }
    }
}
