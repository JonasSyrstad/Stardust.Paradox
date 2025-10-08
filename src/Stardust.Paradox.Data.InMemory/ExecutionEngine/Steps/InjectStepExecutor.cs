using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the inject() step which injects arbitrary values into the traversal stream.
    /// 
    /// Behavior:
    /// - inject(val1, val2, ...): Injects the specified values as traversers
    /// 
    /// This step adds new traversers with the specified values to the stream.
    /// </summary>
    [UsedImplicitly]
    public class InjectStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public InjectStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "inject";

        public string StepDescription => 
            "Injects arbitrary values into the traversal stream. " +
            "inject(val1, val2, ...) adds the specified values as new traversers.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                return; // No values to inject
            }
            
            var newTraversers = new List<Traverser>();
            
            // Keep existing traversers if any
            newTraversers.AddRange(context.Traversers);
            
            // Inject each argument as a new traverser (TinkerPop standard)
            foreach (var arg in step.Arguments)
            {
                var traverser = new Traverser(arg);
                traverser.AddToPath(arg);
                newTraversers.Add(traverser);
            }
            
            context.Traversers = newTraversers;
        }
    }
}
