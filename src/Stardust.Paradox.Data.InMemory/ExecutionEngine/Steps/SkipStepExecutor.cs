using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the skip() step which skips the first n results.
    /// 
    /// Behavior:
    /// - skip(n): Skips the first n results and returns the rest
    /// 
    /// Example:
    /// g.V().skip(5) - skips the first 5 vertices and returns the rest
    /// </summary>
    [UsedImplicitly]
    public class SkipStepExecutor : StepExecutorBase
    {
        public SkipStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "skip";

        public override string StepDescription => 
            "Skips the first n results in the traversal stream. " +
            "skip(n) discards the first n results and keeps the rest.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var skip = step.GetFirstIntArgument();
            if (skip > 0)
            {
                context.Skip(skip);
            }
        }
    }
}
