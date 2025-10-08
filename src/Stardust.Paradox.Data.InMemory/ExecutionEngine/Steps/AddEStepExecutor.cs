using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the addE() step when used mid-traversal (not as start step).
    /// 
    /// Behavior:
    /// - addE('label'): Initiates edge creation
    /// - Must be followed by from() and to() modulators
    /// 
    /// Example:
    /// g.V('v1').as('a').V('v2').addE('knows').from('a').to(V('v2'))
    /// </summary>
    [UsedImplicitly]
    public class AddEStepExecutor : StepExecutorBase
    {
        public AddEStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "adde";

        public override string StepDescription => 
            "Initiates edge creation in mid-traversal. " +
            "Must be followed by from() and to() modulators to specify source and target vertices.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count < 1)
                return;

            var label = step.GetFirstStringArgument();

            // Store the addE operation in metadata - don't execute yet
            // Wait for from() and to() modulators first
            context.SetMetadata("addE_label", label);
            context.SetMetadata("addE_pending", true);

            // Store any properties from this step - but properties come later, not in addE step
            context.SetMetadata("addE_properties", new Dictionary<string, object>());

            // Don't change traversers yet - wait for modulators
        }
    }
}
