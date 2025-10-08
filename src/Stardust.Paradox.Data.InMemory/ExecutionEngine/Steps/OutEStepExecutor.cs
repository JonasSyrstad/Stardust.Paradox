using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the outE() step which traverses to outgoing edges from vertices.
    /// 
    /// Behavior:
    /// - outE(): Get all outgoing edges regardless of label
    /// - outE('label'): Get only outgoing edges with the specified label
    /// 
    /// This step navigates from vertices to their outgoing edges.
    /// </summary>
    [UsedImplicitly]
    public class OutEStepExecutor : StepExecutorBase
    {
        public OutEStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "oute";

        public override string StepDescription => 
            "Navigates from vertices to their outgoing edges. " +
            "outE() gets all outgoing edges. " +
            "outE('label') gets only edges with the specified label.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var outEdges = Database.GetOutEdges(vertexId, edgeLabel);
                    foreach (var edge in outEdges)
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
