using System.Linq;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory.Tests;

public class HasOnExistingService : InMemoryScenarioProviderBase
{
    public override string ScenarioName { get; } = "hasOneExistingService";
    public override string Description { get; } = "Creates a test service with admin user";

    protected override (Scenarios.InMemoryVertexDefinition[] vertices, Scenarios.InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        // Test user ID from TestsBase.CreateTestClaimsIdentity() 
        var testUserId = "550e8400-e29b-41d4-a716-446655440000";
        var existingServiceId = "12345678-1234-5678-9abc-123456789abc";

        var vertices = new Scenarios.InMemoryVertexDefinition[]
        {
            // Test user (from TestsBase.CreateTestClaimsIdentity())
            new Scenarios.InMemoryVertexDefinition(testUserId, "user", Props(
                ("name", "Test User"),
                ("email", "test@veracity.com"),
                ("pk", testUserId)
            )),
            
            // Existing service vertex - remove pk to avoid duplicates
            new Scenarios.InMemoryVertexDefinition(existingServiceId, "serviceDefinition", Props(
                ("name", "Existing Test Service"),
                ("technicalContactEmail", "admin@testservice.com"),
                ("serviceUrl", "https://existing-test-service.veracity.com"),
                ("productionService", true),
                ("type", "standard"),
                ("resourceVersion", 2),
                ("description", "An existing test service for scenario testing"),
                ("shortDescription", "Existing Test Service"),
                ("category", "Testing"),
                ("tenantSettings", "{}"),
                ("accessLevels", ""),
                ("accessLevelsV2", ""),
                ("passwordPolicy", "")
            ))
        };

        // No edges needed - the scenario provides the service, authorization will be handled via PreAuthenticated
        var edges = new Scenarios.InMemoryEdgeDefinition[0];

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        base.ConfigureCustomResponses(database);
    }

    public override void ConfigureScenario(InMemoryGraphDatabase database)
    {
        base.ConfigureScenario(database);
    }
}