using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the hasLabel() step which filters elements by their label.
    /// 
    /// Behavior:
    /// - hasLabel('label'): Keep only elements with the specified label
    /// - hasLabel('label1', 'label2', ...): Keep elements with any of the specified labels
    /// 
    /// This step filters the current traversers based on element labels.
    /// </summary>
    [UsedImplicitly]
    public class HasLabelStepExecutor : StepExecutorBase
    {
        public HasLabelStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "haslabel";

        public override string StepDescription => 
            "Filters elements by their label. " +
            "hasLabel('label') keeps only elements with the specified label. " +
            "Supports multiple labels.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
                return;

            var expectedLabels = new HashSet<string>(
                step.Arguments.Select(arg => arg.ToString()), 
                System.StringComparer.OrdinalIgnoreCase);

            context.Filter(traverser =>
            {
                var label = ExtractLabel(traverser.Value);
                return label != null && expectedLabels.Contains(label);
            });
        }
    }
}
