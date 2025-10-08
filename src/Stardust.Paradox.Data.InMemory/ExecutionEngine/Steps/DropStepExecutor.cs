using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the drop() step which removes elements from the database.
    /// 
    /// Behavior:
    /// - Removes vertices or edges from the database
    /// - Returns empty results after dropping
    /// 
    /// Example:
    /// g.V('1').drop() - removes vertex with ID '1'
    /// g.E('e1').drop() - removes edge with ID 'e1'
    /// </summary>
    [UsedImplicitly]
    public class DropStepExecutor : StepExecutorBase
    {
        public DropStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "drop";

        public override string StepDescription => 
            "Removes elements (vertices or edges) from the database. " +
            "Returns empty results after dropping.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Drop vertices/edges from the database
            foreach (var traverser in context.Traversers)
            {
                var id = ExtractId(traverser.Value);
                if (!string.IsNullOrEmpty(id))
                {
                    // Try to drop as vertex first, then as edge
                    if (!Database.RemoveVertex(id))
                    {
                        Database.RemoveEdge(id);
                    }
                }
            }

            // Drop step returns empty results
            context.Traversers = new List<Traverser>();
        }
    }
}
