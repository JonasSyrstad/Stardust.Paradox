using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

public class HasOnExistingService : InMemoryScenarioProviderBase
{
    public override string ScenarioName { get; } = "hasOneExistingService";
    public override string Description { get; } = "Creates a test service with admin user";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        // Test user ID from TestsBase.CreateTestClaimsIdentity() 
        var testUserId = "550e8400-e29b-41d4-a716-446655440000";
        var existingServiceId = "12345678-1234-5678-9abc-123456789abc";

        var vertices = new InMemoryVertexDefinition[]
        {
            // Test user (from TestsBase.CreateTestClaimsIdentity())
            new InMemoryVertexDefinition(testUserId, "user", Props(
                ("name", "Test User"),
                ("email", "test@veracity.com"),
                ("pk", testUserId)
            )),
                
            // Existing service vertex that the test will update
            new InMemoryVertexDefinition(existingServiceId, "serviceDefinition", Props(
                ("name", "Existing Test Service"),
                ("technicalContactEmail", "admin@testservice.com"),
                ("serviceUrl", "https://existing-test-service.veracity.com"),
                ("productionService", true),
                ("pk", existingServiceId),
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

        // Administrator edge from service to user (IAdministrator : IEdge<IServiceDefinition, IUser>)
        var edges = new InMemoryEdgeDefinition[]
        {
            new InMemoryEdgeDefinition("administrators", existingServiceId, testUserId, Props(
                ("roles", "MYDNV_ADM_SERVICE"),
                ("isOwner", true)
            ))
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        base.ConfigureCustomResponses(database);

        var testUserId = "550e8400-e29b-41d4-a716-446655440000";
        var existingServiceId = "12345678-1234-5678-9abc-123456789abc";

        // Register multiple patterns to catch the administrator lookup

        // Pattern 1: Direct edge lookup with exact IDs
        database.RegisterCustomResponse(
            $@".*administrators.*{existingServiceId}.*{testUserId}.*",
            (query, parameters) =>
            {
                return new[]
                {
                    new Dictionary<string, object>
                    {
                        ["roles"] = "MYDNV_ADM_SERVICE",
                        ["isOwner"] = true,
                        ["outVertextId"] = existingServiceId,
                        ["inVertexId"] = testUserId
                    }
                };
            });

        // Pattern 2: Any administrators query
        database.RegisterCustomResponse(
            @".*administrators.*",
            (query, parameters) =>
            {
                return new[]
                {
                    new Dictionary<string, object>
                    {
                        ["roles"] = "MYDNV_ADM_SERVICE",
                        ["isOwner"] = true,
                        ["outVertextId"] = existingServiceId,
                        ["inVertexId"] = testUserId
                    }
                };
            });

        // Pattern 3: Catch-all for any query that might be looking for edges
        database.RegisterCustomResponse(
            @".*E\(\).*",
            (query, parameters) =>
            {
                if (query.Contains("administrators"))
                {
                    return new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["roles"] = "MYDNV_ADM_SERVICE",
                            ["isOwner"] = true,
                            ["label"] = "administrators",
                            ["outV"] = existingServiceId,
                            ["inV"] = testUserId
                        }
                    };
                }
                return Array.Empty<object>();
            });
    }

    public override void ConfigureScenario(InMemoryGraphDatabase database)
    {
        base.ConfigureScenario(database);
    }
}