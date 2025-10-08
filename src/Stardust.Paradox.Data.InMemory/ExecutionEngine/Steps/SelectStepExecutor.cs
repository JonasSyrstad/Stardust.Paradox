using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the select() step which retrieves previously labeled elements.
    /// 
    /// Behavior:
    /// - select('label'): Returns the element at the specified label
    /// - select('label1', 'label2', ...): Returns a map with the specified labeled elements
    /// - Automatically deduplicates selected values
    /// 
    /// Example:
    /// g.V().as('a').out().as('b').select('a') - returns vertices labeled 'a'
    /// g.V().as('a').out().as('b').select('a', 'b') - returns map {a: vertex1, b: vertex2}
    /// </summary>
    [UsedImplicitly]
    public class SelectStepExecutor : StepExecutorBase
    {
        public SelectStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "select";

        public override string StepDescription => 
            "Retrieves previously labeled elements from the traversal path. " +
            "select('label') returns single labeled element. " +
            "select('label1', 'label2') returns a map of labeled elements.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var labels = step.Arguments.Select(arg => arg.ToString()).ToList();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                if (labels.Count == 1)
                {
                    // Single label selection
                    var selected = traverser.GetTagged<dynamic>(labels[0]);
                    if (selected != null)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = selected;
                        newTraversers.Add(newTraverser);
                    }
                }
                else
                {
                    // Multiple label selection - return as map
                    var selected = new Dictionary<string, dynamic>();
                    bool hasAnySelection = false;

                    foreach (var label in labels)
                    {
                        var value = traverser.GetTagged<dynamic>(label);
                        if (value != null)
                        {
                            selected[label] = value;
                            hasAnySelection = true;
                        }
                    }

                    if (hasAnySelection)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = selected;
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;

            // Apply deduplication to selected values to remove duplicates
            context.Dedup();
        }
    }
}
