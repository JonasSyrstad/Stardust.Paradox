using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the loops() step which returns the number of times the traverser
    /// has gone through a loop.
    /// 
    /// Behavior:
    /// - loops(): Maps traversers to the number of loop iterations
    /// - Works within repeat() steps to access the current loop counter
    /// 
    /// This is typically used within repeat() steps to access loop counter.
    /// </summary>
    [UsedImplicitly]
    public class LoopsStepExecutor : IStepExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public LoopsStepExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        public string StepName => "loops";

        public string StepDescription => 
            "Returns the number of times the traverser has gone through a loop. " +
            "loops() is typically used within repeat() steps.";

        public void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Map each traverser to its loop count
            var updatedTraversers = new List<Traverser>();
            
            foreach (var traverser in context.Traversers)
            {
                var newTraverser = traverser.Split();
                
                // Get loop count from traverser - use 'repeat' as the default loop name
                var loopCount = traverser.GetLoops("repeat");
                
                // If no repeat loop found, try to get any loop count
                if (loopCount == 0 && traverser.Loops.Any())
                {
                    // Get the most recent loop count (last value)
                    loopCount = traverser.Loops.Values.Last();
                }
                
                newTraverser.Value = loopCount;
                newTraverser.AddToPath(loopCount);
                
                updatedTraversers.Add(newTraverser);
            }
            
            context.Traversers = updatedTraversers;
        }
    }
}
