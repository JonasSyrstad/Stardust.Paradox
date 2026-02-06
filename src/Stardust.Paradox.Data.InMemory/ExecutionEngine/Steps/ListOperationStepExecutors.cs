using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    #region List/Set Operation Step Executors

    /// <summary>
    /// Executes the combine() step which appends two lists together (bag semantics, allows duplicates).
    /// </summary>
    [UsedImplicitly]
    public class CombineStepExecutor : StepExecutorBase
    {
        public CombineStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "combine";
        public override string StepDescription => "Appends two lists together (allows duplicates).";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var otherList = ExtractList(step.Arguments.FirstOrDefault());

            foreach (var traverser in context.Traversers)
            {
                var currentList = ExtractList(traverser.Value);
                var combined = new List<object>(currentList.Count + otherList.Count);
                combined.AddRange(currentList);
                combined.AddRange(otherList);

                var newTraverser = traverser.Split();
                newTraverser.Value = combined;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private List<object> ExtractList(object value)
        {
            if (value == null) return new List<object>();
            if (value is IEnumerable<object> enumerable) return enumerable.ToList();
            if (value is IEnumerable e && !(value is string))
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

    /// <summary>
    /// Executes the intersect() step which returns the intersection of two sets.
    /// </summary>
    [UsedImplicitly]
    public class IntersectStepExecutor : StepExecutorBase
    {
        public IntersectStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "intersect";
        public override string StepDescription => "Returns the intersection of two sets.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var otherList = ExtractList(step.Arguments.FirstOrDefault());

            foreach (var traverser in context.Traversers)
            {
                var currentList = ExtractList(traverser.Value);
                var intersection = new List<object>();
                
                foreach (var item in currentList)
                {
                    if (otherList.Any(other => AreEquivalent(item, other)))
                    {
                        intersection.Add(item);
                    }
                }

                var newTraverser = traverser.Split();
                newTraverser.Value = new HashSet<object>(intersection);
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private List<object> ExtractList(object value)
        {
            if (value == null) return new List<object>();
            if (value is IEnumerable<object> enumerable) return enumerable.ToList();
            if (value is IEnumerable e && !(value is string)) return e.Cast<object>().ToList();
            return new List<object> { value };
        }

        private bool AreEquivalent(object x, object y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.Equals(y) || x.ToString() == y.ToString();
        }
    }

    /// <summary>
    /// Executes the difference() step which returns A - B (elements in A but not in B).
    /// </summary>
    [UsedImplicitly]
    public class DifferenceStepExecutor : StepExecutorBase
    {
        public DifferenceStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "difference";
        public override string StepDescription => "Returns A - B (elements in A but not in B).";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var otherList = ExtractList(step.Arguments.FirstOrDefault());

            foreach (var traverser in context.Traversers)
            {
                var currentList = ExtractList(traverser.Value);
                var difference = new List<object>();
                
                foreach (var item in currentList)
                {
                    if (!otherList.Any(other => AreEquivalent(item, other)))
                    {
                        difference.Add(item);
                    }
                }

                var newTraverser = traverser.Split();
                newTraverser.Value = new HashSet<object>(difference);
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private List<object> ExtractList(object value)
        {
            if (value == null) return new List<object>();
            if (value is IEnumerable<object> enumerable) return enumerable.ToList();
            if (value is IEnumerable e && !(value is string)) return e.Cast<object>().ToList();
            return new List<object> { value };
        }

        private bool AreEquivalent(object x, object y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.Equals(y) || x.ToString() == y.ToString();
        }
    }

    /// <summary>
    /// Executes the disjunct() step which returns symmetric difference (A XOR B).
    /// </summary>
    [UsedImplicitly]
    public class DisjunctStepExecutor : StepExecutorBase
    {
        public DisjunctStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "disjunct";
        public override string StepDescription => "Returns symmetric difference (elements in A or B but not both).";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var otherList = ExtractList(step.Arguments.FirstOrDefault());

            foreach (var traverser in context.Traversers)
            {
                var currentList = ExtractList(traverser.Value);
                
                var aMinusB = new List<object>();
                foreach (var item in currentList)
                {
                    if (!otherList.Any(other => AreEquivalent(item, other)))
                    {
                        aMinusB.Add(item);
                    }
                }
                
                var bMinusA = new List<object>();
                foreach (var item in otherList)
                {
                    bool found = false;
                    foreach (var other in currentList)
                    {
                        if (AreEquivalent(item, other))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        bMinusA.Add(item);
                    }
                }
                
                var symmetricDiff = new HashSet<object>(aMinusB.Concat(bMinusA));

                var newTraverser = traverser.Split();
                newTraverser.Value = symmetricDiff;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private List<object> ExtractList(object value)
        {
            if (value == null) return new List<object>();
            if (value is IEnumerable<object> enumerable) return enumerable.ToList();
            if (value is IEnumerable e && !(value is string)) return e.Cast<object>().ToList();
            return new List<object> { value };
        }

        private bool AreEquivalent(object x, object y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.Equals(y) || x.ToString() == y.ToString();
        }
    }

    /// <summary>
    /// Executes the merge() step which returns the union of two sets (no duplicates).
    /// Note: This is different from mergeV/mergeE.
    /// </summary>
    [UsedImplicitly]
    public class MergeSetStepExecutor : StepExecutorBase
    {
        public MergeSetStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "mergeset";
        public override string StepDescription => "Returns the union of two sets (no duplicates).";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var otherList = ExtractList(step.Arguments.FirstOrDefault());

            foreach (var traverser in context.Traversers)
            {
                var currentList = ExtractList(traverser.Value);
                var union = new HashSet<object>(currentList);
                
                foreach (var item in otherList)
                {
                    bool found = false;
                    foreach (var existing in union)
                    {
                        if (AreEquivalent(existing, item))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        union.Add(item);
                    }
                }

                var newTraverser = traverser.Split();
                newTraverser.Value = union;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private List<object> ExtractList(object value)
        {
            if (value == null) return new List<object>();
            if (value is IEnumerable<object> enumerable) return enumerable.ToList();
            if (value is IEnumerable e && !(value is string)) return e.Cast<object>().ToList();
            return new List<object> { value };
        }

        private bool AreEquivalent(object x, object y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.Equals(y) || x.ToString() == y.ToString();
        }
    }

    /// <summary>
    /// Executes the product() step which returns the Cartesian product of two lists.
    /// </summary>
    [UsedImplicitly]
    public class ProductStepExecutor : StepExecutorBase
    {
        public ProductStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "product";
        public override string StepDescription => "Returns the Cartesian product of two lists.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var otherList = ExtractList(step.Arguments.FirstOrDefault());

            foreach (var traverser in context.Traversers)
            {
                var currentList = ExtractList(traverser.Value);
                var product = new List<object>();

                foreach (var a in currentList)
                {
                    foreach (var b in otherList)
                    {
                        product.Add(new List<object> { a, b });
                    }
                }

                var newTraverser = traverser.Split();
                newTraverser.Value = product;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private List<object> ExtractList(object value)
        {
            if (value == null) return new List<object>();
            if (value is IEnumerable<object> enumerable) return enumerable.ToList();
            if (value is IEnumerable e && !(value is string)) return e.Cast<object>().ToList();
            return new List<object> { value };
        }
    }

    /// <summary>
    /// Executes the conjoin() step which joins list elements into a string with a separator.
    /// </summary>
    [UsedImplicitly]
    public class ConjoinStepExecutor : StepExecutorBase
    {
        public ConjoinStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "conjoin";
        public override string StepDescription => "Joins list elements into a string with a separator.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var separator = step.Arguments.FirstOrDefault()?.ToString() ?? "";

            foreach (var traverser in context.Traversers)
            {
                var list = ExtractList(traverser.Value);
                var stringList = new List<string>();
                foreach (var item in list)
                {
                    stringList.Add(item?.ToString() ?? "");
                }
                var joined = string.Join(separator, stringList);

                var newTraverser = traverser.Split();
                newTraverser.Value = joined;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private List<object> ExtractList(object value)
        {
            if (value == null) return new List<object>();
            if (value is IEnumerable<object> enumerable) return enumerable.ToList();
            if (value is IEnumerable e && !(value is string)) return e.Cast<object>().ToList();
            return new List<object> { value };
        }
    }

    #endregion
}
