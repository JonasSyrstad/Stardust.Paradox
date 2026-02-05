using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the E() step which filters or replaces current traversers with specific edges.
    /// This step can be used as a start step or in mid-traversal to navigate to specific edges by ID.
    /// 
    /// Behavior:
    /// - E() without arguments: Gets all edges from the graph
    /// - E(id1, id2, ...): Gets specific edges by their IDs
    /// 
    /// When used as a start step (no existing traversers), it initializes the traversal.
    /// When used mid-traversal, it replaces current traversers with specified edges.
    /// </summary>
    [UsedImplicitly]
    public class EStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public EStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "e";

        public string StepDescription =>
            "Navigates to specific edges by ID or gets all edges. " +
            "E(id1, id2, ...) retrieves edges with the given IDs. " +
            "E() without arguments retrieves all edges in the graph.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Check if this is a start step (no existing traversers)
            bool isStartStep = !context.Traversers.Any();

            if (step.Arguments.Any())
            {
                // E(id1, id2, ...) - get specific edges by ID
                var edgeIds = step.Arguments.Select(arg => arg.ToString()).ToList();

                var newTraversers = new List<Traverser>();

                foreach (var edgeId in edgeIds)
                {
                    var edge = _database.GetEdge(edgeId);
                    if (edge == null)
                    {
                        edge = _database.GetAllEdges().FirstOrDefault(e =>
                            e.Id != null && e.Id.Equals(edgeId, System.StringComparison.OrdinalIgnoreCase));
                    }

                    if (edge != null)
                    {
                        if (isStartStep)
                        {
                            // Start step: Create initial traverser for this edge
                            var newTraverser = new Traverser(edge.ToGremlinResponse());
                            newTraverser.AddToPath(edge.ToGremlinResponse());
                            newTraversers.Add(newTraverser);
                        }
                        else
                        {
                            // Mid-traversal: Create new traverser for each existing traverser
                            foreach (var existingTraverser in context.Traversers)
                            {
                                var newTraverser = existingTraverser.Split();
                                newTraverser.Value = edge.ToGremlinResponse();
                                newTraverser.AddToPath(edge.ToGremlinResponse());
                                newTraversers.Add(newTraverser);
                            }
                        }
                    }
                }

                context.Traversers = newTraversers;
            }
            else
            {
                // E() - get all edges
                var allEdges = _database.GetAllEdges().Select(e => e.ToGremlinResponse()).ToList();

                var newTraversers = new List<Traverser>();

                if (isStartStep)
                {
                    // Start step: Create initial traversers for all edges
                    foreach (var edge in allEdges)
                    {
                        var newTraverser = new Traverser(edge);
                        newTraverser.AddToPath(edge);
                        newTraversers.Add(newTraverser);
                    }
                }
                else
                {
                    // Mid-traversal: Replace current traversers with all edges
                    foreach (var existingTraverser in context.Traversers)
                    {
                        foreach (var edge in allEdges)
                        {
                            var newTraverser = existingTraverser.Split();
                            newTraverser.Value = edge;
                            newTraverser.AddToPath(edge);
                            newTraversers.Add(newTraverser);
                        }
                    }
                }

                context.Traversers = newTraversers;
            }
        }
    }
}
