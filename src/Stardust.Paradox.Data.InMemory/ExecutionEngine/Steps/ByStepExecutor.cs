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
            // Store the .by() arguments in the context for the previous grouping step to use
            if (step.Arguments.Any())
            {
                context.SetMetadata("by_arguments", step.Arguments.ToList());
            }

            // Note: .by() steps don't modify the traversers directly
            // They store metadata that affects how the previous step operates
        }
    }
}
