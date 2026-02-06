using Stardust.Paradox.Data.Annotations.Annotations;
using System;
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
                context.Traversers.Clear();
                return;
            }

            var executors = context.GetMetadata<Dictionary<string, IStepExecutor>>("stepExecutors");

            bool EvaluateNestedStep(Traverser sourceTraverser, TinkerGraphStep nested)
            {
                if (executors == null)
                    return false;

                if (!executors.TryGetValue(nested.StepName, out var nestedExecutor))
                    return false;

                var nestedContext = context.Clone();
                nestedContext.Traversers = new List<Traverser> { sourceTraverser.Split() };

                nestedExecutor.Execute(nested, nestedContext);
                return nestedContext.Traversers.Any();
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                bool anyConditionMet = false;

                foreach (var arg in step.Arguments)
                {
                    if (arg is TinkerGraphStep nestedStep)
                    {
                        if (EvaluateNestedStep(traverser, nestedStep))
                        {
                            anyConditionMet = true;
                            break;
                        }
                        continue;
                    }

                    var conditionStr = arg?.ToString() ?? string.Empty;
                    if (conditionStr.StartsWith("__."))
                        conditionStr = conditionStr.Substring(3);

                    if (EvaluateLogicalCondition(traverser, conditionStr))
                    {
                        anyConditionMet = true;
                        break;
                    }
                }

                if (anyConditionMet)
                    newTraversers.Add(traverser);
            }

            context.Traversers = newTraversers;
        }
    }
}
