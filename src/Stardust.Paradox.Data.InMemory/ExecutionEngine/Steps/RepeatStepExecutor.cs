using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the repeat() step which defines a repeating traversal pattern.
    /// 
    /// Behavior:
    /// - Executes the repeat pattern with emit() and until() modulators
    /// - Works with times(), until(), or both for termination
    /// - Supports emit() to output intermediate results
    /// - Supports complex until conditions including anonymous traversals
    /// 
    /// Example:
    /// g.V('1').repeat(out('next')).times(3) - traverses 'next' edges 3 times
    /// g.V('1').emit().repeat(out()).until(loops().is(3)) - emits at each level up to depth 3
    /// g.V('1').repeat(g.out('next')).times(10) - navigates edges 10 times (g. prefix handled)
    /// g.V('1').repeat(__.out()).until(__.outE().count().is(0)) - complex condition
    /// </summary>
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
            // Store the repeat traversal for execution
            // The repeat step's arguments should contain the nested traversal steps
            
            // Check for emit before repeat (already set by EmitStepExecutor)
            var emitBeforeRepeat = context.GetMetadata<bool>("emit");
            
            // Store repeat step and its arguments
            context.SetMetadata("repeat_step", step);
            context.SetMetadata("repeat_emit_before", emitBeforeRepeat);
            context.SetMetadata("repeat_traversers", new List<Traverser>(context.Traversers));
            
            // Extract the repeat traversal from the step's arguments
            // The first argument should contain the nested traversal as a string
            if (step.Arguments.Any())
            {
                var traversalArg = step.Arguments.First();
                if (traversalArg is string traversalStr)
                {
                    context.SetMetadata("repeat_traversal", traversalStr);
                }
            }
            
            // Clear the emit flag
            context.RemoveMetadata("emit");
            
            // Wait for until() or times() modulator - they will trigger execution
        }
        
        /// <summary>
        /// Execute the actual repeat loop - called by UntilStepExecutor or TimesStepExecutor
        /// </summary>
        public void ExecuteRepeatLoop(TinkerGraphStep repeatStep, TinkerTraversalContext context)
        {
            var originalTraversers = context.GetMetadata<List<Traverser>>("repeat_traversers") 
                                     ?? new List<Traverser>(context.Traversers);
            var emitBeforeRepeat = context.GetMetadata<bool>("repeat_emit_before");
            var emitAfterRepeat = context.GetMetadata<bool>("repeat_emit_after");
            var untilCondition = context.GetMetadata<TinkerGraphStep>("until_condition");
            var timesValue = context.GetMetadata<int?>("times_value");
            
            // Get the repeat traversal
            var repeatTraversalStr = context.GetMetadata<string>("repeat_traversal");
            
            // If no repeat traversal, just return the original traversers
            if (string.IsNullOrEmpty(repeatTraversalStr))
            {
                context.Traversers = originalTraversers;
                return;
            }
            
            var allResults = new List<Traverser>();
            var activeTraversers = new List<Traverser>();
            
            // Initialize active traversers with proper paths
            // CRITICAL: Paths must be initialized before repeat begins for tree() to work
            foreach (var traverser in originalTraversers)
            {
                // Create a copy to avoid modifying the original
                var newTraverser = traverser.Split();
                newTraverser.ResetLoops("repeat");
                
                // Ensure the current vertex is in the path
                // The path should already contain it from the start step, but verify
                var path = newTraverser.GetPath();
                if (path.Count == 0 && newTraverser.Value != null)
                {
                    // Path is empty, add the starting vertex
                    newTraverser.AddToPath(newTraverser.Value);
                }
                
                activeTraversers.Add(newTraverser);
            }
            
            // Emit before repeat (at depth 0) - only if emit() was before repeat()
            if (emitBeforeRepeat)
            {
                // CRITICAL FIX: Emit traversers must be available for subsequent filtering
                // Add them to allResults so they flow through to later steps (has, hasId, etc.)
                allResults.AddRange(activeTraversers.Select(t => t.Split()));
            }
            
            var maxIterations = timesValue ?? int.MaxValue;
            var iteration = 0;
            
            // Handle isolated vertex case: If no edges exist, emit should still return the start vertex
            bool hasExecutedAtLeastOnce = false;
            
            while (iteration < maxIterations && activeTraversers.Any())
            {
                // Increment loop counter BEFORE executing the traversal
                foreach (var traverser in activeTraversers)
                {
                    traverser.IncrementLoops("repeat");
                }
                
                // Execute the repeat traversal on each active traverser
                var nextIterationTraversers = new List<Traverser>();
                
                foreach (var traverser in activeTraversers)
                {
                    // Create a temp context for executing the repeat traversal
                    var tempContext = new TinkerTraversalContext();
                    // CRITICAL FIX: Split the traverser before adding to temp context
                    // This ensures path modifications don't affect the original
                    tempContext.Traversers.Add(traverser.Split());
                    
                    // Execute repeat traversal (e.g., "out('next')" or "g.out('next')")
                    ExecuteRepeatTraversal(repeatTraversalStr, tempContext);
                    
                    // Collect results - paths are already tracked by the step executors (e.g., OutStepExecutor)
                    foreach (var resultTraverser in tempContext.Traversers)
                    {
                        // Copy loop count from parent traverser
                        resultTraverser.Loops["repeat"] = traverser.GetLoops("repeat");
                        nextIterationTraversers.Add(resultTraverser);
                    }
                }
                
                hasExecutedAtLeastOnce = true;
                activeTraversers = nextIterationTraversers;
                iteration++;
                
                // CRITICAL FIX: If no traversers after first iteration and emit is set,
                // we already emitted the start vertex, so don't lose it
                if (!activeTraversers.Any() && emitBeforeRepeat && iteration == 1)
                {
                    // This is the isolated vertex case
                    // allResults already contains the emitted start vertex from above
                    break;
                }
                
                // Check until condition if provided
                // Separate traversers that meet the condition vs those that don't
                List<Traverser> toContinue;
                
                if (untilCondition != null)
                {
                    var toStop = new List<Traverser>();
                    toContinue = new List<Traverser>();
                    
                    foreach (var traverser in activeTraversers)
                    {
                        if (EvaluateUntilCondition(untilCondition, traverser, context))
                        {
                            // Until condition is met - this traverser should stop (and be emitted if emit is set)
                            toStop.Add(traverser);
                        }
                        else
                        {
                            // Until condition not met - continue with this traverser
                            toContinue.Add(traverser);
                        }
                    }
                    
                    // CRITICAL FIX FOR TREE: When until condition is present WITHOUT emit,
                    // we need to collect ALL traversers that met the condition to build the tree
                    // The tree() step needs the complete paths from start to leaf nodes
                    if (emitBeforeRepeat || emitAfterRepeat)
                    {
                        // Emit all active traversers at this iteration (before filtering by until)
                        allResults.AddRange(activeTraversers.Select(t => t.Split()));
                    }
                    else
                    {
                        // CRITICAL: No emit - add the traversers that met the until condition to final results
                        // These traversers maintain their complete paths from root to leaf for tree()
                        allResults.AddRange(toStop.Select(t => t.Split()));
                    }
                }
                else
                {
                    // No until condition
                    toContinue = activeTraversers;
                    
                    // Emit if emit is present
                    if (emitBeforeRepeat || emitAfterRepeat)
                    {
                        allResults.AddRange(activeTraversers.Select(t => t.Split()));
                    }
                }
                
                // Continue with traversers that haven't met the until condition
                activeTraversers = toContinue;
                
                // If no active traversers remain, break
                if (!activeTraversers.Any())
                {
                    break;
                }
            }
            
            // CRITICAL FIX: Handle case where repeat never executed but emit was set
            // (e.g., immediately met until condition or maxIterations was 0)
            if (!hasExecutedAtLeastOnce && emitBeforeRepeat && !allResults.Any())
            {
                // Emit the original traversers
                allResults.AddRange(originalTraversers.Select(t => t.Split()));
            }
            
            // CRITICAL FIX FOR TREE: If no until condition and no emit, add final results
            // This ensures tree() gets the leaf node traversers with complete paths
            if (untilCondition == null && !emitBeforeRepeat && !emitAfterRepeat)
            {
                allResults.AddRange(activeTraversers);
            }
            
            // IMPORTANT: Ensure we always have at least the starting traverser if nothing else worked
            // This handles edge cases where the graph structure doesn't match expected patterns
            if (!allResults.Any() && originalTraversers.Any())
            {
                // Return the original traversers so tree() has something to work with
                allResults.AddRange(originalTraversers.Select(t => t.Split()));
            }
            
            context.Traversers = allResults;
            
            // Clean up metadata
            context.RemoveMetadata("repeat_step");
            context.RemoveMetadata("repeat_traversers");
            context.RemoveMetadata("repeat_traversal");
            context.RemoveMetadata("repeat_emit_before");
            context.RemoveMetadata("emit");
            context.RemoveMetadata("repeat_emit_after");
            context.RemoveMetadata("until_condition");
            context.RemoveMetadata("times_value");
        }
        
        /// <summary>
        /// Execute the repeat traversal string on the given context
        /// </summary>
        private void ExecuteRepeatTraversal(string traversalStr, TinkerTraversalContext context)
        {
            // Remove leading 'g.' if present (anonymous traversal or reference to g)
            // CRITICAL FIX: Handle both 'g.out()' and '__.out()' patterns
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
        
        /// <summary>
        /// Parse repeat traversal string into steps
        /// </summary>
        private List<TinkerGraphStep> ParseRepeatTraversal(string traversalStr)
        {
            var steps = new List<TinkerGraphStep>();
            
            // Handle anonymous traversal prefix
            if (traversalStr.StartsWith("__"))
            {
                traversalStr = traversalStr.Substring(2);
                if (traversalStr.StartsWith("."))
                {
                    traversalStr = traversalStr.Substring(1);
                }
            }
            
            // Simple parser - split by '.' and parse each step
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
        
        /// <summary>
        /// Evaluate the until condition for a single traverser
        /// </summary>
        private bool EvaluateUntilCondition(TinkerGraphStep untilCondition, Traverser traverser, TinkerTraversalContext mainContext)
        {
            // Create a temporary context with just this traverser
            var tempContext = new TinkerTraversalContext();
            tempContext.Traversers.Add(traverser.Split());
            
            // Parse and execute the until condition
            var steps = ParseUntilCondition(untilCondition);
            
            foreach (var step in steps)
            {
                // Special handling for loops() step - we need to inject the current loop count
                if (step.StepName.Equals("loops", StringComparison.OrdinalIgnoreCase))
                {
                    // Manually set the loop count value on the traverser
                    var loopCount = traverser.GetLoops("repeat");
                    foreach (var t in tempContext.Traversers)
                    {
                        t.Value = loopCount;
                    }
                    continue;
                }
                
                ExecuteStepInContext(step, tempContext);
                
                // If no traversers remain, condition is not met
                if (!tempContext.HasTraversers)
                {
                    return false;
                }
            }
            
            // If traversers made it through, condition is met
            return tempContext.HasTraversers;
        }
        
        /// <summary>
        /// Parse the until condition into executable steps
        /// </summary>
        private List<TinkerGraphStep> ParseUntilCondition(TinkerGraphStep untilCondition)
        {
            var steps = new List<TinkerGraphStep>();
            
            // The until condition arguments contain the nested traversal
            // Example: until(loops().is(3)) -> Arguments contain the traversal steps
            // Example: until(__.outE('members').count().is(0)) -> Anonymous traversal
            
            if (untilCondition.Arguments.Any())
            {
                var condition = untilCondition.Arguments.First();
                
                // If the argument is a string, parse it as a nested traversal
                if (condition is string conditionStr)
                {
                    // Parse the condition string (e.g., "loops().is(3)" or "__.outE('members').count().is(0)")
                    steps = ParseNestedTraversal(conditionStr);
                }
                // If it's already a TinkerGraphStep, use it directly
                else if (condition is TinkerGraphStep step)
                {
                    steps.Add(step);
                }
            }
            
            return steps;
        }
        
        /// <summary>
        /// Parse a nested traversal string into steps
        /// </summary>
        private List<TinkerGraphStep> ParseNestedTraversal(string traversalStr)
        {
            var steps = new List<TinkerGraphStep>();
            
            // Handle anonymous traversal prefix (__.)
            if (traversalStr.StartsWith("__"))
            {
                traversalStr = traversalStr.Substring(2);
                if (traversalStr.StartsWith("."))
                {
                    traversalStr = traversalStr.Substring(1);
                }
            }
            
            // Simple parser for nested traversals like "loops().is(3)" or "outE('members').count().is(0)"
            // Split by '.' to get individual steps, but be careful with dots inside parentheses
            var parts = SplitByDotOutsideParentheses(traversalStr);
            
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                
                // Extract step name and arguments
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
                            // Parse simple arguments (numbers, strings, etc.)
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
        
        /// <summary>
        /// Split a string by dots, but ignore dots inside parentheses
        /// </summary>
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
        
        /// <summary>
        /// Split arguments by comma, but respect quotes and nested parentheses
        /// </summary>
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
        
        /// <summary>
        /// Parse a simple argument value
        /// </summary>
        private object ParseSimpleArgument(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return null;
            
            // Remove quotes if present
            if ((arg.StartsWith("'") && arg.EndsWith("'")) || 
                (arg.StartsWith("\"") && arg.EndsWith("\"")))
            {
                return arg.Substring(1, arg.Length - 2);
            }
            
            // Try parsing as number
            if (int.TryParse(arg, out int intVal))
                return intVal;
            
            if (double.TryParse(arg, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                return doubleVal;
            
            // Try parsing as boolean
            if (bool.TryParse(arg, out bool boolVal))
                return boolVal;
            
            return arg;
        }
        
        /// <summary>
        /// Execute a step in the given context using the appropriate step executor
        /// </summary>
        private void ExecuteStepInContext(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var stepName = step.StepName.ToLower();
            
            // Create and execute the appropriate step executor
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
                // Add more step executors as needed
                default:
                    throw new NotSupportedException($"Step '{stepName}' is not supported in nested traversal context");
            }
            
            executor?.Execute(step, context);
        }
    }
}
