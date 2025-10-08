using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the simplePath() step which filters out traversers with cyclic paths.
    /// 
    /// Behavior:
    /// - simplePath(): Keeps only traversers whose path doesn't repeat elements
    /// 
    /// This is the opposite of cyclicPath().
    /// </summary>
    [UsedImplicitly]
    public class SimplePathStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public SimplePathStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "simplepath";

        public string StepDescription => 
            "Filters out traversers with cyclic paths. " +
            "simplePath() keeps only paths without repeated elements.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var result = new List<Traverser>();
            
            foreach (var traverser in context.Traversers)
            {
                // Check if the path has any repeated elements
                var path = traverser.Path;
                var seenElements = new HashSet<object>();
                bool isSimplePath = true;
                
                foreach (var element in path)
                {
                    // Extract ID for comparison if it's a graph element
                    var elementId = GetElementId(element);
                    
                    if (seenElements.Contains(elementId))
                    {
                        isSimplePath = false;
                        break;
                    }
                    
                    seenElements.Add(elementId);
                }
                
                // Only keep traversers with simple (non-cyclic) paths
                if (isSimplePath)
                {
                    result.Add(traverser);
                }
            }
            
            context.Traversers = result;
        }
        
        private object GetElementId(object element)
        {
            // Try to extract ID from graph elements
            if (element is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue("id", out var id))
                {
                    return id;
                }
            }
            
            // For non-graph elements, use the element itself
            return element;
        }
    }
}
