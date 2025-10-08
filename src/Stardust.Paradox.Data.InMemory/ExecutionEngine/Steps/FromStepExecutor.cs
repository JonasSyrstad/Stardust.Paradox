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
    /// - from(vertexId): Specifies source vertex by ID
    /// - Must be used with addE()
    /// 
    /// Example:
    /// g.V('v1').as('a').V('v2').addE('knows').from('a')
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
            "from('label') uses a labeled vertex, from(id) uses a vertex ID.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // 'from' step is a modulator for addE step - it specifies the source vertex for edge creation
            var labelOrId = step.GetFirstStringArgument();

            // Store the from specification in the context for use by addE
            context.SetMetadata("addE_from", labelOrId);

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
                        var edge = Database.AddEdge(label, fromVertexId, toVertexId);
                        if (edge != null)
                        {
                            // Apply properties
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
