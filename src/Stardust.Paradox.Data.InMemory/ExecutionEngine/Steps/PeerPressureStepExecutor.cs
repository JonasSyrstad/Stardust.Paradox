using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the peerPressure() step - implements the peer pressure community detection algorithm
    /// TinkerPop spec: peerPressure() assigns community IDs to vertices based on neighbor communities
    /// </summary>
    public class PeerPressureStepExecutor : StepExecutorBase
    {
        public PeerPressureStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "peerPressure";

        public override string StepDescription => "Implements peer pressure community detection algorithm to assign cluster IDs to vertices";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            // Peer Pressure is a community detection algorithm
            // Each vertex is assigned to the community that most of its neighbors belong to
            
            const int maxIterations = 20;
            var propertyKey = "cluster";
            
            if (step.Arguments.Any())
            {
                propertyKey = step.GetFirstStringArgument() ?? "cluster";
            }

            // Get all vertices
            var vertices = Database.GetAllVertices().ToList();
            if (!vertices.Any())
            {
                return;
            }

            // Initialize: each vertex is in its own community
            var communities = new Dictionary<string, string>();
            foreach (var vertex in vertices)
            {
                communities[vertex.Id] = vertex.Id; // Use vertex ID as initial community ID
            }

            // Perform peer pressure iterations
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool changed = false;
                var newCommunities = new Dictionary<string, string>(communities);

                foreach (var vertex in vertices)
                {
                    // Get neighbors (vertices connected by edges)
                    var neighborCommunities = new Dictionary<string, int>();

                    // Check outgoing edges
                    var outEdges = Database.GetAllEdges().Where(e => e.OutVertexId == vertex.Id);
                    foreach (var edge in outEdges)
                    {
                        if (communities.ContainsKey(edge.InVertexId))
                        {
                            var community = communities[edge.InVertexId];
                            if (!neighborCommunities.ContainsKey(community))
                                neighborCommunities[community] = 0;
                            neighborCommunities[community]++;
                        }
                    }

                    // Check incoming edges
                    var inEdges = Database.GetAllEdges().Where(e => e.InVertexId == vertex.Id);
                    foreach (var edge in inEdges)
                    {
                        if (communities.ContainsKey(edge.OutVertexId))
                        {
                            var community = communities[edge.OutVertexId];
                            if (!neighborCommunities.ContainsKey(community))
                                neighborCommunities[community] = 0;
                            neighborCommunities[community]++;
                        }
                    }

                    // Find the most common community among neighbors
                    if (neighborCommunities.Any())
                    {
                        var mostCommonCommunity = neighborCommunities
                            .OrderByDescending(kvp => kvp.Value)
                            .ThenBy(kvp => kvp.Key) // Tie-breaker for determinism
                            .First()
                            .Key;

                        if (mostCommonCommunity != communities[vertex.Id])
                        {
                            newCommunities[vertex.Id] = mostCommonCommunity;
                            changed = true;
                        }
                    }
                }

                communities = newCommunities;

                // If no changes occurred, algorithm has converged
                if (!changed)
                {
                    break;
                }
            }

            // Apply community assignments as vertex properties
            foreach (var vertex in vertices)
            {
                if (communities.ContainsKey(vertex.Id))
                {
                    vertex.SetProperty(propertyKey, communities[vertex.Id]);
                }
            }

            // Store results in side effects
            context.SideEffects[propertyKey] = communities.Select(kvp => (dynamic)new { vertex = kvp.Key, cluster = kvp.Value }).ToList();

            // Pass traversers through unchanged (side-effect step)
        }
    }
}
