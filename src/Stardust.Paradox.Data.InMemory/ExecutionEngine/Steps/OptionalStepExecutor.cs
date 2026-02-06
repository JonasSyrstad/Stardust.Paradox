using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the optional() step which applies a traversal and returns the result,
    /// or the original traverser if the traversal produces no results.
    /// 
    /// TinkerPop Reference: https://tinkerpop.apache.org/docs/current/reference/#optional-step
    /// 
    /// Behavior:
    /// - Applies the inner traversal to each traverser
    /// - If the traversal produces results, returns those results
    /// - If the traversal produces no results, returns the original traverser
    /// 
    /// Example:
    /// g.V('1').optional(out('knows')) - returns neighbors if any, otherwise returns vertex '1'
    /// </summary>
    [UsedImplicitly]
    public class OptionalStepExecutor : StepExecutorBase
    {
        public OptionalStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "optional";

        public override string StepDescription =>
            "Applies a traversal and returns its result. If the traversal returns no results, " +
            "returns the original traverser. This allows for optional traversal paths. " +
            "Example: g.V('1').optional(out('knows')) returns neighbors if any, otherwise vertex '1'.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0 && step.NestedTraversal == null)
            {
                // No nested traversal - just return current traversers
                return;
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                // Create a new context for the nested traversal
                var nestedContext = new TinkerTraversalContext();
                nestedContext.Traversers = new List<Traverser> { traverser.Split() };
                
                // Copy parameters from parent context
                var parameters = context.GetMetadata<Dictionary<string, object>>("parameters");
                if (parameters != null)
                {
                    nestedContext.SetMetadata("parameters", parameters);
                }

                // Execute the nested traversal using the nested steps if available
                if (step.NestedTraversal != null && step.NestedTraversal.Any())
                {
                    var executor = new TinkerGraphQueryExecutor(Database);
                    
                    // Execute each nested step
                    foreach (var nestedStep in step.NestedTraversal)
                    {
                        ExecuteNestedStep(nestedStep, nestedContext);
                    }
                }
                else if (step.Arguments.Any())
                {
                    // Try to parse the argument as a traversal string
                    var argStr = step.Arguments[0]?.ToString();
                    if (!string.IsNullOrEmpty(argStr) && argStr.StartsWith("__."))
                    {
                        // This is a traversal reference - we need to execute it
                        // For now, just keep the original traverser
                        newTraversers.Add(traverser);
                        continue;
                    }
                }

                if (nestedContext.Traversers.Any())
                {
                    // Nested traversal produced results - use them
                    newTraversers.AddRange(nestedContext.Traversers);
                }
                else
                {
                    // No results from nested traversal - keep the original traverser
                    newTraversers.Add(traverser);
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteNestedStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Look up and execute the step executor for this step
            var executor = CreateStepExecutor(step.StepName);
            if (executor != null)
            {
                executor.Execute(step, context);
            }
        }

        private IStepExecutor CreateStepExecutor(string stepName)
        {
            // Map step names to their executors
            return stepName.ToLower() switch
            {
                "out" => new OutStepExecutor(Database),
                "in" => new InStepExecutor(Database),
                "both" => new BothStepExecutor(Database),
                "oute" => new OutEStepExecutor(Database),
                "ine" => new InEStepExecutor(Database),
                "bothe" => new BothEStepExecutor(Database),
                "has" => new HasStepExecutor(Database),
                "haslabel" => new HasLabelStepExecutor(Database),
                "hasid" => new HasIdStepExecutor(Database),
                "values" => new ValuesStepExecutor(Database),
                "constant" => new ConstantStepExecutor(Database),
                _ => null
            };
        }
    }
}
