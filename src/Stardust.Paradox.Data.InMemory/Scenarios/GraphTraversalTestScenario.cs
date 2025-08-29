using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Graph traversal test scenario specifically designed for testing complex traversals
/// </summary>
public class GraphTraversalTestScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "GraphTraversalTest";
    public override string Description => "Designed for testing complex graph traversal operations and edge cases";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new InMemoryVertexDefinition[]
        {
            // Create a small but complex graph for testing traversals
            new InMemoryVertexDefinition("v1", "node", Props(("value", 1), ("type", "start"))),
            new InMemoryVertexDefinition("v2", "node", Props(("value", 2), ("type", "intermediate"))),
            new InMemoryVertexDefinition("v3", "node", Props(("value", 3), ("type", "intermediate"))),
            new InMemoryVertexDefinition("v4", "node", Props(("value", 4), ("type", "end"))),
            new InMemoryVertexDefinition("v5", "node", Props(("value", 5), ("type", "isolated"))),
                
            // Different vertex types for testing hasLabel
            new InMemoryVertexDefinition("t1", "typeA", Props(("name", "TypeA-1"))),
            new InMemoryVertexDefinition("t2", "typeB", Props(("name", "TypeB-1"))),
            new InMemoryVertexDefinition("t3", "typeA", Props(("name", "TypeA-2"))),
        };

        var edges = new InMemoryEdgeDefinition[]
        {
            new InMemoryEdgeDefinition("connects", "v1", "v2"),
            new InMemoryEdgeDefinition("connects", "v2", "v3"),
            new InMemoryEdgeDefinition("connects", "v3", "v4"),
            new InMemoryEdgeDefinition("connects", "v1", "v3"), // Alternative path
            new InMemoryEdgeDefinition("links", "t1", "t2"),
            new InMemoryEdgeDefinition("links", "t2", "t3"),
                
            // Self-loop for testing
            new InMemoryEdgeDefinition("self", "v2", "v2")
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Path from v1 to v4
        database.RegisterCustomResponse(@"g\.V\('v1'\)\.out\('connects'\)\.out\('connects'\)\.out\('connects'\)", (query, parameters) =>
        {
            return new[] { database.GetVertex("v4")?.ToGremlinResponse() }.Where(x => x != null);
        });

        // All intermediate nodes
        database.RegisterCustomResponse(@"g\.V\(\)\.has\('type', 'intermediate'\)", (query, parameters) =>
        {
            var v2 = database.GetVertex("v2")?.ToGremlinResponse();
            var v3 = database.GetVertex("v3")?.ToGremlinResponse();
            return new[] { v2, v3 }.Where(x => x != null);
        });
    }
}