using System;
using System.Collections.Concurrent;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine
{
    /// <summary>
    /// TinkerGraph-inspired query executor with optimized traversal strategies
    /// Based on Apache TinkerPop's TinkerGraph execution model
    /// </summary>
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

            // Execute each step in sequence
            for (int i = 0; i < traversal.Steps.Count; i++)
            {
                var step = traversal.Steps[i];

                if (step.IsStartStep)
                    continue; // Start steps already handled in initialization

                // Look ahead for .by() modulator steps when executing grouping operations
                if (IsGroupingStep(step))
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

            return context.GetCurrentResults();
        }

        /// <summary>
        /// Check if a step is a grouping step that can be modified by .by()
        /// </summary>
        private bool IsGroupingStep(TinkerGraphStep step)
        {
            var stepName = step.StepName.ToLower();
            return stepName == "group" || stepName == "groupcount";
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
                case "inject":
                    return ExecuteInjectStep(step);
                default:
                    return _database.GetAllVertices().Select(v => v.ToGremlinResponse());
            }
        }

        /// <summary>
        /// Execute inject start step with proper TinkerPop multiple value support
        /// </summary>
        private IEnumerable<dynamic> ExecuteInjectStep(TinkerGraphStep step)
        {
            var results = new List<dynamic>();

            // Inject each argument as a separate traverser (TinkerPop standard)
            foreach (var arg in step.Arguments)
            {
                results.Add(arg);
            }

            return results;
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
                throw new Exception("not implemented?!?");
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
                // Handle CosmosDB partition key array syntax: V([partitionKey, id])
                // For InMemory database, we ignore the partition key and use only the id
                var processedIds = new List<string>();

                foreach (var arg in step.Arguments)
                {
                    // Check if this argument looks like an array from CosmosDB partition key syntax
                    // The argument could be:
                    // 1. A string that looks like "['value1','value2']" 
                    // 2. An actual array/list object
                    // 3. A regular ID string

                    if (arg is System.Collections.IList list && list.Count >= 2)
                    {
                        // Handle case where argument is already parsed as a list/array
                        var id = list[1]?.ToString(); // Use second element (id), ignore first (partition key)
                        if (!string.IsNullOrEmpty(id))
                        {
                            processedIds.Add(id.Trim('"', '\''));
                        }
                    }
                    else
                    {
                        var argString = arg.ToString();

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
                var key = step.Arguments[i].ToString();
                var value = step.Arguments[i + 1];
                edge.SetProperty(key, value);
            }

            return new[] { edge.ToGremlinResponse() };
        }

        #endregion

        

      

     

      
    }
}
