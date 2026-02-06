using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the drop() step which removes elements from the database.
    /// 
    /// Behavior:
    /// - Removes vertices, edges, or properties from the database
    /// - Returns empty results after dropping
    /// 
    /// Example:
    /// g.V('1').drop() - removes vertex with ID '1'
    /// g.E('e1').drop() - removes edge with ID 'e1'
    /// g.V('1').properties('name').drop() - removes the 'name' property from vertex '1'
    /// </summary>
    [UsedImplicitly]
    public class DropStepExecutor : StepExecutorBase
    {
        public DropStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "drop";

        public override string StepDescription => 
            "Removes elements (vertices, edges, or properties) from the database. " +
            "Returns empty results after dropping.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // DEBUG logging for drop step
            Console.WriteLine($"[DEBUG DropStepExecutor] Executing drop() on {context.Traversers.Count} traversers");
            
            // Drop vertices/edges/properties from the database
            foreach (var traverser in context.Traversers)
            {
                // Check if this is a property object (from properties() step)
                if (IsPropertyStructure(traverser.Value))
                {
                    // Handle property drop
                    DropProperty(traverser.Value);
                    continue;
                }
                
                var id = ExtractId(traverser.Value);
                Console.WriteLine($"[DEBUG DropStepExecutor] Extracted ID: {id}");
                
                if (!string.IsNullOrEmpty(id))
                {
                    // Determine if this is a vertex or edge by checking the type or structure
                    var elementType = ExtractType(traverser.Value);
                    Console.WriteLine($"[DEBUG DropStepExecutor] Element type: {elementType}");
                    
                    if (elementType == "edge" || IsEdgeStructure(traverser.Value))
                    {
                        // It's an edge, try to remove it
                        Console.WriteLine($"[DEBUG DropStepExecutor] Removing edge: {id}");
                        var removed = Database.RemoveEdge(id);
                        Console.WriteLine($"[DEBUG DropStepExecutor] Edge removal result: {removed}");
                    }
                    else
                    {
                        // Try to drop as vertex first, then as edge if vertex doesn't exist
                        Console.WriteLine($"[DEBUG DropStepExecutor] Trying to remove as vertex: {id}");
                        if (!Database.RemoveVertex(id))
                        {
                            Console.WriteLine($"[DEBUG DropStepExecutor] Vertex not found, trying as edge: {id}");
                            var removed = Database.RemoveEdge(id);
                            Console.WriteLine($"[DEBUG DropStepExecutor] Edge removal result: {removed}");
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG DropStepExecutor] Vertex removed successfully");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG DropStepExecutor] Could not extract ID from traverser value");
                }
            }

            // Drop step returns empty results
            context.Traversers = new List<Traverser>();
        }

        /// <summary>
        /// Check if the value structure represents a property object (from properties() step)
        /// </summary>
        private bool IsPropertyStructure(dynamic value)
        {
            if (value == null)
                return false;

            try
            {
                // Check for property-specific structure (key, value, id)
                var valueType = value.GetType();
                
                // Check if it's an anonymous type with key and value properties
                var hasKey = valueType.GetProperty("key") != null;
                var hasValue = valueType.GetProperty("value") != null;
                
                if (hasKey && hasValue)
                {
                    return true;
                }
                
                // Also check using dynamic access
                try
                {
                    var key = value.key;
                    var val = value.value;
                    if (key != null && val != null)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
            }
            catch
            {
                // If we can't determine, return false
            }

            return false;
        }

        /// <summary>
        /// Drop a property from a vertex
        /// </summary>
        private void DropProperty(dynamic propertyObj)
        {
            try
            {
                string id = null;
                string key = null;
                
                // Try to get id and key from the property object
                try
                {
                    id = propertyObj.id?.ToString();
                    key = propertyObj.key?.ToString();
                }
                catch
                {
                    // Try reflection
                    var valueType = propertyObj.GetType();
                    var idProp = valueType.GetProperty("id");
                    var keyProp = valueType.GetProperty("key");
                    
                    if (idProp != null)
                        id = idProp.GetValue(propertyObj)?.ToString();
                    if (keyProp != null)
                        key = keyProp.GetValue(propertyObj)?.ToString();
                }
                
                Console.WriteLine($"[DEBUG DropStepExecutor] Dropping property: id={id}, key={key}");
                
                if (id != null && id.Contains("_"))
                {
                    // Extract vertex ID from property ID (format: "vertexId_propertyKey")
                    var underscoreIndex = id.LastIndexOf('_');
                    if (underscoreIndex > 0)
                    {
                        var vertexId = id.Substring(0, underscoreIndex);
                        var propertyKey = key ?? id.Substring(underscoreIndex + 1);
                        
                        Console.WriteLine($"[DEBUG DropStepExecutor] Parsed vertexId={vertexId}, propertyKey={propertyKey}");
                        
                        // Get the vertex and remove the property
                        var vertex = Database.GetVertex(vertexId);
                        if (vertex != null)
                        {
                            // Remove property from vertex
                            Database.RemoveVertexProperty(vertexId, propertyKey);
                            Console.WriteLine($"[DEBUG DropStepExecutor] Property removed successfully from vertex");
                            return;
                        }
                        
                        // Try as edge
                        var edge = Database.GetEdge(vertexId);
                        if (edge != null)
                        {
                            Database.RemoveEdgeProperty(vertexId, propertyKey);
                            Console.WriteLine($"[DEBUG DropStepExecutor] Edge property removed successfully");
                            return;
                        }
                        
                        Console.WriteLine($"[DEBUG DropStepExecutor] Could not find vertex or edge with id={vertexId}");
                    }
                }
                else if (key != null)
                {
                    Console.WriteLine($"[DEBUG DropStepExecutor] Property id doesn't contain underscore, cannot determine parent element");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG DropStepExecutor] Error dropping property: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if the value structure represents an edge
        /// </summary>
        private bool IsEdgeStructure(dynamic value)
        {
            if (value == null)
                return false;

            try
            {
                // Check for edge-specific properties
                if (value is Core.GremlinResponseObject responseObj)
                {
                    var type = responseObj.Get<string>("type");
                    if (type == "edge")
                        return true;
                        
                    // Check for inV/outV properties which indicate an edge
                    var hasInV = responseObj.Get<object>("inV") != null;
                    var hasOutV = responseObj.Get<object>("outV") != null;
                    return hasInV || hasOutV;
                }

                // Check dynamic object
                try
                {
                    var dynamicValue = (dynamic)value;
                    if (dynamicValue.inV != null || dynamicValue.outV != null)
                        return true;
                    if (dynamicValue.type == "edge")
                        return true;
                }
                catch
                {
                    // Ignore
                }
            }
            catch
            {
                // If we can't determine, return false
            }

            return false;
        }
    }
}
