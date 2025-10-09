using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    [UsedImplicitly]
    public class RepeatStepExecutor : StepExecutorBase
    {
        public RepeatStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "repeat";

        public override string StepDescription =>
            "Defines a repeating traversal pattern with optional emit() and until() modulators.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var emitBeforeRepeat = context.GetMetadata<bool>("emit");
            context.SetMetadata("repeat_step", step);
            context.SetMetadata("repeat_emit_before", emitBeforeRepeat);
            context.SetMetadata("repeat_traversers", new List<Traverser>(context.Traversers));

            if (step.Arguments.Any())
            {
                var traversalArg = step.Arguments.First();
                if (traversalArg is string traversalStr)
                {
                    context.SetMetadata("repeat_traversal", traversalStr);
                }
            }
            context.RemoveMetadata("emit");
            var allSteps = context.GetMetadata<List<TinkerGraphStep>>("all_steps");
            var currentStepIndex = context.GetMetadata<int>("current_step_index");

            if (allSteps != null && currentStepIndex >= 0 && currentStepIndex + 1 < allSteps.Count)
            {
                var nextStep = allSteps[currentStepIndex + 1];
                var nextStepName = nextStep.StepName.ToLower();

                if (nextStepName != "times" && nextStepName != "until")
                {
                    ExecuteRepeatLoop(step, context);
                    context.RemoveMetadata("repeat_step");
                    context.RemoveMetadata("repeat_traversers");
                    context.RemoveMetadata("repeat_traversal");
                    context.RemoveMetadata("repeat_emit_before");
                    context.RemoveMetadata("emit");
                }
            }
            else
            {
                ExecuteRepeatLoop(step, context);
                context.RemoveMetadata("repeat_step");
                context.RemoveMetadata("repeat_traversers");
                context.RemoveMetadata("repeat_traversal");
                context.RemoveMetadata("repeat_emit_before");
                context.RemoveMetadata("emit");
            }
        }

        public void ExecuteRepeatLoop(TinkerGraphStep repeatStep, TinkerTraversalContext context)
        {
            var originalTraversers = context.GetMetadata<List<Traverser>>("repeat_traversers")
                                     ?? new List<Traverser>(context.Traversers);
            var emitBeforeRepeat = context.GetMetadata<bool>("repeat_emit_before");
            var emitAfterRepeatObj = context.GetMetadata<object>("repeat_emit_after");
            var emitAfterRepeat = emitAfterRepeatObj is bool b && b;
            var untilCondition = context.GetMetadata<TinkerGraphStep>("until_condition");
            var timesValue = context.GetMetadata<int?>("times_value");
            var repeatTraversalStr = context.GetMetadata<string>("repeat_traversal");

            if (string.IsNullOrEmpty(repeatTraversalStr))
            {
                context.Traversers = originalTraversers;
                return;
            }

            var allResults = new List<Traverser>();
            var activeTraversers = new List<Traverser>();

            foreach (var traverser in originalTraversers)
            {
                var newTraverser = traverser.Split();
                newTraverser.ResetLoops("repeat");
                
                // CRITICAL FIX: Ensure the starting vertex is in the path for tree() step
                var path = newTraverser.GetPath();
                if (path.Count == 0 && newTraverser.Value != null)
                {
                    newTraverser.AddToPath(newTraverser.Value);
                }
                
                activeTraversers.Add(newTraverser);
            }

            if (emitBeforeRepeat)
            {
                allResults.AddRange(activeTraversers.Select(t => t.Split()));
            }

            var maxIterations = timesValue ?? int.MaxValue;
            var iteration = 0;

            while (iteration < maxIterations && activeTraversers.Any())
            {
                foreach (var traverser in activeTraversers)
                {
                    traverser.IncrementLoops("repeat");
                }

                var nextIterationTraversers = new List<Traverser>();
                foreach (var traverser in activeTraversers)
                {
                    var tempContext = new TinkerTraversalContext();
                    // Split the traverser for execution in temp context
                    var tempTraverser = traverser.Split();
                    tempContext.Traversers.Add(tempTraverser);
                    
                    // Set path tracking for tree() support
                    tempContext.SetMetadata("track_paths", true);
                    
                    ExecuteRepeatTraversal(repeatTraversalStr, tempContext);
                    
                    foreach (var resultTraverser in tempContext.Traversers)
                    {
                        // Preserve loop count
                        resultTraverser.Loops["repeat"] = traverser.GetLoops("repeat");
                        nextIterationTraversers.Add(resultTraverser);
                    }
                }
                
                activeTraversers = nextIterationTraversers;
                iteration++;

                if (!activeTraversers.Any())
                {
                    break;
                }

                if (emitBeforeRepeat || emitAfterRepeat)
                {
                    allResults.AddRange(activeTraversers.Select(t => t.Split()));
                }

                if (untilCondition != null)
                {
                    var toContinue = new List<Traverser>();
                    var toStop = new List<Traverser>();

                    foreach (var traverser in activeTraversers)
                    {
                        if (EvaluateUntilCondition(untilCondition, traverser, context))
                        {
                            toStop.Add(traverser);
                        }
                        else
                        {
                            toContinue.Add(traverser);
                        }
                    }
                    
                    if (!emitBeforeRepeat && !emitAfterRepeat)
                    {
                        allResults.AddRange(toStop.Select(t => t.Split()));
                    }
                    activeTraversers = toContinue;
                    if (!activeTraversers.Any())
                    {
                        break;
                    }
                }
            }

            if (untilCondition == null && !emitBeforeRepeat && !emitAfterRepeat)
            {
                allResults.AddRange(activeTraversers);
            }

            context.Traversers = allResults;
            context.RemoveMetadata("repeat_step");
            context.RemoveMetadata("repeat_traversers");
            context.RemoveMetadata("repeat_traversal");
            context.RemoveMetadata("repeat_emit_before");
            context.RemoveMetadata("emit");
            context.RemoveMetadata("repeat_emit_after");
            context.RemoveMetadata("until_condition");
            context.RemoveMetadata("times_value");
        }

        private void ExecuteRepeatTraversal(string traversalStr, TinkerTraversalContext context)
        {
            traversalStr = traversalStr.TrimStart();
            if (traversalStr.StartsWith("g."))
            {
                traversalStr = traversalStr.Substring(2);
            }
            else if (traversalStr.StartsWith("__."))
            {
                traversalStr = traversalStr.Substring(3);
            }

            var steps = ParseRepeatTraversal(traversalStr);
            foreach (var step in steps)
            {
                ExecuteStepInContext(step, context);
            }
        }

        private List<TinkerGraphStep> ParseRepeatTraversal(string traversalStr)
        {
            var steps = new List<TinkerGraphStep>();
            if (traversalStr.StartsWith("__"))
            {
                traversalStr = traversalStr.Substring(2);
                if (traversalStr.StartsWith("."))
                {
                    traversalStr = traversalStr.Substring(1);
                }
            }

            var parts = traversalStr.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*(\([^)]*\))?");
                if (match.Success)
                {
                    var stepName = match.Groups[1].Value;
                    var step = new TinkerGraphStep(stepName);
                    if (match.Groups[2].Success)
                    {
                        var argsStr = match.Groups[2].Value.Trim('(', ')');
                        if (!string.IsNullOrEmpty(argsStr))
                        {
                            var args = argsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var arg in args)
                            {
                                step.Arguments.Add(ParseSimpleArgument(arg.Trim()));
                            }
                        }
                    }
                    steps.Add(step);
                }
            }
            return steps;
        }

        private bool EvaluateUntilCondition(TinkerGraphStep untilCondition, Traverser traverser, TinkerTraversalContext mainContext)
        {
            var tempContext = new TinkerTraversalContext();
            tempContext.Traversers.Add(traverser.Split());
            var steps = ParseUntilCondition(untilCondition);

            foreach (var step in steps)
            {
                if (step.StepName.Equals("loops", StringComparison.OrdinalIgnoreCase))
                {
                    var loopCount = traverser.GetLoops("repeat");
                    foreach (var t in tempContext.Traversers)
                    {
                        t.Value = loopCount;
                    }
                    continue;
                }
                ExecuteStepInContext(step, tempContext);
                
                // CRITICAL FIX: Don't exit early when there are no traversers!
                // Some steps like count() need to execute even on empty input.
                // Only check at the very end after ALL steps have executed.
                // 
                // This allows patterns like: until(__.outE('parent').count().is(0))
                // to work correctly - even when outE returns no edges, count() still 
                // needs to run and return 0, then is(0) can match it.
            }
            
            // After all steps execute, check if we have traversers
            // If we do, the condition is TRUE (satisfied)
            // If we don't, the condition is FALSE (not satisfied)
            return tempContext.HasTraversers;
        }

        private List<TinkerGraphStep> ParseUntilCondition(TinkerGraphStep untilCondition)
        {
            var steps = new List<TinkerGraphStep>();
            if (untilCondition.Arguments.Any())
            {
                var condition = untilCondition.Arguments.First();
                if (condition is string conditionStr)
                {
                    steps = ParseNestedTraversal(conditionStr);
                }
                else if (condition is TinkerGraphStep step)
                {
                    steps.Add(step);
                }
            }
            return steps;
        }

        private List<TinkerGraphStep> ParseNestedTraversal(string traversalStr)
        {
            var steps = new List<TinkerGraphStep>();
            if (traversalStr.StartsWith("__"))
            {
                traversalStr = traversalStr.Substring(2);
                if (traversalStr.StartsWith("."))
                {
                    traversalStr = traversalStr.Substring(1);
                }
            }

            var parts = SplitByDotOutsideParentheses(traversalStr);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*(\([^)]*\))?");
                if (match.Success)
                {
                    var stepName = match.Groups[1].Value;
                    var step = new TinkerGraphStep(stepName);
                    if (match.Groups[2].Success)
                    {
                        var argsStr = match.Groups[2].Value.Trim('(', ')');
                        if (!string.IsNullOrEmpty(argsStr))
                        {
                            var args = SplitArgumentsRespectingQuotes(argsStr);
                            foreach (var arg in args)
                            {
                                step.Arguments.Add(ParseSimpleArgument(arg.Trim()));
                            }
                        }
                    }
                    steps.Add(step);
                }
            }
            return steps;
        }

        private List<string> SplitByDotOutsideParentheses(string input)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            var depth = 0;
            foreach (var ch in input)
            {
                if (ch == '(')
                {
                    depth++;
                    current.Append(ch);
                }
                else if (ch == ')')
                {
                    depth--;
                    current.Append(ch);
                }
                else if (ch == '.' && depth == 0)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }
            return result;
        }

        private List<string> SplitArgumentsRespectingQuotes(string argsStr)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;
            var quoteChar = '\0';
            var depth = 0;
            foreach (var ch in argsStr)
            {
                if ((ch == '\'' || ch == '"') && !inQuotes)
                {
                    inQuotes = true;
                    quoteChar = ch;
                    current.Append(ch);
                }
                else if (ch == quoteChar && inQuotes)
                {
                    inQuotes = false;
                    current.Append(ch);
                }
                else if (ch == '(' && !inQuotes)
                {
                    depth++;
                    current.Append(ch);
                }
                else if (ch == ')' && !inQuotes)
                {
                    depth--;
                    current.Append(ch);
                }
                else if (ch == ',' && !inQuotes && depth == 0)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }
            return result;
        }

        private object ParseSimpleArgument(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return null;

            if ((arg.StartsWith("'") && arg.EndsWith("'")) ||
                (arg.StartsWith("\"") && arg.EndsWith("\"")))
            {
                return arg.Substring(1, arg.Length - 2);
            }

            if (int.TryParse(arg, out int intVal))
                return intVal;

            if (double.TryParse(arg, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                return doubleVal;

            if (bool.TryParse(arg, out bool boolVal))
                return boolVal;

            return arg;
        }

        private void ExecuteStepInContext(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var stepName = step.StepName.ToLower();
            IStepExecutor executor = null;
            switch (stepName)
            {
                case "loops":
                    executor = new LoopsStepExecutor(Database);
                    break;
                case "is":
                    executor = new IsStepExecutor(Database);
                    break;
                case "has":
                    executor = new HasStepExecutor(Database);
                    break;
                case "haslabel":
                    executor = new HasLabelStepExecutor(Database);
                    break;
                case "hasid":
                    executor = new HasIdStepExecutor(Database);
                    break;
                case "out":
                    executor = new OutStepExecutor(Database);
                    break;
                case "in":
                    executor = new InStepExecutor(Database);
                    break;
                case "both":
                    executor = new BothStepExecutor(Database);
                    break;
                case "oute":
                    executor = new OutEStepExecutor(Database);
                    break;
                case "ine":
                    executor = new InEStepExecutor(Database);
                    break;
                case "bothe":
                    executor = new BothEStepExecutor(Database);
                    break;
                case "inv":
                    executor = new InVStepExecutor(Database);
                    break;
                case "outv":
                    executor = new OutVStepExecutor(Database);
                    break;
                case "bothv":
                    executor = new BothVStepExecutor(Database);
                    break;
                case "otherv":
                    executor = new OtherVStepExecutor(Database);
                    break;
                case "as":
                    executor = new AsStepExecutor(Database);
                    break;
                case "dedup":
                    executor = new DedupStepExecutor(Database);
                    break;
                case "values":
                    executor = new ValuesStepExecutor(Database);
                    break;
                case "limit":
                    executor = new LimitStepExecutor(Database);
                    break;
                case "count":
                    executor = new CountStepExecutor(Database);
                    break;
                default:
                    throw new NotSupportedException($"Step '{stepName}' is not supported in nested traversal context");
            }
            executor?.Execute(step, context);
        }
    }
}
