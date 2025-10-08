using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the outV() step which navigates from edges to their outgoing (source) vertices.
    /// 
    /// Behavior:
    /// - outV(): From an edge, navigate to the source vertex (outV)
    /// 
    /// This step only works when the current traverser value is an edge.
    /// </summary>
    [UsedImplicitly]
    public class OutVStepExecutor : StepExecutorBase
    {
        public OutVStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "outv";

        public override string StepDescription => 
            "Navigates from edges to their source vertices (outV). " +
            "This step retrieves the vertex at the outgoing end of an edge.";

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
                        var vertex = Database.GetVertex(edge.OutVertexId);
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
