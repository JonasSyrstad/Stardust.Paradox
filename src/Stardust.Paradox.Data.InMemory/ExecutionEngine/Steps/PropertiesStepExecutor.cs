using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the properties() step which extracts property objects from elements.
    /// 
    /// Behavior:
    /// - properties(): Returns all property objects from the element
    /// - properties('key1', 'key2', ...): Returns only the specified property objects
    /// 
    /// Each property is returned as an object with: key, value, id, label
    /// 
    /// Example:
    /// g.V().properties() - all properties of all vertices
    /// g.V().properties('name', 'age') - only name and age properties
    /// </summary>
    [UsedImplicitly]
    public class PropertiesStepExecutor : StepExecutorBase
    {
        public PropertiesStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "properties";

        public override string StepDescription => 
            "Extracts property objects from elements. " +
            "properties() returns all properties. " +
            "properties('key1', 'key2') returns only the specified properties. " +
            "Each property is returned as an object with key, value, id, and label.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var propertyKeys = step.Arguments.Any()
                ? step.Arguments.Select(arg => arg.ToString()).ToList()
                : new List<string>();

            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var properties = ExtractProperties(traverser.Value);
                if (properties != null)
                {
                    if (propertyKeys.Any())
                    {
                        // Return only requested properties as property objects
                        foreach (var key in propertyKeys)
                        {
                            if (properties.ContainsKey(key))
                            {
                                // Create a property-like object with key-value info
                                var propertyObj = new
                                {
                                    key = key,
                                    value = properties[key],
                                    id = $"{ExtractId(traverser.Value)}_{key}",
                                    label = key
                                };

                                var newTraverser = traverser.Split();
                                newTraverser.Value = propertyObj;
                                newTraversers.Add(newTraverser);
                            }
                        }
                    }
                    else
                    {
                        // Return all properties as property objects
                        foreach (var prop in properties)
                        {
                            var propertyObj = new
                            {
                                key = prop.Key,
                                value = prop.Value,
                                id = $"{ExtractId(traverser.Value)}_{prop.Key}",
                                label = prop.Key
                            };

                            var newTraverser = traverser.Split();
                            newTraverser.Value = propertyObj;
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
