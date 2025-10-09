using Stardust.Paradox.Data.Annotations.Annotations;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the by() modulator step.
    /// 
    /// Behavior:
    /// - Stores arguments for the previous step (e.g., group(), order())
    /// - by('property'): Specifies property to use
    /// - by(traversal): Specifies traversal to apply
    /// 
    /// Example:
    /// g.V().group().by('label') - groups by label
    /// g.V().group().by('dept').by(count()) - groups by dept and counts
    /// </summary>
    [UsedImplicitly]
    public class ByStepExecutor : StepExecutorBase
    {
        public ByStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "by";

        public override string StepDescription => 
            "Modulator that specifies how a previous step should operate. " +
            "by('property') specifies a property to use. " +
            "by(traversal) specifies a traversal to apply.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // This by() step should have been consumed by the previous step via TinkerGraphQueryExecutor's lookahead
            // If we reach here, it means it wasn't consumed, which is unexpected
            // Just skip execution silently as it's likely handled by lookahead logic
        }
    }
}
