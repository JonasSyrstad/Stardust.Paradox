using System;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the sack() step - accesses or modifies the sack value carried by traversers
    /// TinkerPop spec: sack() returns the current sack value
    /// Can also be used with operators like sack(mult) to modify the sack
    /// </summary>
    public class SackStepExecutor : StepExecutorBase
    {
        public SackStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "sack";

        public override string StepDescription => "Accesses or modifies the sack value carried by traversers during traversal";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                // sack() - emit the current sack value
                foreach (var traverser in context.Traversers)
                {
                    // If traverser has a sack, use it; otherwise use null
                    traverser.Current = traverser.Sack;
                }
            }
            else
            {
                // sack(operator) - modify the sack value
                var operation = step.Arguments[0]?.ToString()?.ToLower();

                switch (operation)
                {
                    case "mult":
                        // Multiply sack by current value
                        foreach (var traverser in context.Traversers)
                        {
                            if (traverser.Sack != null && traverser.Current != null)
                            {
                                traverser.Sack = MultiplyValues(traverser.Sack, traverser.Current);
                            }
                        }
                        break;

                    case "sum":
                    case "add":
                        // Add current value to sack
                        foreach (var traverser in context.Traversers)
                        {
                            if (traverser.Sack != null && traverser.Current != null)
                            {
                                traverser.Sack = AddValues(traverser.Sack, traverser.Current);
                            }
                        }
                        break;

                    case "assign":
                        // Assign current value to sack
                        foreach (var traverser in context.Traversers)
                        {
                            traverser.Sack = traverser.Current;
                        }
                        break;

                    default:
                        throw new NotSupportedException($"sack() operator '{operation}' is not supported. Supported operators: mult, sum, add, assign");
                }
            }
        }

        private object MultiplyValues(object sack, object current)
        {
            try
            {
                if (sack is int si && current is int ci)
                    return si * ci;
                if (sack is long sl && current is long cl)
                    return sl * cl;
                if (sack is float sf && current is float cf)
                    return sf * cf;
                if (sack is double sd && current is double cd)
                    return sd * cd;
                if (sack is decimal sde && current is decimal cde)
                    return sde * cde;

                // Try converting to double
                var sackDouble = Convert.ToDouble(sack);
                var currentDouble = Convert.ToDouble(current);
                return sackDouble * currentDouble;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot multiply sack value {sack} with current value {current}", ex);
            }
        }

        private object AddValues(object sack, object current)
        {
            try
            {
                if (sack is int si && current is int ci)
                    return si + ci;
                if (sack is long sl && current is long cl)
                    return sl + cl;
                if (sack is float sf && current is float cf)
                    return sf + cf;
                if (sack is double sd && current is double cd)
                    return sd + cd;
                if (sack is decimal sde && current is decimal cde)
                    return sde + cde;
                if (sack is string ss && current is string cs)
                    return ss + cs;

                // Try converting to double
                var sackDouble = Convert.ToDouble(sack);
                var currentDouble = Convert.ToDouble(current);
                return sackDouble + currentDouble;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot add sack value {sack} with current value {current}", ex);
            }
        }
    }
}
