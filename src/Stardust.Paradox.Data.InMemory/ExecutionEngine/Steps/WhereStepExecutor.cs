using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the where() filter step which applies conditional predicates.
    /// 
    /// Behavior:
    /// - Filters results based on predicates
    /// - Supports hasId(), otherV().hasId() patterns
    /// 
    /// Example:
    /// g.V().where(hasId('v1')) - filters to vertex with ID 'v1'
    /// g.V().outE().where(otherV().hasId('v2')) - filters edges connected to 'v2'
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
            "Supports patterns like hasId(), otherV().hasId() for filtering.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count == 0)
                return;

            var predicate = step.Arguments[0].ToString();

            // Parse common where predicate patterns
            if (predicate.Contains("__.otherV().hasId(") || predicate.Contains("otherV().hasId("))
            {
                // Handle __.otherV().hasId('vertexId') pattern
                var hasIdPattern = @"hasId\(['""]?([^'"")\s,]+)['""]?\)";
                var match = System.Text.RegularExpressions.Regex.Match(predicate, hasIdPattern);

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
                var match = System.Text.RegularExpressions.Regex.Match(predicate, hasIdPattern);

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
