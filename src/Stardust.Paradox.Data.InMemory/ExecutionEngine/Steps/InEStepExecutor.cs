using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the inE() step which traverses to incoming edges to vertices.
    /// 
    /// Behavior:
    /// - inE(): Get all incoming edges regardless of label
    /// - inE('label'): Get only incoming edges with the specified label
    /// 
    /// This step navigates from vertices to their incoming edges.
    /// </summary>
    [UsedImplicitly]
    public class InEStepExecutor : StepExecutorBase
    {
        public InEStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "ine";

        public override string StepDescription => 
            "Navigates from vertices to their incoming edges. " +
            "inE() gets all incoming edges. " +
            "inE('label') gets only edges with the specified label.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var inEdges = Database.GetInEdges(vertexId, edgeLabel);
                    foreach (var edge in inEdges)
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
