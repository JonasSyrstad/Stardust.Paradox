using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the groupCount() barrier step which counts occurrences by group.
    /// 
    /// Behavior:
    /// - Groups elements and counts occurrences in each group
    /// - Can use .by() modulator to specify grouping key
    /// - Returns a single map with group keys and counts
    /// 
    /// Example:
    /// g.V().groupCount() - counts all vertices (grouped by value)
    /// g.V().groupCount().by('label') - counts vertices by label
    /// g.V().groupCount().by('name') - counts vertices by name property
    /// </summary>
    [UsedImplicitly]
    public class GroupCountStepExecutor : StepExecutorBase
    {
        public GroupCountStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "groupcount";

        public override string StepDescription => 
            "Barrier step that groups elements and counts occurrences. " +
            "Can use .by() modulator to specify grouping key (e.g., label, property). " +
            "Returns a single map with group keys and their counts.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Enhanced group count implementation with proper .by() support
            var groups = new Dictionary<string, long>();

            // Check for .by() arguments from modulator step
            var allByArguments = context.GetMetadata<List<List<object>>>("all_by_arguments");
            var byArguments = context.GetMetadata<List<object>>("by_arguments");

            var hasByModulator = (allByArguments != null && allByArguments.Any()) || (byArguments != null && byArguments.Any());

            foreach (var traverser in context.Traversers)
            {
                var key = "default";

                // Use .by() arguments if available, otherwise fall back to direct arguments
                if (allByArguments != null && allByArguments.Any())
                {
                    // Use the first .by() argument from the dual modulator system
                    var groupingKey = allByArguments[0][0].ToString();
                    if (groupingKey.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        key = ExtractLabel(traverser.Value) ?? "unknown";
                    }
                    else
                    {
                        var properties = ExtractProperties(traverser.Value);
                        if (properties != null && properties.ContainsKey(groupingKey))
                        {
                            var propertyValue = properties[groupingKey];
                            key = propertyValue?.ToString() ?? "null";
                        }
                        else
                        {
                            key = "null";
                        }
                    }
                }
                else if (byArguments != null && byArguments.Any())
                {
                    // Use the single .by() argument
                    var groupingKey = byArguments[0].ToString();
                    if (groupingKey.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        key = ExtractLabel(traverser.Value) ?? "unknown";
                    }
                    else
                    {
                        var properties = ExtractProperties(traverser.Value);
                        if (properties != null && properties.ContainsKey(groupingKey))
                        {
                            var propertyValue = properties[groupingKey];
                            key = propertyValue?.ToString() ?? "null";
                        }
                        else
                        {
                            key = "null";
                        }
                    }
                }
                else if (step.Arguments.Any())
                {
                    var groupingKey = step.Arguments[0].ToString();
                    if (groupingKey.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        key = ExtractLabel(traverser.Value) ?? "unknown";
                    }
                    else
                    {
                        var properties = ExtractProperties(traverser.Value);
                        if (properties != null && properties.ContainsKey(groupingKey))
                        {
                            var propertyValue = properties[groupingKey];
                            key = propertyValue?.ToString() ?? "null";
                        }
                        else
                        {
                            key = "null";
                        }
                    }
                }
                else
                {
                    key = traverser.Value?.ToString() ?? "null";
                }

                if (!groups.ContainsKey(key))
                {
                    groups[key] = 0;
                }

                groups[key] += traverser.Bulk;
            }

            // Clear the .by() arguments after use
            context.RemoveMetadata("all_by_arguments");
            context.RemoveMetadata("by_arguments");

            context.Clear();
            context.Traversers.Add(new Traverser(groups));
        }
    }
}
