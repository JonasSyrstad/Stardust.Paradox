using Stardust.Paradox.Data.Annotations.Annotations;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the hasId() step which filters elements by their ID.
    /// 
    /// Behavior:
    /// - hasId(id1, id2, ...): Filters to keep only elements with one of the specified IDs
    /// 
    /// Example:
    /// g.V().hasId('vertex1', 'vertex2') - vertices with ID vertex1 or vertex2
    /// </summary>
    [UsedImplicitly]
    public class HasIdStepExecutor : StepExecutorBase
    {
        public HasIdStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "hasid";

        public override string StepDescription => 
            "Filters elements by their ID. " +
            "hasId(id1, id2, ...) keeps only elements whose ID matches one of the provided values.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Get parameters from context to resolve ParameterReference objects
            var parameters = context.GetMetadata<System.Collections.Generic.Dictionary<string, object>>("parameters") 
                             ?? new System.Collections.Generic.Dictionary<string, object>();
            
            // Resolve all parameter references in the arguments
            var resolvedIds = new System.Collections.Generic.HashSet<string>();
            foreach (var arg in step.Arguments)
            {
                var resolved = ResolveParameter(arg, parameters);
                if (resolved != null)
                {
                    resolvedIds.Add(resolved.ToString());
                }
            }

            context.Filter(traverser =>
            {
                var id = ExtractId(traverser.Value);
                return id != null && resolvedIds.Contains(id);
            });
        }
        
        /// <summary>
        /// Resolve a ParameterReference to its actual value from the parameters dictionary
        /// </summary>
        private object ResolveParameter(object value, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            if (value is ParameterReference paramRef)
            {
                if (parameters.TryGetValue(paramRef.ParameterName, out var resolvedValue))
                {
                    return resolvedValue;
                }
                // If parameter not found, return the parameter name as a string (fallback)
                return paramRef.ParameterName;
            }
            return value;
        }
    }
}
