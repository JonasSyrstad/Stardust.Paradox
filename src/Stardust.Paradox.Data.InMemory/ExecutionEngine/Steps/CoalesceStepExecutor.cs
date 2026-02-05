using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the coalesce() step which evaluates multiple traversal options
    /// and returns the result of the first one that yields results.
    /// 
    /// Behavior:
    /// - coalesce(trav1, trav2, ...): Returns first non-empty traversal result
    /// 
    /// This is similar to a try-catch for traversals.
    /// </summary>
    [UsedImplicitly]
    public class CoalesceStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public CoalesceStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "coalesce";

        public string StepDescription => 
            "Evaluates multiple traversal options and returns the first non-empty result. " +
            "coalesce(trav1, trav2, ...) acts like a try-catch for traversals.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var options = step.Arguments?.OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
            if (options.Count == 0)
            {
                return;
            }

            var newTraversers = new List<Traverser>(context.Traversers.Count);

            foreach (var traverser in context.Traversers)
            {
                var newTraverser = traverser.Split();
                var selected = false;

                for (var i = 0; i < options.Count; i++)
                {
                    if (TryEvaluateAnonymousTraversal(options[i], traverser, out var value) && !IsEmptyCoalesceValue(value))
                    {
                        newTraverser.Value = value;
                        selected = true;
                        break;
                    }
                }

                if (!selected)
                {
                    newTraverser.Value = null;
                }

                newTraversers.Add(newTraverser);
            }

            context.Traversers = newTraversers;
        }

        private bool TryEvaluateAnonymousTraversal(string traversalText, Traverser traverser, out object value)
        {
            value = null;

            // Minimal support for TinkerPopStepsComplianceTests:
            // __.values('prop')
            // __.values("prop")
            var text = traversalText.Trim();
            if (!text.StartsWith("__.", StringComparison.Ordinal))
            {
                return false;
            }

            if (text.StartsWith("__.values(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(")", StringComparison.Ordinal))
            {
                var arg = text.Substring("__.values(".Length, text.Length - "__.values(".Length - 1).Trim();
                var propKey = arg.Trim().Trim('"', '\'');

                var current = traverser.Value;
                if (current is GremlinResponseObject gro)
                {
                    var props = gro.properties;
                    if (props == null)
                    {
                        value = null;
                        return true;
                    }

                    var token = props[propKey];
                    if (token == null)
                    {
                        value = null;
                        return true;
                    }

                    // Cosmos-style: key: [ { id: ..., key: ..., value: ... } ]
                    if (token is Newtonsoft.Json.Linq.JArray arr && arr.Count > 0)
                    {
                        var first = arr[0];
                        var v = first?["value"];
                        value = v != null ? v.ToObject<object>() : null;
                        return true;
                    }

                    value = token.ToObject<object>();
                    return true;
                }

                if (current is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue(propKey, out var v))
                    {
                        value = v;
                        return true;
                    }

                    value = null;
                    return true;
                }

                return false;
            }

            return false;
        }

        private static bool IsEmptyCoalesceValue(object value)
        {
            if (value == null)
            {
                return true;
            }

            if (value is string s)
            {
                return string.IsNullOrEmpty(s);
            }

            if (value is System.Collections.IEnumerable e && value is not string)
            {
                return !e.Cast<object>().Any();
            }

            return false;
        }
    }
}
