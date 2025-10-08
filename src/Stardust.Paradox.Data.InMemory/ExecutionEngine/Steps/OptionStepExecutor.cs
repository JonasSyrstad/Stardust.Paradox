using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the option() step which defines a branch for choose() or branch() steps.
    /// 
    /// Behavior:
    /// - option(value, traversal): Defines a case branch
    /// - Used with choose() to specify what happens for specific values
    /// 
    /// This is similar to case statements in switch/match expressions.
    /// </summary>
    [UsedImplicitly]
    public class OptionStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public OptionStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "option";

        public string StepDescription => 
            "Defines a branch case for choose() or branch() steps. " +
            "option(value, traversal) specifies what to do for specific values.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Option step is a modulator for choose/branch steps
            // Store it as metadata for the parent step to use
            var options = context.GetMetadata<List<TinkerGraphStep>>("option_steps") ?? new List<TinkerGraphStep>();
            options.Add(step);
            context.SetMetadata("option_steps", options);
            
            // The option step itself doesn't modify traversers,
            // it just provides configuration for parent steps
        }
    }
}
