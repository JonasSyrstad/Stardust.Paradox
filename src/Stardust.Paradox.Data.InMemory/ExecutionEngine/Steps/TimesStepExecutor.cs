using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the times() modulator step which specifies repetition count for repeat().
    /// 
    /// Behavior:
    /// - Executes the repeat() pattern n times
    /// - Triggers repeat execution if repeat step is waiting for this modulator
    /// 
    /// Example:
    /// g.V('node_0').repeat(out('next')).times(10) - navigates 10 steps through 'next' edges
    /// </summary>
    [UsedImplicitly]
    public class TimesStepExecutor : StepExecutorBase
    {
        public TimesStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "times";

        public override string StepDescription => 
            "Modulator for repeat() that specifies how many times to repeat the pattern. " +
            "times(n) executes the repeat pattern n times.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
                return;

            var times = Convert.ToInt32(step.Arguments[0]);
            
            // Store the times value as metadata
            context.SetMetadata("times_value", times);
            
            // Check if repeat step is waiting for this modulator
            var repeatStep = context.GetMetadata<TinkerGraphStep>("repeat_step");
            if (repeatStep != null)
            {
                // Trigger repeat execution now that we have the times value
                var repeatExecutor = new RepeatStepExecutor(Database);
                repeatExecutor.ExecuteRepeatLoop(repeatStep, context);
            }
        }
    }
}
