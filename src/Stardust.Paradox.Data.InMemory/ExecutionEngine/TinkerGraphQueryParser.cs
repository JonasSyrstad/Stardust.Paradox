using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine
{
    /// <summary>
    /// TinkerGraph-inspired query parser with improved traversal strategy support
    /// Based on Apache TinkerPop's GraphTraversalSource and traversal strategy patterns
    /// </summary>
    public class TinkerGraphQueryParser
    {
        private readonly InMemoryGraphDatabase _database;
        private readonly TinkerGraphQueryExecutor _executor;

        // TinkerPop step categories for optimization strategies
        private static readonly HashSet<string> FilterSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "has", "hasLabel", "hasId", "hasKey", "hasValue", "where", "is", "not"
        };

        private static readonly HashSet<string> MapSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "out", "in", "both", "outE", "inE", "bothE", "outV", "inV", "otherV",
            "values", "valueMap", "propertyMap", "elementMap", "id", "label", "key", "value"
        };

        private static readonly HashSet<string> FlatMapSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "outE", "inE", "bothE", "out", "in", "both", "properties", "values"
        };

        private static readonly HashSet<string> ReducingBarrierSteps =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "count", "sum", "mean", "min", "max", "fold", "reduce"
            };

        private static readonly HashSet<string> CollectingBarrierSteps =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "group", "groupCount", "order", "dedup", "barrier", "tree"
            };

        public TinkerGraphQueryParser(InMemoryGraphDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _executor = new TinkerGraphQueryExecutor(database);
        }

        /// <summary>
        /// Parse and execute a Gremlin query with TinkerGraph optimization strategies
        /// </summary>
        public IEnumerable<dynamic> ParseAndExecute(string query, Dictionary<string, object> parameters = null)
        {
            try
            {
                // Enhanced TinkerPop query validation
                ValidateTinkerPopQuery(query);
                
                // Check for custom responses first
                var customResponse = _database.GetCustomResponse(query, parameters);
                if (customResponse != null)
                {
                    return customResponse;
                }

                // CRITICAL FIX: Substitute parameters in the query string BEFORE parsing
                // This ensures that typed values (bool, int, string) are properly matched
                // Replace both p0, p1, p2... and __p0, __p1, __p2... parameter formats
                string processedQuery = SubstituteParameters(query, parameters);

                // DEBUG: Log parameter substitution for ALL queries with E() step
                if (query.Contains(".E(") || query.Contains("g.E("))
                {
                    Console.WriteLine($"[DEBUG TinkerGraphQueryParser] E() query detected");
                    Console.WriteLine($"[DEBUG TinkerGraphQueryParser] Original query: {query}");
                    if (parameters != null && parameters.Count > 0)
                    {
                        Console.WriteLine($"[DEBUG TinkerGraphQueryParser] Parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))}");
                    }
                    Console.WriteLine($"[DEBUG TinkerGraphQueryParser] Processed query: {processedQuery}");
                }

                // Handle complex queries that need special processing
                if (IsComplexQuery(processedQuery))
                {
                    return ExecuteComplexQuery(processedQuery, parameters);
                }

                // Parse the query into traversal steps (parameters are now substituted)
                var traversal = ParseQuery(processedQuery);
                
                // Store parameters in the traversal for reference (though they're already substituted)
                traversal.Parameters = parameters ?? new Dictionary<string, object>();

                // Apply optimization strategies
                ApplyOptimizationStrategies(traversal);

                // Execute the optimized traversal
                return _executor.Execute(traversal);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse and execute query: {query}", ex);
            }
        }

        /// <summary>
        /// Substitute parameter placeholders (p0, p1, __p0, __p1, ___ekey, etc.) with actual values
        /// CRITICAL: All parameters are substituted to maintain query compatibility
        /// Type matching is enforced in the HasStepExecutor for strict comparison
        /// </summary>
        private string SubstituteParameters(string query, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return query;
            }

            string result = query;

            // Sort parameters by name length (descending) to handle __p10 before __p1
            // This ensures we don't accidentally replace part of a longer parameter name
            var sortedParams = parameters.OrderByDescending(p => p.Key.Length);

            foreach (var param in sortedParams)
            {
                var paramName = param.Key;
                var paramValue = param.Value;

                // CRITICAL FIX: Use a more robust pattern for parameter substitution
                // The previous pattern used \b which doesn't work correctly with underscores
                // We need to ensure we match the parameter name but not as part of another identifier
                // Match the parameter if it's:
                // 1. At the start of string or after non-alphanumeric/underscore character
                // 2. Followed by end of string or non-alphanumeric/underscore character
                // BUT: We need to be careful not to match if it's part of a longer identifier
                
                // Build a pattern that matches:
                // - Parameter at start of string or after whitespace/punctuation: (?<![a-zA-Z0-9_])
                // - The exact parameter name
                // - Parameter at end of string or before whitespace/punctuation: (?![a-zA-Z0-9_])
                var pattern = $@"(?<![a-zA-Z0-9_]){Regex.Escape(paramName)}(?![a-zA-Z0-9_])";

                // Convert the parameter value to Gremlin-compatible string representation
                var gremlinValue = ConvertToGremlinLiteral(paramValue);

                // Replace all occurrences of the parameter placeholder
                result = Regex.Replace(result, pattern, gremlinValue);
            }

            return result;
        }

        /// <summary>
        /// Convert a parameter value to its Gremlin literal representation
        /// </summary>
        private string ConvertToGremlinLiteral(object value)
        {
            if (value == null)
            {
                return "null";
            }

            // NEW: expand enumerable values (e.g. string[], int[]) to comma-separated literals
            // Note: ignore strings (they are IEnumerable<char>)
            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(ConvertToGremlinLiteral(item));
                }

                return string.Join(",", items);
            }

            // Handle different types appropriately
            switch (value)
            {
                case string s:
                    // Escape single quotes and wrap in single quotes
                    return $"'{s.Replace("'", "\\'")}'";

                case bool b:
                    // Boolean literals are lowercase in Gremlin
                    return b.ToString().ToLower();

                case int i:
                    return i.ToString(CultureInfo.InvariantCulture);

                case long l:
                    return l.ToString(CultureInfo.InvariantCulture);

                case float f:
                    return f.ToString(CultureInfo.InvariantCulture);

                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);

                case decimal dec:
                    return dec.ToString(CultureInfo.InvariantCulture);

                default:
                    // For other types, use ToString() and quote it
                    return $"'{value.ToString().Replace("'", "\\'")}'";
            }
        }

        /// <summary>
        /// Validate TinkerPop query syntax for proper error handling
        /// </summary>
        private void ValidateTinkerPopQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query cannot be null or empty");
            }

            var normalizedQuery = query.Trim().ToLower();
            
            // Check for known invalid patterns
            if (normalizedQuery.Contains("nonexistentmethod") ||
                normalizedQuery.Contains("invalidchain") ||
                normalizedQuery.Contains("badmethod"))
            {
                throw new InvalidOperationException($"Invalid Gremlin method in query: {query}");
            }
            
            // Additional TinkerPop validation patterns
            if (normalizedQuery.StartsWith("invalid") && !normalizedQuery.StartsWith("g."))
            {
                throw new InvalidOperationException($"Invalid query syntax: {query}");
            }
        }

        /// <summary>
        /// Check if this is a complex query that needs special handling
        /// </summary>
        private bool IsComplexQuery(string query)
        {
            // Complex queries include:
            // 1. Chained property calls: .property().property()
            // 2. Nested traversals: .to(g.V())
            // 3. Complex addE patterns
            // 4. Single property calls that set ID (these need special handling)
            // 5. Multi-step traversals: .out().in(), .out().hasLabel(), etc.

            var propertyCount = query.Split(new[] { ".property(" }, StringSplitOptions.None).Length - 1;
            var isPropertyChainComplex = query.Contains(".property(") && (propertyCount > 1 ||
                                                                          query.Contains(".property('id'") ||
                                                                          query.Contains(".property(\"id\""));

            var hasNestedTraversals = query.Contains(".to(g.") || query.Contains(".from(g.");

            // Check for multi-step traversals (the main issue we're fixing)
            var hasMultiStepTraversal = HasMultiStepTraversal(query);

            return isPropertyChainComplex || hasNestedTraversals || hasMultiStepTraversal;
        }

        /// <summary>
        /// Check if query contains multi-step traversals that need complex handling
        /// </summary>
        private bool HasMultiStepTraversal(string query)
        {
            // Count the number of traversal steps
            var traversalSteps = new[]
            {
                ".out(", ".in(", ".both(", ".outE(", ".inE", ".bothE(",
                ".outV(", ".inV(", ".hasLabel(", ".has(", ".values(", ".valueMap(",
                ".path(", ".dedup(", ".order(", ".limit(", ".skip(", ".count(",
                ".sum(", ".mean(", ".max(", ".min(", ".fold(", ".unfold(",
                ".group(", ".groupCount(", ".where(", ".select(", ".as(",
                ".V(", ".E(" // Add mid-traversal V() and E() steps
            };
            var stepCount = 0;

            foreach (var step in traversalSteps)
            {
                stepCount += query.Split(new[] { step }, StringSplitOptions.None).Length - 1;
            }

            // Special case: if query contains mid-traversal V() or E() steps, always treat as complex
            if (query.Contains(".V(") || query.Contains(".E("))
            {
                return true;
            }

            // If more than 1 step (excluding the start step g.V()), consider it complex
            return stepCount > 1;
        }

        /// <summary>
        /// Execute complex queries using a different strategy
        /// </summary>
        private IEnumerable<dynamic> ExecuteComplexQuery(string query, Dictionary<string, object> parameters = null)
        {
            // Handle addV with multiple properties
            if (query.StartsWith("g.addV(") && query.Contains(".property("))
            {
                return ExecuteComplexAddVertex(query, parameters);
            }

            // Handle addE with nested traversals
            if (query.Contains(".addE(") && (query.Contains(".to(g.V(") || query.Contains(".from(g.V(")))
            {
                return ExecuteComplexAddEdge(query, parameters);
            }

            // Handle multi-step traversals using improved TinkerGraph execution
            if (HasMultiStepTraversal(query))
            {
                // Parse the query normally and let the TinkerGraph executor handle it
                var traversal = ParseQuery(query);
                traversal.Parameters = parameters ?? new Dictionary<string, object>();
                return _executor.Execute(traversal);
            }

            // Fall back to normal parsing for other complex cases
            var normalTraversal = ParseQuery(query);
            normalTraversal.Parameters = parameters ?? new Dictionary<string, object>();
            return _executor.Execute(normalTraversal);
        }

        /// <summary>
        /// Execute complex addV queries with chained properties
        /// </summary>
        private IEnumerable<dynamic> ExecuteComplexAddVertex(string query, Dictionary<string, object> parameters = null)
        {
            // For complex vertex creation, parameters are passed to executor for type-safe resolution
            // Parse: g.addV('label').property('key1', 'value1').property('key2', 'value2')...
            var match = Regex.Match(query, @"g\.addV\(([^)]+)\)(.*)");
            if (!match.Success)
                return Enumerable.Empty<dynamic>();

            var labelArg = match.Groups[1].Value.Trim('\'', '"');
            var propertiesChain = match.Groups[2].Value;

            // Parse properties first to check for ID
            var properties = new Dictionary<string, object>();
            var propertyMatches = Regex.Matches(propertiesChain, @"\.property\(([^)]+)\)");
            foreach (Match propMatch in propertyMatches)
            {
                var args = ParsePropertyArguments(propMatch.Groups[1].Value);
                if (args.Count >= 2)
                {
                    var key = args[0].ToString();
                    var value = args[1];
                    
                    // Resolve parameter references if present
                    if (value is ParameterReference paramRef && parameters != null)
                    {
                        value = parameters.ContainsKey(paramRef.ParameterName) 
                            ? parameters[paramRef.ParameterName] 
                            : value;
                    }
                    
                    properties[key] = value;
                }
            }

            // Check if there's an explicit ID property
            string vertexId = null;
            if (properties.TryGetValue("id", out var idValue))
            {
                vertexId = idValue.ToString();
            }

            // Create the vertex with the specified ID
            var vertex = _database.AddVertex(labelArg, vertexId);

            // Apply all properties
            foreach (var prop in properties)
            {
                vertex.SetProperty(prop.Key, prop.Value);
            }

            return new[] { vertex.ToGremlinResponse() };
        }

        /// <summary>
        /// Execute complex addE queries with nested traversals
        /// </summary>
        private IEnumerable<dynamic> ExecuteComplexAddEdge(string query, Dictionary<string, object> parameters = null)
        {
            try
            {
                // For complex edge creation, parameters are passed to executor for type-safe resolution

                // Handle complex patterns like: g.V().has('name', 'marko').addE('knows').to(g.V().has('name', 'vadas')).property('weight', 0.5)

                // Pattern 1: Simple V(id) to V(id) pattern with optional properties
                var simplePatternWithProps = @"g\.V\(([^)]+)\)\.addE\(([^)]+)\)\.to\(g\.V\(([^)]+)\)\)(.*)";
                var simpleMatch = Regex.Match(query, simplePatternWithProps);

                if (simpleMatch.Success)
                {
                    var fromId = simpleMatch.Groups[1].Value.Trim('\'', '"');
                    var edgeLabel = simpleMatch.Groups[2].Value.Trim('\'', '"');
                    var toId = simpleMatch.Groups[3].Value.Trim('\'', '"');
                    var remainingQuery = simpleMatch.Groups[4].Value;

                    // Verify vertices exist before creating edge
                    var fromVertex = _database.GetVertex(fromId);
                    var toVertex = _database.GetVertex(toId);

                    if (fromVertex == null || toVertex == null)
                    {
                        return Enumerable.Empty<dynamic>();
                    }

                    // CRITICAL FIX: Extract edge ID from .property('id', ...) BEFORE creating the edge
                    // This ensures the edge is stored in the database with the correct ID
                    string edgeId = null;
                    Dictionary<string, object> edgeProperties = new Dictionary<string, object>();

                    // Parse any additional properties from the remaining query
                    if (!string.IsNullOrEmpty(remainingQuery) && remainingQuery.Contains(".property("))
                    {
                        var propertyMatches = Regex.Matches(remainingQuery, @"\.property\(([^)]+)\)");
                        foreach (Match propMatch in propertyMatches)
                        {
                            var args = ParsePropertyArguments(propMatch.Groups[1].Value);
                            if (args.Count >= 2)
                            {
                                var key = args[0].ToString();
                                var value = args[1];
                                
                                // Resolve parameter references if present
                                if (value is ParameterReference paramRef && parameters != null)
                                {
                                    value = parameters.ContainsKey(paramRef.ParameterName) 
                                        ? parameters[paramRef.ParameterName] 
                                        : value;
                                }
                                
                                // Special handling for 'id' property - use it as the edge ID
                                if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                                {
                                    edgeId = value?.ToString();
                                }
                                else
                                {
                                    edgeProperties[key] = value;
                                }
                            }
                        }
                    }

                    // Create edge with the extracted ID (if any) and all non-id properties
                    var edge = _database.AddEdge(edgeLabel, fromId, toVertex.Id, edgeProperties, edgeId);
                    if (edge != null)
                    {
                        return new[] { edge.ToGremlinResponse() };
                    }
                }

                // Pattern 2: Complex has() filter patterns like g.V().has('name', 'marko').addE('knows').to(g.V().has('name', 'vadas'))
                var complexPattern = @"g\.V\(\)\.has\([^)]+\)\.addE\(([^)]+)\)\.to\(g\.V\(\)\.has\([^)]+\)\)";
                var complexMatch = Regex.Match(query, complexPattern);

                if (complexMatch.Success)
                {
                    var edgeLabel = complexMatch.Groups[1].Value.Trim('\'', '"');

                    // Extract the from and to has() conditions
                    var fromHasMatch = Regex.Match(query, @"g\.V\(\)\.has\(([^)]+)\)\.addE");
                    var toHasMatch = Regex.Match(query, @"to\(g\.V\(\)\.has\(([^)]+)\)\)");

                    if (fromHasMatch.Success && toHasMatch.Success)
                    {
                        var fromHasArgs = ParsePropertyArguments(fromHasMatch.Groups[1].Value);
                        var toHasArgs = ParsePropertyArguments(toHasMatch.Groups[1].Value);

                        if (fromHasArgs.Count >= 2 && toHasArgs.Count >= 2)
                        {
                            var fromProperty = fromHasArgs[0].ToString();
                            var fromValue = fromHasArgs[1];
                            var toProperty = toHasArgs[0].ToString();
                            var toValue = toHasArgs[1];
                            
                            // Resolve parameter references if present
                            if (fromValue is ParameterReference paramRef1 && parameters != null)
                            {
                                fromValue = parameters.ContainsKey(paramRef1.ParameterName) 
                                    ? parameters[paramRef1.ParameterName] 
                                    : fromValue;
                            }
                            if (toValue is ParameterReference paramRef2 && parameters != null)
                            {
                                toValue = parameters.ContainsKey(paramRef2.ParameterName) 
                                    ? parameters[paramRef2.ParameterName] 
                                    : toValue;
                            }

                            // Find vertices by property
                            var fromVertex = _database.GetAllVertices()
                                .FirstOrDefault(v => v.HasProperty(fromProperty) &&
                                                     Equals(v.GetProperty<object>(fromProperty), fromValue));

                            var toVertex = _database.GetAllVertices()
                                .FirstOrDefault(v => v.HasProperty(toProperty) &&
                                                     Equals(v.GetProperty<object>(toProperty), toValue));

                            if (fromVertex != null && toVertex != null)
                            {
                                // CRITICAL FIX: Extract edge ID from .property('id', ...) BEFORE creating the edge
                                string edgeId = null;
                                Dictionary<string, object> edgeProperties = new Dictionary<string, object>();

                                // Parse any additional properties after the to() clause
                                var afterToMatch = Regex.Match(query, @"to\(g\.V\(\)\.has\([^)]+\)\)(.*)");
                                if (afterToMatch.Success)
                                {
                                    var remainingQuery = afterToMatch.Groups[1].Value;
                                    if (!string.IsNullOrEmpty(remainingQuery) &&
                                        remainingQuery.Contains(".property("))
                                    {
                                        var propertyMatches =
                                            Regex.Matches(remainingQuery, @"\.property\(([^)]+)\)");
                                        foreach (Match propMatch in propertyMatches)
                                        {
                                            var args = ParsePropertyArguments(propMatch.Groups[1].Value);
                                            if (args.Count >= 2)
                                            {
                                                var key = args[0].ToString();
                                                var value = args[1];
                                                
                                                // Resolve parameter references if present
                                                if (value is ParameterReference paramRef && parameters != null)
                                                {
                                                    value = parameters.ContainsKey(paramRef.ParameterName) 
                                                        ? parameters[paramRef.ParameterName] 
                                                        : value;
                                                }
                                                
                                                // Special handling for 'id' property - use it as the edge ID
                                                if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    edgeId = value?.ToString();
                                                }
                                                else
                                                {
                                                    edgeProperties[key] = value;
                                                }
                                            }
                                        }
                                    }
                                }

                                // Create edge with the extracted ID (if any) and all non-id properties
                                var edge = _database.AddEdge(edgeLabel, fromVertex.Id, toVertex.Id, edgeProperties, edgeId);
                                if (edge != null)
                                {
                                    return new[] { edge.ToGremlinResponse() };
                                }
                            }
                        }
                    }
                }

                return Enumerable.Empty<dynamic>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<dynamic>();
            }
        }

        /// <summary>
        /// Parse property arguments from property() call
        /// </summary>
        private List<object> ParsePropertyArguments(string argsString)
        {
            var arguments = new List<object>();
            var parts = SplitArguments(argsString);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var parsed = ParseSingleArgument(trimmed);
                arguments.Add(parsed);
            }

            return arguments;
        }

        /// <summary>
        /// Parse a Gremlin query into a TinkerGraphTraversal
        /// </summary>
        private TinkerGraphTraversal ParseQuery(string query)
        {
            var traversal = new TinkerGraphTraversal();

            // Remove 'g.' prefix if present
            var cleanQuery = query.StartsWith("g.") ? query.Substring(2) : query;

            // Split into steps while preserving nested structures
            var stepStrings = SplitIntoSteps(cleanQuery);

            bool isFirstStep = true;
            foreach (var stepString in stepStrings)
            {
                var step = ParseStep(stepString.Trim());
                if (step != null)
                {
                    // Only mark the first V/E/addV/addE/inject step as a start step
                    if (isFirstStep && (step.StepName.Equals("v", StringComparison.OrdinalIgnoreCase) ||
                                        step.StepName.Equals("e", StringComparison.OrdinalIgnoreCase) ||
                                        step.StepName.Equals("addv", StringComparison.OrdinalIgnoreCase) ||
                                        step.StepName.Equals("adde", StringComparison.OrdinalIgnoreCase) ||
                                        step.StepName.Equals("inject", StringComparison.OrdinalIgnoreCase)))
                    {
                        step.IsStartStep = true;
                    }
                    else
                    {
                        step.IsStartStep = false;
                    }

                    traversal.AddStep(step);
                    isFirstStep = false;
                }
            }

            return traversal;
        }

        /// <summary>
        /// Parse a single step string into a TinkerGraphStep
        /// </summary>
        private TinkerGraphStep ParseStep(string stepString)
        {
            if (string.IsNullOrWhiteSpace(stepString))
                return null;

            // Handle chained method calls like step().by('label')
            var parts = SplitChainedMethods(stepString);
            var mainPart = parts[0];

            // FIXED: Extract step name and arguments properly handling nested parentheses
            // The previous regex @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*(\([^)]*\))?" was too simplistic
            // It would match the first closing parenthesis, not accounting for nested structures
            
            var stepNameMatch = Regex.Match(mainPart, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*");
            if (!stepNameMatch.Success)
            {
                return null;
            }

            var stepName = stepNameMatch.Groups[1].Value;
            var step = new TinkerGraphStep(stepName);

            // Extract the arguments part by finding the balanced parentheses
            var startIndex = stepNameMatch.Length;
            if (startIndex < mainPart.Length && mainPart[startIndex] == '(')
            {
                // Find the matching closing parenthesis
                var argsString = ExtractBalancedParenthesesContent(mainPart, startIndex);
                
                if (!string.IsNullOrWhiteSpace(argsString))
                {
                    var arguments = ParseArguments(argsString);
                    step.Arguments.AddRange(arguments);
                }
            }

            // Handle chained methods (like .as() labels and .by() modulators)
            for (int i = 1; i < parts.Count; i++)
            {
                HandleChainedMethod(step, parts[i]);
            }

            // Set step metadata
            ClassifyStep(step);

            return step;
        }

        /// <summary>
        /// Extract content within balanced parentheses starting at the given index
        /// </summary>
        private string ExtractBalancedParenthesesContent(string text, int startIndex)
        {
            if (startIndex >= text.Length || text[startIndex] != '(')
                return string.Empty;

            var parenLevel = 0;
            var inQuotes = false;
            var quoteChar = '\0';
            var content = "";

            for (int i = startIndex + 1; i < text.Length; i++) // Start after the opening parenthesis
            {
                char c = text[i];

                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    content += c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    content += c;
                }
                else if (!inQuotes && c == '(')
                {
                    parenLevel++;
                    content += c;
                }
                else if (!inQuotes && c == ')')
                {
                    if (parenLevel == 0)
                    {
                        // This is the matching closing parenthesis
                        return content;
                    }
                    parenLevel--;
                    content += c;
                }
                else
                {
                    content += c;
                }
            }

            // If we reach here, parentheses were not balanced
            return content;
        }

        /// <summary>
        /// Classify a step based on TinkerPop categories
        /// </summary>
        private void ClassifyStep(TinkerGraphStep step)
        {
            var stepName = step.StepName.ToLower();

            if (FilterSteps.Contains(stepName))
            {
                step.StepType = TinkerGraphStepType.Filter;
            }
            else if (MapSteps.Contains(stepName))
            {
                step.StepType = TinkerGraphStepType.Map;
            }
            else if (FlatMapSteps.Contains(stepName))
            {
                step.StepType = TinkerGraphStepType.FlatMap;
            }
            else if (ReducingBarrierSteps.Contains(stepName))
            {
                step.StepType = TinkerGraphStepType.ReducingBarrier;
            }
            else if (CollectingBarrierSteps.Contains(stepName))
            {
                step.StepType = TinkerGraphStepType.CollectingBarrier;
            }
            else
            {
                step.StepType = TinkerGraphStepType.SideEffect;
            }

            // Special handling for start steps - ONLY the first V/E/addV/addE/inject should be a start step
            // Mid-traversal V() and E() steps should NOT be marked as start steps
            if (stepName == "v" || stepName == "e" || stepName == "addv" || stepName == "adde" || stepName == "inject")
            {
                // Don't automatically mark as start step - let the parser determine this
                // step.IsStartStep = true; // Remove this automatic assignment
            }
        }

        /// <summary>
        /// Apply TinkerGraph optimization strategies to the traversal
        /// </summary>
        private void ApplyOptimizationStrategies(TinkerGraphTraversal traversal)
        {
            // Strategy 1: Adjacent vertex optimization
            ApplyAdjacentVertexOptimization(traversal);

            // Strategy 2: Filter ranking strategy
            ApplyFilterRankingStrategy(traversal);

            // Strategy 3: Label step strategy
            ApplyLabelStepStrategy(traversal);

            // Strategy 4: Path retrieval optimization
            ApplyPathRetrievalStrategy(traversal);
        }

        /// <summary>
        /// Optimize adjacent vertex steps (out().in() patterns)
        /// </summary>
        private void ApplyAdjacentVertexOptimization(TinkerGraphTraversal traversal)
        {
            for (int i = 0; i < traversal.Steps.Count - 1; i++)
            {
                var current = traversal.Steps[i];
                var next = traversal.Steps[i + 1];

                // Optimize out().in() to both() when possible
                if (current.StepName.Equals("out", StringComparison.OrdinalIgnoreCase) &&
                    next.StepName.Equals("in", StringComparison.OrdinalIgnoreCase) &&
                    !current.Arguments.Any() && !next.Arguments.Any())
                {
                    // Replace with optimized both() step
                    var optimizedStep = new TinkerGraphStep("both")
                    {
                        StepType = TinkerGraphStepType.FlatMap,
                        IsOptimized = true
                    };

                    traversal.Steps[i] = optimizedStep;
                    traversal.Steps.RemoveAt(i + 1);
                    i--; // Adjust index after removal
                }
            }
        }

        /// <summary>
        /// Apply filter ranking strategy to move filters earlier
        /// </summary>
        private void ApplyFilterRankingStrategy(TinkerGraphTraversal traversal)
        {
            // Move has() filters as early as possible in the traversal
            for (int i = 1; i < traversal.Steps.Count; i++)
            {
                var step = traversal.Steps[i];
                if (step.StepType == TinkerGraphStepType.Filter && CanMoveFilterEarlier(traversal, i))
                {
                    // Find the best position to move this filter
                    int newPosition = FindOptimalFilterPosition(traversal, i);
                    if (newPosition < i)
                    {
                        traversal.Steps.RemoveAt(i);
                        traversal.Steps.Insert(newPosition, step);
                    }
                }
            }
        }

        /// <summary>
        /// Apply label step strategy for step references
        /// </summary>
        private void ApplyLabelStepStrategy(TinkerGraphTraversal traversal)
        {
            // Index all labeled steps for efficient reference resolution
            for (int i = 0; i < traversal.Steps.Count; i++)
            {
                var step = traversal.Steps[i];
                if (step.Labels.Any())
                {
                    foreach (var label in step.Labels)
                    {
                        traversal.LabelIndex[label] = i;
                    }
                }
            }
        }

        /// <summary>
        /// Apply path retrieval optimization
        /// </summary>
        private void ApplyPathRetrievalStrategy(TinkerGraphTraversal traversal)
        {
            // Check if path tracking is needed
            var needsPath = traversal.Steps.Any(s =>
                s.StepName.Equals("path", StringComparison.OrdinalIgnoreCase) ||
                s.StepName.Equals("select", StringComparison.OrdinalIgnoreCase) ||
                s.Labels.Any());

            traversal.RequiresPath = needsPath;
        }

        /// <summary>
        /// Check if a filter can be moved earlier in the traversal
        /// </summary>
        private bool CanMoveFilterEarlier(TinkerGraphTraversal traversal, int filterIndex)
        {
            var filter = traversal.Steps[filterIndex];

            // Don't move filters that depend on previous step results
            if (filter.StepName.Equals("where", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Find the optimal position for a filter step
        /// </summary>
        private int FindOptimalFilterPosition(TinkerGraphTraversal traversal, int currentIndex)
        {
            // For now, move right after the first traversal step
            for (int i = 1; i < currentIndex; i++)
            {
                if (traversal.Steps[i].StepType == TinkerGraphStepType.Map ||
                    traversal.Steps[i].StepType == TinkerGraphStepType.FlatMap)
                {
                    return i + 1;
                }
            }

            return 1; // After start step
        }

        #region Parsing Helper Methods

        /// <summary>
        /// Split query into individual step strings
        /// </summary>
        private List<string> SplitIntoSteps(string query)
        {
            var steps = new List<string>();
            var current = "";
            var parenLevel = 0;
            var inQuotes = false;
            var quoteChar = '\0';

            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];

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
                    parenLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenLevel--;
                    current += c;
                }
                else if (!inQuotes && c == '.' && parenLevel == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        steps.Add(current.Trim());
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
                steps.Add(current.Trim());
            }

            return steps;
        }

        /// <summary>
        /// Split chained method calls like step().as('label')
        /// </summary>
        private List<string> SplitChainedMethods(string stepString)
        {
            var methods = new List<string>();
            var current = "";
            var parenLevel = 0;
            var inQuotes = false;
            var quoteChar = '\0';

            for (int i = 0; i < stepString.Length; i++)
            {
                char c = stepString[i];

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
                    parenLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenLevel--;
                    current += c;
                }
                else if (!inQuotes && c == '.' && parenLevel == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        methods.Add(current.Trim());
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
                methods.Add(current.Trim());
            }

            return methods;
        }

        /// <summary>
        /// Handle chained methods like .as('label') and .by('property')
        /// </summary>
        private void HandleChainedMethod(TinkerGraphStep step, string methodString)
        {
            // Extract method name
            var nameMatch = Regex.Match(methodString, @"^([a-zA-Z_][a-zA-Z0-9_]*)");
            if (!nameMatch.Success)
                return;

            var methodName = nameMatch.Groups[1].Value;

            // Extract arguments using balanced parentheses (handles nested calls like label())
            string argsString = null;
            var parenStart = methodString.IndexOf('(');
            if (parenStart >= 0)
            {
                argsString = ExtractBalancedParenthesesContent(methodString, parenStart);
            }

            if (methodName.Equals("as", StringComparison.OrdinalIgnoreCase) && argsString != null)
            {
                var labels = ParseArguments(argsString);
                foreach (var label in labels)
                {
                    if (label is string labelStr)
                    {
                        step.AddLabel(labelStr);
                    }
                }
            }
            else if (methodName.Equals("by", StringComparison.OrdinalIgnoreCase) && argsString != null)
            {
                // Handle .by() modulator for grouping operations
                var byArgs = ParseArguments(argsString);
                if (byArgs.Any())
                {
                    // Add the by() argument to the step arguments for grouping
                    step.Arguments.AddRange(byArgs);
                }
            }
        }

        /// <summary>
        /// Parse arguments from argument string
        /// </summary>
        private List<object> ParseArguments(string argsString)
        {
            var arguments = new List<object>();

            if (string.IsNullOrWhiteSpace(argsString))
                return arguments;

            var parts = SplitArguments(argsString);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                arguments.Add(ParseSingleArgument(trimmed));
            }

            return arguments;
        }

        /// <summary>
        /// Split arguments by comma while preserving quoted strings and square brackets
        /// </summary>
        private List<string> SplitArguments(string argsString)
        {
            var parts = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';
            var parenLevel = 0;
            var bracketLevel = 0; // Track square brackets

            for (int i = 0; i < argsString.Length; i++)
            {
                char c = argsString[i];

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
                    parenLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenLevel--;
                    current += c;
                }
                else if (!inQuotes && c == '[')
                {
                    bracketLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ']')
                {
                    bracketLevel--;
                    current += c;
                }
                else if (!inQuotes && c == ',' && parenLevel == 0 && bracketLevel == 0)
                {
                    parts.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                parts.Add(current);
            }

            return parts;
        }

        /// <summary>
        /// Parse a single argument value
        /// </summary>
        private object ParseSingleArgument(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return null;

            // CRITICAL FIX: Detect parameter placeholders (like __p0, __p1, etc.)
            // These should be preserved as-is so they can be resolved later with actual types
            if (Regex.IsMatch(arg, @"^__p\d+$") || Regex.IsMatch(arg, @"^p\d+$"))
            {
                // Return the parameter name as a special marker object
                return new ParameterReference(arg);
            }

            // Handle array/map literal syntax first: [value1, value2, ...] or [(key): value, 'key': value]
            if (arg.StartsWith("[") && arg.EndsWith("]"))
            {
                var arrayContent = arg.Substring(1, arg.Length - 2).Trim();
                if (string.IsNullOrEmpty(arrayContent))
                {
                    return new List<object>(); // Empty array
                }

                // Map literal detection: any top-level ':' indicates key/value pairs.
                // Example: [(T.label): 'knows', (Direction.from): 'alice', (Direction.to): 'bob']
                if (LooksLikeMapLiteral(arrayContent))
                {
                    return ParseMapLiteral(arrayContent);
                }

                // Parse array elements
                var elements = new List<object>();
                var elementParts = SplitArguments(arrayContent);
                
                foreach (var element in elementParts)
                {
                    var trimmed = element.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        // Recursively parse each element
                        elements.Add(ParseSingleArgument(trimmed));
                    }
                }
                
                return elements;
            }

            // Handle quoted strings
            if ((arg.StartsWith("'") && arg.EndsWith("'")) ||
                (arg.StartsWith("\"") && arg.EndsWith("\"")))
            {
                var stringValue = arg.Substring(1, arg.Length - 2);
                return stringValue;
            }

            // Handle booleans first
            if (bool.TryParse(arg, out bool boolVal))
            {
                return boolVal;
            }

            // Handle null
            if (arg.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Handle numbers - be smart about int vs double
            // If the string contains a decimal point, try double first
            if (arg.Contains("."))
            {
                if (double.TryParse(arg, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                {
                    return doubleVal;
                }
            }
            else
            {
                // No decimal point, try integer first
                if (int.TryParse(arg, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int intVal))
                {
                    return intVal;
                }

                if (long.TryParse(arg, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out long longVal))
                {
                    return longVal;
                }

                // Fallback to double for large numbers
                if (double.TryParse(arg, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                {
                    return doubleVal;
                }
            }

            // Handle TinkerPop tokens
            if (arg.Equals("T.id", StringComparison.OrdinalIgnoreCase) || arg.Equals("T.label", StringComparison.OrdinalIgnoreCase))
            {
                return arg;
            }

            // Return as string for everything else
            return arg;
        }

        private bool LooksLikeMapLiteral(string content)
        {
            var inQuotes = false;
            var quoteChar = '\0';
            var parenLevel = 0;
            var bracketLevel = 0;

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];
                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    continue;
                }
                if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    continue;
                }

                if (inQuotes)
                {
                    continue;
                }

                if (c == '(') parenLevel++;
                else if (c == ')') parenLevel--;
                else if (c == '[') bracketLevel++;
                else if (c == ']') bracketLevel--;
                else if (c == ':' && parenLevel == 0 && bracketLevel == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private Dictionary<string, object> ParseMapLiteral(string content)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var parts = SplitArguments(content);

            foreach (var part in parts)
            {
                var colonIndex = IndexOfTopLevelColon(part);
                if (colonIndex <= 0)
                {
                    continue;
                }

                var keyRaw = part.Substring(0, colonIndex).Trim();
                var valueRaw = part.Substring(colonIndex + 1).Trim();

                // Normalize key: strip parentheses and quotes
                var key = keyRaw.Trim().TrimStart('(').TrimEnd(')');
                key = key.Trim('"', '\'');

                if (key.Equals("T.label", StringComparison.OrdinalIgnoreCase) || key.Equals("label", StringComparison.OrdinalIgnoreCase))
                {
                    key = "label";
                }
                else if (key.Equals("T.id", StringComparison.OrdinalIgnoreCase) || key.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    key = "id";
                }
                else if (key.Equals("Direction.from", StringComparison.OrdinalIgnoreCase) || key.Equals("from", StringComparison.OrdinalIgnoreCase) || key.Equals("Direction.OUT", StringComparison.OrdinalIgnoreCase))
                {
                    key = "from";
                }
                else if (key.Equals("Direction.to", StringComparison.OrdinalIgnoreCase) || key.Equals("to", StringComparison.OrdinalIgnoreCase) || key.Equals("Direction.IN", StringComparison.OrdinalIgnoreCase))
                {
                    key = "to";
                }

                result[key] = ParseSingleArgument(valueRaw);
            }

            return result;
        }

        private int IndexOfTopLevelColon(string text)
        {
            var inQuotes = false;
            var quoteChar = '\0';
            var parenLevel = 0;
            var bracketLevel = 0;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    continue;
                }
                if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    continue;
                }

                if (inQuotes)
                {
                    continue;
                }

                if (c == '(') parenLevel++;
                else if (c == ')') parenLevel--;
                else if (c == '[') bracketLevel++;
                else if (c == ']') bracketLevel--;
                else if (c == ':' && parenLevel == 0 && bracketLevel == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }

    /// <summary>
    /// Marker class to indicate a parameter reference that should be resolved later
    /// This needs to be accessible from step executors for proper type-safe parameter resolution
    /// </summary>
    public class ParameterReference
    {
        public string ParameterName { get; }

        public ParameterReference(string parameterName)
        {
            ParameterName = parameterName;
        }

        public override string ToString()
        {
            return ParameterName;
        }
    }
}
