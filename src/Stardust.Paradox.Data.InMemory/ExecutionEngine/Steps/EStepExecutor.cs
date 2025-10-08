using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the E() step which filters or replaces current traversers with specific edges.
    /// This step can be used in mid-traversal to navigate to specific edges by ID.
    /// 
    /// Behavior:
    /// - E() without arguments: Gets all edges from the graph
    /// - E(id1, id2, ...): Gets specific edges by their IDs
    /// 
    /// This step replaces current traversers rather than filtering them.
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
            if (step.Arguments.Any())
            {
                // E(id1, id2, ...) - get specific edges by ID
                var edgeIds = step.Arguments.Select(arg => arg.ToString()).ToList();
                var newTraversers = new List<Traverser>();
                
                foreach (var edgeId in edgeIds)
                {
                    var edge = _database.GetEdge(edgeId);
                    if (edge != null)
                    {
                        // For each existing traverser, create a new one with the specified edge
                        foreach (var existingTraverser in context.Traversers)
                        {
                            var newTraverser = existingTraverser.Split();
                            newTraverser.Value = edge.ToGremlinResponse();
                            
                            // Add to path for path tracking
                            newTraverser.AddToPath(edge.ToGremlinResponse());
                            
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
                
                context.Traversers = newTraversers;
            }
            else
            {
                // E() - get all edges (replace current traversers)
                var allEdges = _database.GetAllEdges().Select(e => e.ToGremlinResponse()).ToList();
                var newTraversers = new List<Traverser>();
                
                foreach (var existingTraverser in context.Traversers)
                {
                    foreach (var edge in allEdges)
                    {
                        var newTraverser = existingTraverser.Split();
                        newTraverser.Value = edge;
                        
                        // Add to path for path tracking
                        newTraverser.AddToPath(edge);
                        
                        newTraversers.Add(newTraverser);
                    }
                }
                
                context.Traversers = newTraversers;
            }
        }
    }
}
