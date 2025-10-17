using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the to() modulator for edge creation with addE().
    /// 
    /// Behavior:
    /// - to('label'): Specifies target vertex by label reference
    /// - to(vertexId): Specifies target vertex by ID
    /// - Must be used with addE()
    /// 
    /// Example:
    /// g.V('v1').as('a').V('v2').addE('knows').to('a')
    /// </summary>
    [UsedImplicitly]
    public class ToStepExecutor : StepExecutorBase
    {
        public ToStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "to";

        public override string StepDescription => 
            "Modulator for addE() that specifies the target vertex for edge creation. " +
            "to('label') uses a labeled vertex, to(id) uses a vertex ID.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // 'to' step is a modulator for addE step - it specifies the target vertex for edge creation
            var labelOrId = step.GetFirstStringArgument();

            // Store the to specification in the context for use by addE
            context.SetMetadata("addE_to", labelOrId);

            // Check if we have all needed parts to execute the edge creation
            TryExecutePendingAddE(context);
        }

        private void TryExecutePendingAddE(TinkerTraversalContext context)
        {
            // Only execute if we have a pending addE and both from and to specifications
            if (!context.HasMetadata("addE_pending") ||
                !context.HasMetadata("addE_from") ||
                !context.HasMetadata("addE_to"))
            {
                return;
            }

            var label = context.GetMetadata<string>("addE_label");
            var fromSpec = context.GetMetadata<string>("addE_from");
            var toSpec = context.GetMetadata<string>("addE_to");
            var properties = context.GetMetadata<Dictionary<string, object>>("addE_properties") ?? new Dictionary<string, object>();

            // Extract the 'id' property if present - it should be used as the edge ID
            string edgeId = null;
            if (properties.ContainsKey("id"))
            {
                edgeId = properties["id"]?.ToString();
                // Remove from properties dictionary since it's used as the ID, not a property
                properties.Remove("id");
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                // Resolve from and to vertices
                string fromVertexId = null;
                string toVertexId = null;

                // Resolve fromSpec
                if (!string.IsNullOrEmpty(fromSpec))
                {
                    // Check if it's a label reference
                    var fromVertex = traverser.GetTagged<dynamic>(fromSpec);
                    if (fromVertex != null)
                    {
                        fromVertexId = ExtractId(fromVertex);
                    }
                    else
                    {
                        // Assume it's a direct vertex ID
                        fromVertexId = fromSpec;
                    }
                }

                // Resolve toSpec
                if (!string.IsNullOrEmpty(toSpec))
                {
                    // Check if it's a label reference
                    var toVertex = traverser.GetTagged<dynamic>(toSpec);
                    if (toVertex != null)
                    {
                        toVertexId = ExtractId(toVertex);
                    }
                    else
                    {
                        // Assume it's a direct vertex ID
                        toVertexId = toSpec;
                    }
                }

                // Create the edge if we have both vertices
                if (!string.IsNullOrEmpty(fromVertexId) && !string.IsNullOrEmpty(toVertexId))
                {
                    var fromVertexObj = Database.GetVertex(fromVertexId);
                    var toVertexObj = Database.GetVertex(toVertexId);

                    if (fromVertexObj != null && toVertexObj != null)
                    {
                        // Use the provided edge ID if available, otherwise let database generate one
                        var edge = Database.AddEdge(label, fromVertexId, toVertexId, edgeId);
                        if (edge != null)
                        {
                            // Apply remaining properties (excluding 'id' which was already handled)
                            foreach (var prop in properties)
                            {
                                edge.SetProperty(prop.Key, prop.Value);
                            }

                            var newTraverser = traverser.Split();
                            newTraverser.Value = edge.ToGremlinResponse();
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            if (newTraversers.Any())
            {
                context.Traversers = newTraversers;
            }

            // Clear the metadata after use
            context.RemoveMetadata("addE_pending");
            context.RemoveMetadata("addE_label");
            context.RemoveMetadata("addE_from");
            context.RemoveMetadata("addE_to");
            context.RemoveMetadata("addE_properties");
        }
    }
}
