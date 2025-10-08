using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the value() step which extracts the value from a property.
    /// Similar to values() but returns a single value instead of all values.
    /// 
    /// Behavior:
    /// - value(): Extracts the value from a single property object
    /// </summary>
    [UsedImplicitly]
    public class ValueStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public ValueStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "value";

        public string StepDescription => 
            "Extracts the value from a property object. " +
            "value() returns the value of a property element.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var result = new List<Traverser>();
            
            foreach (var traverser in context.Traversers)
            {
                var current = traverser.Value;
                
                // Extract value from property object
                if (current is JObject jObj)
                {
                    if (jObj.TryGetValue("value", out var valueToken))
                    {
                        traverser.Value = valueToken.ToObject<object>();
                        traverser.AddToPath(traverser.Value);
                        result.Add(traverser);
                    }
                }
                else if (current is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("value", out var value))
                    {
                        traverser.Value = value;
                        traverser.AddToPath(value);
                        result.Add(traverser);
                    }
                }
                else
                {
                    // If it's not a property object, keep the current value
                    result.Add(traverser);
                }
            }
            
            context.Traversers = result;
        }
    }
}
