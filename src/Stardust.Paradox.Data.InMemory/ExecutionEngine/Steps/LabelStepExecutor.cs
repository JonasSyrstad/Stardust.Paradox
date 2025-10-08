using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the label() step which extracts the label from elements.
    /// 
    /// Behavior:
    /// - Returns the label of each element in the stream
    /// 
    /// Example:
    /// g.V().label() - returns the labels of all vertices
    /// </summary>
    [UsedImplicitly]
    public class LabelStepExecutor : StepExecutorBase
    {
        public LabelStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "label";

        public override string StepDescription => 
            "Extracts the label from each element in the traversal stream.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var label = ExtractLabel(traverser.Value);
                if (label != null)
                {
                    var newTraverser = traverser.Split();
                    newTraverser.Value = label;
                    newTraversers.Add(newTraverser);
                }
            }

            context.Traversers = newTraversers;
        }
    }
}
