using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the path() step which returns the path taken through the graph.
    /// 
    /// Behavior:
    /// - Returns the complete path of elements traversed to reach the current position
    /// - Each path is a list of elements visited
    /// 
    /// Example:
    /// g.V('1').out().out().path() - returns paths showing how we reached each vertex
    /// </summary>
    [UsedImplicitly]
    public class PathStepExecutor : StepExecutorBase
    {
        public PathStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "path";

        public override string StepDescription => 
            "Returns the complete path taken through the graph to reach each element. " +
            "Each result is a list showing all traversed elements.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var newTraverser = traverser.Split();

                // Get the path from the traverser
                var path = traverser.GetPath();

                // If path is empty, at least include the current value
                if (!path.Any())
                {
                    path = new List<dynamic> { traverser.Value };
                }

                // Set the path directly as the traverser value (not wrapped)
                newTraverser.Value = path;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }
}
