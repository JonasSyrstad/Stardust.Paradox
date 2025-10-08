using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the valueMap() step which creates a map of property key-value pairs.
    /// 
    /// Behavior:
    /// - valueMap(): Returns a map of all properties
    /// - valueMap('key1', 'key2', ...): Returns a map of only the specified properties
    /// 
    /// Example:
    /// g.V().valueMap() - map of all properties for each vertex
    /// g.V().valueMap('name', 'age') - map containing only name and age properties
    /// </summary>
    [UsedImplicitly]
    public class ValueMapStepExecutor : StepExecutorBase
    {
        public ValueMapStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "valuemap";

        public override string StepDescription => 
            "Creates a map of property key-value pairs. " +
            "valueMap() returns all properties as a map. " +
            "valueMap('key1', 'key2') returns only the specified properties as a map.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var properties = ExtractProperties(traverser.Value);
                var newTraverser = traverser.Split();

                // If specific property keys are requested, filter the properties
                if (step.Arguments.Any())
                {
                    var requestedKeys = step.Arguments.Select(arg => arg.ToString()).ToHashSet();
                    var filteredProperties = new Dictionary<string, object>();

                    foreach (var kvp in properties ?? new Dictionary<string, object>())
                    {
                        if (requestedKeys.Contains(kvp.Key))
                        {
                            filteredProperties[kvp.Key] = kvp.Value;
                        }
                    }

                    newTraverser.Value = filteredProperties;
                }
                else
                {
                    // Return all properties if no specific keys requested
                    newTraverser.Value = properties ?? new Dictionary<string, object>();
                }

                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }
}
