using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the fold() barrier step which collects all results into a single list.
    /// 
    /// Behavior:
    /// - Collects all traverser values into a single list
    /// - Returns one result containing the list
    /// 
    /// Example:
    /// g.V().values('name').fold() - returns ['name1', 'name2', 'name3']
    /// </summary>
    [UsedImplicitly]
    public class FoldStepExecutor : StepExecutorBase
    {
        public FoldStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "fold";

        public override string StepDescription => 
            "Barrier step that collects all results into a single list. " +
            "Returns one result containing all collected values.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Collect all traverser values into a single list
            var results = new List<dynamic>();

            foreach (var traverser in context.Traversers)
            {
                // Add each bulk instance to the results
                for (int i = 0; i < traverser.Bulk; i++)
                {
                    results.Add(traverser.Value);
                }
            }

            // Clear context and add a single traverser with the collected list
            context.Clear();
            context.Traversers.Add(new Traverser(results));
        }
    }
}
