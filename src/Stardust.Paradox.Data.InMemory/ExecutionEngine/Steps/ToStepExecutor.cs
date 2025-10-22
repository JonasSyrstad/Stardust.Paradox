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
    /// - to(V('id')): Uses current traverser as source, creates edge to specified vertex
    /// - Must be used with addE()
    /// 
    /// Example:
    /// g.V('v1').as('a').V('v2').addE('knows').to('a')
    /// g.V('v1').addE('knows').to(V('v2'))
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
            "to('label') uses a labeled vertex, to(V('id')) uses direct vertex ID.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // 'to' step is a modulator for addE step - it specifies the target vertex for edge creation
            var argument = step.GetFirstStringArgument();

            // Store the raw to specification in the context for immediate resolution
            context.SetMetadata("addE_to_spec", argument);

            // Try to resolve the vertex ID now
            string toVertexId = ResolveVertexId(argument, context);
            
            if (!string.IsNullOrEmpty(toVertexId))
            {
                context.SetMetadata("addE_to", toVertexId);
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
            // For addE().from().to() pattern, we need both
            if (!context.HasMetadata("addE_pending"))
            {
                return;
            }

            // We need either both from and to, or just to (where from is implicit from current traverser)
            string fromVertexId = context.GetMetadata<string>("addE_from");
            string toVertexId = context.GetMetadata<string>("addE_to");

            // If we don't have to yet, we can't execute
            if (string.IsNullOrEmpty(toVertexId))
            {
                return;
            }

            var label = context.GetMetadata<string>("addE_label");
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
                // If from is not specified, use current traverser as source
                if (string.IsNullOrEmpty(fromVertexId))
                {
                    fromVertexId = ExtractId(traverser.Value);
                }

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
