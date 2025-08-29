using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Advanced Gremlin query executor supporting CosmosDB-compatible steps
    /// Inspired by Apache TinkerPop's TinkerGraph implementation
    /// </summary>
    public class AdvancedGremlinQueryExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        // CosmosDB supported steps - expanded based on TinkerPop reference
        private static readonly HashSet<string> SupportedSteps = new HashSet<string>
        {
            // Graph traversal
            "V", "E", "in", "out", "both", "inE", "outE", "bothE", "inV", "outV", "bothV", "otherV",
            
            // Vertex/Edge operations
            "addV", "addE", "property", "properties", "values", "valueMap", "elementMap", "drop",
            
            // Filtering
            "has", "hasLabel", "hasId", "hasKey", "hasValue", "hasNot", "where", "is", "not",
            "and", "or", "filter", "dedup", "unique",
            
            // Projection
            "select", "project", "by", "as", "path", "simplePath", "cyclicPath",
            
            // Ordering and limiting
            "order", "range", "limit", "skip", "tail", "sample", "coin",
            
            // Aggregation
            "count", "sum", "max", "min", "mean", "fold", "unfold", "group", "groupCount",
            
            // Conditional
            "choose", "union", "coalesce", "optional", "constant", "identity",
            
            // Loops
            "repeat", "until", "emit", "times", "local",
            
            // Modifiers
            "to", "from", "asc", "desc", "shuffle", "cap", "barrier",
            
            // Predicates
            "eq", "neq", "lt", "lte", "gt", "gte", "within", "without",
            "containing", "notContaining", "startingWith", "endingWith",
            "inside", "outside", "between"
        };

        public AdvancedGremlinQueryExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Execute a parsed Gremlin query with TinkerGraph-style traversal context management
        /// </summary>
        public IEnumerable<dynamic> Execute(ParsedGremlinQuery query)
        {
            // Use the new TinkerGraph-style traversal context
            var context = new TinkerTraversalContext();
            
            // Execute each step in sequence
            foreach (var step in query.Steps)
            {
                context = ExecuteStepWithTinkerContext(step, context, query.Parameters);
                
                // If no traversers at any point, short-circuit unless it's a terminal step
                if (!context.HasTraversers)
                {
                    if (IsTerminalStep(step.StepName))
                    {
                        // Terminal steps can return empty results
                        break;
                    }
                    else if (!IsOptionalStep(step.StepName))
                    {
                        // Non-optional steps that return no results terminate the traversal
                        return new List<dynamic>();
                    }
                }
            }
            
            return context.GetCurrentResults();
        }

        /// <summary>
        /// Execute using legacy TraversalContext for backwards compatibility
        /// </summary>
        public IEnumerable<dynamic> ExecuteLegacy(ParsedGremlinQuery query)
        {
            // Initialize legacy traversal context
            var context = new TraversalContext();
            
            // Execute each step in sequence
            foreach (var step in query.Steps)
            {
                context = ExecuteStep(step, context, query.Parameters);
                
                // If no results at any point, short-circuit
                if (context.CurrentResults == null || !context.CurrentResults.Any())
                {
                    if (IsTerminalStep(step.StepName))
                    {
                        // Terminal steps can return empty results
                        break;
                    }
                    else if (!IsOptionalStep(step.StepName))
                    {
                        // Non-optional steps that return no results terminate the traversal
                        return new List<dynamic>();
                    }
                }
            }
            
            return context.CurrentResults ?? new List<dynamic>();
        }

        private bool IsTerminalStep(string stepName)
        {
            return new[] { "count", "sum", "max", "min", "mean", "fold" }.Contains(stepName.ToLower());
        }

        private bool IsOptionalStep(string stepName)
        {
            return new[] { "optional", "coalesce" }.Contains(stepName.ToLower());
        }

        /// <summary>
        /// Execute a single step with TinkerGraph-style context management
        /// </summary>
        private TinkerTraversalContext ExecuteStepWithTinkerContext(GremlinStep step, TinkerTraversalContext context, Dictionary<string, object> parameters)
        {
            switch (step.StepName.ToLower())
            {
                case "v":
                    return ExecuteVWithTinkerContext(step, context);
                case "e":
                    return ExecuteEWithTinkerContext(step, context);
                case "addv":
                    return ExecuteAddVWithTinkerContext(step, context);
                case "adde":
                    return ExecuteAddEWithTinkerContext(step, context);
                case "out":
                    return ExecuteOutWithTinkerContext(step, context);
                case "in":
                    return ExecuteInWithTinkerContext(step, context);
                case "both":
                    return ExecuteBothWithTinkerContext(step, context);
                case "oute":
                    return ExecuteOutEWithTinkerContext(step, context);
                case "ine":
                    return ExecuteInEWithTinkerContext(step, context);
                case "bothe":
                    return ExecuteBothEWithTinkerContext(step, context);
                case "inv":
                    return ExecuteInVWithTinkerContext(step, context);
                case "outv":
                    return ExecuteOutVWithTinkerContext(step, context);
                case "bothv":
                    return ExecuteBothVWithTinkerContext(step, context);
                case "otherv":
                    return ExecuteOtherVWithTinkerContext(step, context);
                case "has":
                    return ExecuteHasWithTinkerContext(step, context);
                case "haslabel":
                    return ExecuteHasLabelWithTinkerContext(step, context);
                case "hasid":
                    return ExecuteHasIdWithTinkerContext(step, context);
                case "where":
                    return ExecuteWhereWithTinkerContext(step, context, parameters);
                case "filter":
                    return ExecuteFilterWithTinkerContext(step, context);
                case "dedup":
                    return ExecuteDedupWithTinkerContext(step, context);
                case "limit":
                    return ExecuteLimitWithTinkerContext(step, context);
                case "skip":
                    return ExecuteSkipWithTinkerContext(step, context);
                case "range":
                    return ExecuteRangeWithTinkerContext(step, context);
                case "order":
                    return ExecuteOrderWithTinkerContext(step, context);
                case "sample":
                    return ExecuteSampleWithTinkerContext(step, context);
                case "tail":
                    return ExecuteTailWithTinkerContext(step, context);
                case "count":
                    return ExecuteCountWithTinkerContext(step, context);
                case "sum":
                    return ExecuteSumWithTinkerContext(step, context);
                case "values":
                    return ExecuteValuesWithTinkerContext(step, context);
                case "valuemap":
                    return ExecuteValueMapWithTinkerContext(step, context);
                case "property":
                    return ExecutePropertyWithTinkerContext(step, context);
                case "as":
                    return ExecuteAsWithTinkerContext(step, context);
                default:
                    // Return current context for unsupported steps
                    return context;
            }
        }

        #region TinkerGraph-style Step Implementations

        private TinkerTraversalContext ExecuteVWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            var newContext = new TinkerTraversalContext
            {
                Variables = context.Variables,
                SideEffects = context.SideEffects
            };

            if (step.Arguments.Count == 0)
            {
                // g.V() - get all vertices
                var vertices = _database.GetAllVertices().Select(v => v.ToGremlinResponse());
                newContext.Traversers = vertices.Select(v => new Traverser(v)).ToList();
            }
            else
            {
                // g.V(id1, id2, ...) - get specific vertices
                var traversers = new List<Traverser>();
                foreach (var arg in step.Arguments)
                {
                    var vertex = _database.GetVertex(arg.ToString());
                    if (vertex != null)
                    {
                        traversers.Add(new Traverser(vertex.ToGremlinResponse()));
                    }
                }
                newContext.Traversers = traversers;
            }

            return newContext;
        }

        private TinkerTraversalContext ExecuteEWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            var newContext = new TinkerTraversalContext
            {
                Variables = context.Variables,
                SideEffects = context.SideEffects
            };

            if (step.Arguments.Count == 0)
            {
                // g.E() - get all edges
                var edges = _database.GetAllEdges().Select(e => e.ToGremlinResponse());
                newContext.Traversers = edges.Select(e => new Traverser(e)).ToList();
            }
            else
            {
                // g.E(id1, id2, ...) - get specific edges
                var traversers = new List<Traverser>();
                foreach (var arg in step.Arguments)
                {
                    var edge = _database.GetEdge(arg.ToString());
                    if (edge != null)
                    {
                        traversers.Add(new Traverser(edge.ToGremlinResponse()));
                    }
                }
                newContext.Traversers = traversers;
            }

            return newContext;
        }

        private TinkerTraversalContext ExecuteOutWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var newContext = new TinkerTraversalContext
            {
                Variables = context.Variables,
                SideEffects = context.SideEffects
            };

            var traversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var item = traverser.Value;
                if (item.type == "vertex")
                {
                    var outVertices = _database.GetOutVertices(item.id.ToString(), edgeLabel);
                    foreach (var vertex in outVertices)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = vertex.ToGremlinResponse();
                        newTraverser.AddToPath(item);
                        traversers.Add(newTraverser);
                    }
                }
            }

            newContext.Traversers = traversers;
            return newContext;
        }

        private TinkerTraversalContext ExecuteInWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var newContext = new TinkerTraversalContext
            {
                Variables = context.Variables,
                SideEffects = context.SideEffects
            };

            var traversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var item = traverser.Value;
                if (item.type == "vertex")
                {
                    var inVertices = _database.GetInVertices(item.id.ToString(), edgeLabel);
                    foreach (var vertex in inVertices)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = vertex.ToGremlinResponse();
                        newTraverser.AddToPath(item);
                        traversers.Add(newTraverser);
                    }
                }
            }

            newContext.Traversers = traversers;
            return newContext;
        }

        private TinkerTraversalContext ExecuteHasWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count < 2)
                return context;

            var key = step.Arguments[0].ToString();
            var value = step.Arguments[1];

            context.Filter(traverser =>
            {
                var item = traverser.Value;
                return HasProperty(item, key, value);
            });

            return context;
        }

        private TinkerTraversalContext ExecuteHasLabelWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var label = step.Arguments[0].ToString();

            context.Filter(traverser =>
            {
                var item = traverser.Value;
                return item.label?.ToString() == label;
            });

            return context;
        }

        private TinkerTraversalContext ExecuteHasIdWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var ids = step.Arguments.Select(arg => arg.ToString()).ToHashSet();

            context.Filter(traverser =>
            {
                var item = traverser.Value;
                var itemId = item.id?.ToString();
                return itemId != null && ids.Contains(itemId);
            });

            return context;
        }

        private TinkerTraversalContext ExecuteDedupWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            context.Dedup(traverser => traverser.Value?.id?.ToString() ?? traverser.Value?.ToString());
            return context;
        }

        private TinkerTraversalContext ExecuteLimitWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var limit = Convert.ToInt64(step.Arguments[0]);
            context.Limit(limit);
            return context;
        }

        private TinkerTraversalContext ExecuteSkipWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var skip = Convert.ToInt64(step.Arguments[0]);
            context.Skip(skip);
            return context;
        }

        private TinkerTraversalContext ExecuteRangeWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count < 2)
                return context;

            var low = Convert.ToInt64(step.Arguments[0]);
            var high = Convert.ToInt64(step.Arguments[1]);
            context.Range(low, high);
            return context;
        }

        private TinkerTraversalContext ExecuteSampleWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var amountToSample = Convert.ToInt32(step.Arguments[0]);
            context.Sample(amountToSample);
            return context;
        }

        private TinkerTraversalContext ExecuteCountWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            var count = context.Count;
            var newContext = new TinkerTraversalContext
            {
                Variables = context.Variables,
                SideEffects = context.SideEffects
            };
            newContext.Traversers.Add(new Traverser(count));
            return newContext;
        }

        private TinkerTraversalContext ExecuteAsWithTinkerContext(GremlinStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count > 0)
            {
                var label = step.Arguments[0].ToString();
                context.AddStepLabel(label);
            }
            return context;
        }

        // Placeholder implementations for other TinkerGraph-style methods
        private TinkerTraversalContext ExecuteAddVWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteAddEWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteBothWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteOutEWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteInEWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteBothEWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteInVWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteOutVWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteBothVWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteOtherVWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteWhereWithTinkerContext(GremlinStep step, TinkerTraversalContext context, Dictionary<string, object> parameters) => context;
        private TinkerTraversalContext ExecuteFilterWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteOrderWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteTailWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteSumWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteValuesWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecuteValueMapWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;
        private TinkerTraversalContext ExecutePropertyWithTinkerContext(GremlinStep step, TinkerTraversalContext context) => context;

        #endregion

        #region Legacy Step Implementations

        /// <summary>
        /// Execute a single step with legacy context management
        /// </summary>
        private TraversalContext ExecuteStep(GremlinStep step, TraversalContext context, Dictionary<string, object> parameters)
        {
            switch (step.StepName.ToLower())
            {
                case "v":
                    return ExecuteV(step, context);
                case "e":
                    return ExecuteE(step, context);
                case "addv":
                    return ExecuteAddV(step, context);
                case "adde":
                    return ExecuteAddE(step, context);
                case "out":
                    return ExecuteOut(step, context);
                case "in":
                    return ExecuteIn(step, context);
                case "both":
                    return ExecuteBoth(step, context);
                case "oute":
                    return ExecuteOutE(step, context);
                case "ine":
                    return ExecuteInE(step, context);
                case "bothe":
                    return ExecuteBothE(step, context);
                case "inv":
                    return ExecuteInV(step, context);
                case "outv":
                    return ExecuteOutV(step, context);
                case "bothv":
                    return ExecuteBothV(step, context);
                case "otherv":
                    return ExecuteOtherV(step, context);
                case "has":
                    return ExecuteHas(step, context);
                case "haslabel":
                    return ExecuteHasLabel(step, context);
                case "hasid":
                    return ExecuteHasId(step, context);
                case "where":
                    return ExecuteWhere(step, context, parameters);
                case "filter":
                    return ExecuteFilter(step, context);
                case "dedup":
                    return ExecuteDedup(step, context);
                case "limit":
                    return ExecuteLimit(step, context);
                case "skip":
                    return ExecuteSkip(step, context);
                case "range":
                    return ExecuteRange(step, context);
                case "order":
                    return ExecuteOrder(step, context);
                case "sample":
                    return ExecuteSample(step, context);
                case "tail":
                    return ExecuteTail(step, context);
                case "count":
                    return ExecuteCount(step, context);
                case "sum":
                    return ExecuteSum(step, context);
                case "values":
                    return ExecuteValues(step, context);
                case "valuemap":
                    return ExecuteValueMap(step, context);
                case "property":
                    return ExecuteProperty(step, context);
                default:
                    // Return current context for unsupported steps
                    return context;
            }
        }

        private TraversalContext ExecuteWhere(GremlinStep step, TraversalContext context, Dictionary<string, object> parameters)
        {
            if (step.Arguments.Count == 0)
                return context;

            var predicate = step.Arguments[0].ToString();
            
            // Handle nested traversal patterns like __.otherV().hasId('id')
            if (predicate.Contains("otherv") && predicate.Contains("hasid"))
            {
                return ExecuteWhereOtherVHasId(predicate, context, parameters);
            }
            
            // Handle other where patterns
            var filteredElements = new List<dynamic>();
            
            foreach (var element in context.CurrentResults)
            {
                if (EvaluatePredicate(predicate, element, parameters))
                {
                    filteredElements.Add(element);
                }
            }
            
            return new TraversalContext 
            { 
                CurrentResults = filteredElements,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteWhereOtherVHasId(string predicate, TraversalContext context, Dictionary<string, object> parameters)
        {
            // Extract the target ID from patterns like __.otherV().hasId('id')
            var targetId = ExtractIdFromPredicate(predicate, parameters);
            
            if (string.IsNullOrEmpty(targetId))
                return new TraversalContext { Variables = context.Variables };

            var filteredElements = new List<dynamic>();

            foreach (var element in context.CurrentResults)
            {
                if (element.type == "edge")
                {
                    // For edges, check if either vertex matches the target ID
                    var inVId = element.inV?.ToString();
                    var outVId = element.outV?.ToString();
                    
                    if (inVId == targetId || outVId == targetId)
                    {
                        filteredElements.Add(element);
                    }
                }
                else if (element.type == "vertex")
                {
                    // For vertices in a where clause, this is more complex
                    // For now, check if the vertex ID matches
                    var vertexId = element.id?.ToString();
                    if (vertexId == targetId)
                    {
                        filteredElements.Add(element);
                    }
                }
            }

            return new TraversalContext
            {
                CurrentResults = filteredElements,
                Variables = context.Variables,
                Aggregates = context.Aggregates,
                Paths = context.Paths
            };
        }

        private string ExtractIdFromPredicate(string predicate, Dictionary<string, object> parameters)
        {
            // Try to extract ID from various patterns
            var patterns = new[]
            {
                @"hasid\s*\(\s*['""]([^'""]*)['""]?\s*\)",  // hasId('id')
                @"hasid\s*\(\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\)", // hasId(paramName)
                @"\.hasid\s*\(\s*['""]([^'""]*)['""]?\s*\)", // .hasId('id')
                @"\.hasid\s*\(\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\)" // .hasId(paramName)
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(predicate, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var value = match.Groups[1].Value;
                    
                    // Check if it's a parameter name
                    if (parameters != null && parameters.ContainsKey(value))
                    {
                        return parameters[value]?.ToString();
                    }
                    
                    return value;
                }
            }

            return null;
        }

        private bool EvaluatePredicate(string predicate, dynamic element, Dictionary<string, object> parameters)
        {
            // Simple predicate evaluation - can be expanded
            return true; // For now, pass all elements that reach here
        }

        private TraversalContext ExecuteOtherV(GremlinStep step, TraversalContext context)
        {
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "edge")
                {
                    // For otherV(), we need to determine the "other" vertex
                    // Since we don't have perfect context tracking, we'll return both vertices
                    // The where clause will filter it correctly
                    var inVertex = _database.GetVertex(item.inV.ToString());
                    var outVertex = _database.GetVertex(item.outV.ToString());
                    
                    if (inVertex != null) results.Add(inVertex.ToGremlinResponse());
                    if (outVertex != null) results.Add(outVertex.ToGremlinResponse());
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results.Distinct(),
                Variables = context.Variables
            };
        }

        #endregion

        #region Graph Traversal Start Steps

        private TraversalContext ExecuteV(GremlinStep step, TraversalContext context)
        {
            var newContext = new TraversalContext { Variables = context.Variables };

            if (step.Arguments.Count == 0)
            {
                // g.V() - get all vertices
                newContext.CurrentResults = _database.GetAllVertices().Select(v => v.ToGremlinResponse());
            }
            else
            {
                // g.V(id1, id2, ...) - get specific vertices
                var results = new List<dynamic>();
                foreach (var arg in step.Arguments)
                {
                    var vertex = _database.GetVertex(arg.ToString());
                    if (vertex != null)
                    {
                        results.Add(vertex.ToGremlinResponse());
                    }
                }
                newContext.CurrentResults = results;
            }

            return newContext;
        }

        private TraversalContext ExecuteE(GremlinStep step, TraversalContext context)
        {
            var newContext = new TraversalContext { Variables = context.Variables };

            if (step.Arguments.Count == 0)
            {
                // g.E() - get all edges
                newContext.CurrentResults = _database.GetAllEdges().Select(e => e.ToGremlinResponse());
            }
            else
            {
                // g.E(id1, id2, ...) - get specific edges
                var results = new List<dynamic>();
                foreach (var arg in step.Arguments)
                {
                    var edge = _database.GetEdge(arg.ToString());
                    if (edge != null)
                    {
                        results.Add(edge.ToGremlinResponse());
                    }
                }
                newContext.CurrentResults = results;
            }

            return newContext;
        }

        #endregion

        #region Vertex/Edge Creation Steps

        private TraversalContext ExecuteAddV(GremlinStep step, TraversalContext context)
        {
            var label = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : "vertex";
            var vertex = _database.AddVertex(label);
            
            return new TraversalContext 
            { 
                CurrentResults = new[] { vertex.ToGremlinResponse() },
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteAddE(GremlinStep step, TraversalContext context)
        {
            var label = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : "edge";
            
            // For addE, we need to track this as a pending edge that needs to() call
            // For now, return a placeholder
            return new TraversalContext 
            { 
                CurrentResults = new[] { new { label = label, type = "pendingEdge" } },
                Variables = context.Variables
            };
        }

        #endregion

        #region Navigation Steps

        private TraversalContext ExecuteOut(GremlinStep step, TraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "vertex")
                {
                    var outVertices = _database.GetOutVertices(item.id.ToString(), edgeLabel);
                    foreach (var vertex in outVertices)
                    {
                        results.Add(vertex.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteIn(GremlinStep step, TraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "vertex")
                {
                    var inVertices = _database.GetInVertices(item.id.ToString(), edgeLabel);
                    foreach (var vertex in inVertices)
                    {
                        results.Add(vertex.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteBoth(GremlinStep step, TraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "vertex")
                {
                    var outVertices = _database.GetOutVertices(item.id.ToString(), edgeLabel);
                    var inVertices = _database.GetInVertices(item.id.ToString(), edgeLabel);
                    
                    foreach (var vertex in outVertices)
                    {
                        results.Add(vertex.ToGremlinResponse());
                    }
                    foreach (var vertex in inVertices)
                    {
                        results.Add(vertex.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results.Distinct(),
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteOutE(GremlinStep step, TraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "vertex")
                {
                    var outEdges = _database.GetOutEdges(item.id.ToString(), edgeLabel);
                    foreach (var edge in outEdges)
                    {
                        results.Add(edge.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteInE(GremlinStep step, TraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "vertex")
                {
                    var inEdges = _database.GetInEdges(item.id.ToString(), edgeLabel);
                    foreach (var edge in inEdges)
                    {
                        results.Add(edge.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteBothE(GremlinStep step, TraversalContext context)
        {
            var edgeLabel = step.Arguments.Count > 0 ? step.Arguments[0].ToString() : null;
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "vertex")
                {
                    var outEdges = _database.GetOutEdges(item.id.ToString(), edgeLabel);
                    var inEdges = _database.GetInEdges(item.id.ToString(), edgeLabel);
                    
                    foreach (var edge in outEdges)
                    {
                        results.Add(edge.ToGremlinResponse());
                    }
                    foreach (var edge in inEdges)
                    {
                        results.Add(edge.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results.Distinct(),
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteInV(GremlinStep step, TraversalContext context)
        {
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "edge")
                {
                    var vertex = _database.GetVertex(item.inV.ToString());
                    if (vertex != null)
                    {
                        results.Add(vertex.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteOutV(GremlinStep step, TraversalContext context)
        {
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "edge")
                {
                    var vertex = _database.GetVertex(item.outV.ToString());
                    if (vertex != null)
                    {
                        results.Add(vertex.ToGremlinResponse());
                    }
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteBothV(GremlinStep step, TraversalContext context)
        {
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "edge")
                {
                    var inVertex = _database.GetVertex(item.inV.ToString());
                    var outVertex = _database.GetVertex(item.outV.ToString());
                    
                    if (inVertex != null) results.Add(inVertex.ToGremlinResponse());
                    if (outVertex != null) results.Add(outVertex.ToGremlinResponse());
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results.Distinct(),
                Variables = context.Variables
            };
        }

        #endregion

        #region Filtering Steps

        private TraversalContext ExecuteHas(GremlinStep step, TraversalContext context)
        {
            if (step.Arguments.Count < 2)
                return context;

            var key = step.Arguments[0].ToString();
            var value = step.Arguments[1];
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (HasProperty(item, key, value))
                {
                    results.Add(item);
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteHasLabel(GremlinStep step, TraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var label = step.Arguments[0].ToString();
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.label?.ToString() == label)
                {
                    results.Add(item);
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteHasId(GremlinStep step, TraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var ids = step.Arguments.Select(arg => arg.ToString()).ToHashSet();
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                var itemId = item.id?.ToString();
                if (itemId != null && ids.Contains(itemId))
                {
                    results.Add(item);
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteIs(GremlinStep step, TraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var value = step.Arguments[0];
            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.Equals(value) || item.ToString() == value.ToString())
                {
                    results.Add(item);
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteNot(GremlinStep step, TraversalContext context)
        {
            // Simplified not implementation - would need to parse nested traversal
            return context;
        }

        private TraversalContext ExecuteFilter(GremlinStep step, TraversalContext context)
        {
            // Simplified filter implementation
            return context;
        }

        private TraversalContext ExecuteDedup(GremlinStep step, TraversalContext context)
        {
            var results = context.CurrentResults.Distinct().ToList();
            
            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        #endregion

        #region Property Steps

        private TraversalContext ExecuteProperty(GremlinStep step, TraversalContext context)
        {
            if (step.Arguments.Count < 2)
                return context;

            var key = step.Arguments[0].ToString();
            var value = step.Arguments[1];

            var results = new List<dynamic>();

            foreach (var item in context.CurrentResults)
            {
                if (item.type == "vertex" && item.id != null)
                {
                    var vertex = _database.GetVertex(item.id.ToString());
                    if (vertex != null)
                    {
                        vertex.SetProperty(key, value);
                        results.Add(vertex.ToGremlinResponse());
                    }
                }
                else if (item.type == "edge" && item.id != null)
                {
                    var edge = _database.GetEdge(item.id.ToString());
                    if (edge != null)
                    {
                        edge.SetProperty(key, value);
                        results.Add(edge.ToGremlinResponse());
                    }
                }
                else
                {
                    // Pass through items that don't support properties
                    results.Add(item);
                }
            }

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        // Simplified implementations for other methods
        private TraversalContext ExecuteProperties(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteValues(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteValueMap(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteElementMap(GremlinStep step, TraversalContext context) => context;

        #endregion

        #region Aggregation Steps

        private TraversalContext ExecuteCount(GremlinStep step, TraversalContext context)
        {
            var count = context.CurrentResults.Count();
            
            return new TraversalContext 
            { 
                CurrentResults = new dynamic[] { (long)count },
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteSum(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteMax(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteMin(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteMean(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteFold(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteUnfold(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteGroup(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteGroupCount(GremlinStep step, TraversalContext context) => context;

        #endregion

        #region Ordering and Limiting Steps

        private TraversalContext ExecuteOrder(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteBy(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteRange(GremlinStep step, TraversalContext context) => context;

        private TraversalContext ExecuteLimit(GremlinStep step, TraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return context;

            var limit = Convert.ToInt32(step.Arguments[0]);
            var results = context.CurrentResults.Take(limit).ToList();

            return new TraversalContext 
            { 
                CurrentResults = results,
                Variables = context.Variables
            };
        }

        private TraversalContext ExecuteSkip(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteTail(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteSample(GremlinStep step, TraversalContext context) => context;

        #endregion

        #region Conditional Steps

        private TraversalContext ExecuteChoose(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteUnion(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteCoalesce(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteOptional(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteConstant(GremlinStep step, TraversalContext context) => context;

        #endregion

        #region Utility Steps

        private TraversalContext ExecuteDrop(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteIdentity(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteSelect(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteProject(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecuteAs(GremlinStep step, TraversalContext context) => context;
        private TraversalContext ExecutePath(GremlinStep step, TraversalContext context) => context;

        #endregion

        #region Helper Methods

        private bool HasProperty(dynamic item, string key, object value)
        {
            try
            {
                if (item.properties != null)
                {
                    var properties = item.properties;
                    if (properties is Dictionary<string, object> propDict)
                    {
                        if (!propDict.ContainsKey(key))
                            return false;

                        var propValue = propDict[key];
                        return propValue?.ToString().Equals(value?.ToString(), StringComparison.OrdinalIgnoreCase) == true;
                    }
                }
            }
            catch
            {
                // Ignore errors in property access
            }

            return false;
        }

        #endregion
    }
}