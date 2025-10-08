using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the store() step which lazily collects objects into a side-effect collection.
    /// 
    /// Behavior:
    /// - store(sideEffectKey): Lazily stores traversers in a named collection
    /// - Does not remove traversers from the main stream
    /// 
    /// Similar to aggregate() but with lazy evaluation.
    /// </summary>
    [UsedImplicitly]
    public class StoreStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public StoreStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "store";

        public string StepDescription => 
            "Lazily collects objects into a side-effect collection. " +
            "store(key) stores traversers without removing them from the stream.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                return; // No side-effect key specified
            }
            
            var sideEffectKey = step.Arguments.First().ToString();
            
            // Get or create the storage collection
            var storage = context.GetMetadata<List<dynamic>>(sideEffectKey) ?? new List<dynamic>();
            
            // Add all current traverser values to the storage
            foreach (var traverser in context.Traversers)
            {
                storage.Add(traverser.Value);
            }
            
            // Store the collection
            context.SetMetadata(sideEffectKey, storage);
            
            // Traversers continue through the pipeline unchanged
        }
    }
}
