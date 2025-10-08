using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the or() logical step - at least one condition must pass.
    /// 
    /// Behavior:
    /// - Filters to keep only elements where at least one condition is met
    /// - or(): With no arguments, filters all out
    /// - or(condition1, condition2, ...): At least one condition must be true
    /// 
    /// Example:
    /// g.V().or(has('age', lt(20)), has('age', gt(60)))
    /// </summary>
    [UsedImplicitly]
    public class OrStepExecutor : StepExecutorBase
    {
        public OrStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "or";

        public override string StepDescription => 
            "Logical OR filter - at least one condition must pass. " +
            "or(condition1, condition2, ...) keeps only elements matching at least one condition.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                // or() with no arguments filters all out
                context.Traversers.Clear();
                return;
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                bool anyConditionMet = false;

                // Evaluate each condition
                foreach (var arg in step.Arguments)
                {
                    var conditionStr = arg.ToString();

                    // Parse and evaluate the condition using the base class helper
                    if (EvaluateLogicalCondition(traverser, conditionStr))
                    {
                        anyConditionMet = true;
                        break; // Short-circuit: at least one condition is true
                    }
                }

                // Only keep traversers where at least one condition is met
                if (anyConditionMet)
                {
                    newTraversers.Add(traverser);
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
