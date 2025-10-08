using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the in() step which traverses incoming edges to vertices.
    /// 
    /// Behavior:
    /// - in(): Traverse all incoming edges regardless of label
    /// - in('label'): Traverse only incoming edges with the specified label
    /// 
    /// This step navigates from vertices to their adjacent vertices via incoming edges.
    /// It adds the source vertices to the path for tracking.
    /// </summary>
    [UsedImplicitly]
    public class InStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public InStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "in";

        public string StepDescription => 
            "Traverses from vertices to adjacent vertices via incoming edges. " +
            "in() traverses all incoming edges. " +
            "in('label') traverses only edges with the specified label.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var edgeLabel = step.GetFirstStringArgument();
            var newTraversers = new List<Traverser>();

            foreach (var traverser in context.Traversers)
            {
                var vertexId = ExtractVertexId(traverser.Value);
                if (!string.IsNullOrEmpty(vertexId))
                {
                    var inVertices = _database.GetInVertices(vertexId, edgeLabel);
                    foreach (var vertex in inVertices)
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
