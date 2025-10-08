using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the profile() step - provides profiling/performance metrics for the traversal
    /// TinkerPop spec: profile() returns metrics about the traversal execution
    /// </summary>
    public class ProfileStepExecutor : StepExecutorBase
    {
        public ProfileStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "profile";

        public override string StepDescription => "Provides profiling and performance metrics for the traversal";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Collect profiling metrics from the traversal context
            var metrics = new Dictionary<string, object>();

            // Basic metrics
            metrics["traverserCount"] = context.Traversers.Count;
            metrics["stepCount"] = context.GetMetadata<int>("stepCount");

            // Side effects count
            metrics["sideEffectCount"] = context.SideEffects.Count;
            metrics["sideEffectKeys"] = context.SideEffects.Keys.ToList();

            // Execution metrics (if available)
            var startTime = context.GetMetadata<long>("startTime");
            if (startTime > 0)
            {
                var elapsed = Stopwatch.GetTimestamp() - startTime;
                var elapsedMs = elapsed * 1000.0 / Stopwatch.Frequency;
                metrics["durationMs"] = elapsedMs;
            }
            else
            {
                // Set start time for future profiling
                context.SetMetadata("startTime", Stopwatch.GetTimestamp());
            }

            // Database stats
            var vertexCount = Database.GetAllVertices().Count();
            var edgeCount = Database.GetAllEdges().Count();
            
            metrics["vertexCount"] = vertexCount;
            metrics["edgeCount"] = edgeCount;

            // Memory usage estimate (rough approximation)
            metrics["estimatedMemoryKB"] = (vertexCount + edgeCount) * 0.5; // Very rough estimate

            // Build profile result structure similar to TinkerPop format
            var profileResult = new
            {
                metrics = new[]
                {
                    new
                    {
                        id = "7.0.0()",
                        name = "TinkerGraphStep(vertex,[])",
                        counts = new Dictionary<string, long>
                        {
                            ["traverserCount"] = context.Traversers.Count,
                            ["elementCount"] = context.Traversers.Count
                        },
                        durationNs = metrics.ContainsKey("durationMs") ? 
                            (long)((double)metrics["durationMs"] * 1_000_000) : 0L
                    }
                },
                duration = metrics.ContainsKey("durationMs") ? (double)metrics["durationMs"] : 0.0,
                graph = new
                {
                    vertices = vertexCount,
                    edges = edgeCount
                }
            };

            // Replace current traverser results with profile information
            context.Traversers.Clear();
            var profileTraverser = new Traverser(profileResult);
            context.Traversers.Add(profileTraverser);
        }
    }
}
