using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the inV() step which navigates from edges to their incoming (target) vertices.
    /// 
    /// Behavior:
    /// - inV(): From an edge, navigate to the target vertex (inV)
    /// 
    /// This step only works when the current traverser value is an edge.
    /// </summary>
    [UsedImplicitly]
    public class InVStepExecutor : StepExecutorBase
    {
        public InVStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "inv";

        public override string StepDescription => 
            "Navigates from edges to their target vertices (inV). " +
            "This step retrieves the vertex at the incoming end of an edge.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var edgeId = ExtractEdgeId(traverser.Value);
                if (!string.IsNullOrEmpty(edgeId))
                {
                    var edge = Database.GetEdge(edgeId);
                    if (edge != null)
                    {
                        var vertex = Database.GetVertex(edge.InVertexId);
                        if (vertex != null)
                        {
                            var newTraverser = traverser.Split();
                            newTraverser.Value = vertex.ToGremlinResponse();
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
