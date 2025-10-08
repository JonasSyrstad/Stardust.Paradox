using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the explain() step - returns an explanation of the traversal execution plan
    /// TinkerPop spec: explain() provides details about how the query will be executed
    /// </summary>
    public class ExplainStepExecutor : StepExecutorBase
    {
        public ExplainStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "explain";

        public override string StepDescription => "Returns an explanation of the traversal execution plan with optimization suggestions";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Build an explanation of the traversal steps
            var explanation = new Dictionary<string, object>();

            // Get all executed steps from context metadata
            var executedSteps = context.GetMetadata<List<string>>("allSteps") ?? new List<string>();

            // Build step chain
            var stepChain = string.Join(" -> ", executedSteps);
            
            explanation["category"] = "traversal";
            explanation["traversal"] = stepChain;
            
            // Strategy information
            explanation["strategies"] = new List<string>
            {
                "InMemoryGraphStep",
                "IdentityRemovalStrategy",
                "FilterRankingStrategy"
            };

            // Traversal info
            explanation["originalTraversal"] = $"[{stepChain}]";
            explanation["finalTraversal"] = $"[{stepChain}]";
            
            // Traverser counts
            explanation["inputTraverserCount"] = context.GetMetadata<int>("inputTraverserCount");
            explanation["currentTraverserCount"] = context.Traversers.Count;

            // Database statistics
            explanation["databaseInfo"] = new Dictionary<string, object>
            {
                ["vertexCount"] = Database.GetAllVertices().Count(),
                ["edgeCount"] = Database.GetAllEdges().Count(),
                ["type"] = "InMemoryGraph"
            };

            // Estimated complexity
            var complexity = CalculateComplexity(executedSteps);
            explanation["estimatedComplexity"] = complexity;
            explanation["complexityRating"] = GetComplexityRating(complexity);

            // Optimization suggestions
            var suggestions = GenerateOptimizationSuggestions(executedSteps);
            if (suggestions.Any())
            {
                explanation["optimizationSuggestions"] = suggestions;
            }

            // Replace current traversers with explanation result
            context.Traversers.Clear();
            var explainTraverser = new Traverser(explanation);
            context.Traversers.Add(explainTraverser);
        }

        private int CalculateComplexity(List<string> steps)
        {
            int complexity = 0;

            foreach (var step in steps)
            {
                var stepLower = step.ToLower();
                
                // High complexity steps
                if (stepLower.Contains("repeat") || stepLower.Contains("until"))
                    complexity += 10;
                else if (stepLower.Contains("match"))
                    complexity += 8;
                else if (stepLower.Contains("union"))
                    complexity += 5;
                else if (stepLower.Contains("coalesce") || stepLower.Contains("choose"))
                    complexity += 4;
                // Medium complexity steps
                else if (stepLower.Contains("path") || stepLower.Contains("tree"))
                    complexity += 3;
                else if (stepLower.Contains("group") || stepLower.Contains("aggregate"))
                    complexity += 3;
                // Low complexity steps
                else if (stepLower.Contains("has") || stepLower.Contains("filter"))
                    complexity += 1;
                else
                    complexity += 1;
            }

            return complexity;
        }

        private string GetComplexityRating(int complexity)
        {
            if (complexity <= 5) return "LOW";
            if (complexity <= 15) return "MEDIUM";
            if (complexity <= 30) return "HIGH";
            return "VERY_HIGH";
        }

        private List<string> GenerateOptimizationSuggestions(List<string> steps)
        {
            var suggestions = new List<string>();

            // Check for filter after expansion
            for (int i = 1; i < steps.Count; i++)
            {
                var current = steps[i].ToLower();
                var previous = steps[i - 1].ToLower();

                if ((previous.Contains("out") || previous.Contains("in") || previous.Contains("both")) &&
                    (current.Contains("has") || current.Contains("filter")))
                {
                    suggestions.Add("Consider moving filters earlier in the traversal for better performance");
                    break;
                }
            }

            // Check for missing dedup after expansions
            bool hasExpansion = steps.Any(s => 
                s.ToLower().Contains("out") || s.ToLower().Contains("in") || s.ToLower().Contains("both"));
            bool hasDedup = steps.Any(s => s.ToLower().Contains("dedup"));

            if (hasExpansion && !hasDedup)
            {
                suggestions.Add("Consider adding dedup() to remove duplicate results after edge traversals");
            }

            // Check for limit usage with ordering
            bool hasOrder = steps.Any(s => s.ToLower().Contains("order"));
            bool hasLimit = steps.Any(s => s.ToLower().Contains("limit"));

            if (hasOrder && !hasLimit)
            {
                suggestions.Add("Consider adding limit() after order() to reduce result set size");
            }

            return suggestions;
        }
    }
}
