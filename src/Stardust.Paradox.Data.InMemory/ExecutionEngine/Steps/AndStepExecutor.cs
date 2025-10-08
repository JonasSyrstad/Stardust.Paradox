using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the and() logical step - all conditions must pass.
    /// 
    /// Behavior:
    /// - Filters to keep only elements where all conditions are met
    /// - and(): With no arguments, passes all through
    /// - and(condition1, condition2, ...): All conditions must be true
    /// 
    /// Example:
    /// g.V().and(has('age', gt(25)), has('name', 'John'))
    /// </summary>
    [UsedImplicitly]
    public class AndStepExecutor : StepExecutorBase
    {
        public AndStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "and";

        public override string StepDescription => 
            "Logical AND filter - all conditions must pass. " +
            "and(condition1, condition2, ...) keeps only elements matching all conditions.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                // and() with no arguments passes all through
                return;
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                bool allConditionsMet = true;

                // Evaluate each condition
                foreach (var arg in step.Arguments)
                {
                    var conditionStr = arg.ToString();

                    // Parse and evaluate the condition using the base class helper
                    if (!EvaluateLogicalCondition(traverser, conditionStr))
                    {
                        allConditionsMet = false;
                        break;
                    }
                }

                // Only keep traversers where all conditions are met
                if (allConditionsMet)
                {
                    newTraversers.Add(traverser);
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
