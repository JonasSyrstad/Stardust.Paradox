using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the until() step which is a modulator for repeat() steps.
    /// This step defines the termination condition for a loop.
    /// 
    /// Behavior:
    /// - until(predicate): Sets the termination condition for repeat traversal
    /// - Triggers repeat execution if repeat step is waiting for this modulator
    /// 
    /// Note: This is typically used in conjunction with repeat() steps.
    /// </summary>
    [UsedImplicitly]
    public class UntilStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public UntilStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "until";

        public string StepDescription => 
            "Modulator for repeat() steps that defines loop termination condition. " +
            "until(predicate) specifies when to stop repeating.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Store until condition for the repeat step to use
            context.SetMetadata("until_condition", step);
            
            // Check if repeat step is waiting for this modulator
            var repeatStep = context.GetMetadata<TinkerGraphStep>("repeat_step");
            if (repeatStep != null)
            {
                // Trigger repeat execution now that we have the until condition
                var repeatExecutor = new RepeatStepExecutor(_database);
                repeatExecutor.ExecuteRepeatLoop(repeatStep, context);
            }
        }
    }
}
