using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the has() step which filters elements based on property existence or value.
    /// 
    /// Behavior:
    /// - has('property'): Filters to keep only elements that have the specified property
    /// - has('property', value): Filters to keep only elements where property equals value
    /// - has('property', predicate): Filters using a predicate (gt, lt, within, etc.)
    /// - has('label', value): Special handling for label filtering
    /// - has('id', value): Special handling for ID filtering
    /// 
    /// Supported predicates: gt, gte, lt, lte, eq, neq, within, without
    /// 
    /// Example:
    /// g.V().has('age') - vertices that have an age property
    /// g.V().has('age', 25) - vertices where age equals 25
    /// g.V().has('age', gt(25)) - vertices where age is greater than 25
    /// </summary>
    [UsedImplicitly]
    public class HasStepExecutor : StepExecutorBase
    {
        public HasStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "has";

        public override string StepDescription => 
            "Filters elements based on property existence or value matching. " +
            "has('key') checks if property exists. " +
            "has('key', value) checks if property equals value. " +
            "has('key', predicate) evaluates a predicate (gt, lt, within, etc.). " +
            "Special handling for 'label' and 'id' properties.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count < 1)
                return;

            var key = step.Arguments[0].ToString();
            
            if (step.Arguments.Count == 1)
            {
                // has(key) - check if property exists
                if (key.Equals("label", StringComparison.OrdinalIgnoreCase))
                {
                    // Special case: has('label') - check if element has a label (all elements do)
                    context.Filter(traverser => !string.IsNullOrEmpty(ExtractLabel(traverser.Value)));
                }
                else if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    // Special case: has('id') - check if element has an ID (all elements do)
                    context.Filter(traverser => !string.IsNullOrEmpty(ExtractId(traverser.Value)));
                }
                else
                {
                    // Normal case: check if property exists
                    context.Filter(traverser =>
                    {
                        var properties = ExtractProperties(traverser.Value);
                        return properties != null && properties.ContainsKey(key);
                    });
                }
            }
            else if (step.Arguments.Count >= 2)
            {
                // has(key, value) - check property value
                var expectedValue = step.Arguments[1];
                
                if (key.Equals("label", StringComparison.OrdinalIgnoreCase))
                {
                    // Special case: has('label', value) - check element label
                    context.Filter(traverser =>
                    {
                        var actualLabel = ExtractLabel(traverser.Value);
                        if (expectedValue is string expectedStr && actualLabel is string actualStr)
                        {
                            return expectedStr.Equals(actualStr, StringComparison.OrdinalIgnoreCase);
                        }
                        return Equals(actualLabel, expectedValue);
                    });
                }
                else if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    // Special case: has('id', value) - check element ID
                    context.Filter(traverser =>
                    {
                        var actualId = ExtractId(traverser.Value);
                        if (expectedValue is string expectedStr && actualId is string actualStr)
                        {
                            return expectedStr.Equals(actualStr, StringComparison.OrdinalIgnoreCase);
                        }
                        return Equals(actualId, expectedValue);
                    });
                }
                else
                {
                    // Check if this is a predicate (starts with known predicate functions)
                    var valueStr = expectedValue?.ToString() ?? "";
                    if (valueStr.StartsWith("gt(") || valueStr.StartsWith("gte(") || 
                        valueStr.StartsWith("lt(") || valueStr.StartsWith("lte(") || 
                        valueStr.StartsWith("neq(") || valueStr.StartsWith("eq(") ||
                        valueStr.StartsWith("within(") || valueStr.StartsWith("without("))
                    {
                        // Parse and apply predicate
                        context.Filter(traverser => EvaluatePredicate(traverser, key, valueStr));
                    }
                    else
                    {
                        // Normal case: check property value
                        context.Filter(traverser =>
                        {
                            var properties = ExtractProperties(traverser.Value);
                            if (properties != null && properties.ContainsKey(key))
                            {
                                var actualValue = properties[key];
                                
                                // Handle different value types and comparisons
                                if (expectedValue is string expectedStr && actualValue is string actualStr)
                                {
                                    return expectedStr.Equals(actualStr, StringComparison.OrdinalIgnoreCase);
                                }
                                else if (expectedValue is double && actualValue != null)
                                {
                                    // Handle numeric comparisons (for weight properties)
                                    if (double.TryParse(actualValue.ToString(), out double actualDouble))
                                    {
                                        var expectedDouble = (double)expectedValue;
                                        return Math.Abs(expectedDouble - actualDouble) < 0.0001; // Allow for floating point precision
                                    }
                                }
                                else if (expectedValue is int && actualValue != null)
                                {
                                    if (int.TryParse(actualValue.ToString(), out int actualInt))
                                    {
                                        var expectedInt = (int)expectedValue;
                                        return expectedInt == actualInt;
                                    }
                                }
                                else if (expectedValue is bool && actualValue != null)
                                {
                                    if (bool.TryParse(actualValue.ToString(), out bool actualBool))
                                    {
                                        var expectedBool = (bool)expectedValue;
                                        return expectedBool == actualBool;
                                    }
                                }
                                
                                return Equals(actualValue, expectedValue);
                            }
                            return false;
                        });
                    }
                }
            }
        }

        private bool EvaluatePredicate(Traverser traverser, string propertyKey, string predicate)
        {
            var properties = ExtractProperties(traverser.Value);
            if (properties == null || !properties.ContainsKey(propertyKey))
                return false;
            
            var actualValue = properties[propertyKey];
            
            // Handle incomplete predicates (missing closing parenthesis due to parsing)
            var normalizedPredicate = predicate;
            if (!normalizedPredicate.EndsWith(")"))
            {
                normalizedPredicate += ")";
            }
            
            // Parse predicate (e.g., "gt(80000)", "gte(100)", etc.)
            if (normalizedPredicate.StartsWith("gt(") && normalizedPredicate.EndsWith(")"))
            {
                var valueStr = normalizedPredicate.Substring(3, normalizedPredicate.Length - 4);
                if (double.TryParse(valueStr, out double threshold))
                {
                    if (double.TryParse(actualValue?.ToString(), out double actual))
                    {
                        return actual > threshold;
                    }
                }
            }
            else if (normalizedPredicate.StartsWith("gte(") && normalizedPredicate.EndsWith(")"))
            {
                var valueStr = normalizedPredicate.Substring(4, normalizedPredicate.Length - 5);
                if (double.TryParse(valueStr, out double threshold))
                {
                    if (double.TryParse(actualValue?.ToString(), out double actual))
                    {
                        return actual >= threshold;
                    }
                }
            }
            else if (normalizedPredicate.StartsWith("lt(") && normalizedPredicate.EndsWith(")"))
            {
                var valueStr = normalizedPredicate.Substring(3, normalizedPredicate.Length - 4);
                if (double.TryParse(valueStr, out double threshold))
                {
                    if (double.TryParse(actualValue?.ToString(), out double actual))
                    {
                        return actual < threshold;
                    }
                }
            }
            else if (normalizedPredicate.StartsWith("lte(") && normalizedPredicate.EndsWith(")"))
            {
                var valueStr = normalizedPredicate.Substring(4, normalizedPredicate.Length - 5);
                if (double.TryParse(valueStr, out double threshold))
                {
                    if (double.TryParse(actualValue?.ToString(), out double actual))
                    {
                        return actual <= threshold;
                    }
                }
            }
            else if (normalizedPredicate.StartsWith("neq(") && normalizedPredicate.EndsWith(")"))
            {
                var valueStr = normalizedPredicate.Substring(4, normalizedPredicate.Length - 5);
                if (double.TryParse(valueStr, out double threshold))
                {
                    if (double.TryParse(actualValue?.ToString(), out double actual))
                    {
                        return actual != threshold;
                    }
                }
                else
                {
                    // String comparison
                    return !valueStr.Equals(actualValue?.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
            else if (normalizedPredicate.StartsWith("eq(") && normalizedPredicate.EndsWith(")"))
            {
                var valueStr = normalizedPredicate.Substring(3, normalizedPredicate.Length - 4);
                if (double.TryParse(valueStr, out double threshold))
                {
                    if (double.TryParse(actualValue?.ToString(), out double actual))
                    {
                        return actual == threshold;
                    }
                }
                else
                {
                    // String comparison
                    return valueStr.Equals(actualValue?.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
            else if (normalizedPredicate.StartsWith("within(") && normalizedPredicate.EndsWith(")"))
            {
                // within(value1, value2, value3, ...) predicate
                var valuesStr = normalizedPredicate.Substring(7, normalizedPredicate.Length - 8);
                var withinValues = SplitWithinValues(valuesStr);
                
                var actualValueStr = actualValue?.ToString();
                if (actualValueStr != null)
                {
                    // Check if the actual value matches any of the within values
                    return withinValues.Any(v => v.Equals(actualValueStr, StringComparison.OrdinalIgnoreCase));
                }
                return false;
            }
            else if (normalizedPredicate.StartsWith("without(") && normalizedPredicate.EndsWith(")"))
            {
                // without(value1, value2, value3, ...) predicate
                var valuesStr = normalizedPredicate.Substring(8, normalizedPredicate.Length - 9);
                var withoutValues = SplitWithinValues(valuesStr);
                
                var actualValueStr = actualValue?.ToString();
                if (actualValueStr != null)
                {
                    // Check if the actual value does NOT match any of the without values
                    return !withoutValues.Any(v => v.Equals(actualValueStr, StringComparison.OrdinalIgnoreCase));
                }
                return true; // If no value, it's not in the excluded list
            }
            
            return false;
        }

        /// <summary>
        /// Split within() predicate values by comma while respecting quotes
        /// </summary>
        private System.Collections.Generic.List<string> SplitWithinValues(string valuesStr)
        {
            var values = new System.Collections.Generic.List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';

            for (int i = 0; i < valuesStr.Length; i++)
            {
                char c = valuesStr[i];

                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    // Don't include the quote in the value
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    // Don't include the quote in the value
                }
                else if (!inQuotes && c == ',')
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        values.Add(current.Trim());
                        current = "";
                    }
                }
                else if (c != '\'' && c != '"') // Skip quotes entirely
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                values.Add(current.Trim());
            }

            return values;
        }
    }
}
