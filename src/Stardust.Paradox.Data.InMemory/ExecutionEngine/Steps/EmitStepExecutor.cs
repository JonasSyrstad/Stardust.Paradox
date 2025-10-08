using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the emit() step which is a modulator for repeat() steps.
    /// This step emits the current traverser before/during/after the loop.
    /// 
    /// Behavior:
    /// - emit(): Marks traversers to be emitted during repeat traversal
    /// - When placed before repeat(), emits at the start of each iteration
    /// - When placed after repeat(), emits at the end of each iteration
    /// 
    /// Note: This is typically used in conjunction with repeat() steps.
    /// </summary>
    [UsedImplicitly]
    public class EmitStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public EmitStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "emit";

        public string StepDescription => 
            "Modulator for repeat() steps that emits traversers before/during/after loops. " +
            "emit() marks traversers to be included in the final results.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Check if repeat step already exists (emit after repeat)
            var repeatStep = context.GetMetadata<TinkerGraphStep>("repeat_step");
            
            if (repeatStep != null)
            {
                // emit() called after repeat() - mark for emitting after each iteration
                context.SetMetadata("repeat_emit_after", true);
                
                // Trigger repeat execution if all modulators are present
                var untilCondition = context.GetMetadata<TinkerGraphStep>("until_condition");
                var timesValue = context.GetMetadata<int?>("times_value");
                
                if (untilCondition != null || timesValue.HasValue)
                {
                    var repeatExecutor = new RepeatStepExecutor(_database);
                    repeatExecutor.Execute(repeatStep, context);
                }
            }
            else
            {
                // emit() called before repeat() - mark for emitting before each iteration
                context.SetMetadata("emit", true);
            }
        }
    }
}
