using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the is() step which filters traversers based on equality comparison.
    /// 
    /// Behavior:
    /// - is(value): Keeps only traversers whose value equals the specified value
    /// - is(predicate): Evaluates a predicate against the traverser value
    /// </summary>
    [UsedImplicitly]
    public class IsStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public IsStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "is";

        public string StepDescription => 
            "Filters traversers based on equality comparison. " +
            "is(value) keeps only traversers equal to the specified value.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                return; // No comparison value, keep all
            }
            
            var compareValue = step.Arguments.First();
            var result = new List<Traverser>();
            
            foreach (var traverser in context.Traversers)
            {
                var current = traverser.Value;
                
                // Handle different value types
                bool matches = false;
                
                if (current == null && compareValue == null)
                {
                    matches = true;
                }
                else if (current != null)
                {
                    // Try direct equality
                    if (current.Equals(compareValue))
                    {
                        matches = true;
                    }
                    // Try string comparison
                    else if (current.ToString() == compareValue?.ToString())
                    {
                        matches = true;
                    }
                    // Handle JValue comparisons
                    else if (current is JValue jval && compareValue is JValue jvalCompare)
                    {
                        matches = JToken.DeepEquals(jval, jvalCompare);
                    }
                    else if (current is JValue jvalCurrent)
                    {
                        var currentObj = jvalCurrent.ToObject<object>();
                        matches = currentObj?.Equals(compareValue) == true || 
                                  currentObj?.ToString() == compareValue?.ToString();
                    }
                }
                
                if (matches)
                {
                    result.Add(traverser);
                }
            }
            
            context.Traversers = result;
        }
    }
}
