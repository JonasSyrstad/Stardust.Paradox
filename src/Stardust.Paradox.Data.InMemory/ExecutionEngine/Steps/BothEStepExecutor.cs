using System.Collections.Generic;
using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the bothE() step which traverses to both incoming and outgoing edges from vertices.
    /// 
    /// Behavior:
    /// - bothE(): Get all edges (incoming and outgoing) regardless of label
    /// - bothE('label'): Get only edges with the specified label in both directions
    /// 
    /// This step navigates from vertices to their edges in both directions.
    /// </summary>
    [UsedImplicitly]
    public class BothEStepExecutor : StepExecutorBase
    {
        public BothEStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "bothe";

        public override string StepDescription => 
            "Navigates from vertices to their edges in both directions. " +
            "bothE() gets all edges. " +
            "bothE('label') gets only edges with the specified label.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var bothEdges = Database.GetBothEdges(vertexId, edgeLabel);
                    foreach (var edge in bothEdges)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = edge.ToGremlinResponse();
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
