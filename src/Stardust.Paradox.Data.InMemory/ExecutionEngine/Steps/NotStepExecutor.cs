using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the not() logical step - inverts the condition.
    /// 
    /// Behavior:
    /// - Filters to keep only elements where the condition is NOT met
    /// - not(): With no arguments, filters all out
    /// - not(condition): Keeps elements where condition is false
    /// 
    /// Example:
    /// g.V().not(has('age', gt(25)))
    /// </summary>
    [UsedImplicitly]
    public class NotStepExecutor : StepExecutorBase
    {
        public NotStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "not";

        public override string StepDescription => 
            "Logical NOT filter - inverts a condition. " +
            "not(condition) keeps only elements where condition is false.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                // not() with no arguments filters all out
                context.Traversers = new List<Traverser>();
                return;
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var conditionStr = step.Arguments[0].ToString();

                // Parse and evaluate the condition, then invert
                if (!EvaluateLogicalCondition(traverser, conditionStr))
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
