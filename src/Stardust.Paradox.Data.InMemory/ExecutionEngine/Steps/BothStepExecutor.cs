using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the both() step which traverses both incoming and outgoing edges from vertices.
    /// 
    /// Behavior:
    /// - both(): Traverse all edges (both incoming and outgoing) regardless of label
    /// - both('label'): Traverse only edges with the specified label in both directions
    /// 
    /// This step navigates from vertices to their adjacent vertices via edges in both directions.
    /// </summary>
    [UsedImplicitly]
    public class BothStepExecutor : StepExecutorBase
    {
        public BothStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "both";

        public override string StepDescription => 
            "Traverses from vertices to adjacent vertices via edges in both directions. " +
            "both() traverses all edges. " +
            "both('label') traverses only edges with the specified label.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var bothVertices = Database.GetBothVertices(vertexId, edgeLabel);
                    foreach (var vertex in bothVertices)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = vertex.ToGremlinResponse();
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
