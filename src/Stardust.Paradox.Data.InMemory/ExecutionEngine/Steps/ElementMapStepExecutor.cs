using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the elementMap() step which creates a comprehensive map including element metadata and properties.
    /// 
    /// Behavior:
    /// - Returns a flattened map with: id, label, type, and all properties
    /// - Properties are merged directly into the map (not nested)
    /// 
    /// Example:
    /// g.V().elementMap() - returns {id: 'v1', label: 'person', type: 'vertex', name: 'John', age: 30}
    /// </summary>
    [UsedImplicitly]
    public class ElementMapStepExecutor : StepExecutorBase
    {
        public ElementMapStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "elementmap";

        public override string StepDescription => 
            "Creates a comprehensive map including element metadata (id, label, type) and all properties. " +
            "Properties are flattened into the map directly.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var elementMap = new Dictionary<string, object>();

                // Extract basic element information
                var id = ExtractId(traverser.Value);
                var label = ExtractLabel(traverser.Value);
                var type = ExtractType(traverser.Value);
                var properties = ExtractProperties(traverser.Value);

                // Add core element metadata
                elementMap["id"] = id;
                elementMap["label"] = label;
                elementMap["type"] = type;

                // Add all properties directly to the element map (flattened)
                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        elementMap[prop.Key] = prop.Value;
                    }
                }

                var newTraverser = traverser.Split();
                newTraverser.Value = elementMap;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }
}
