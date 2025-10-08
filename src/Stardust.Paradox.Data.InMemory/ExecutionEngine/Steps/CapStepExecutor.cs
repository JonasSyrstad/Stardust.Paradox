using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the cap() step which retrieves a side-effect collection.
    /// 
    /// Behavior:
    /// - cap(sideEffectKey): Retrieves and returns the named side-effect collection
    /// - Terminates the traversal and returns the side-effect
    /// 
    /// Used with aggregate(), store(), or other side-effect steps.
    /// </summary>
    [UsedImplicitly]
    public class CapStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public CapStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "cap";

        public string StepDescription => 
            "Retrieves a side-effect collection. " +
            "cap(key) returns the named side-effect and terminates traversal.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                context.Traversers.Clear();
                return;
            }
            
            var sideEffectKey = step.Arguments.First().ToString();
            
            // Retrieve the side-effect collection
            var sideEffect = context.GetMetadata<object>(sideEffectKey);
            
            if (sideEffect != null)
            {
                // Replace all traversers with a single traverser containing the side-effect
                var traverser = new Traverser(sideEffect);
                traverser.AddToPath(sideEffect);
                
                context.Traversers = new List<Traverser> { traverser };
            }
            else
            {
                // No side-effect found, return empty
                context.Traversers.Clear();
            }
        }
    }
}
