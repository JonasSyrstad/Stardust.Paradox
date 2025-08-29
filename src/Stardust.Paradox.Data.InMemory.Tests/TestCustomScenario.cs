using System.Linq;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Test custom scenario for testing purposes
/// </summary>
public class TestCustomScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "TestCustom";
    public override string Description => "Custom test scenario for unit testing";

    protected override (Scenarios.InMemoryVertexDefinition[] vertices, Scenarios.InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new Scenarios.InMemoryVertexDefinition[]
        {
            new Scenarios.InMemoryVertexDefinition("custom1", "custom", Props(
                ("name", "Custom One"),
                ("type", "test")
            )),
            new Scenarios.InMemoryVertexDefinition("custom2", "custom", Props(
                ("name", "Custom Two"),
                ("type", "test")
            ))
        };

        var edges = new Scenarios.InMemoryEdgeDefinition[]
        {
            new Scenarios.InMemoryEdgeDefinition("connects", "custom1", "custom2")
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('custom'\)\.count\(\)", (query, parameters) =>
        {
            return new dynamic[] { 2L };
        });
    }
}