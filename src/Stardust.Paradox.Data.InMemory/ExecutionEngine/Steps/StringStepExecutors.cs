using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    #region String Step Executors

    /// <summary>
    /// Executes the concat() step which concatenates strings together.
    /// TinkerPop Reference: https://tinkerpop.apache.org/docs/current/dev/provider/#_concat
    /// </summary>
    [UsedImplicitly]
    public class ConcatStepExecutor : StepExecutorBase
    {
        public ConcatStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "concat";
        public override string StepDescription => "Concatenates the incoming string with provided strings.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var baseString = traverser.Value?.ToString() ?? "";
                var result = new StringBuilder(baseString);

                foreach (var arg in step.Arguments)
                {
                    result.Append(arg?.ToString() ?? "");
                }

                var newTraverser = traverser.Split();
                newTraverser.Value = result.ToString();
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the trim() step which removes leading and trailing whitespace.
    /// </summary>
    [UsedImplicitly]
    public class TrimStepExecutor : StepExecutorBase
    {
        public TrimStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "trim";
        public override string StepDescription => "Removes leading and trailing whitespace from strings.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();
                newTraverser.Value = str.Trim();
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the lTrim() step which removes leading whitespace.
    /// </summary>
    [UsedImplicitly]
    public class LTrimStepExecutor : StepExecutorBase
    {
        public LTrimStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "ltrim";
        public override string StepDescription => "Removes leading whitespace from strings.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();
                newTraverser.Value = str.TrimStart();
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the rTrim() step which removes trailing whitespace.
    /// </summary>
    [UsedImplicitly]
    public class RTrimStepExecutor : StepExecutorBase
    {
        public RTrimStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "rtrim";
        public override string StepDescription => "Removes trailing whitespace from strings.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();
                newTraverser.Value = str.TrimEnd();
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the toLower() step which converts strings to lowercase.
    /// </summary>
    [UsedImplicitly]
    public class ToLowerStepExecutor : StepExecutorBase
    {
        public ToLowerStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "tolower";
        public override string StepDescription => "Converts strings to lowercase.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();
                newTraverser.Value = str.ToLowerInvariant();
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the toUpper() step which converts strings to uppercase.
    /// </summary>
    [UsedImplicitly]
    public class ToUpperStepExecutor : StepExecutorBase
    {
        public ToUpperStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "toupper";
        public override string StepDescription => "Converts strings to uppercase.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();
                newTraverser.Value = str.ToUpperInvariant();
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the substring() step which extracts a substring.
    /// substring(startIndex) - from startIndex to end
    /// substring(startIndex, length) - from startIndex with given length
    /// Negative indices count from end of string.
    /// </summary>
    [UsedImplicitly]
    public class SubstringStepExecutor : StepExecutorBase
    {
        public SubstringStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "substring";
        public override string StepDescription => "Extracts a substring. Negative indices count from end.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();

                try
                {
                    var startIndex = step.Arguments.Any() ? Convert.ToInt32(step.Arguments[0]) : 0;
                    
                    // Handle negative index (count from end)
                    if (startIndex < 0)
                    {
                        startIndex = Math.Max(0, str.Length + startIndex);
                    }
                    
                    startIndex = Math.Min(startIndex, str.Length);

                    if (step.Arguments.Count > 1)
                    {
                        var endIndex = Convert.ToInt32(step.Arguments[1]);
                        if (endIndex < 0)
                        {
                            endIndex = Math.Max(0, str.Length + endIndex);
                        }
                        endIndex = Math.Min(endIndex, str.Length);
                        var length = Math.Max(0, endIndex - startIndex);
                        newTraverser.Value = str.Substring(startIndex, length);
                    }
                    else
                    {
                        newTraverser.Value = str.Substring(startIndex);
                    }
                }
                catch
                {
                    newTraverser.Value = "";
                }

                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the split() step which splits a string by a delimiter.
    /// </summary>
    [UsedImplicitly]
    public class SplitStepExecutor : StepExecutorBase
    {
        public SplitStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "split";
        public override string StepDescription => "Splits a string by the given delimiter.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var delimiter = step.Arguments.FirstOrDefault()?.ToString() ?? "";

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();
                
                var parts = string.IsNullOrEmpty(delimiter) 
                    ? new[] { str } 
                    : str.Split(new[] { delimiter }, StringSplitOptions.None);
                
                var list = new List<object>(parts.Length);
                for (var i = 0; i < parts.Length; i++)
                {
                    list.Add(parts[i]);
                }

                newTraverser.Value = list;
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the replace() step which replaces occurrences of a substring.
    /// </summary>
    [UsedImplicitly]
    public class ReplaceStepExecutor : StepExecutorBase
    {
        public ReplaceStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "replace";
        public override string StepDescription => "Replaces occurrences of a substring with another string.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();
            var oldValue = step.Arguments.FirstOrDefault()?.ToString() ?? "";
            var newValue = step.Arguments.Skip(1).FirstOrDefault()?.ToString() ?? "";

            foreach (var traverser in context.Traversers)
            {
                var str = traverser.Value?.ToString() ?? "";
                var newTraverser = traverser.Split();
                newTraverser.Value = str.Replace(oldValue, newValue);
                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the reverse() step which reverses strings (or lists).
    /// </summary>
    [UsedImplicitly]
    public class ReverseStepExecutor : StepExecutorBase
    {
        public ReverseStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "reverse";
        public override string StepDescription => "Reverses strings or lists.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var newTraverser = traverser.Split();

                if (traverser.Value is string str)
                {
                    var charArray = str.ToCharArray();
                    Array.Reverse(charArray);
                    newTraverser.Value = new string(charArray);
                }
                else if (traverser.Value is System.Collections.IList list)
                {
                    var reversed = list.Cast<object>().Reverse().ToList();
                    newTraverser.Value = reversed;
                }
                else
                {
                    newTraverser.Value = traverser.Value;
                }

                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    /// <summary>
    /// Executes the length() step which returns the length of strings (or lists).
    /// </summary>
    [UsedImplicitly]
    public class LengthStepExecutor : StepExecutorBase
    {
        public LengthStepExecutor(InMemoryGraphDatabase database) : base(database) { }

        public override string StepName => "length";
        public override string StepDescription => "Returns the length of strings or lists.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var newTraverser = traverser.Split();

                if (traverser.Value is string str)
                {
                    newTraverser.Value = str.Length;
                }
                else if (traverser.Value is System.Collections.ICollection collection)
                {
                    newTraverser.Value = collection.Count;
                }
                else if (traverser.Value is System.Collections.IEnumerable enumerable)
                {
                    newTraverser.Value = enumerable.Cast<object>().Count();
                }
                else
                {
                    newTraverser.Value = 0;
                }

                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }
    }

    #endregion
}
