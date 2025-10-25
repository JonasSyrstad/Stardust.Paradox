using Stardust.Paradox.Data.Annotations.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the hasNot() step which filters elements based on property absence.
    /// 
    /// Behavior:
  /// - hasNot('property'): Filters to keep only elements that do NOT have the specified property
    /// 
    /// This is the inverse of has('property').
    /// 
 /// Example:
    /// g.V().hasNot('age') - vertices that do not have an age property
    /// </summary>
    [UsedImplicitly]
    public class HasNotStepExecutor : StepExecutorBase
    {
     public HasNotStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
  }

        public override string StepName => "hasNot";

        public override string StepDescription =>
            "Filters elements based on property absence. " +
            "hasNot('key') checks if property does NOT exist.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (step.Arguments.Count < 1)
return;

      // Resolve parameter references for type-safe matching
        var parameters = context.GetMetadata<Dictionary<string, object>>("parameters");
     var resolvedArguments = step.Arguments.Select(arg => ResolveParameterReference(arg, parameters)).ToList();

         var key = resolvedArguments[0].ToString();

   // hasNot(key) - check if property does NOT exist
            if (key.Equals("label", System.StringComparison.OrdinalIgnoreCase))
            {
     // Special case: hasNot('label') - filter out all elements (all elements have labels)
       context.Filter(traverser => false);
        }
   else if (key.Equals("id", System.StringComparison.OrdinalIgnoreCase))
      {
                // Special case: hasNot('id') - filter out all elements (all elements have IDs)
   context.Filter(traverser => false);
         }
         else
            {
 // Normal case: check if property does NOT exist
     context.Filter(traverser =>
             {
        var properties = ExtractProperties(traverser.Value);
      return properties == null || !properties.ContainsKey(key);
         });
     }
      }

  /// <summary>
        /// Resolve parameter references like __p0, __p1 to their actual values
        /// </summary>
        private object ResolveParameterReference(object argument, Dictionary<string, object> parameters)
        {
    // Handle direct ParameterReference objects (from TinkerGraphQueryParser)
            if (argument is ParameterReference paramRef && parameters != null)
    {
    if (parameters.TryGetValue(paramRef.ParameterName, out var value))
{
    return value;
       }
     }

    // Handle string arguments that might be parameter names (__p0, p1, etc.)
      if (argument is string argStr && parameters != null)
            {
    // Check if this string looks like a parameter reference
      if (System.Text.RegularExpressions.Regex.IsMatch(argStr, @"^__p\d+$") || 
         System.Text.RegularExpressions.Regex.IsMatch(argStr, @"^p\d+$"))
              {
           // Try to resolve it from parameters
  if (parameters.TryGetValue(argStr, out var value))
    {
     return value;
               }
      }
   }

            return argument;
        }
    }
}
