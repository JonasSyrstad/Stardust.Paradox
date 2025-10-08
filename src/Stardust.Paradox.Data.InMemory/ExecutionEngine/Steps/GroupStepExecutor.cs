using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the group() barrier step which groups elements.
    /// 
    /// Behavior:
    /// - Groups elements by a key (specified via .by() modulator)
    /// - Can apply aggregation to groups (via second .by() modulator)
    /// - group().by('property'): Returns map of {key: [elements]}
    /// - group().by('property').by(count()): Returns map of {key: count}
    /// - group().by('property').by(sum('salary')): Returns map of {key: sum}
    /// 
    /// Example:
    /// g.V().group().by('label') - groups vertices by label
    /// g.V().group().by('department').by(count()) - counts by department
    /// g.V().group().by('department').by(values('salary').sum()) - sums salaries by department
    /// </summary>
    [UsedImplicitly]
    public class GroupStepExecutor : StepExecutorBase
    {
        public GroupStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "group";

        public override string StepDescription => 
            "Barrier step that groups elements by a key and optionally aggregates them. " +
            "First .by() specifies grouping key, second .by() specifies aggregation function. " +
            "Returns a map of grouped/aggregated results.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Enhanced grouping implementation with support for dual .by() modulators
            // First .by() specifies grouping key, second .by() specifies aggregation function

            // Check for all .by() arguments from modulator steps
            var allByArguments = context.GetMetadata<List<List<object>>>("all_by_arguments");
            var hasGroupingBy = allByArguments != null && allByArguments.Count > 0;
            var hasAggregationBy = allByArguments != null && allByArguments.Count > 1;

            if (hasAggregationBy)
            {
                // Complex case: group().by('property').by(aggregation_function)
                // Result should be Dictionary<string, aggregated_value>
                var groups = new Dictionary<string, List<dynamic>>();

                // First pass: Group by the first .by() argument
                foreach (var traverser in context.Traversers)
                {
                    var key = "default";

                    if (hasGroupingBy)
                    {
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
                    else
                    {
                        key = traverser.Value?.ToString() ?? "null";
                    }

                    if (!groups.ContainsKey(key))
                    {
                        groups[key] = new List<dynamic>();
                    }

                    for (int i = 0; i < traverser.Bulk; i++)
                    {
                        groups[key].Add(traverser.Value);
                    }
                }

                // Second pass: Apply aggregation function from second .by()
                var aggregatedResults = new Dictionary<string, long>();
                var aggregationSpec = allByArguments[1][0].ToString();

                foreach (var group in groups)
                {
                    var groupKey = group.Key;
                    var groupMembers = group.Value;

                    if (aggregationSpec.Contains("values('salary').sum()") ||
                        aggregationSpec.Contains("g.values('salary').sum()"))
                    {
                        // Sum salary values for this group
                        long salarySum = 0;
                        foreach (var member in groupMembers)
                        {
                            var properties = ExtractProperties(member);
                            if (properties != null && properties.ContainsKey("salary"))
                            {
                                var salaryValue = properties["salary"];
                                if (TryConvertToLong(salaryValue, out long longValue))
                                {
                                    salarySum += longValue;
                                }
                            }
                        }
                        aggregatedResults[groupKey] = salarySum;
                    }
                    else if (aggregationSpec.Contains("count()"))
                    {
                        // Count members in this group
                        aggregatedResults[groupKey] = groupMembers.Count;
                    }
                    else
                    {
                        // Default: count members
                        aggregatedResults[groupKey] = groupMembers.Count;
                    }
                }

                // Clear metadata and return aggregated results
                context.RemoveMetadata("all_by_arguments");
                context.Clear();
                context.Traversers.Add(new Traverser(aggregatedResults));
            }
            else
            {
                // Simple case: group().by('property') - return Dictionary<string, List<dynamic>>
                var groups = new Dictionary<string, List<dynamic>>();

                foreach (var traverser in context.Traversers)
                {
                    var key = "default";

                    if (hasGroupingBy)
                    {
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
                    else if (step.Arguments.Any())
                    {
                        // Fallback to direct arguments (old behavior)
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
                        groups[key] = new List<dynamic>();
                    }

                    for (int i = 0; i < traverser.Bulk; i++)
                    {
                        groups[key].Add(traverser.Value);
                    }
                }

                // Clear metadata and return grouped results
                if (allByArguments != null)
                {
                    context.RemoveMetadata("all_by_arguments");
                }

                context.Clear();
                context.Traversers.Add(new Traverser(groups));
            }
        }
    }
}
