using System;
using System.Collections.Concurrent;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine
{
    public class TinkerGraphQueryExecutor
    {
        private static ConcurrentDictionary<string, Type> _StepExecutors = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        static TinkerGraphQueryExecutor()
        {

        }

        private readonly InMemoryGraphDatabase _database;
        private static bool _initalized;
        private static object _lock = new object();
        public TinkerGraphQueryExecutor(InMemoryGraphDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));

            // Register step executors with the database instance
            if (_initalized) return;
            lock (_lock)
            {
                if (_initalized) return;
                RegisterStepExecutors();
                _initalized = true;
            }
        }

        private void RegisterStepExecutors()
        {
            var thisAssembly = typeof(TinkerGraphQueryExecutor).Assembly;
            var stepExecutorTypes = thisAssembly.GetTypes()
                .Where(type => typeof(IStepExecutor).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

            foreach (var executorType in stepExecutorTypes)
            {
                try
                {
                    // Create instance with database parameter
                    var instance = (IStepExecutor)Activator.CreateInstance(executorType, _database);

                    // Register by step name
                    _StepExecutors.TryAdd(instance.StepName.ToLower(), executorType);

                    System.Diagnostics.Debug.WriteLine($"Registered step executor: {instance.StepName} ({executorType.Name})");
                }
                catch (Exception ex)
                {
                    // Log registration errors
                    System.Diagnostics.Debug.WriteLine($"Failed to register step executor {executorType.Name}: {ex.Message}");
                }
            }
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
            
            // Store parameters in context for step executors to resolve ParameterReference objects
            context.SetMetadata("parameters", traversal.Parameters);

            // Execute each step in sequence
            for (int i = 0; i < traversal.Steps.Count; i++)
            {
                var step = traversal.Steps[i];

                if (step.IsStartStep)
                    continue; // Start steps already handled in initialization

                // Set current step information in context for lookahead functionality
                context.SetMetadata("current_step_index", i);
                context.SetMetadata("all_steps", traversal.Steps);

                // Look ahead for .by() modulator steps when executing grouping/modulating operations
                if (IsGroupingStep(step) || step.StepName.Equals("dedup", StringComparison.OrdinalIgnoreCase))
                {
                    var byArguments = new List<List<object>>();
                    int nextIndex = i + 1;

                    // Collect all consecutive .by() modulators
                    while (nextIndex < traversal.Steps.Count)
                    {
                        var nextStep = traversal.Steps[nextIndex];
                        if (nextStep.StepName.Equals("by", StringComparison.OrdinalIgnoreCase))
                        {
                            byArguments.Add(nextStep.Arguments.ToList());
                            nextIndex++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Pre-store all .by() arguments for the grouping step
                    if (byArguments.Any())
                    {
                        context.SetMetadata("all_by_arguments", byArguments);
                    }

                    // Execute the grouping step with all .by() arguments available
                    ExecuteStep(step, context);

                    // Skip all processed .by() steps
                    i = nextIndex - 1; // -1 because the loop will increment by 1
                    continue;
                }

                ExecuteStep(step, context);

                // Check if there are any remaining terminal aggregation steps in the pipeline
                var hasRemainingTerminalSteps = false;
                for (int j = i + 1; j < traversal.Steps.Count; j++)
                {
                    if (IsTerminalAggregationStep(traversal.Steps[j]))
                    {
                        hasRemainingTerminalSteps = true;
                        break;
                    }
                    // Tree step should also always execute
                    if (traversal.Steps[j].StepName.Equals("tree", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRemainingTerminalSteps = true;
                        break;
                    }
                }

                // Early termination if no traversers remain AND no terminal aggregation steps are left
                if (!context.HasTraversers && !hasRemainingTerminalSteps)
                {
                    break;
                }
            }

            // Finalize deferred addE() so that any chained property() steps are included
            // in the edge creation (and therefore properly indexed).
            TryFinalizePendingAddE(context);

            return context.GetCurrentResults();
        }

        private static string TryExtractElementId(object value)
        {
            if (value == null)
                return null;

            if (value is string s)
                return s;

            try
            {
                var dyn = value as dynamic;
                if (dyn == null)
                    return null;

                var idVal = dyn.id;
                return idVal?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void TryFinalizePendingAddE(TinkerTraversalContext context)
        {
            if (!context.HasMetadata("addE_pending"))
            {
                return;
            }

            var label = context.GetMetadata<string>("addE_label");
            var fromVertexId = context.GetMetadata<string>("addE_from");
            var toVertexId = context.GetMetadata<string>("addE_to");
            var properties = context.GetMetadata<Dictionary<string, object>>("addE_properties") ?? new Dictionary<string, object>();

            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(toVertexId))
            {
                return;
            }

            string edgeId = context.GetMetadata<string>("addE_edgeId");
            if (string.IsNullOrEmpty(edgeId) && properties.TryGetValue("id", out var idValue) && idValue != null)
            {
                edgeId = idValue.ToString();
                properties.Remove("id");
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var effectiveFromId = fromVertexId;
                if (string.IsNullOrEmpty(effectiveFromId))
                {
                    effectiveFromId = TryExtractElementId(traverser.Value);
                }

                if (string.IsNullOrEmpty(effectiveFromId))
                {
                    continue;
                }

                var fromVertex = _database.GetVertex(effectiveFromId);
                var toVertex = _database.GetVertex(toVertexId);
                if (fromVertex == null || toVertex == null)
                {
                    continue;
                }

                var edge = _database.AddEdge(label, effectiveFromId, toVertexId, properties, edgeId);
                if (edge == null)
                {
                    continue;
                }

                var newTraverser = traverser.Split();
                newTraverser.Value = edge.ToGremlinResponse();
                newTraversers.Add(newTraverser);
            }

            if (newTraversers.Any())
            {
                context.Traversers = newTraversers;
            }

            context.RemoveMetadata("addE_pending");
            context.RemoveMetadata("addE_label");
            context.RemoveMetadata("addE_from");
            context.RemoveMetadata("addE_from_spec");
            context.RemoveMetadata("addE_to");
            context.RemoveMetadata("addE_to_spec");
            context.RemoveMetadata("addE_properties");
            context.RemoveMetadata("addE_edgeId");
        }

        /// <summary>
        /// Check if a step is a grouping step that can be modified by .by()
        /// </summary>
        private bool IsGroupingStep(TinkerGraphStep step)
        {
            var stepName = step.StepName.ToLower();
            return stepName == "group" || stepName == "groupcount" || stepName == "order";
        }

        /// <summary>
        /// Check if a step is a terminal aggregation step that should run even with empty input
        /// </summary>
        private bool IsTerminalAggregationStep(TinkerGraphStep step)
        {
            var stepName = step.StepName.ToLower();
            return stepName == "count" || stepName == "sum" || stepName == "mean" ||
                   stepName == "min" || stepName == "max" || stepName == "fold";
        }

        /// <summary>
        /// Initialize the traversal context with start step
        /// </summary>
        private TinkerTraversalContext InitializeTraversalContext(TinkerGraphTraversal traversal)
        {
            var firstStep = traversal.Steps.FirstOrDefault();

            if (firstStep?.IsStartStep == true)
            {
                var initialResults = ExecuteStartStep(firstStep, traversal.Parameters);
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
        private IEnumerable<dynamic> ExecuteStartStep(TinkerGraphStep step, Dictionary<string, object> parameters = null)
        {
            switch (step.StepName.ToLower())
            {
                case "v":
                    return ExecuteVertexStep(step, parameters);
                case "e":
                    return ExecuteEdgeStep(step, parameters);
                case "addv":
                    return ExecuteAddVertexStep(step, parameters);
                case "adde":
                    return ExecuteAddEdgeStep(step, parameters);
                case "inject":
                    return ExecuteInjectStep(step, parameters);
                default:
                    return _database.GetAllVertices().Select(v => v.ToGremlinResponse());
            }
        }

        /// <summary>
        /// Execute inject start step with proper TinkerPop multiple value support
        /// </summary>
        private IEnumerable<dynamic> ExecuteInjectStep(TinkerGraphStep step, Dictionary<string, object> parameters = null)
        {
            var results = new List<dynamic>();

            // Inject each argument as a separate traverser (TinkerPop standard)
            foreach (var arg in step.Arguments)
            {
                var resolvedArg = ResolveParameter(arg, parameters ?? new Dictionary<string, object>());
                results.Add(resolvedArg);
            }

            return results;
        }

        /// <summary>
        /// Resolve a ParameterReference to its actual value from the parameters dictionary
        /// </summary>
        private object ResolveParameter(object value, Dictionary<string, object> parameters)
        {
            if (value is ParameterReference paramRef)
            {
                if (parameters.TryGetValue(paramRef.ParameterName, out var resolvedValue))
                {
                    return resolvedValue;
                }
                // If parameter not found, return the parameter name as a string (fallback)
                return paramRef.ParameterName;
            }
            
            // Handle lists that might contain ParameterReference objects
            if (value is List<object> list)
            {
                return list.Select(item => ResolveParameter(item, parameters)).ToList();
            }
            
            // Handle generic IList
            if (value is System.Collections.IList ilist && !(value is string))
            {
                var resolved = new List<object>();
                foreach (var item in ilist)
                {
                    resolved.Add(ResolveParameter(item, parameters));
                }
                return resolved;
            }
            
            return value;
        }

        /// <summary>
        /// Execute a single step in the traversal
        /// </summary>
        private void ExecuteStep(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (_StepExecutors.TryGetValue(step.StepName.ToLower(), out var stepExecutorType))
            {
                var stepExecutor = (IStepExecutor)Activator.CreateInstance(stepExecutorType, _database);
                stepExecutor.Execute(step, context);
            }
            else 
            {
                // Provide detailed error message about missing step executor
                var registeredSteps = string.Join(", ", _StepExecutors.Keys.OrderBy(k => k));
                throw new NotImplementedException(
                    $"Step executor not found for step '{step.StepName}'. " +
                    $"Registered steps: {registeredSteps}. " +
                    $"Please implement a step executor class that implements IStepExecutor for the '{step.StepName}' step.");
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

        private IEnumerable<dynamic> ExecuteVertexStep(TinkerGraphStep step, Dictionary<string, object> parameters = null)
        {
            if (parameters == null)
                parameters = new Dictionary<string, object>();
                
            if (step.Arguments.Any())
            {
                // Handle CosmosDB partition key array syntax: V([partitionKey, id])
                // For InMemory database, we ignore the partition key and use only the id
                var processedIds = new List<string>();

                foreach (var arg in step.Arguments)
                {
                    // Resolve ParameterReference if present
                    var resolvedArg = ResolveParameter(arg, parameters);
                    
                    // Check if this argument is already parsed as a list (from array syntax)
                    if (resolvedArg is List<object> list && list.Count >= 1)
                    {
                        // Array syntax like [partitionKey, id] - use the last element as the ID
                        // (CosmosDB uses [partition, id], standard Gremlin might use [id])
                        var id = list.Last()?.ToString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            processedIds.Add(id.Trim('"', '\''));
                        }
                    }
                    else if (resolvedArg is System.Collections.IList ilist && ilist.Count >= 1)
                    {
                        // Handle generic IList
                        var id = ilist[ilist.Count - 1]?.ToString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            processedIds.Add(id.Trim('"', '\''));
                        }
                    }
                    else
                    {
                        var argString = resolvedArg?.ToString() ?? "";

                        // Check if this is an array format like "['string','string']"
                        if (argString.StartsWith("['") && argString.EndsWith("']"))
                        {
                            // Parse the array content - this is specifically for the ['string','string'] format
                            var content = argString.Substring(2, argString.Length - 4); // Remove [' and ']
                            var parts = content.Split(new[] { "','" }, StringSplitOptions.None);

                            if (parts.Length >= 2)
                            {
                                // Extract the ID (second element), ignoring partition key (first element)
                                var id = parts[1].Trim();
                                processedIds.Add(id);
                            }
                            else if (parts.Length == 1)
                            {
                                // Single element array, use it as ID
                                var id = parts[0].Trim();
                                processedIds.Add(id);
                            }
                        }
                        else if (argString.StartsWith("[") && argString.EndsWith("]"))
                        {
                            // Parse the array content for unquoted arrays like [string,string]
                            var content = argString.Substring(1, argString.Length - 2); // Remove [ and ]
                            var parts = content.Split(',');

                            if (parts.Length >= 2)
                            {
                                // Extract the ID (second element), ignoring partition key (first element)
                                var id = parts[1].Trim().Trim('"', '\'');
                                processedIds.Add(id);
                            }
                            else if (parts.Length == 1)
                            {
                                // Single element array, use it as ID
                                var id = parts[0].Trim().Trim('"', '\'');
                                processedIds.Add(id);
                            }
                        }
                        else
                        {
                            // Regular ID format
                            processedIds.Add(argString);
                        }
                    }
                }

                // V(id1, id2, ...) - get specific vertices using processed IDs
                return processedIds.Select(id => _database.GetVertex(id))
                                 .Where(v => v != null)
                                 .Select(v => v.ToGremlinResponse());
            }

            // V() - get all vertices
            return _database.GetAllVertices().Select(v => v.ToGremlinResponse());
        }

        private IEnumerable<dynamic> ExecuteEdgeStep(TinkerGraphStep step, Dictionary<string, object> parameters = null)
        {
            if (parameters == null)
                parameters = new Dictionary<string, object>();
                
            if (step.Arguments.Any())
            {
                // E(id1, id2, ...) - get specific edges
                var ids = step.Arguments.Select(arg => ResolveParameter(arg, parameters).ToString());
                return ids.Select(id => _database.GetEdge(id))
                         .Where(e => e != null)
                         .Select(e => e.ToGremlinResponse());
            }

            // E() - get all edges
            return _database.GetAllEdges().Select(e => e.ToGremlinResponse());
        }

        private IEnumerable<dynamic> ExecuteAddVertexStep(TinkerGraphStep step, Dictionary<string, object> parameters = null)
        {
            if (parameters == null)
                parameters = new Dictionary<string, object>();
                
            var label = step.GetFirstStringArgument() ?? "vertex";
            var vertex = _database.AddVertex(label);

            // Add properties from arguments (property pairs)
            for (int i = 1; i < step.Arguments.Count - 1; i += 2)
            {
                var key = ResolveParameter(step.Arguments[i], parameters).ToString();
                var value = ResolveParameter(step.Arguments[i + 1], parameters);
                vertex.SetProperty(key, value);
            }

            return new[] { vertex.ToGremlinResponse() };
        }

        private IEnumerable<dynamic> ExecuteAddEdgeStep(TinkerGraphStep step, Dictionary<string, object> parameters = null)
        {
            if (parameters == null)
                parameters = new Dictionary<string, object>();
                
            if (step.Arguments.Count < 3)
                return Enumerable.Empty<dynamic>();

            var label = step.GetFirstStringArgument();
            var fromId = ResolveParameter(step.Arguments[1], parameters).ToString();
            var toId = ResolveParameter(step.Arguments[2], parameters).ToString();

            // Ensure vertices exist before creating edge
            var fromVertex = _database.GetVertex(fromId);
            var toVertex = _database.GetVertex(toId);

            if (fromVertex == null || toVertex == null)
            {
                return Enumerable.Empty<dynamic>();
            }

            var edge = _database.AddEdge(label, fromId, toId);
            if (edge == null)
                return Enumerable.Empty<dynamic>();

            // Add properties from remaining arguments
            for (int i = 3; i < step.Arguments.Count - 1; i += 2)
            {
                var key = ResolveParameter(step.Arguments[i], parameters).ToString();
                var value = ResolveParameter(step.Arguments[i + 1], parameters);
                edge.SetProperty(key, value);
            }

            return new[] { edge.ToGremlinResponse() };
        }

        #endregion
    }
}
