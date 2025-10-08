using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the filter() step which filters traversers based on a boolean predicate.
    /// 
    /// Behavior:
    /// - Executes the filter traversal for each traverser
    /// - Keeps only traversers where the filter returns true/non-empty results
    /// - Common patterns: filter(__.has('prop', 'value')), filter(__.otherV().hasId('id'))
    /// 
    /// Example:
    /// g.V().filter(__.has('age', gt(30))) - keeps vertices with age > 30
    /// g.V().outE().filter(__.otherV().hasId('v2')) - keeps edges to vertex v2
    /// </summary>
    [UsedImplicitly]
    public class FilterStepExecutor : StepExecutorBase
    {
        public FilterStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "filter";

        public override string StepDescription => 
            "Filters traversers based on a boolean predicate. " +
            "Keeps only traversers where the filter traversal returns non-empty results.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Get the filter pattern
            var filterPattern = step.Arguments.FirstOrDefault()?.ToString() ?? "";
            
            if (string.IsNullOrWhiteSpace(filterPattern))
            {
                // No filter pattern, keep all traversers
                return;
            }

            var filteredTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                if (EvaluateFilter(traverser, filterPattern, context))
                {
                    filteredTraversers.Add(traverser);
                }
            }

            context.Traversers = filteredTraversers;
        }

        /// <summary>
        /// Evaluate the filter predicate for a traverser
        /// </summary>
        private bool EvaluateFilter(Traverser traverser, string filterPattern, TinkerTraversalContext context)
        {
            // Parse and execute the filter pattern
            // Common patterns:
            // - __.has('property', 'value')
            // - __.otherV().hasId('id')
            // - __.where(predicate)

            // Create a temporary context with just this traverser
            var tempContext = new TinkerTraversalContext();
            tempContext.Traversers.Add(traverser.Split());

            try
            {
                // Execute the filter pattern
                ExecuteFilterPattern(filterPattern, tempContext);

                // If the result is non-empty, the filter passes
                return tempContext.Traversers.Any();
            }
            catch
            {
                // If there's an error, the filter fails
                return false;
            }
        }

        /// <summary>
        /// Execute a filter pattern string
        /// </summary>
        private void ExecuteFilterPattern(string pattern, TinkerTraversalContext context)
        {
            // Remove __.  prefix if present
            pattern = pattern.TrimStart('_', '.');

            // Parse and execute the filter steps
            if (pattern.StartsWith("otherV()"))
            {
                // Execute otherV()
                ExecuteOtherVStep(context);

                // Check for chained steps
                var remaining = pattern.Substring(8).TrimStart('.');
                if (!string.IsNullOrEmpty(remaining))
                {
                    ExecuteFilterPattern(remaining, context);
                }
            }
            else if (pattern.StartsWith("hasId("))
            {
                // Extract the ID argument
                var startIdx = pattern.IndexOf('(') + 1;
                var endIdx = pattern.IndexOf(')', startIdx);
                if (endIdx > startIdx)
                {
                    var idArg = pattern.Substring(startIdx, endIdx - startIdx).Trim('\'', '"');
                    ExecuteHasIdStep(idArg, context);
                }
            }
            else if (pattern.StartsWith("has("))
            {
                // Extract property and value
                var startIdx = pattern.IndexOf('(') + 1;
                var endIdx = pattern.LastIndexOf(')');
                if (endIdx > startIdx)
                {
                    var args = pattern.Substring(startIdx, endIdx - startIdx);
                    var parts = SplitArguments(args);
                    
                    if (parts.Count >= 2)
                    {
                        var propertyName = parts[0].Trim('\'', '"');
                        var propertyValue = parts[1].Trim('\'', '"');
                        ExecuteHasStep(propertyName, propertyValue, context);
                    }
                    else if (parts.Count == 1)
                    {
                        // has(label) - check for label existence
                        var propertyName = parts[0].Trim('\'', '"');
                        ExecuteHasLabelStep(propertyName, context);
                    }
                }
            }
            else if (pattern.StartsWith("where("))
            {
                // Execute where step
                var startIdx = pattern.IndexOf('(') + 1;
                var endIdx = pattern.LastIndexOf(')');
                if (endIdx > startIdx)
                {
                    var wherePattern = pattern.Substring(startIdx, endIdx - startIdx);
                    ExecuteFilterPattern(wherePattern, context);
                }
            }
        }

        /// <summary>
        /// Execute otherV() step for edges
        /// </summary>
        private void ExecuteOtherVStep(TinkerTraversalContext context)
        {
            var results = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var edgeId = ExtractEdgeId(traverser.Value);
                if (edgeId != null)
                {
                    var edge = Database.GetEdge(edgeId);
                    if (edge != null)
                    {
                        // Get the "other" vertex (the one we're traversing to)
                        var otherVertex = Database.GetVertex(edge.InV);
                        if (otherVertex != null)
                        {
                            var newTraverser = traverser.Split();
                            newTraverser.Value = otherVertex.ToGremlinResponse();
                            results.Add(newTraverser);
                        }
                    }
                }
            }

            context.Traversers = results;
        }

        /// <summary>
        /// Execute hasId() filter
        /// </summary>
        private void ExecuteHasIdStep(string id, TinkerTraversalContext context)
        {
            var results = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractId(traverser.Value);
                if (vertexId == id)
                {
                    results.Add(traverser);
                }
            }

            context.Traversers = results;
        }

        /// <summary>
        /// Execute has() filter for property check
        /// </summary>
        private void ExecuteHasStep(string propertyName, string propertyValue, TinkerTraversalContext context)
        {
            var results = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var properties = ExtractProperties(traverser.Value);
                if (properties != null && properties.ContainsKey(propertyName))
                {
                    var actualValue = properties[propertyName]?.ToString();
                    if (actualValue == propertyValue)
                    {
                        results.Add(traverser);
                    }
                }
            }

            context.Traversers = results;
        }

        /// <summary>
        /// Execute hasLabel() filter
        /// </summary>
        private void ExecuteHasLabelStep(string label, TinkerTraversalContext context)
        {
            var results = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var actualLabel = ExtractLabel(traverser.Value);
                if (actualLabel == label)
                {
                    results.Add(traverser);
                }
            }

            context.Traversers = results;
        }

        /// <summary>
        /// Split arguments from a comma-separated string
        /// </summary>
        private List<string> SplitArguments(string args)
        {
            var result = new List<string>();
            var current = "";
            var depth = 0;
            var inString = false;
            var stringChar = '\0';

            foreach (var ch in args)
            {
                if (!inString && (ch == '\'' || ch == '"'))
                {
                    inString = true;
                    stringChar = ch;
                    current += ch;
                }
                else if (inString && ch == stringChar)
                {
                    inString = false;
                    current += ch;
                }
                else if (!inString && ch == '(')
                {
                    depth++;
                    current += ch;
                }
                else if (!inString && ch == ')')
                {
                    depth--;
                    current += ch;
                }
                else if (!inString && ch == ',' && depth == 0)
                {
                    result.Add(current.Trim());
                    current = "";
                }
                else
                {
                    current += ch;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                result.Add(current.Trim());
            }

            return result;
        }

        /// <summary>
        /// Extract edge ID from a traverser value
        /// </summary>
        private string ExtractEdgeId(dynamic value)
        {
            if (value == null) return null;

            try
            {
                // Handle different edge formats
                if (value is IDictionary<string, object> dict)
                {
                    if (dict.ContainsKey("id"))
                    {
                        return dict["id"]?.ToString();
                    }
                }

                return ExtractId(value);
            }
            catch
            {
                return null;
            }
        }
    }
}
