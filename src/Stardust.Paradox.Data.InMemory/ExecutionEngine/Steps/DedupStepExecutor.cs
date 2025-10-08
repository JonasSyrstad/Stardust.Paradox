using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the dedup() step which removes duplicate values.
    /// 
    /// Behavior:
    /// - Removes duplicate traversers based on their values
    /// - Uses deduplication logic from TinkerTraversalContext
    /// 
    /// Example:
    /// g.V().values('name').dedup() - returns unique names
    /// </summary>
    [UsedImplicitly]
    public class DedupStepExecutor : StepExecutorBase
    {
        public DedupStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "dedup";

        public override string StepDescription => 
            "Removes duplicate values from the traversal stream. " +
            "Keeps only unique values based on their representation.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            context.Dedup();
        }
    }
}
