using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the otherV() step which navigates from edges to the "other" vertex.
    /// For an outgoing edge, otherV returns the inV (target).
    /// For an incoming edge, otherV returns the outV (source).
    /// 
    /// Behavior:
    /// - otherV(): From an edge, navigate to the vertex at the other end
    /// 
    /// This step returns the vertex at the "other end" of the edge.
    /// By default, it returns the inV (target vertex) for outgoing edges.
    /// </summary>
    [UsedImplicitly]
    public class OtherVStepExecutor : StepExecutorBase
    {
        public OtherVStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "otherv";

        public override string StepDescription => 
            "Navigates from edges to the vertex at the other end. " +
            "Returns the inV (target) for outgoing edges, or outV (source) for incoming edges.";

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
                        // For otherV, we return the inV (target vertex) by default for outgoing edges
                        var vertex = Database.GetVertex(edge.InVertexId);
                        if (vertex != null)
                        {
                            var newTraverser = traverser.Split();
                            newTraverser.Value = vertex.ToGremlinResponse();

                            // Add to path for path tracking
                            newTraverser.AddToPath(vertex.ToGremlinResponse());

                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
