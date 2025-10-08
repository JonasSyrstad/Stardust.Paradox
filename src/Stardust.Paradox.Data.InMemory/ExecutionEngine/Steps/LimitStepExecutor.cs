using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the limit() step which restricts the number of results.
    /// 
    /// Behavior:
    /// - limit(n): Keeps only the first n results
    /// - limit(0): Returns empty results
    /// 
    /// Example:
    /// g.V().limit(10) - returns at most 10 vertices
    /// </summary>
    [UsedImplicitly]
    public class LimitStepExecutor : StepExecutorBase
    {
        public LimitStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "limit";

        public override string StepDescription => 
            "Restricts the number of results to at most n elements. " +
            "limit(n) keeps only the first n results.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var limit = step.GetFirstIntArgument();
            if (limit >= 0)  // Allow limit(0) to work correctly
            {
                context.Limit(limit);
            }
        }
    }
}
