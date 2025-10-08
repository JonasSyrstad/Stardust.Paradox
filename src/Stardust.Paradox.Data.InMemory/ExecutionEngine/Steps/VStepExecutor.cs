using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the V() step which filters or replaces current traversers with specific vertices.
    /// This step can be used in mid-traversal to navigate to specific vertices by ID.
    /// 
    /// Behavior:
    /// - V() without arguments: Gets all vertices from the graph
    /// - V(id1, id2, ...): Gets specific vertices by their IDs
    /// - Supports CosmosDB partition key array syntax: V([partitionKey, id])
    /// 
    /// This step replaces current traversers rather than filtering them.
    /// </summary>
    [UsedImplicitly]
    public class VStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public VStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "v";

        public string StepDescription => 
            "Navigates to specific vertices by ID or gets all vertices. " +
            "V(id1, id2, ...) retrieves vertices with the given IDs. " +
            "V() without arguments retrieves all vertices in the graph. " +
            "Supports CosmosDB partition key syntax [partitionKey, id].";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Any())
            {
                // Handle CosmosDB partition key array syntax: V([partitionKey, id])
                var processedIds = new List<string>();
                
                foreach (var arg in step.Arguments)
                {
                    // Check if this argument is already parsed as a list (from array syntax)
                    if (arg is List<object> list && list.Count >= 1)
                    {
                        // Array syntax like [partitionKey, id] - use the last element as the ID
                        // (CosmosDB uses [partition, id], standard Gremlin might use [id])
                        var id = list.Last()?.ToString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            processedIds.Add(id.Trim('"', '\''));
                        }
                    }
                    else if (arg is System.Collections.IList ilist && ilist.Count >= 1)
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
                        var argString = arg?.ToString() ?? "";
                        
                        // Check if this is an array format like "['string','string']"
                        if (argString.StartsWith("['") && argString.EndsWith("']"))
                        {
                            // Parse the array content - this is specifically for the ['string','string'] format
                            var content = argString.Substring(2, argString.Length - 4); // Remove [' and ']
                            var parts = content.Split(new[] { "','" }, System.StringSplitOptions.None);
                            
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
                
                // V(id1, id2, ...) in the middle of traversal replaces current traversers
                var newTraversers = new List<Traverser>();
                
                foreach (var vertexId in processedIds)
                {
                    var vertex = _database.GetVertex(vertexId);
                    if (vertex != null)
                    {
                        // For each existing traverser, create a new one with the specified vertex
                        foreach (var existingTraverser in context.Traversers)
                        {
                            var newTraverser = existingTraverser.Split();
                            newTraverser.Value = vertex.ToGremlinResponse();
                             
                            // Add to path for path tracking
                            newTraverser.AddToPath(vertex.ToGremlinResponse());
                            
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
                
                context.Traversers = newTraversers;
            }
            else
            {
                // V() - get all vertices (replace current traversers)
                var allVertices = _database.GetAllVertices().Select(v => v.ToGremlinResponse()).ToList();
                var newTraversers = new List<Traverser>();
                
                foreach (var existingTraverser in context.Traversers)
                {
                    foreach (var vertex in allVertices)
                    {
                        var newTraverser = existingTraverser.Split();
                        newTraverser.Value = vertex;
                        newTraversers.Add(newTraverser);
                    }
                }
                
                context.Traversers = newTraversers;
            }
        }
    }
}
