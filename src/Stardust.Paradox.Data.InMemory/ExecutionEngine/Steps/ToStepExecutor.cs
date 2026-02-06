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

            // Always store raw spec; it may be a label like 'a'/'b' (from .as())
            context.SetMetadata("addE_to_spec", argument);

            // Only set addE_to when it is clearly a direct V('id') or direct id
            string toVertexId = ResolveVertexId(argument, context);
            if (!string.IsNullOrEmpty(toVertexId) && Database.GetVertex(toVertexId) != null)
            {
                context.SetMetadata("addE_to", toVertexId);
            }

            // Check if we have all needed parts to execute the edge creation
            TryExecutePendingAddE(context);
        }

        private void TryExecutePendingAddE(TinkerTraversalContext context)
        {
            if (!context.HasMetadata("addE_pending"))
            {
                return;
            }

            // Prefer raw specs (labels) when present
            var fromSpec = context.GetMetadata<string>("addE_from_spec");
            var toSpec = context.GetMetadata<string>("addE_to_spec");

            // If we don't even have a to-spec we can't execute
            if (string.IsNullOrEmpty(toSpec) && string.IsNullOrEmpty(context.GetMetadata<string>("addE_to")))
            {
                return;
            }

            var label = context.GetMetadata<string>("addE_label");
            var properties = context.GetMetadata<Dictionary<string, object>>("addE_properties") ?? new Dictionary<string, object>();

            // Extract the 'id' property if present - it should be used as the edge ID
            string edgeId = context.GetMetadata<string>("addE_edgeId");
            if (string.IsNullOrEmpty(edgeId) && properties.ContainsKey("id"))
            {
                edgeId = properties["id"]?.ToString();
                properties.Remove("id");
            }

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var fromVertexId = context.GetMetadata<string>("addE_from");
                var toVertexId = context.GetMetadata<string>("addE_to");

                // Resolve FROM
                if (!string.IsNullOrEmpty(fromSpec))
                {
                    // Try label first
                    var taggedFrom = traverser.GetTagged<dynamic>(fromSpec);
                    if (taggedFrom != null)
                        fromVertexId = ExtractId(taggedFrom);
                    else
                    {
                        // fall back to parsing V('id')/id
                        var parsed = ResolveVertexId(fromSpec, context);
                        if (!string.IsNullOrEmpty(parsed))
                            fromVertexId = parsed;
                    }
                }
                else if (string.IsNullOrEmpty(fromVertexId))
                {
                    fromVertexId = ExtractId(traverser.Value);
                }

                // Resolve TO
                if (!string.IsNullOrEmpty(toSpec))
                {
                    var taggedTo = traverser.GetTagged<dynamic>(toSpec);
                    if (taggedTo != null)
                        toVertexId = ExtractId(taggedTo);
                    else
                    {
                        var parsed = ResolveVertexId(toSpec, context);
                        if (!string.IsNullOrEmpty(parsed))
                            toVertexId = parsed;
                    }
                }

                // Verify both vertices exist
                var fromVertex = Database.GetVertex(fromVertexId);
                var toVertex = Database.GetVertex(toVertexId);

                if (fromVertex != null && toVertex != null)
                {
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
            context.RemoveMetadata("addE_edgeId");
        }

        private string ResolveVertexId(string specification, TinkerTraversalContext context)
        {
            if (string.IsNullOrEmpty(specification))
                return null;

            // Handle V('id') pattern
            if (specification.StartsWith("V(") && specification.EndsWith(")"))
            {
                var idPart = specification.Substring(2, specification.Length - 3).Trim();
                if ((idPart.StartsWith("'") && idPart.EndsWith("'")) ||
                    (idPart.StartsWith("\"") && idPart.EndsWith("\"")))
                {
                    return idPart.Substring(1, idPart.Length - 2);
                }
                return idPart;
            }

            // If we can resolve from labeled vertices in traversers, do so
            foreach (var traverser in context.Traversers)
            {
                var tagged = traverser.GetTagged<dynamic>(specification);
                if (tagged != null)
                {
                    var vertexId = ExtractId(tagged);
                    if (!string.IsNullOrEmpty(vertexId))
                        return vertexId;
                }
            }

            // Otherwise treat as direct id/label spec
            return specification;
        }
    }
}
