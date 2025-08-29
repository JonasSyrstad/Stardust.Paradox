using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory
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

        private static readonly HashSet<string> ReducingBarrierSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "count", "sum", "mean", "min", "max", "fold", "reduce"
        };

        private static readonly HashSet<string> CollectingBarrierSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "group", "groupCount", "order", "dedup", "barrier"
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
                // Check for custom responses first
                var customResponse = _database.GetCustomResponse(query, parameters);
                if (customResponse != null)
                {
                    return customResponse;
                }

                // Substitute parameters
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
            var isPropertyChainComplex = query.Contains(".property(") && (propertyCount > 1 || query.Contains(".property('id'") || query.Contains(".property(\"id\""));
            
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
            var traversalSteps = new[] { 
                ".out(", ".in(", ".both(", ".outE(", ".inE", ".bothE(", 
                ".outV(", ".inV(", ".hasLabel(", ".has(", ".values(", ".valueMap(",
                ".path(", ".dedup(", ".order(", ".limit(", ".skip(", ".count(",
                ".sum(", ".mean(", ".max(", ".min(", ".fold(", ".unfold(",
                ".group(", ".groupCount(", ".where(", ".select(", ".as("
            };
            var stepCount = 0;
            
            foreach (var step in traversalSteps)
            {
                stepCount += query.Split(new[] { step }, StringSplitOptions.None).Length - 1;
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
                
                // Pattern 1: Simple V(id) to V(id) pattern  
                var simplePattern = @"g\.V\(([^)]+)\)\.addE\(([^)]+)\)\.to\(g\.V\(([^)]+)\)\)";
                var simpleMatch = Regex.Match(query, simplePattern);
                
                if (simpleMatch.Success)
                {
                    var fromId = simpleMatch.Groups[1].Value.Trim('\'', '"');
                    var edgeLabel = simpleMatch.Groups[2].Value.Trim('\'', '"');
                    var toId = simpleMatch.Groups[3].Value.Trim('\'', '"');

                    var edge = _database.AddEdge(edgeLabel, fromId, toId);
                    if (edge != null)
                    {
                        // Parse any additional properties
                        var remainingQuery = query.Substring(simpleMatch.Length);
                        if (remainingQuery.Contains(".property("))
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
                                    // Parse any additional properties
                                    var propertyMatches = Regex.Matches(query, @"\.property\(([^)]+)\)");
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
                // If complex parsing fails, return empty
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
                    
                arguments.Add(ParseSingleArgument(trimmed));
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
            
            foreach (var stepString in stepStrings)
            {
                var step = ParseStep(stepString.Trim());
                if (step != null)
                {
                    traversal.AddStep(step);
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
            
            // Extract step name and arguments
            var match = Regex.Match(mainPart, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*(\([^)]*\))?");
            if (!match.Success)
                return null;

            var stepName = match.Groups[1].Value;
            var step = new TinkerGraphStep(stepName);

            // Parse arguments if present
            if (match.Groups[2].Success)
            {
                var argsString = match.Groups[2].Value.Trim('(', ')');
                if (!string.IsNullOrWhiteSpace(argsString))
                {
                    step.Arguments.AddRange(ParseArguments(argsString));
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

            // Special handling for start steps
            if (stepName == "v" || stepName == "e" || stepName == "addv" || stepName == "adde")
            {
                step.IsStartStep = true;
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
        /// Split arguments by comma while preserving quoted strings
        /// </summary>
        private List<string> SplitArguments(string argsString)
        {
            var parts = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';
            var parenLevel = 0;

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
                else if (!inQuotes && c == ',' && parenLevel == 0)
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

            // Handle quoted strings
            if ((arg.StartsWith("'") && arg.EndsWith("'")) || 
                (arg.StartsWith("\"") && arg.EndsWith("\"")))
            {
                return arg.Substring(1, arg.Length - 2);
            }

            // Handle numbers
            if (int.TryParse(arg, out int intVal))
                return intVal;
            if (long.TryParse(arg, out long longVal))
                return longVal;
            if (double.TryParse(arg, out double doubleVal))
                return doubleVal;

            // Handle booleans
            if (bool.TryParse(arg, out bool boolVal))
                return boolVal;

            // Handle null
            if (arg.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

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
            foreach (var param in parameters)
            {
                var value = FormatParameterValue(param.Value);
                result = result.Replace(param.Key, value);
            }
            return result;
        }

        /// <summary>
        /// Format a parameter value for substitution
        /// </summary>
        private string FormatParameterValue(object value)
        {
            if (value == null) return "null";
            if (value is string) return $"'{value}'";
            if (value is bool) return value.ToString().ToLower();
            return value.ToString();
        }

        #endregion
    }
}