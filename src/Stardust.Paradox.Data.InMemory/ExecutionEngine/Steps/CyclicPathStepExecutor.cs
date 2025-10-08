using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the cyclicPath() step which filters for traversers with cyclic paths.
    /// 
    /// Behavior:
    /// - cyclicPath(): Keeps only traversers whose path repeats elements
    /// 
    /// This is the opposite of simplePath().
    /// </summary>
    [UsedImplicitly]
    public class CyclicPathStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public CyclicPathStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "cyclicpath";

        public string StepDescription => 
            "Filters for traversers with cyclic paths. " +
            "cyclicPath() keeps only paths with repeated elements.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var result = new List<Traverser>();
            
            foreach (var traverser in context.Traversers)
            {
                // Check if the path has any repeated elements
                var path = traverser.Path;
                var seenElements = new HashSet<object>();
                bool hasCycle = false;
                
                foreach (var element in path)
                {
                    // Extract ID for comparison if it's a graph element
                    var elementId = GetElementId(element);
                    
                    if (seenElements.Contains(elementId))
                    {
                        hasCycle = true;
                        break;
                    }
                    
                    seenElements.Add(elementId);
                }
                
                // Only keep traversers with cyclic paths
                if (hasCycle)
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
