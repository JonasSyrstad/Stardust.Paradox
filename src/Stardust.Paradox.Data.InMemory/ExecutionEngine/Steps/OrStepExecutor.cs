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
    /// g.V().or(has('age', gt(65)), has('name', 'John'))
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
            "or(condition1, condition2, ...) keeps elements matching any condition.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                // or() with no arguments filters all out
                context.Traversers = new List<Traverser>();
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

                    // Parse and evaluate the condition
                    if (EvaluateLogicalCondition(traverser, conditionStr))
                    {
                        anyConditionMet = true;
                        break;
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
        
        private bool EvaluateLogicalCondition(Traverser traverser, string conditionStr)
        {
            // Parse the condition string
            if (string.IsNullOrWhiteSpace(conditionStr))
                return false;

            // Extract the condition type (has, etc.)
            if (conditionStr.StartsWith("has("))
            {
                // Parse has() condition
                var content = conditionStr.Substring(4, conditionStr.Length - 5);
                var parts = SplitConditionArguments(content);

                if (parts.Count == 0)
                    return false;

                var propertyKey = parts[0].Trim().Trim('\'', '"');

                if (parts.Count == 1)
                {
                    // has('property') - check if property exists
                    var properties = ExtractProperties(traverser.Value);
                    return properties != null && properties.ContainsKey(propertyKey);
                }
                else if (parts.Count == 2)
                {
                    // has('property', value) or has('property', predicate)
                    var valueOrPredicate = parts[1].Trim();

                    var properties = ExtractProperties(traverser.Value);
                    if (properties == null || !properties.ContainsKey(propertyKey))
                        return false;

                    var actualValue = properties[propertyKey];
                    var expectedValue = valueOrPredicate.Trim('\'', '"');

                    // Try different value types
                    if (bool.TryParse(expectedValue, out bool boolVal))
                    {
                        if (actualValue is bool actualBool)
                            return actualBool == boolVal;
                        if (bool.TryParse(actualValue?.ToString(), out bool parsedBool))
                            return parsedBool == boolVal;
                    }

                    if (int.TryParse(expectedValue, out int intVal))
                    {
                        if (actualValue is int actualInt)
                            return actualInt == intVal;
                        if (int.TryParse(actualValue?.ToString(), out int parsedInt))
                            return parsedInt == intVal;
                    }

                    // String comparison
                    return actualValue?.ToString().Equals(expectedValue, System.StringComparison.OrdinalIgnoreCase) == true;
                }
            }

            return false;
        }
        
        private List<string> SplitConditionArguments(string content)
        {
            var parts = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';
            var parenDepth = 0;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    current += c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    current += c;
                }
                else if (!inQuotes && c == '(')
                {
                    parenDepth++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenDepth--;
                    current += c;
                }
                else if (!inQuotes && parenDepth == 0 && c == ',')
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        parts.Add(current.Trim());
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                parts.Add(current.Trim());
            }

            return parts;
        }
    }
}
