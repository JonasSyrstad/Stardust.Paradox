using Stardust.Paradox.Data.Annotations.Annotations;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the as() step which labels the current element for later reference.
    /// 
    /// Behavior:
    /// - as('label'): Creates a label for the current element
    /// - Labeled elements can be retrieved with select()
    /// 
    /// Example:
    /// g.V().as('a').out().as('b').select('a', 'b')
    /// </summary>
    [UsedImplicitly]
    public class AsStepExecutor : StepExecutorBase
    {
        public AsStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "as";

        public override string StepDescription => 
            "Labels the current element for later reference. " +
            "as('label') creates a label that can be used with select().";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // 'as' step creates a label for the current element for later reference
            var label = step.GetFirstStringArgument();

            if (!string.IsNullOrEmpty(label))
            {
                foreach (var traverser in context.Traversers)
                {
                    traverser.AddLabel(label, traverser.Value);
                }
            }
        }
    }
}
