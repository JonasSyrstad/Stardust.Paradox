using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the id() step which extracts the ID from elements.
    /// 
    /// Behavior:
    /// - Returns the ID of each element in the stream
    /// 
    /// Example:
    /// g.V().id() - returns the IDs of all vertices
    /// </summary>
    [UsedImplicitly]
    public class IdStepExecutor : StepExecutorBase
    {
        public IdStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "id";

        public override string StepDescription => 
            "Extracts the ID from each element in the traversal stream.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var id = ExtractId(traverser.Value);
                if (id != null)
                {
                    var newTraverser = traverser.Split();
                    newTraverser.Value = id;
                    newTraversers.Add(newTraverser);
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
