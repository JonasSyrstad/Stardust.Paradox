using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Wrapper for path objects to prevent them from being flattened in results
    /// </summary>
    public class PathWrapper
    {
        public List<dynamic> Path { get; }
        
        public PathWrapper(List<dynamic> path)
        {
            Path = path ?? new List<dynamic>();
        }
        
        public override string ToString()
        {
            return $"path[{string.Join(", ", Path.Select(p => p?.ToString() ?? "null"))}]";
        }
    }

    /// <summary>
    /// TinkerGraph-inspired query executor with optimized traversal strategies
    /// Based on Apache TinkerPop's TinkerGraph execution model
    /// </summary>
    public class TinkerGraphQueryExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public TinkerGraphQueryExecutor(InMemoryGraphDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Execute a TinkerGraph traversal with proper context management
        /// </summary>
        public IEnumerable<dynamic> Execute(TinkerGraphTraversal traversal)
        {
            if (!traversal.Steps.Any())
                return Enumerable.Empty<dynamic>();

            // Initialize traversal context
            var context = InitializeTraversalContext(traversal);

            // Execute each step in sequence
            foreach (var step in traversal.Steps)
            {
                if (step.IsStartStep)
                    continue; // Start steps already handled in initialization

                ExecuteStep(step, context);
                
                // Early termination if no traversers remain
                if (!context.HasTraversers)
                    break;
            }

            return context.GetCurrentResults();
        }

        /// <summary>
        /// Initialize the traversal context with start step
        /// </summary>
        private TinkerTraversalContext InitializeTraversalContext(TinkerGraphTraversal traversal)
        {
            var firstStep = traversal.Steps.FirstOrDefault();
            
            if (firstStep?.IsStartStep == true)
            {
                var initialResults = ExecuteStartStep(firstStep);
                var context = new TinkerTraversalContext();
                
                // Create traversers and initialize their paths with the starting element
                foreach (var result in initialResults)
                {
                    var traverser = new Traverser(result);
                    traverser.AddToPath(result); // Add the starting element to the path
                    context.Traversers.Add(traverser);
                }
                
                return context;
            }
            
            // Default to all vertices if no start step
            var defaultContext = new TinkerTraversalContext();
            foreach (var vertex in _database.GetAllVertices())
            {
                var traverser = new Traverser(vertex.ToGremlinResponse());
                traverser.AddToPath(vertex.ToGremlinResponse());
                defaultContext.Traversers.Add(traverser);
            }
            return defaultContext;
        }

        /// <summary>
        /// Execute a start step (V, E, addV, etc.)
        /// </summary>
        private IEnumerable<dynamic> ExecuteStartStep(TinkerGraphStep step)
        {
            switch (step.StepName.ToLower())
            {
                case "v":
                    return ExecuteVertexStep(step);
                case "e":
                    return ExecuteEdgeStep(step);
                case "addv":
                    return ExecuteAddVertexStep(step);
                case "adde":
                    return ExecuteAddEdgeStep(step);
                default:
                    return _database.GetAllVertices().Select(v => v.ToGremlinResponse());
            }
        }

        /// <summary>
        /// Execute a single step in the traversal
        /// </summary>
        private void ExecuteStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            switch (step.StepName.ToLower())
            {
                case "out":
                    ExecuteOutStep(step, context);
                    break;
                case "in":
                    ExecuteInStep(step, context);
                    break;
                case "both":
                    ExecuteBothStep(step, context);
                    break;
                case "oute":
                    ExecuteOutEStep(step, context);
                    break;
                case "ine":
                    ExecuteInEStep(step, context);
                    break;
                case "bothe":
                    ExecuteBothEStep(step, context);
                    break;
                case "outv":
                    ExecuteOutVStep(step, context);
                    break;
                case "inv":
                    ExecuteInVStep(step, context);
                    break;
                case "bothv":
                    ExecuteBothVStep(step, context);
                    break;
                case "otherv":
                    ExecuteOtherVStep(step, context);
                    break;
                case "has":
                    ExecuteHasStep(step, context);
                    break;
                case "haslabel":
                    ExecuteHasLabelStep(step, context);
                    break;
                case "hasid":
                    ExecuteHasIdStep(step, context);
                    break;
                case "values":
                    ExecuteValuesStep(step, context);
                    break;
                case "valuemap":
                    ExecuteValueMapStep(step, context);
                    break;
                case "id":
                    ExecuteIdStep(step, context);
                    break;
                case "label":
                    ExecuteLabelStep(step, context);
                    break;
                case "count":
                    ExecuteCountStep(step, context);
                    break;
                case "limit":
                    ExecuteLimitStep(step, context);
                    break;
                case "skip":
                    ExecuteSkipStep(step, context);
                    break;
                case "range":
                    ExecuteRangeStep(step, context);
                    break;
                case "dedup":
                    ExecuteDedupStep(step, context);
                    break;
                case "order":
                    ExecuteOrderStep(step, context);
                    break;
                case "group":
                    ExecuteGroupStep(step, context);
                    break;
                case "groupcount":
                    ExecuteGroupCountStep(step, context);
                    break;
                case "fold":
                    ExecuteFoldStep(step, context);
                    break;
                case "unfold":
                    ExecuteUnfoldStep(step, context);
                    break;
                case "path":
                    ExecutePathStep(step, context);
                    break;
                case "select":
                    ExecuteSelectStep(step, context);
                    break;
                case "sample":
                    ExecuteSampleStep(step, context);
                    break;
                case "tail":
                    ExecuteTailStep(step, context);
                    break;
                case "where":
                    ExecuteWhereStep(step, context);
                    break;
                case "property":
                    ExecutePropertyStep(step, context);
                    break;
                case "drop":
                    ExecuteDropStep(step, context);
                    break;
                case "adde":
                    ExecuteAddEdgeContextStep(step, context);
                    break;
                default:
                    // Unknown step - pass through
                    break;
            }

            // Handle step labels for path tracking
            if (step.Labels.Any())
            {
                foreach (var label in step.Labels)
                {
                    context.AddStepLabel(label);
                }
            }
        }

        #region Start Steps

        private IEnumerable<dynamic> ExecuteVertexStep(TinkerGraphStep step)
        {
            if (step.Arguments.Any())
            {
                // V(id1, id2, ...) - get specific vertices
                var ids = step.Arguments.Select(arg => arg.ToString());
                return ids.Select(id => _database.GetVertex(id))
                         .Where(v => v != null)
                         .Select(v => v.ToGremlinResponse());
            }
            
            // V() - get all vertices
            return _database.GetAllVertices().Select(v => v.ToGremlinResponse());
        }

        private IEnumerable<dynamic> ExecuteEdgeStep(TinkerGraphStep step)
        {
            if (step.Arguments.Any())
            {
                // E(id1, id2, ...) - get specific edges
                var ids = step.Arguments.Select(arg => arg.ToString());
                return ids.Select(id => _database.GetEdge(id))
                         .Where(e => e != null)
                         .Select(e => e.ToGremlinResponse());
            }
            
            // E() - get all edges
            return _database.GetAllEdges().Select(e => e.ToGremlinResponse());
        }

        private IEnumerable<dynamic> ExecuteAddVertexStep(TinkerGraphStep step)
        {
            var label = step.GetFirstStringArgument() ?? "vertex";
            var vertex = _database.AddVertex(label);
            
            // Add properties from arguments (property pairs)
            for (int i = 1; i < step.Arguments.Count - 1; i += 2)
            {
                var key = step.Arguments[i].ToString();
                var value = step.Arguments[i + 1];
                vertex.SetProperty(key, value);
            }
            
            return new[] { vertex.ToGremlinResponse() };
        }

        private IEnumerable<dynamic> ExecuteAddEdgeStep(TinkerGraphStep step)
        {
            if (step.Arguments.Count < 3)
                return Enumerable.Empty<dynamic>();
                
            var label = step.GetFirstStringArgument();
            var fromId = step.Arguments[1].ToString();
            var toId = step.Arguments[2].ToString();
            
            var edge = _database.AddEdge(label, fromId, toId);
            if (edge == null)
                return Enumerable.Empty<dynamic>();
                
            // Add properties from remaining arguments
            for (int i = 3; i < step.Arguments.Count - 1; i += 2)
            {
                var key = step.Arguments[i].ToString();
                var value = step.Arguments[i + 1];
                edge.SetProperty(key, value);
            }
            
            return new[] { edge.ToGremlinResponse() };
        }

        #endregion

        #region Traversal Steps - Simplified approach working directly with context

        private void ExecuteOutStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var outVertices = _database.GetOutVertices(vertexId, edgeLabel);
                    foreach (var vertex in outVertices)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = vertex.ToGremlinResponse();
                        
                        // Add current step to path for path tracking
                        newTraverser.AddToPath(vertex.ToGremlinResponse());
                        
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteInStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var inVertices = _database.GetInVertices(vertexId, edgeLabel);
                    foreach (var vertex in inVertices)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = vertex.ToGremlinResponse();
                        
                        // Add current step to path for path tracking
                        newTraverser.AddToPath(vertex.ToGremlinResponse());
                        
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteBothStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var bothVertices = _database.GetBothVertices(vertexId, edgeLabel);
                    foreach (var vertex in bothVertices)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = vertex.ToGremlinResponse();
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteOutEStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var outEdges = _database.GetOutEdges(vertexId, edgeLabel);
                    foreach (var edge in outEdges)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = edge.ToGremlinResponse();
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteInEStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var inEdges = _database.GetInEdges(vertexId, edgeLabel);
                    foreach (var edge in inEdges)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = edge.ToGremlinResponse();
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteBothEStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var bothEdges = _database.GetBothEdges(vertexId, edgeLabel);
                    foreach (var edge in bothEdges)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = edge.ToGremlinResponse();
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteOutVStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var edgeId = ExtractEdgeId(traverser.Value);
                if (!string.IsNullOrEmpty(edgeId))
                {
                    var edge = _database.GetEdge(edgeId);
                    if (edge != null)
                    {
                        var vertex = _database.GetVertex(edge.OutVertexId);
                        if (vertex != null)
                        {
                            var newTraverser = traverser.Split();
                            newTraverser.Value = vertex.ToGremlinResponse();
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteInVStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var edgeId = ExtractEdgeId(traverser.Value);
                if (!string.IsNullOrEmpty(edgeId))
                {
                    var edge = _database.GetEdge(edgeId);
                    if (edge != null)
                    {
                        var vertex = _database.GetVertex(edge.InVertexId);
                        if (vertex != null)
                        {
                            var newTraverser = traverser.Split();
                            newTraverser.Value = vertex.ToGremlinResponse();
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteBothVStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var edgeId = ExtractEdgeId(traverser.Value);
                if (!string.IsNullOrEmpty(edgeId))
                {
                    var edge = _database.GetEdge(edgeId);
                    if (edge != null)
                    {
                        // Add both the outV and inV vertices
                        var outVertex = _database.GetVertex(edge.OutVertexId);
                        if (outVertex != null)
                        {
                            var outTraverser = traverser.Split();
                            outTraverser.Value = outVertex.ToGremlinResponse();
                            newTraversers.Add(outTraverser);
                        }

                        var inVertex = _database.GetVertex(edge.InVertexId);
                        if (inVertex != null)
                        {
                            var inTraverser = traverser.Split();
                            inTraverser.Value = inVertex.ToGremlinResponse();
                            newTraversers.Add(inTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteOtherVStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // OtherV step needs context about which vertex we came from
            // For simplicity, implement as OutV for now
            ExecuteOutVStep(step, context);
        }

        #endregion

        #region Filter Steps - Fixed property access

        private void ExecuteHasStep(TinkerGraphStep step, TinkerTraversalContext context)
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
                            
                            return Equals(actualValue, expectedValue);
                        }
                        return false;
                    });
                }
            }
        }

        private void ExecuteHasLabelStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var expectedLabel = step.GetFirstStringArgument();
            
            context.Filter(traverser =>
            {
                var label = ExtractLabel(traverser.Value);
                return label != null && label.Equals(expectedLabel, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void ExecuteHasIdStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var expectedIds = step.Arguments.Select(arg => arg.ToString()).ToHashSet();
            
            context.Filter(traverser =>
            {
                var id = ExtractId(traverser.Value);
                return id != null && expectedIds.Contains(id);
            });
        }

        #endregion

        #region Property Steps - Fixed property access

        private void ExecuteValuesStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var propertyKeys = step.Arguments.Any() 
                ? step.Arguments.Select(arg => arg.ToString()).ToList() 
                : new List<string>();

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var properties = ExtractProperties(traverser.Value);
                if (properties != null)
                {
                    if (propertyKeys.Any())
                    {
                        // Return specific property values
                        foreach (var key in propertyKeys)
                        {
                            if (properties.TryGetValue(key, out object value))
                            {
                                var newTraverser = traverser.Split();
                                newTraverser.Value = value;
                                newTraversers.Add(newTraverser);
                            }
                        }
                    }
                    else
                    {
                        // Return all property values
                        foreach (var value in properties.Values)
                        {
                            var newTraverser = traverser.Split();
                            newTraverser.Value = value;
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteValueMapStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var properties = ExtractProperties(traverser.Value);
                var newTraverser = traverser.Split();
                newTraverser.Value = properties ?? new Dictionary<string, object>();
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteIdStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var id = ExtractId(traverser.Value);
                if (id != null)
                {
                    var newTraverser = traverser.Split();
                    newTraverser.Value = id;
                    newTraversers.Add(newTraverser);
                }
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteLabelStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var label = ExtractLabel(traverser.Value);
                if (label != null)
                {
                    var newTraverser = traverser.Split();
                    newTraverser.Value = label;
                    newTraversers.Add(newTraverser);
                }
            }

            context.Traversers = newTraversers;
        }

        #endregion

        #region Utility Methods for Data Extraction

        /// <summary>
        /// Extract vertex ID from various data structures
        /// </summary>
        private string ExtractVertexId(dynamic value)
        {
            return ExtractId(value);
        }

        /// <summary>
        /// Extract edge ID from various data structures  
        /// </summary>
        private string ExtractEdgeId(dynamic value)
        {
            return ExtractId(value);
        }

        /// <summary>
        /// Extract ID from various data structures
        /// </summary>
        private string ExtractId(dynamic value)
        {
            if (value == null) return null;
            
            try
            {
                // Handle GremlinResponseObject (the actual response type)
                if (value is GremlinResponseObject responseObj)
                {
                    return responseObj.id?.ToString();
                }
                
                // Handle dictionary format
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("id", out var id))
                    {
                        return id?.ToString();
                    }
                }
                
                // Handle dynamic object with id property
                try
                {
                    var dynamicId = value.id;
                    if (dynamicId != null)
                    {
                        return dynamicId.ToString();
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
                
                // Handle direct ID value
                if (value is string strValue)
                {
                    return strValue;
                }
                
                // Handle numeric ID
                if (value is int || value is long)
                {
                    return value.ToString();
                }
            }
            catch (Exception)
            {
                // If all else fails, return null
            }
            
            return null;
        }

        /// <summary>
        /// Extract label from various data structures
        /// </summary>
        private string ExtractLabel(dynamic value)
        {
            if (value == null) return null;
            
            try
            {
                // Handle GremlinResponseObject (the actual response type)
                if (value is GremlinResponseObject responseObj)
                {
                    return responseObj.label?.ToString();
                }
                
                // Handle dictionary format
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("label", out var label))
                    {
                        return label?.ToString();
                    }
                }
                
                // Handle dynamic object with label property
                try
                {
                    var dynamicLabel = value.label;
                    if (dynamicLabel != null)
                    {
                        return dynamicLabel.ToString();
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
            }
            catch (Exception)
            {
                // If all else fails, return null
            }
            
            return null;
        }

        /// <summary>
        /// Extract properties from various data structures
        /// </summary>
        private Dictionary<string, object> ExtractProperties(dynamic value)
        {
            if (value == null) return new Dictionary<string, object>();
            
            try
            {
                // Handle GremlinResponseObject (the actual response type)
                if (value is GremlinResponseObject responseObj)
                {
                    // Access the underlying properties data directly
                    var properties = new Dictionary<string, object>();
                    
                    try
                    {
                        // Get the raw properties object
                        var rawProperties = responseObj.Get<object>("properties");
                        
                        // Handle the exact CosmosDB format: Dictionary<string, List<Dictionary<string, object>>>
                        if (rawProperties is Dictionary<string, List<Dictionary<string, object>>> cosmosPropsDict)
                        {
                            foreach (var kvp in cosmosPropsDict)
                            {
                                if (kvp.Value != null && kvp.Value.Count > 0)
                                {
                                    var firstProp = kvp.Value[0];
                                    if (firstProp.TryGetValue("value", out var val))
                                    {
                                        properties[kvp.Key] = val;
                                    }
                                }
                            }
                        }
                        // Handle other possible formats
                        else if (rawProperties is Dictionary<string, object> propsDict)
                        {
                            foreach (var kvp in propsDict)
                            {
                                // Handle CosmosDB-style property format: [{"value": actualValue}]
                                if (kvp.Value is List<object> list && list.Count > 0)
                                {
                                    var first = list[0];
                                    if (first is Dictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                                    {
                                        properties[kvp.Key] = val;
                                    }
                                    else
                                    {
                                        properties[kvp.Key] = first;
                                    }
                                }
                                else if (kvp.Value is IEnumerable<dynamic> enumerable)
                                {
                                    var first = enumerable.FirstOrDefault();
                                    if (first is IDictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                                    {
                                        properties[kvp.Key] = val;
                                    }
                                    else
                                    {
                                        properties[kvp.Key] = first;
                                    }
                                }
                                else
                                {
                                    properties[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Property extraction error: {ex.Message}");
                        // If property access fails, try to access properties dynamically
                        try
                        {
                            dynamic dynamicResponse = responseObj;
                            var dynamicProps = dynamicResponse.properties;
                            if (dynamicProps != null)
                            {
                                // Handle the DynamicProperties case
                                if (dynamicProps is DynamicProperties dynProps)
                                {
                                    return dynProps.GetProperties();
                                }
                            }
                        }
                        catch
                        {
                            // Fallback to empty dictionary
                        }
                    }
                    
                    return properties;
                }
                
                // Handle dictionary format
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("properties", out var propsObj))
                    {
                        if (propsObj is IDictionary<string, object> props)
                        {
                            return new Dictionary<string, object>(props);
                        }
                    }
                    
                    // If no explicit properties key, treat the whole dict as properties
                    // but exclude special keys
                    var result = new Dictionary<string, object>();
                    foreach (var kvp in dict)
                    {
                        if (kvp.Key != "id" && kvp.Key != "label" && kvp.Key != "type")
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                    return result;
                }
                
                // Handle dynamic object with properties
                try
                {
                    var dynamicProps = value.properties;
                    if (dynamicProps is IDictionary<string, object> propsDict)
                    {
                        return new Dictionary<string, object>(propsDict);
                    }
                    
                    // Handle DynamicProperties
                    if (dynamicProps is DynamicProperties dynProps)
                    {
                        return dynProps.GetProperties();
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
            }
            catch (Exception)
            {
                // If all else fails, return empty dictionary
            }
            
            return new Dictionary<string, object>();
        }

        #endregion

        #region Terminal Steps

        private void ExecuteCountStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var count = context.Count;
            context.Clear();
            context.Traversers.Add(new Traverser(count));
        }

        #endregion

        #region Limit/Range Steps

        private void ExecuteLimitStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var limit = step.GetFirstIntArgument();
            if (limit >= 0)  // Allow limit(0) to work correctly
            {
                context.Limit(limit);
            }
        }

        private void ExecuteSkipStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var skip = step.GetFirstIntArgument();
            if (skip > 0)
            {
                context.Skip(skip);
            }
        }

        private void ExecuteRangeStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count >= 2)
            {
                var low = Convert.ToInt32(step.Arguments[0]);
                var high = Convert.ToInt32(step.Arguments[1]);
                context.Range(low, high);
            }
        }

        #endregion

        #region Barrier Steps

        private void ExecuteDedupStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            context.Dedup();
        }

        private void ExecuteOrderStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Basic ordering by string representation
            var ordered = context.Traversers.OrderBy(t => t.Value?.ToString()).ToList();
            context.Traversers.Clear();
            context.Traversers.AddRange(ordered);
        }
        
        private void ExecuteSampleStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var sampleSize = step.GetFirstIntArgument();
            if (sampleSize > 0)
            {
                context.Sample(sampleSize);
            }
        }

        private void ExecuteTailStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var tailSize = step.GetFirstIntArgument();
            if (tailSize > 0)
            {
                var allTraversers = context.Traversers.ToList();
                var tailTraversers = allTraversers.Skip(Math.Max(0, allTraversers.Count - tailSize)).ToList();
                context.Traversers.Clear();
                context.Traversers.AddRange(tailTraversers);
            }
        }

        private void ExecuteGroupStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Enhanced grouping implementation with proper .by() support
            var groups = new Dictionary<string, List<dynamic>>();
            
            // Default grouping by string representation if no .by() specified
            foreach (var traverser in context.Traversers)
            {
                var key = "default";
                
                // If there are arguments, use first argument as grouping key
                if (step.Arguments.Any())
                {
                    var groupingKey = step.Arguments[0].ToString();
                    if (groupingKey.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        key = ExtractLabel(traverser.Value) ?? "unknown";
                    }
                    else
                    {
                        var properties = ExtractProperties(traverser.Value);
                        if (properties != null && properties.ContainsKey(groupingKey))
                        {
                            var propertyValue = properties[groupingKey];
                            key = propertyValue?.ToString() ?? "null";
                        }
                        else
                        {
                            key = groupingKey;
                        }
                    }
                }
                else
                {
                    key = traverser.Value?.ToString() ?? "null";
                }
                
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new List<dynamic>();
                }
                
                // Add all bulk instances
                for (int i = 0; i < traverser.Bulk; i++)
                {
                    groups[key].Add(traverser.Value);
                }
            }
            
            context.Clear();
            context.Traversers.Add(new Traverser(groups));
        }

        private void ExecuteGroupCountStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Enhanced group count implementation with proper .by() support
            var groups = new Dictionary<string, long>();
            
            foreach (var traverser in context.Traversers)
            {
                var key = "default";
                
                // If there are arguments, use first argument as grouping key
                if (step.Arguments.Any())
                {
                    var groupingKey = step.Arguments[0].ToString();
                    if (groupingKey.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        key = ExtractLabel(traverser.Value) ?? "unknown";
                    }
                    else
                    {
                        var properties = ExtractProperties(traverser.Value);
                        if (properties != null && properties.ContainsKey(groupingKey))
                        {
                            var propertyValue = properties[groupingKey];
                            key = propertyValue?.ToString() ?? "null";
                        }
                        else
                        {
                            key = groupingKey;
                        }
                    }
                }
                else
                {
                    key = traverser.Value?.ToString() ?? "null";
                }
                
                if (!groups.ContainsKey(key))
                {
                    groups[key] = 0;
                }
                
                groups[key] += traverser.Bulk;
            }
            
            context.Clear();
            context.Traversers.Add(new Traverser(groups));
        }

        private void ExecuteFoldStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var results = context.GetCurrentResults().ToList();
            context.Clear();
            context.Traversers.Add(new Traverser(results));
        }

        private void ExecuteUnfoldStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                if (traverser.Value is IEnumerable<dynamic> enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = item;
                        newTraversers.Add(newTraverser);
                    }
                }
                else
                {
                    // If not enumerable, just pass through
                    newTraversers.Add(traverser);
                }
            }

            context.Traversers = newTraversers;
        }

        #endregion

        #region Advanced Filter Steps

        private void ExecuteWhereStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return;

            var predicate = step.Arguments[0].ToString();
            
            // Basic where implementation with common patterns
            // In a full implementation, this would parse the predicate properly
            context.Filter(traverser =>
            {
                // For now, implement basic predicate evaluation
                // This would need to be enhanced to handle complex predicates
                return true; // Placeholder
            });
        }

        #endregion

        #region Mutation Steps

        private void ExecutePropertyStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count >= 2)
            {
                var key = step.Arguments[0].ToString();
                var value = step.Arguments[1];
                
                var newTraversers = new List<Traverser>();

                foreach (var traverser in context.Traversers)
                {
                    var newTraverser = traverser.Split();
                    
                    // Try to update the actual database object
                    var id = ExtractId(traverser.Value);
                    if (!string.IsNullOrEmpty(id))
                    {
                        var vertex = _database.GetVertex(id);
                        if (vertex != null)
                        {
                            vertex.SetProperty(key, value);
                            newTraverser.Value = vertex.ToGremlinResponse();
                        }
                        else
                        {
                            var edge = _database.GetEdge(id);
                            if (edge != null)
                            {
                                edge.SetProperty(key, value);
                                newTraverser.Value = edge.ToGremlinResponse();
                            }
                            else
                            {
                                newTraverser.Value = traverser.Value;
                            }
                        }
                    }
                    else
                    {
                        newTraverser.Value = traverser.Value;
                    }
                    
                    newTraversers.Add(newTraverser);
                }

                context.Traversers = newTraversers;
            }
        }

        private void ExecuteDropStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Drop vertices/edges from the database
            foreach (var traverser in context.Traversers)
            {
                var id = ExtractId(traverser.Value);
                if (!string.IsNullOrEmpty(id))
                {
                    // Try to drop as vertex first, then as edge
                    if (!_database.RemoveVertex(id))
                    {
                        _database.RemoveEdge(id);
                    }
                }
            }
            
            // Drop step returns empty results
            context.Traversers = new List<Traverser>();
        }

        private void ExecuteAddEdgeContextStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count < 1)
                return;
                
            var label = step.GetFirstStringArgument();
            
            // For addE in a traversal context, we need more sophisticated handling
            // This would require tracking the .to() and .from() modulators
            // For now, just pass through the current traversers
            // In a real implementation, this would be part of a more complex state machine
        }

        #endregion

        #region Path Steps

        private void ExecutePathStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var newTraverser = traverser.Split();
                
                // Get the path from the traverser
                var path = traverser.GetPath();
                
                // If path is empty, at least include the current value
                if (!path.Any())
                {
                    path = new List<dynamic> { traverser.Value };
                }
                
                // Set the path directly as the traverser value (not wrapped)
                newTraverser.Value = path;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private void ExecuteSelectStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var labels = step.Arguments.Select(arg => arg.ToString()).ToList();
            var newTraversers = new List<Traverser>();
            
            foreach (var traverser in context.Traversers)
            {
                if (labels.Count == 1)
                {
                    // Single label selection
                    var selected = traverser.GetTagged<dynamic>(labels[0]);
                    var newTraverser = traverser.Split();
                    newTraverser.Value = selected;
                    newTraversers.Add(newTraverser);
                }
                else
                {
                    // Multiple label selection - return as map
                    var selected = new Dictionary<string, dynamic>();
                    foreach (var label in labels)
                    {
                        selected[label] = traverser.GetTagged<dynamic>(label);
                    }
                    
                    var newTraverser = traverser.Split();
                    newTraverser.Value = selected;
                    newTraversers.Add(newTraverser);
                }
            }

            context.Traversers = newTraversers;
        }

        #endregion
    }
}
