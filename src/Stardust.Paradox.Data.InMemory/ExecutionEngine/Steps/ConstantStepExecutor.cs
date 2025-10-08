using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the constant() step which maps any traverser to a constant value.
    /// 
    /// Behavior:
    /// - constant(value): Maps all traversers to the specified constant value
    /// </summary>
    [UsedImplicitly]
    public class ConstantStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public ConstantStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "constant";

        public string StepDescription => 
            "Maps any traverser to a constant value. " +
            "constant(value) replaces the current element with the specified value.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                return; // No constant value specified, no-op
            }
            
            var constantValue = step.Arguments.First();
            
            // Map all traversers to the constant value
            foreach (var traverser in context.Traversers)
            {
                traverser.Value = constantValue;
                
                // Add to path for path tracking
                traverser.AddToPath(constantValue);
            }
        }
    }
}
