using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the where() filter step which applies conditional predicates.
    /// 
    /// Behavior:
    /// - Filters results based on predicates
    /// - Supports hasId(), otherV().hasId() patterns
    /// - Supports select('label').not(has(...)) patterns for filtering based on labeled elements
    /// 
    /// Example:
    /// g.V().where(hasId('v1')) - filters to vertex with ID 'v1'
    /// g.V().outE().where(otherV().hasId('v2')) - filters edges connected to 'v2'
    /// g.V().as('a').out().as('b').path().unfold().where(select('a').not(has('name','John'))) - filters unfolded path elements
    /// </summary>
    [UsedImplicitly]
    public class WhereStepExecutor : StepExecutorBase
    {
        public WhereStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "where";

        public override string StepDescription => 
            "Filters results based on conditional predicates. " +
            "Supports patterns like hasId(), otherV().hasId(), and select('label').predicate() for filtering.";

        /// <summary>
        /// Handle logical operators like or(...) and and(...) within where() step
        /// </summary>
        private void HandleLogicalOperator(TinkerGraphStep step, TinkerTraversalContext context, string predicate)
        {
            bool isOrOperator = predicate.StartsWith("or(");
     
            // Extract the conditions between the parentheses
            var conditionsStart = predicate.IndexOf('(') + 1;
            var conditionsEnd = predicate.LastIndexOf(')');
       
            if (conditionsEnd <= conditionsStart)
              return; // Invalid format

            var conditionsStr = predicate.Substring(conditionsStart, conditionsEnd - conditionsStart);
  
            // Split by commas but respect nested parentheses
            var conditions = SplitConditions(conditionsStr);

            // Create a new step with the or() or and() arguments for the respective executor
            var logicalStep = new TinkerGraphStep(isOrOperator ? "or" : "and");
            foreach (var condition in conditions)
       {
          logicalStep.Arguments.Add(condition);
    }
    
            // Get the appropriate executor
            var executorType = isOrOperator ? typeof(OrStepExecutor) : typeof(AndStepExecutor);
            var executor = System.Activator.CreateInstance(executorType, Database) as IStepExecutor;
   
            if (executor != null)
            {
                executor.Execute(logicalStep, context);
            }
        }

        /// <summary>
        /// Split conditions by comma, respecting nested parentheses
        /// </summary>
        private List<string> SplitConditions(string conditionsStr)
        {
            var conditions = new List<string>();
            var currentCondition = "";
            var parenDepth = 0;
       
            foreach (var ch in conditionsStr)
             {
                if (ch == '(')
                    parenDepth++;
                else if (ch == ')')
                    parenDepth--;
                else if (ch == ',' && parenDepth == 0)
                 {
                    conditions.Add(currentCondition.Trim());
                    currentCondition = "";
                    continue;
               }
       
                currentCondition += ch;
            }
    
            if (!string.IsNullOrWhiteSpace(currentCondition))
                conditions.Add(currentCondition.Trim());
     
            return conditions;
        }

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return;

            var predicate = step.Arguments[0].ToString();

            // Handle or(...) and and(...) logical operators first
            if (predicate.StartsWith("or(") || predicate.StartsWith("and("))
            {
                HandleLogicalOperator(step, context, predicate);
                return;
            }

            // Handle select('label').not(has(...)) pattern
            var selectNotHasMatch = Regex.Match(predicate, @"select\(['""]?(\w+)['""]?\)\.not\(has\(['""]?(\w+)['""]?\s*,\s*['""]?([^'""]+)['""]?\)\)", RegexOptions.IgnoreCase);
            if (selectNotHasMatch.Success)
            {
                var label = selectNotHasMatch.Groups[1].Value;
                var propertyKey = selectNotHasMatch.Groups[2].Value;
                var propertyValue = selectNotHasMatch.Groups[3].Value;

                context.Filter(traverser =>
                {
                    // Get the labeled element from the traverser
                    var selectedElement = traverser.GetTagged<dynamic>(label);
                    
                    if (selectedElement == null)
                    {
                        // If we can't find the labeled element, keep the traverser
                        // (this might be a vertex/element that wasn't labeled)
                        return true;
                    }

                    // Check if the selected element has the specified property with the specified value
                    var properties = ExtractProperties(selectedElement);
                    if (properties == null || !properties.ContainsKey(propertyKey))
                    {
                        // Property doesn't exist - passes the not(has(...)) filter
                        return true;
                    }

                    var actualValue = properties[propertyKey];
                    var actualValueStr = actualValue?.ToString() ?? "";
                    
                    // not(has(...)) means: keep if property value does NOT match
                    return !actualValueStr.Equals(propertyValue, System.StringComparison.Ordinal);
                });
                return;
            }

            // Handle select('label').has(...) pattern
            var selectHasMatch = Regex.Match(predicate, @"select\(['""]?(\w+)['""]?\)\.has\(['""]?(\w+)['""]?\s*,\s*['""]?([^'""]+)['""]?\)\)", RegexOptions.IgnoreCase);
            if (selectHasMatch.Success)
            {
                var label = selectHasMatch.Groups[1].Value;
                var propertyKey = selectHasMatch.Groups[2].Value;
                var propertyValue = selectHasMatch.Groups[3].Value;

                context.Filter(traverser =>
                {
                    // Get the labeled element from the traverser
                    var selectedElement = traverser.GetTagged<dynamic>(label);
                    
                    if (selectedElement == null)
                    {
                        // If we can't find the labeled element, filter it out
                        return false;
                    }

                    // Check if the selected element has the specified property with the specified value
                    var properties = ExtractProperties(selectedElement);
                    if (properties == null || !properties.ContainsKey(propertyKey))
                    {
                        // Property doesn't exist - doesn't pass the has(...) filter
                        return false;
                    }

                    var actualValue = properties[propertyKey];
                    var actualValueStr = actualValue?.ToString() ?? "";
                    
                    // has(...) means: keep if property value matches
                    return actualValueStr.Equals(propertyValue, System.StringComparison.Ordinal);
                });
                return;
            }

            // Handle select('label').not(has('property')) pattern (property existence check)
            var selectNotHasExistsMatch = Regex.Match(predicate, @"select\(['""]?(\w+)['""]?\)\.not\(has\(['""]?(\w+)['""]?\)\)", RegexOptions.IgnoreCase);
            if (selectNotHasExistsMatch.Success)
            {
                var label = selectNotHasExistsMatch.Groups[1].Value;
                var propertyKey = selectNotHasExistsMatch.Groups[2].Value;

                context.Filter(traverser =>
                {
                    // Get the labeled element from the traverser
                    var selectedElement = traverser.GetTagged<dynamic>(label);
                    
                    if (selectedElement == null)
                    {
                        // If we can't find the labeled element, keep the traverser
                        return true;
                    }

                    // Check if the selected element has the specified property
                    var properties = ExtractProperties(selectedElement);
                    
                    // not(has('property')) means: keep if property does NOT exist
                    return properties == null || !properties.ContainsKey(propertyKey);
                });
                return;
            }

            // Parse common where predicate patterns
            if (predicate.Contains("__.otherV().hasId(") || predicate.Contains("otherV().hasId("))
            {
                // Handle __.otherV().hasId('vertexId') pattern
                var hasIdPattern = @"hasId\(['""]?([^'"")\s,]+)['""]?\)";
                var match = Regex.Match(predicate, hasIdPattern);

                if (match.Success)
                {
                    var targetVertexId = match.Groups[1].Value.Trim();

                    var newTraversers = new List<Traverser>();

                    foreach (var traverser in context.Traversers)
                    {
                        // This filter should only apply to edges
                        var edgeId = ExtractEdgeId(traverser.Value);
                        if (!string.IsNullOrEmpty(edgeId))
                        {
                            var edge = Database.GetEdge(edgeId);
                            if (edge != null)
                            {
                                // Check if the edge connects to the target vertex
                                // For outgoing edges (outE), otherV would be the inV
                                var matches = edge.InVertexId.Equals(targetVertexId, System.StringComparison.OrdinalIgnoreCase);

                                if (matches)
                                {
                                    newTraversers.Add(traverser);
                                }
                            }
                        }
                    }

                    context.Traversers = newTraversers;
                }
            }
            else if (predicate.Contains("hasId("))
            {
                // Handle direct hasId('vertexId') pattern
                var hasIdPattern = @"hasId\(['""]?([^'"")\s,]+)['""]?\)";
                var match = Regex.Match(predicate, hasIdPattern);

                if (match.Success)
                {
                    var targetId = match.Groups[1].Value.Trim();

                    context.Filter(traverser =>
                    {
                        var id = ExtractId(traverser.Value);
                        return id != null && id.Equals(targetId, System.StringComparison.OrdinalIgnoreCase);
                    });
                }
            }
            else
            {
                // For other predicates, leave traversers unchanged
                // This would need enhancement to handle complex predicates
            }
        }
    }
}
