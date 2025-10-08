using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the without() step which filters out specified property values.
    /// 
    /// Behavior:
    /// - Filters elements to exclude those with specified property values
    /// - without(propertyKey, value1, value2, ...): Filters out elements where property equals any of the values
    /// 
    /// Example:
    /// g.V().without('status', 'inactive', 'deleted')
    /// </summary>
    [UsedImplicitly]
    public class WithoutStepExecutor : StepExecutorBase
    {
        public WithoutStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "without";

        public override string StepDescription => 
            "Filters out elements with specified property values. " +
            "without(propertyKey, value1, value2, ...) excludes elements where property matches any value.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count < 2)
                return;

            var propertyKey = step.Arguments[0].ToString();
            var excludedValues = new HashSet<string>(
                step.Arguments.Skip(1).Select(arg => arg.ToString()),
                StringComparer.OrdinalIgnoreCase);

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var properties = ExtractProperties(traverser.Value);

                // If property doesn't exist, keep the traverser
                if (properties == null || !properties.ContainsKey(propertyKey))
                {
                    newTraversers.Add(traverser);
                    continue;
                }

                var actualValue = properties[propertyKey]?.ToString();

                // Only keep if value is NOT in the excluded set
                if (actualValue != null && !excludedValues.Contains(actualValue))
                {
                    newTraversers.Add(traverser);
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
