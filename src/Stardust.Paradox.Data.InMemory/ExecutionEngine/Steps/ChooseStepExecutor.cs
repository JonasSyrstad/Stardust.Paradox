using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the choose() step which implements conditional branching.
    /// Routes traversers to different traversals based on a condition.
    /// 
    /// Behavior:
    /// - choose(predicate, trueTraversal, falseTraversal): Conditional branching
    /// - choose(function): Routes based on function result
    /// 
    /// This implements if-then-else logic in traversals.
    /// </summary>
    [UsedImplicitly]
    public class ChooseStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public ChooseStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "choose";

        public string StepDescription => 
            "Implements conditional branching logic. " +
            "choose(predicate, trueTraversal, falseTraversal) routes traversers based on conditions.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Minimal implementation for option-based selection:
            // choose(__.values('status')).option('active', __.constant(1)).option('inactive', __.constant(0))
            if (step.Arguments.Count == 1 && step.Arguments[0] is string chooser && chooser.TrimStart().StartsWith("__.", StringComparison.Ordinal))
            {
                var options = context.GetMetadata<List<TinkerGraphStep>>("option_steps") ?? new List<TinkerGraphStep>();
                var optionMap = BuildOptionMap(options);

                var newTraversers = new List<Traverser>(context.Traversers.Count);
                foreach (var traverser in context.Traversers)
                {
                    var key = EvaluateValuesTraversal(chooser, traverser);
                    if (key != null && optionMap.TryGetValue(key, out var mapped))
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = mapped;
                        newTraversers.Add(newTraverser);
                    }
                    else
                    {
                        // If no option matches, keep original traverser
                        newTraversers.Add(traverser);
                    }
                }

                context.Traversers = newTraversers;
                return;
            }

            // Fallback to previous behavior (metadata for future richer implementation)
            context.SetMetadata("choose_step", step);
        }

        private static Dictionary<string, object> BuildOptionMap(List<TinkerGraphStep> optionSteps)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var optionStep in optionSteps)
            {
                if (optionStep.Arguments.Count < 2)
                {
                    continue;
                }

                var matchValue = optionStep.Arguments[0]?.ToString();
                if (string.IsNullOrEmpty(matchValue))
                {
                    continue;
                }

                var traversal = optionStep.Arguments[1];
                if (traversal is string traversalText && TryParseConstantTraversal(traversalText, out var constantValue))
                {
                    map[matchValue] = constantValue;
                }
            }

            return map;
        }

        private static bool TryParseConstantTraversal(string traversalText, out object constantValue)
        {
            constantValue = null;
            var text = traversalText.Trim();
            if (!text.StartsWith("__.", StringComparison.Ordinal))
            {
                return false;
            }

            if (!text.StartsWith("__.constant(", StringComparison.OrdinalIgnoreCase) || !text.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            var inner = text.Substring("__.constant(".Length, text.Length - "__.constant(".Length - 1).Trim();
            if ((inner.StartsWith("'", StringComparison.Ordinal) && inner.EndsWith("'", StringComparison.Ordinal)) ||
                (inner.StartsWith("\"", StringComparison.Ordinal) && inner.EndsWith("\"", StringComparison.Ordinal)))
            {
                constantValue = inner.Substring(1, inner.Length - 2);
                return true;
            }

            if (int.TryParse(inner, out var intVal))
            {
                constantValue = intVal;
                return true;
            }

            if (long.TryParse(inner, out var longVal))
            {
                constantValue = longVal;
                return true;
            }

            if (double.TryParse(inner, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
            {
                constantValue = doubleVal;
                return true;
            }

            if (bool.TryParse(inner, out var boolVal))
            {
                constantValue = boolVal;
                return true;
            }

            return false;
        }

        private static string EvaluateValuesTraversal(string traversalText, Traverser traverser)
        {
            var text = traversalText.Trim();
            if (!text.StartsWith("__.values(", StringComparison.OrdinalIgnoreCase) || !text.EndsWith(")", StringComparison.Ordinal))
            {
                return null;
            }

            var inner = text.Substring("__.values(".Length, text.Length - "__.values(".Length - 1).Trim();
            var propKey = inner.Trim('"', '\'');

            if (traverser.Value is GremlinResponseObject gro)
            {
                var props = gro.properties;
                var token = props?[propKey];
                if (token == null)
                {
                    return null;
                }

                if (token is Newtonsoft.Json.Linq.JArray arr && arr.Count > 0)
                {
                    var v = arr[0]?["value"];
                    return v?.ToString();
                }

                return token.ToString();
            }

            if (traverser.Value is IDictionary<string, object> dict && dict.TryGetValue(propKey, out var v2))
            {
                return v2?.ToString();
            }

            return null;
        }
    }
}
