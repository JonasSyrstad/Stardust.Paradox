using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the addV() step which adds a new vertex to the graph.
    /// 
    /// Behavior:
    /// - addV(label): Creates a new vertex with the specified label
    /// - addV(): Creates a new vertex with default "vertex" label
    /// 
    /// This step creates a new vertex and replaces current traversers with it.
    /// </summary>
    [UsedImplicitly]
    public class AddVStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public AddVStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "addv";

        public string StepDescription => 
            "Adds a new vertex to the graph. " +
            "addV(label) creates a vertex with the specified label. " +
            "addV() creates a vertex with the default 'vertex' label.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Get the label from arguments, default to "vertex"
            var label = step.Arguments.FirstOrDefault()?.ToString() ?? "vertex";
            
            // Create the new vertex
            var vertex = _database.AddVertex(label);
            
            // Replace all current traversers with the new vertex
            var newTraversers = new List<Traverser>();
            
            if (context.Traversers.Any())
            {
                // If there are existing traversers, create one new traverser per existing traverser
                foreach (var existingTraverser in context.Traversers)
                {
                    var newTraverser = existingTraverser.Split();
                    newTraverser.Value = vertex.ToGremlinResponse();
                    
                    // Add to path for path tracking
                    newTraverser.AddToPath(vertex.ToGremlinResponse());
                    
                    newTraversers.Add(newTraverser);
                }
            }
            else
            {
                // If no existing traversers (e.g., this is a start step), create a new one
                var traverser = new Traverser(vertex.ToGremlinResponse());
                traverser.AddToPath(vertex.ToGremlinResponse());
                newTraversers.Add(traverser);
            }
            
            context.Traversers = newTraversers;
        }
    }
}
