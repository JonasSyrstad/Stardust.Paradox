using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the values() step which extracts property values from elements.
    /// 
    /// Behavior:
    /// - values(): Returns all property values from the element
    /// - values('key1', 'key2', ...): Returns only the specified property values
    /// 
    /// Example:
    /// g.V().values() - all property values of all vertices
    /// g.V().values('name') - only name property values
    /// </summary>
    [UsedImplicitly]
    public class ValuesStepExecutor : StepExecutorBase
    {
        public ValuesStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "values";

        public override string StepDescription => 
            "Extracts property values from elements. " +
            "values() returns all property values. " +
            "values('key1', 'key2') returns only the specified property values.";

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
                        // Return specific property values
                        foreach (var key in propertyKeys)
                        {
                            if (properties.TryGetValue(key, out object value))
                            {
                                var newTraverser = traverser.Split();
                                newTraverser.Value = value;
                                newTraversers.Add(newTraverser);
                            }
                        }
                    }
                    else
                    {
                        // Return all property values
                        foreach (var value in properties.Values)
                        {
                            var newTraverser = traverser.Split();
                            newTraverser.Value = value;
                            newTraversers.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
