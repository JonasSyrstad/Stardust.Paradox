using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the tree() step which creates a hierarchical tree structure.
    /// 
    /// Behavior:
    /// - Creates a tree based on the paths taken through the graph
    /// - Returns CosmosDB/TinkerPop compatible format for VertexTreeRoot deserialization
    /// - Each node includes: id, label, type, properties, and children
    /// 
    /// Example:
    /// g.V('root').out().out().tree() - creates tree showing hierarchy
    /// </summary>
    [UsedImplicitly]
    public class TreeStepExecutor : StepExecutorBase
    {
        public TreeStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "tree";

        public override string StepDescription => 
            "Creates a hierarchical tree structure based on traversal paths. " +
            "Returns CosmosDB-compatible format with nested vertex structure.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Tree step creates a hierarchical structure based on the paths taken through the graph

            // If there are no traversers, we still need to return an empty tree
            if (!context.Traversers.Any())
            {
                var emptyTree = new JObject();
                context.Clear();
                context.Traversers.Add(new Traverser(emptyTree));
                return;
            }

            // Collect all paths and organize them into a tree structure
            var allPaths = new List<List<dynamic>>();

            foreach (var traverser in context.Traversers)
            {
                var path = traverser.GetPath();
                if (path.Any())
                {
                    allPaths.Add(path);
                }
                else
                {
                    // If no path is tracked, check if the traverser value is a vertex
                    // This happens when tree() is called without intermediate steps tracking paths
                    var value = traverser.Value;
                    if (value != null)
                    {
                        allPaths.Add(new List<dynamic> { value });
                    }
                }
            }

            // Build tree structure as a Dictionary compatible with VertexTreeRoot deserialization
            var treeStructure = BuildCosmosDBCompatibleTreeStructure(allPaths);

            // Convert to JObject for compatibility with VertexTreeRoot
            var json = JsonConvert.SerializeObject(treeStructure);
            var treeJObject = JsonConvert.DeserializeObject<JObject>(json);

            // Tree step always returns one result, even if empty
            context.Clear();
            context.Traversers.Add(new Traverser(treeJObject));
        }

        /// <summary>
        /// Build a tree structure that exactly matches CosmosDB vertex tree format
        /// Compatible with deserialization into List<Dictionary<string, Vertex>>
        /// </summary>
        private Dictionary<string, object> BuildCosmosDBCompatibleTreeStructure(List<List<dynamic>> paths)
        {
            var result = new Dictionary<string, object>();

            if (!paths.Any())
            {
                return result;
            }

            foreach (var path in paths)
            {
                if (!path.Any()) continue;

                var currentLevel = result;

                for (int i = 0; i < path.Count; i++)
                {
                    var vertex = path[i];
                    var vertexId = ExtractId(vertex) ?? vertex?.ToString() ?? "null";

                    if (!currentLevel.ContainsKey(vertexId))
                    {
                        // Create vertex structure compatible with Vertex class
                        var vertexKey = CreateCosmosDBCompatibleVertexKey(vertex);
                        var vertexValue = new Dictionary<string, object>();

                        var vertexStructure = new Dictionary<string, object>
                        {
                            ["key"] = vertexKey,
                            ["value"] = vertexValue
                        };

                        currentLevel[vertexId] = vertexStructure;
                    }

                    // Move to the children level for the next iteration
                    if (i < path.Count - 1)
                    {
                        var vertexStructure = currentLevel[vertexId] as Dictionary<string, object>;
                        if (vertexStructure != null && vertexStructure.ContainsKey("value"))
                        {
                            currentLevel = vertexStructure["value"] as Dictionary<string, object>;
                            if (currentLevel == null)
                            {
                                // This shouldn't happen, but handle it gracefully
                                currentLevel = new Dictionary<string, object>();
                                vertexStructure["value"] = currentLevel;
                            }
                        }
                        else
                        {
                            // Fallback: create new children dictionary
                            currentLevel = new Dictionary<string, object>();
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Create vertex key data that exactly matches CosmosDB Key class format
        /// </summary>
        private Dictionary<string, object> CreateCosmosDBCompatibleVertexKey(dynamic vertex)
        {
            var vertexKey = new Dictionary<string, object>();

            try
            {
                // Extract core vertex properties for Key class
                var id = ExtractId(vertex);
                var label = ExtractLabel(vertex);
                var type = ExtractType(vertex);
                var properties = ExtractProperties(vertex);

                // Always include these core properties to match Key class
                vertexKey["id"] = id ?? "unknown";
                vertexKey["label"] = label ?? "vertex";
                vertexKey["type"] = type ?? "vertex";

                // Convert properties to CosmosDB format
                var cosmosDbProperties = ConvertToCosmosDBPropertiesFormat(properties);
                vertexKey["properties"] = cosmosDbProperties;
            }
            catch (Exception)
            {
                // Fallback: create minimal vertex key data matching Key class structure
                vertexKey["id"] = vertex?.ToString() ?? "unknown";
                vertexKey["label"] = "vertex";
                vertexKey["type"] = "vertex";
                vertexKey["properties"] = new Dictionary<string, object>();
            }

            return vertexKey;
        }

        /// <summary>
        /// Convert properties to CosmosDB format that matches the Properties class structure
        /// </summary>
        private Dictionary<string, object> ConvertToCosmosDBPropertiesFormat(Dictionary<string, object> properties)
        {
            var cosmosDbProperties = new Dictionary<string, object>();

            if (properties == null)
            {
                return cosmosDbProperties;
            }

            foreach (var kvp in properties)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                // Convert each property to CosmosDB value list format
                if (value is string stringValue)
                {
                    cosmosDbProperties[key] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["id"] = Guid.NewGuid().ToString(),
                            ["value"] = stringValue
                        }
                    };
                }
                else if (value is long longValue)
                {
                    cosmosDbProperties[key] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["id"] = Guid.NewGuid().ToString(),
                            ["value"] = longValue
                        }
                    };
                }
                else if (value is int intValue)
                {
                    cosmosDbProperties[key] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["id"] = Guid.NewGuid().ToString(),
                            ["value"] = (long)intValue
                        }
                    };
                }
                else if (value is bool boolValue)
                {
                    cosmosDbProperties[key] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["id"] = Guid.NewGuid().ToString(),
                            ["value"] = boolValue
                        }
                    };
                }
                else
                {
                    // Default to string representation
                    cosmosDbProperties[key] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["id"] = Guid.NewGuid().ToString(),
                            ["value"] = value?.ToString() ?? ""
                        }
                    };
                }
            }

            return cosmosDbProperties;
        }
    }
}
