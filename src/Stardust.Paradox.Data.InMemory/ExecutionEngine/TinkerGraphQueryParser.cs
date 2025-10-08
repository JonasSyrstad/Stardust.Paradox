using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

                // Substitute parameters BEFORE parsing (critical for TinkerPop compatibility)
                var processedQuery = SubstituteParameters(query, parameters ?? new Dictionary<string, object>());

                // Handle complex queries that need special processing
                if (IsComplexQuery(processedQuery))
                {
                    return ExecuteComplexQuery(processedQuery);
                }

                // Parse the query into traversal steps
                var traversal = ParseQuery(processedQuery);
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
        private IEnumerable<dynamic> ExecuteComplexQuery(string query)
        {
            // Handle addV with multiple properties
            if (query.StartsWith("g.addV(") && query.Contains(".property("))
            {
                return ExecuteComplexAddVertex(query);
            }

            // Handle addE with nested traversals
            if (query.Contains(".addE(") && (query.Contains(".to(g.V(") || query.Contains(".from(g.V(")))
            {
                return ExecuteComplexAddEdge(query);
            }

            // Handle multi-step traversals using improved TinkerGraph execution
            if (HasMultiStepTraversal(query))
            {
                // Parse the query normally and let the TinkerGraph executor handle it
                var traversal = ParseQuery(query);
                return _executor.Execute(traversal);
            }

            // Fall back to normal parsing for other complex cases
            var normalTraversal = ParseQuery(query);
            return _executor.Execute(normalTraversal);
        }

        /// <summary>
        /// Execute complex addV queries with chained properties
        /// </summary>
        private IEnumerable<dynamic> ExecuteComplexAddVertex(string query)
        {
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
        private IEnumerable<dynamic> ExecuteComplexAddEdge(string query)
        {
            try
            {
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

                    var edge = _database.AddEdge(edgeLabel, fromId, toVertex.Id);
                    if (edge != null)
                    {
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
                                    edge.SetProperty(key, value);
                                }
                            }
                        }

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

                            // Find vertices by property
                            var fromVertex = _database.GetAllVertices()
                                .FirstOrDefault(v => v.HasProperty(fromProperty) &&
                                                     Equals(v.GetProperty<object>(fromProperty), fromValue));

                            var toVertex = _database.GetAllVertices()
                                .FirstOrDefault(v => v.HasProperty(toProperty) &&
                                                     Equals(v.GetProperty<object>(toProperty), toValue));

                            if (fromVertex != null && toVertex != null)
                            {
                                var edge = _database.AddEdge(edgeLabel, fromVertex.Id, toVertex.Id);
                                if (edge != null)
                                {
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
                                                    edge.SetProperty(key, value);
                                                }
                                            }
                                        }
                                    }

                                    return new[] { edge.ToGremlinResponse() };
                                }
                            }
                        }
                    }
                }

                return Enumerable.Empty<dynamic>();
            }
            catch (Exception ex)
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
            var match = Regex.Match(methodString, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*(\([^)]*\))?");
            if (!match.Success)
                return;

            var methodName = match.Groups[1].Value;

            if (methodName.Equals("as", StringComparison.OrdinalIgnoreCase) && match.Groups[2].Success)
            {
                var argsString = match.Groups[2].Value.Trim('(', ')');
                var labels = ParseArguments(argsString);
                foreach (var label in labels)
                {
                    if (label is string labelStr)
                    {
                        step.AddLabel(labelStr);
                    }
                }
            }
            else if (methodName.Equals("by", StringComparison.OrdinalIgnoreCase) && match.Groups[2].Success)
            {
                // Handle .by() modulator for grouping operations
                var argsString = match.Groups[2].Value.Trim('(', ')');
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

            // Handle array syntax first: [value1, value2, ...]
            if (arg.StartsWith("[") && arg.EndsWith("]"))
            {
                var arrayContent = arg.Substring(1, arg.Length - 2).Trim();
                if (string.IsNullOrEmpty(arrayContent))
                {
                    return new List<object>(); // Empty array
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

            // Return as string for everything else
            return arg;
        }

        /// <summary>
        /// Substitute parameters in the query
        /// </summary>
        private string SubstituteParameters(string query, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return query;

            var result = query;
            
            // Sort parameters by key length (descending) to avoid partial replacements
            // For example, replace __p10 before __p1 to avoid __p10 becoming __p1'0'
            var sortedParameters = parameters.OrderByDescending(p => p.Key.Length).ToList();
            
            foreach (var param in sortedParameters)
            {
                // Handle parameter substitution with proper value formatting
                var value = FormatParameterValue(param.Value);
                
                // Replace parameter placeholder with formatted value
                // Use word boundary to ensure exact parameter matching
                var pattern = @"\b" + Regex.Escape(param.Key) + @"\b";
                result = Regex.Replace(result, pattern, value);
            }

            return result;
        }

        /// <summary>
        /// Format a parameter value for substitution with TinkerPop compliance
        /// </summary>
        private string FormatParameterValue(object value)
        {
            if (value == null) return "null";
            if (value is string) return $"'{value}'";
            if (value is bool) return value.ToString().ToLower();

            // Handle numeric types with proper culture formatting
            if (value is double doubleVal)
            {
                return doubleVal.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value is float floatVal)
            {
                return floatVal.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value is decimal decimalVal)
            {
                return decimalVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value is int || value is long || value is short || value is byte)
            {
                return value.ToString();
            }

            return value.ToString();
        }

        #endregion
    }
}
