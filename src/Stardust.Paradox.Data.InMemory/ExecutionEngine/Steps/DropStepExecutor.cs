using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the drop() step which removes elements from the database.
    /// 
    /// Behavior:
    /// - Removes vertices or edges from the database
    /// - Returns empty results after dropping
    /// 
    /// Example:
    /// g.V('1').drop() - removes vertex with ID '1'
    /// g.E('e1').drop() - removes edge with ID 'e1'
    /// </summary>
    [UsedImplicitly]
    public class DropStepExecutor : StepExecutorBase
    {
        public DropStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "drop";

        public override string StepDescription => 
            "Removes elements (vertices or edges) from the database. " +
            "Returns empty results after dropping.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Drop vertices/edges from the database
            foreach (var traverser in context.Traversers)
            {
                var id = ExtractId(traverser.Value);
                if (!string.IsNullOrEmpty(id))
                {
                    // Determine if this is a vertex or edge by checking the type or structure
                    var elementType = ExtractType(traverser.Value);
                    
                    if (elementType == "edge" || IsEdgeStructure(traverser.Value))
                    {
                        // It's an edge, try to remove it
                        Database.RemoveEdge(id);
                    }
                    else
                    {
                        // Try to drop as vertex first, then as edge if vertex doesn't exist
                        if (!Database.RemoveVertex(id))
                        {
                            Database.RemoveEdge(id);
                        }
                    }
                }
            }

            // Drop step returns empty results
            context.Traversers = new List<Traverser>();
        }

        /// <summary>
        /// Check if the value structure represents an edge
        /// </summary>
        private bool IsEdgeStructure(dynamic value)
        {
            if (value == null)
                return false;

            try
            {
                // Check for edge-specific properties
                if (value is Core.GremlinResponseObject responseObj)
                {
                    var type = responseObj.Get<string>("type");
                    if (type == "edge")
                        return true;
                        
                    // Check for inV/outV properties which indicate an edge
                    var hasInV = responseObj.Get<object>("inV") != null;
                    var hasOutV = responseObj.Get<object>("outV") != null;
                    return hasInV || hasOutV;
                }

                // Check dynamic object
                try
                {
                    var dynamicValue = (dynamic)value;
                    if (dynamicValue.inV != null || dynamicValue.outV != null)
                        return true;
                    if (dynamicValue.type == "edge")
                        return true;
                }
                catch
                {
                    // Ignore
                }
            }
            catch
            {
                // If we can't determine, return false
            }

            return false;
        }
    }
}
