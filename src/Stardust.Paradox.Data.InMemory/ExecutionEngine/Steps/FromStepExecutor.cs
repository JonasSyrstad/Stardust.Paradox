using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
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
            var argument = step.GetFirstStringArgument();

            context.SetMetadata("addE_from_spec", argument);

            string fromVertexId = ResolveVertexId(argument, context);

            if (!string.IsNullOrEmpty(fromVertexId))
            {
                context.SetMetadata("addE_from", fromVertexId);
            }

            // Do not execute addE here; `to()` will materialize when target is known.
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
            // kept for backward compatibility but no-op
        }
    }
}
