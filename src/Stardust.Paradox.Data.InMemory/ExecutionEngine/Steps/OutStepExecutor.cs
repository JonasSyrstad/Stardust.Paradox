using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the out() step which traverses outgoing edges from vertices.
    /// 
    /// Behavior:
    /// - out(): Traverse all outgoing edges regardless of label
    /// - out('label'): Traverse only outgoing edges with the specified label
    /// 
    /// This step navigates from vertices to their adjacent vertices via outgoing edges.
    /// It adds the destination vertices to the path for tracking.
    /// </summary>
    [UsedImplicitly]
    public class OutStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public OutStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "out";

        public string StepDescription => 
            "Traverses from vertices to adjacent vertices via outgoing edges. " +
            "out() traverses all outgoing edges. " +
            "out('label') traverses only edges with the specified label.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Get parameters from context to resolve ParameterReference objects
            var parameters = context.GetMetadata<Dictionary<string, object>>("parameters") 
                             ?? new Dictionary<string, object>();
            
            // Resolve the edge label parameter if present
            var edgeLabel = step.GetFirstStringArgument();
            if (!string.IsNullOrEmpty(edgeLabel))
            {
                // Check if this is a ParameterReference that needs resolving
                if (step.Arguments.Count > 0 && step.Arguments[0] is ParameterReference paramRef)
                {
                    if (parameters.TryGetValue(paramRef.ParameterName, out var resolvedValue))
                    {
                        edgeLabel = resolvedValue?.ToString();
                    }
                }
            }
            
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var outVertices = _database.GetOutVertices(vertexId, edgeLabel);
                    foreach (var vertex in outVertices)
                    {
                        var newTraverser = traverser.Split();
                        newTraverser.Value = vertex.ToGremlinResponse();
                        
                        // Add current step to path for path tracking
                        newTraverser.AddToPath(vertex.ToGremlinResponse());
                        
                        newTraversers.Add(newTraverser);
                    }
                }
            }

            context.Traversers = newTraversers;
        }

        private string ExtractVertexId(dynamic value)
        {
            if (value == null) return null;
            
            try
            {
                if (value is GremlinResponseObject responseObj)
                {
                    return responseObj.id?.ToString();
                }
                
                if (value is System.Collections.Generic.IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("id", out var id))
                    {
                        return id?.ToString();
                    }
                }
                
                try
                {
                    var dynamicId = value.id;
                    if (dynamicId != null)
                    {
                        return dynamicId.ToString();
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
                
                if (value is string strValue)
                {
                    return strValue;
                }
                
                if (value is int || value is long)
                {
                    return value.ToString();
                }
            }
            catch (System.Exception)
            {
                // If all else fails, return null
            }
            
            return null;
        }
    }
}
