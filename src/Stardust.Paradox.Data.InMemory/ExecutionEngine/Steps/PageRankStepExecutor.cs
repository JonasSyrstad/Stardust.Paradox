using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the pageRank() step - calculates PageRank scores for vertices
    /// TinkerPop spec: pageRank() computes the PageRank of vertices
    /// This is a simplified implementation for in-memory processing
    /// </summary>
    public class PageRankStepExecutor : StepExecutorBase
    {
        public PageRankStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "pageRank";

        public override string StepDescription => "Calculates PageRank scores for vertices in the graph";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // PageRank is typically used as a vertex program
            // For simplicity, we'll calculate PageRank for all vertices in the graph
            // and store it as a vertex property
            
            const double dampingFactor = 0.85;
            const int maxIterations = 20;
            const double convergenceThreshold = 0.0001;

            // Get all vertices
            var vertices = Database.GetAllVertices().ToList();
            if (!vertices.Any())
            {
                return;
            }

            // Initialize PageRank scores
            var pageRanks = new Dictionary<string, double>();
            var newPageRanks = new Dictionary<string, double>();
            double initialRank = 1.0 / vertices.Count;

            foreach (var vertex in vertices)
            {
                pageRanks[vertex.Id] = initialRank;
                newPageRanks[vertex.Id] = 0.0;
            }

            // Perform PageRank iterations
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool hasConverged = true;

                foreach (var vertex in vertices)
                {
                    // Calculate new PageRank
                    double rank = (1.0 - dampingFactor) / vertices.Count;

                    // Sum contributions from incoming edges
                    var incomingEdges = Database.GetAllEdges()
                        .Where(e => e.InVertexId == vertex.Id)
                        .ToList();

                    foreach (var edge in incomingEdges)
                    {
                        var sourceVertex = vertices.FirstOrDefault(v => v.Id == edge.OutVertexId);
                        if (sourceVertex != null)
                        {
                            // Count outgoing edges from source
                            int outDegree = Database.GetAllEdges()
                                .Count(e => e.OutVertexId == sourceVertex.Id);

                            if (outDegree > 0)
                            {
                                rank += dampingFactor * (pageRanks[sourceVertex.Id] / outDegree);
                            }
                        }
                    }

                    newPageRanks[vertex.Id] = rank;

                    // Check convergence
                    if (Math.Abs(rank - pageRanks[vertex.Id]) > convergenceThreshold)
                    {
                        hasConverged = false;
                    }
                }

                // Update PageRanks
                foreach (var kvp in newPageRanks)
                {
                    pageRanks[kvp.Key] = kvp.Value;
                }

                if (hasConverged)
                {
                    break;
                }
            }

            // Store PageRank values as a vertex property or in context
            var pageRankPropertyKey = "pageRank";
            if (step.Arguments.Any())
            {
                pageRankPropertyKey = step.GetFirstStringArgument() ?? "pageRank";
            }

            // Add PageRank as a property to vertices
            foreach (var vertex in vertices)
            {
                if (pageRanks.ContainsKey(vertex.Id))
                {
                    vertex.SetProperty(pageRankPropertyKey, pageRanks[vertex.Id]);
                }
            }

            // Store in side effects for access via cap()
            context.SideEffects[pageRankPropertyKey] = pageRanks.Select(kvp => (dynamic)new { vertex = kvp.Key, pageRank = kvp.Value }).ToList();

            // Pass traversers through unchanged (side-effect step)
        }
    }
}
