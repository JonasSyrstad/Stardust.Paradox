using Stardust.Paradox.Data.Annotations.Annotations;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the order() step which sorts results.
    /// 
    /// Behavior:
    /// - Orders results by their string representation
    /// - Can be modified with .by() modulators (future enhancement)
    /// 
    /// Example:
    /// g.V().values('name').order() - returns names in sorted order
    /// </summary>
    [UsedImplicitly]
    public class OrderStepExecutor : StepExecutorBase
    {
        public OrderStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "order";

        public override string StepDescription => 
            "Sorts results in the traversal stream. " +
            "By default orders by string representation of values.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Basic ordering by string representation
            var ordered = context.Traversers.OrderBy(t => t.Value?.ToString()).ToList();
            context.Traversers.Clear();
            context.Traversers.AddRange(ordered);
        }
    }
}
