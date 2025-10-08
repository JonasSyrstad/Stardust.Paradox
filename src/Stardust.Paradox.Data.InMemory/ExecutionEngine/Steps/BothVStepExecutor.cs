using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the bothV() step which navigates from edges to both their source and target vertices.
    /// 
    /// Behavior:
    /// - bothV(): From an edge, navigate to both the source vertex (outV) and target vertex (inV)
    /// 
    /// This step only works when the current traverser value is an edge.
    /// </summary>
    [UsedImplicitly]
    public class BothVStepExecutor : StepExecutorBase
    {
        public BothVStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "bothv";

        public override string StepDescription => 
            "Navigates from edges to both their source and target vertices (bothV). " +
            "This step retrieves both vertices connected by an edge.";

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
                        // Add both the outV and inV vertices
                        var outVertex = Database.GetVertex(edge.OutVertexId);
                        if (outVertex != null)
                        {
                            var outTraverser = traverser.Split();
                            outTraverser.Value = outVertex.ToGremlinResponse();
                            newTraversers.Add(outTraverser);
                        }

                        var inVertex = Database.GetVertex(edge.InVertexId);
                        if (inVertex != null)
                        {
                            var inTraverser = traverser.Split();
                            inTraverser.Value = inVertex.ToGremlinResponse();
                            newTraversers.Add(inTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
