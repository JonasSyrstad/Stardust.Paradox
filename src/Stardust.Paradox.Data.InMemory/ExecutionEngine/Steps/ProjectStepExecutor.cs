using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the project() step which creates a projection map with specified keys.
    /// 
    /// Behavior:
    /// - project(key1, key2, ...): Creates map with specified keys
    /// - Followed by .by() steps to define the values for each key
    /// 
    /// This is used for custom property projections.
    /// </summary>
    [UsedImplicitly]
    public class ProjectStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public ProjectStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "project";

        public string StepDescription => 
            "Creates a projection map with specified keys. " +
            "project(key1, key2, ...) followed by .by() steps defines values.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                return; // No projection keys specified
            }
            
            // Get the projection keys
            var keys = step.Arguments.Select(arg => arg.ToString()).ToList();
            
            // Store keys for subsequent .by() steps to use
            context.SetMetadata("project_keys", keys);
            context.SetMetadata("project_values", new List<object>());
            
            // Create projection maps for each traverser
            var result = new List<Traverser>();
            
            foreach (var traverser in context.Traversers)
            {
                // Create a new map with the specified keys
                var projectionMap = new Dictionary<string, object>();
                
                // Initialize with null values (will be filled by .by() steps)
                foreach (var key in keys)
                {
                    projectionMap[key] = null;
                }
                
                traverser.Value = projectionMap;
                traverser.AddToPath(projectionMap);
                result.Add(traverser);
            }
            
            context.Traversers = result;
        }
    }
}
