using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the unfold() step which expands collections into individual elements.
    /// 
    /// Behavior:
    /// - If the value is a collection, creates separate traversers for each element
    /// - If the value is not a collection, passes it through unchanged
    /// 
    /// Example:
    /// g.V().fold().unfold() - expands the folded list back to individual vertices
    /// </summary>
    [UsedImplicitly]
    public class UnfoldStepExecutor : StepExecutorBase
    {
        public UnfoldStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "unfold";

        public override string StepDescription => 
            "Expands collections into individual elements. " +
            "Each element in a collection becomes a separate result.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                if (traverser.Value is IEnumerable<dynamic> enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = item;
                        newTraversers.Add(newTraverser);
                    }
                }
                else
                {
                    // If not enumerable, just pass through
                    newTraversers.Add(traverser);
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
