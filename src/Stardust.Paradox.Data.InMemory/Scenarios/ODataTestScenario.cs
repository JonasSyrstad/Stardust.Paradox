using System;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// OData test scenario with sample person data for OData query testing
/// </summary>
public class ODataTestScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "OdataTestScenario";
    public override string Description => "Test data for OData extension testing with filtering, ordering, and paging";

    protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new ScenarioVertexDefinition("alice", "person", Props(
                ("name", "Alice"),
                ("age", 30),
                ("active", true),
                ("email", "alice@example.com"),
                ("description", "Software developer")
            )),
            new ScenarioVertexDefinition("bob", "person", Props(
                ("name", "Bob"),
                ("age", 25),
                ("active", true),
                ("email", "bob@test.com"),
                ("description", "Designer")
            )),
            new ScenarioVertexDefinition("charlie", "person", Props(
                ("name", "Charlie"),
                ("age", 35),
                ("active", true),
                ("email", "charlie@example.com"),
                ("description", "Manager")
            )),
            new ScenarioVertexDefinition("eve", "person", Props(
                ("name", "Eve"),
                ("age", 28),
                ("active", true),
                ("email", "eve@test.com"),
                ("description", "Analyst")
            )),
            new ScenarioVertexDefinition("frank", "person", Props(
                ("name", "Frank"),
                ("age", 22),
                ("active", false),
                ("email", "frank@test.com"),
                ("description", "Intern")
            ))
        };

        return (vertices, null); // No edges needed for these tests
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // No custom responses needed for basic OData testing
    }
}
