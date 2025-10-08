using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the repeat() step which defines a repeating traversal pattern.
    /// 
    /// Behavior:
    /// - Stores the repeat pattern for execution with times() modulator
    /// - Must be followed by times(n) to specify repetition count
    /// 
    /// Example:
    /// g.V('1').repeat(out('next')).times(3) - traverses 'next' edges 3 times
    /// </summary>
    [UsedImplicitly]
    public class RepeatStepExecutor : StepExecutorBase
    {
        public RepeatStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "repeat";

        public override string StepDescription => 
            "Defines a repeating traversal pattern. " +
            "Must be followed by times(n) to specify how many times to repeat.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Store the repeat pattern for later execution with times()
            context.SetMetadata("repeat_step", step);
            context.SetMetadata("repeat_traversers", new List<Traverser>(context.Traversers));
        }
    }
}
