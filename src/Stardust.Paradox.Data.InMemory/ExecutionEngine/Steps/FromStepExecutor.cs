using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the from() modulator for edge creation with addE().
    /// 
    /// Behavior:
    /// - from('label'): Specifies source vertex by label reference
    /// - from(V('id')): Parses vertex ID from V() syntax
    /// - Must be used with addE()
    /// 
    /// Example:
    /// g.V('v1').as('a').V('v2').addE('knows').from('a')
    /// g.V('v2').addE('knows').from(V('v1'))
    /// </summary>
    [UsedImplicitly]
    public class FromStepExecutor : StepExecutorBase
    {
        public FromStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "from";

        public override string StepDescription => 
            "Modulator for addE() that specifies the source vertex for edge creation. " +
            "from('label') uses a labeled vertex, from(V('id')) parses direct vertex ID.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // 'from' step is a modulator for addE step - it specifies the source vertex for edge creation
            var argument = step.GetFirstStringArgument();

            // Store the raw from specification in the context for use by to() or for immediate execution
            context.SetMetadata("addE_from_spec", argument);

            // Try to resolve the vertex ID now
            string fromVertexId = ResolveVertexId(argument, context);
            
            if (!string.IsNullOrEmpty(fromVertexId))
            {
                context.SetMetadata("addE_from", fromVertexId);
            }

            // Check if we have all needed parts to execute the edge creation
            TryExecutePendingAddE(context);
        }

        private string ResolveVertexId(string specification, TinkerTraversalContext context)
        {
            if (string.IsNullOrEmpty(specification))
                return null;

            // Handle V('id') pattern
            if (specification.StartsWith("V(") && specification.EndsWith(")"))
            {
                // Extract the ID from V('id')
                var idPart = specification.Substring(2, specification.Length - 3).Trim();
                // Remove quotes if present
                if ((idPart.StartsWith("'") && idPart.EndsWith("'")) ||
                    (idPart.StartsWith("\"") && idPart.EndsWith("\"")))
                {
                    return idPart.Substring(1, idPart.Length - 2);
                }
                return idPart;
            }

            // Try to resolve from labeled vertices in all traversers
            foreach (var traverser in context.Traversers)
            {
                var tagged = traverser.GetTagged<dynamic>(specification);
                if (tagged != null)
                {
                    var vertexId = ExtractId(tagged);
                    if (!string.IsNullOrEmpty(vertexId))
                    {
                        return vertexId;
                    }
                }
            }

            // If we can't resolve it, assume it's a direct vertex ID
            return specification;
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
            var fromVertexId = context.GetMetadata<string>("addE_from");
            var toVertexId = context.GetMetadata<string>("addE_to");
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
                // Verify both vertices exist
                var fromVertex = Database.GetVertex(fromVertexId);
                var toVertex = Database.GetVertex(toVertexId);

                if (fromVertex != null && toVertex != null)
                {
                    // Use the properties overload to ensure indices are updated
                    var edge = Database.AddEdge(label, fromVertexId, toVertexId, properties, edgeId);
                    if (edge != null)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = edge.ToGremlinResponse();
                        newTraversers.Add(newTraverser);
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
            context.RemoveMetadata("addE_from_spec");
            context.RemoveMetadata("addE_to");
            context.RemoveMetadata("addE_to_spec");
            context.RemoveMetadata("addE_properties");
        }
    }
}
