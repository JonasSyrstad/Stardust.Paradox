using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the sample() step which randomly samples n results.
    /// 
    /// Behavior:
    /// - sample(n): Randomly selects at most n results
    /// 
    /// Example:
    /// g.V().sample(5) - returns 5 random vertices
    /// </summary>
    [UsedImplicitly]
    public class SampleStepExecutor : StepExecutorBase
    {
        public SampleStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "sample";

        public override string StepDescription => 
            "Randomly samples n results from the traversal stream. " +
            "sample(n) keeps at most n randomly selected elements.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var sampleSize = step.GetFirstIntArgument();
            if (sampleSize > 0)
            {
                context.Sample(sampleSize);
            }
        }
    }
}
