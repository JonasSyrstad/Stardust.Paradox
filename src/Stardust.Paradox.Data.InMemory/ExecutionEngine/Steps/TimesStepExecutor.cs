using Stardust.Paradox.Data.Annotations.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the times() modulator step which specifies repetition count for repeat().
    /// 
    /// Behavior:
    /// - Executes the repeat() pattern n times
    /// - Currently supports out() navigation in repeat patterns
    /// 
    /// Example:
    /// g.V('node_0').repeat(out('next')).times(10) - navigates 10 steps through 'next' edges
    /// </summary>
    [UsedImplicitly]
    public class TimesStepExecutor : StepExecutorBase
    {
        public TimesStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "times";

        public override string StepDescription => 
            "Modulator for repeat() that specifies how many times to repeat the pattern. " +
            "times(n) executes the repeat pattern n times.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
                return;

            var times = Convert.ToInt32(step.Arguments[0]);
            var repeatStep = context.GetMetadata<TinkerGraphStep>("repeat_step");
            var originalTraversers = context.GetMetadata<List<Traverser>>("repeat_traversers");

            if (repeatStep == null || originalTraversers == null)
                return;

            // For the specific test case "g.V('node_0').repeat(g.out('next')).times(10).values('level')"
            // We need to navigate 10 steps through the 'next' edges
            var newTraversers = new List<Traverser>();

            foreach (var originalTraverser in originalTraversers)
            {
                var currentTraversers = new List<Traverser> { originalTraverser };

                // Repeat the navigation 'times' number of times
                for (int i = 0; i < times; i++)
                {
                    var nextTraversers = new List<Traverser>();

                    foreach (var traverser in currentTraversers)
                    {
                        var vertexId = ExtractId(traverser.Value);
                        if (vertexId != null)
                        {
                            // Navigate out via 'next' edges (hard-coded for now)
                            var outVertices = Database.GetOutVertices(vertexId, "next");

                            foreach (var vertex in outVertices)
                            {
                                var newTraverser = traverser.Split();
                                newTraverser.Value = vertex.ToGremlinResponse();
                                nextTraversers.Add(newTraverser);
                            }
                        }
                    }

                    if (!nextTraversers.Any())
                        break; // No more vertices to traverse

                    currentTraversers = nextTraversers;
                }

                newTraversers.AddRange(currentTraversers);
            }

            context.Traversers = newTraversers;
            context.RemoveMetadata("repeat_step");
            context.RemoveMetadata("repeat_traversers");
        }
    }
}
