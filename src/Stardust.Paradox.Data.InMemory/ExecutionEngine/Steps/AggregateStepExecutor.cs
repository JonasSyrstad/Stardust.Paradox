using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the aggregate() step which collects objects into a side-effect collection.
    /// 
    /// Behavior:
    /// - aggregate(sideEffectKey): Stores traversers in a named collection
    /// - Does not remove traversers from the main stream
    /// 
    /// This is used for collecting intermediate results.
    /// </summary>
    [UsedImplicitly]
    public class AggregateStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public AggregateStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "aggregate";

        public string StepDescription => 
            "Collects objects into a side-effect collection. " +
            "aggregate(key) stores traversers without removing them from the stream.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                return; // No side-effect key specified
            }
            
            var sideEffectKey = step.Arguments.First().ToString();
            
            // Get or create the aggregation collection
            var aggregation = context.GetMetadata<List<dynamic>>(sideEffectKey) ?? new List<dynamic>();
            
            // Add all current traverser values to the aggregation
            foreach (var traverser in context.Traversers)
            {
                aggregation.Add(traverser.Value);
            }
            
            // Store the aggregation
            context.SetMetadata(sideEffectKey, aggregation);
            
            // Traversers continue through the pipeline unchanged
        }
    }
}
