using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the property() step which adds or updates a property on elements.
    /// 
    /// Behavior:
    /// - property('key', value): Sets property on current elements
    /// - Can be used with addE() to add edge properties
    /// - Updates the database directly and maintains property indices
    /// 
    /// Example:
    /// g.V('1').property('age', 30) - sets age property to 30
    /// g.addE('knows').from('v1').to('v2').property('since', 2020)
    /// </summary>
    [UsedImplicitly]
    public class PropertyStepExecutor : StepExecutorBase
    {
        public PropertyStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "property";

        public override string StepDescription => 
            "Adds or updates a property on elements. " +
            "property('key', value) sets the property on current elements and updates indices.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count >= 2)
            {
                var key = step.Arguments[0].ToString();
                var value = step.Arguments[1];

                // Check if this is a property step for a pending addE operation
                if (context.HasMetadata("addE_pending"))
                {
                    // Add property to the pending addE operation
                    var existingProperties = context.GetMetadata<Dictionary<string, object>>("addE_properties") ?? new Dictionary<string, object>();
                    existingProperties[key] = value;
                    context.SetMetadata("addE_properties", existingProperties);

                    // Don't change traversers yet - wait for modulators
                    return;
                }

                // Normal property step execution
                var newTraversers = new List<Traverser>();

                foreach (var traverser in context.Traversers)
                {
                    var newTraverser = traverser.Split();

                    // Try to update the actual database object
                    var id = ExtractId(traverser.Value);
                    if (!string.IsNullOrEmpty(id))
                    {
                        var vertex = Database.GetVertex(id);
                        if (vertex != null)
                        {
                            vertex.SetProperty(key, value);
                            // Update vertex property index
                            Database.UpdateVertexPropertyIndexForProperty(vertex.Id, key, value);
                            newTraverser.Value = vertex.ToGremlinResponse();
                        }
                        else
                        {
                            var edge = Database.GetEdge(id);
                            if (edge != null)
                            {
                                // Special handling for 'id' property on edges
                                // When property('id', newId) is called on a newly created edge,
                                // we need to recreate the edge with the new ID
                                if (key == "id" && value != null)
                                {
                                    var newId = value.ToString();
                                    if (newId != id)
                                    {
                                        // Create new edge with specified ID
                                        var newEdge = Database.AddEdge(
                                            edge.Label,
                                            edge.OutVertexId,
                                            edge.InVertexId,
                                            newId);

                                        if (newEdge != null)
                                        {
                                            // Copy all properties from old edge to new edge
                                            foreach (var prop in edge.Properties)
                                            {
                                                newEdge.SetProperty(prop.Key, prop.Value);
                                            }

                                            // Remove the old edge
                                            Database.RemoveEdge(id);

                                            // Update the traverser with the new edge
                                            newTraverser.Value = newEdge.ToGremlinResponse();
                                        }
                                        else
                                        {
                                            // If we can't create the new edge, keep the old one
                                            newTraverser.Value = edge.ToGremlinResponse();
                                        }
                                    }
                                    else
                                    {
                                        // ID is the same, just return the edge
                                        newTraverser.Value = edge.ToGremlinResponse();
                                    }
                                }
                                else
                                {
                                    // Normal property update - update property and index
                                    edge.SetProperty(key, value);
                                    // CRITICAL FIX: Update edge property index
                                    Database.UpdateEdgePropertyIndexForProperty(edge.Id, key, value);
                                    newTraverser.Value = edge.ToGremlinResponse();
                                }
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
    }
}
