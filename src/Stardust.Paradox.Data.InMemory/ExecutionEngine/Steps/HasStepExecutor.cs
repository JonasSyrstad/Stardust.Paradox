using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

            // Resolve parameter references for type-safe matching
            var parameters = context.GetMetadata<Dictionary<string, object>>("parameters");
            var resolvedArguments = step.Arguments.Select(arg => ResolveParameterReference(arg, parameters)).ToList();

            var key = resolvedArguments[0].ToString();

            if (resolvedArguments.Count == 1)
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
            else if (resolvedArguments.Count >= 2)
            {
                // has(key, value) - check property value with TYPE-SAFE MATCHING
                var expectedValue = resolvedArguments[1];

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
                    // Special case: has('id', value/predicate) - check element ID
                    
                    // Check if this is a predicate (e.g., within(...))
                    var valueStr = expectedValue?.ToString() ?? "";
                    if (valueStr.StartsWith("within(") || valueStr.StartsWith("without("))
                    {
                        // Parse and apply predicate for ID matching
                        var resolvedPredicate = ResolveParameterReferencesInPredicateString(valueStr, parameters);
                        context.Filter(traverser =>
                        {
                            var actualId = ExtractId(traverser.Value);
                            return EvaluatePredicate(actualId, resolvedPredicate);
                        });
                    }
                    else
                    {
                        // Single value comparison
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
                }
                else
                {
                    // Check if this is a predicate (starts with known predicate functions)
                    var valueStr = expectedValue?.ToString() ?? "";
                    if (valueStr.StartsWith("gt(") || valueStr.StartsWith("gte(") ||
                        valueStr.StartsWith("lt(") || valueStr.StartsWith("lte(") ||
                        valueStr.StartsWith("neq(") || valueStr.StartsWith("eq(") ||
                        valueStr.StartsWith("within(") || valueStr.StartsWith("without(") ||
                        valueStr.StartsWith("containing(") || valueStr.StartsWith("notContaining(") ||
                        valueStr.StartsWith("startingWith(") || valueStr.StartsWith("notStartingWith(") ||
                        valueStr.StartsWith("endingWith(") || valueStr.StartsWith("notEndingWith("))
                    {
                        // Parse and apply predicate - need to resolve any parameter references inside the predicate
                        var resolvedPredicate = ResolveParameterReferencesInPredicateString(valueStr, parameters);
                        context.Filter(traverser =>
                        {
                            var properties = ExtractProperties(traverser.Value);
                            if (properties != null && properties.ContainsKey(key))
                            {
                                var actualValue = properties[key];
                                return EvaluatePredicate(actualValue, resolvedPredicate);
                            }
                            return false;
                        });
                    }
                    else
                    {
                        // Normal case: check property value with TYPE-SAFE MATCHING
                        context.Filter(traverser =>
                        {
                            var properties = ExtractProperties(traverser.Value);
                            if (properties != null && properties.ContainsKey(key))
                            {
                                var actualValue = properties[key];

                                // Handle boolean expected value - STRICT TYPE MATCHING
                                // A boolean expected value should ONLY match actual boolean values
                                if (expectedValue is bool expectedBool)
                                {
                                    // Only match if actualValue is also a boolean with the same value
                                    if (actualValue is bool actualBool)
                                    {
                                        return expectedBool == actualBool;
                                    }

                                    // Boolean expected value does NOT match string "true"/"false"
                                    return false;
                                }

                                // Handle string expected value
                                // If expected value is the string "true" or "false", check both boolean and string representations
                                if (expectedValue is string expectedStr)
                                {
                                    if (expectedStr.Equals("true", StringComparison.Ordinal))
                                    {
                                        // Expected "true" string - match boolean true or string "true" (case-sensitive)
                                        if (actualValue is bool boolVal)
                                            return boolVal == true;
                                        if (actualValue is string actualStr)
                                            return actualStr.Equals("true", StringComparison.Ordinal);
                                        return false;
                                    }
                                    else if (expectedStr.Equals("false", StringComparison.Ordinal))
                                    {
                                        // Expected "false" string - match boolean false or string "false" (case-sensitive)
                                        if (actualValue is bool boolVal)
                                            return boolVal == false;
                                        if (actualValue is string actualStr)
                                            return actualStr.Equals("false", StringComparison.Ordinal);
                                        return false;
                                    }
                                    else if (actualValue is string actualStr)
                                    {
                                        // Regular string comparison - now CASE-SENSITIVE to match CosmosDB behavior
                                        return expectedStr.Equals(actualStr, StringComparison.Ordinal);
                                    }

                                    return false;
                                }

                                // Handle different value types and comparisons
                                if (expectedValue is double && actualValue != null)
                                {
                                    // Handle numeric comparisons (for weight properties)
                                    if (double.TryParse(actualValue.ToString(), out double actualDouble))
                                    {
                                        var expectedDouble = (double)expectedValue;
                                        return Math.Abs(expectedDouble - actualDouble) <
                                               0.0001; // Allow for floating point precision
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

                                return Equals(actualValue, expectedValue);
                            }

                            return false;
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Resolve parameter references like __p0, __p1 to their actual values with type preservation
        /// </summary>
        private object ResolveParameterReference(object argument, Dictionary<string, object> parameters)
        {
            // Handle direct ParameterReference objects (from TinkerGraphQueryParser)
            if (argument is ParameterReference paramRef && parameters != null)
            {
                if (parameters.TryGetValue(paramRef.ParameterName, out var value))
                {
                    return value; // Return the actual value with its original type
                }
            }

            // Handle string arguments that might be parameter names (__p0, p1, etc.)
            if (argument is string argStr && parameters != null)
            {
                // Check if this string looks like a parameter reference
                if (Regex.IsMatch(argStr, @"^__p\d+$") || Regex.IsMatch(argStr, @"^p\d+$"))
                {
                    // Try to resolve it from parameters
                    if (parameters.TryGetValue(argStr, out var value))
                    {
                        return value; // Return the actual value with its original type
                    }
                }
            }

            return argument;
        }

        /// <summary>
        /// Resolve parameter references within a predicate string like "gt(__p0)" or "within(__p0, __p1)"
        /// </summary>
        private string ResolveParameterReferencesInPredicateString(string predicateStr, Dictionary<string, object> parameters)
        {
            if (parameters == null || string.IsNullOrEmpty(predicateStr))
                return predicateStr;

            // Pattern to match parameter references like __p0, __p1, p0, p1 etc.
            var pattern = @"(__p\d+|p\d+)";
            
            return Regex.Replace(predicateStr, pattern, match =>
            {
                var paramName = match.Value;
                if (parameters.TryGetValue(paramName, out var value))
                {
                    // Convert the value to a string representation suitable for the predicate
                    if (value is string strVal)
                    {
                        return $"'{strVal}'";
                    }
                    else if (value is bool boolVal)
                    {
                        return boolVal.ToString().ToLower();
                    }
                    else if (value != null)
                    {
                        return value.ToString();
                    }
                }
                // If parameter not found, return as-is
                return match.Value;
            });
        }
    }
}
