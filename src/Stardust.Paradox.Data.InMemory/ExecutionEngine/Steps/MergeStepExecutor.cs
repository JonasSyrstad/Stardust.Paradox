using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the merge() step which returns the union of two collections without duplicates.
    /// TinkerPop Reference: https://tinkerpop.apache.org/docs/current/dev/provider/#_merge
    /// </summary>
    [UsedImplicitly]
    public sealed class MergeStepExecutor : StepExecutorBase
    {
        public MergeStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "merge";

        public override string StepDescription => "Returns the union of the incoming collection and the provided collection without duplicates.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var other = step.Arguments.Count > 0 ? ExtractList(step.Arguments[0]) : new List<object>();

            var newTraversers = new List<Traverser>();
            foreach (var traverser in context.Traversers)
            {
                var current = ExtractList(traverser.Value);

                var merged = new List<object>();
                AddDistinct(merged, current);
                AddDistinct(merged, other);

                var newTraverser = traverser.Split();
                newTraverser.Value = merged;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private static void AddDistinct(List<object> target, List<object> source)
        {
            foreach (var item in source)
            {
                var exists = false;
                for (var i = 0; i < target.Count; i++)
                {
                    if (Equals(target[i], item))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    target.Add(item);
                }
            }
        }

        private static List<object> ExtractList(object value)
        {
            if (value == null)
            {
                return new List<object>();
            }

            if (value is IEnumerable<object> generic)
            {
                return new List<object>(generic);
            }

            if (value is IEnumerable e && value is not string)
            {
                var list = new List<object>();
                foreach (var item in e)
                {
                    list.Add(item);
                }

                return list;
            }

            return new List<object> { value };
        }
    }
}
