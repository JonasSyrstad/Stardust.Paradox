using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory.Tests;

public class ServiceElementScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "ServiceElementScenario";
    public override string Description => "Service element management test scenario for business layer";

    // Constants that match the TestsBase user ID
    private const string TestUserId = "550e8400-e29b-41d4-a716-446655440000";
    private const string TenantId = "31729bb9-5b5a-423b-b8ff-1cb81dfbb5b3";
    private const string TestProfileId = "74f0fc83-51e3-48af-ac58-25176301cd6f";
    private const string TestProfile2Id = "74f0fcaa-51e3-48af-ac58-25176301cd6f";
    private const string TestUser2Id = "550e84aa-e29b-41d4-a716-446655440000";

        

    protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            // Tenant
            new ScenarioVertexDefinition(TenantId, "tenantEntity", Props(
                ("affiliationMode", "Restricted"),
                ("isDisabled", "false"),
                ("isLegacy", "false"),
                ("domains", "|string|"),
                ("vtm_toggle_UserGroup", "true"),
                ("modifiedDateTime", 1737557972L),
                ("pk", TenantId),
                ("createDateTime", 1734437620L),
                ("entityType", "tenant"),
                ("createdBy", TestUserId),
                ("name", "ServiceElement Test Tenant"),
                ("legalEntityId", "stech1"),
                ("legalEntityName", "ServiceElement Test Tenant"),
                ("modifiedBy", TestUserId),
                ("dnvCustomerId", "stch1"),
                ("tenantType", "veracity_private")
            )),

            // Tenant Service
            new ScenarioVertexDefinition("b43d002f-a372-431a-8104-e3df7dd5734f", "tenantEntity", Props(
                ("autoAssignSubscription", "false"),
                ("production", "false"),
                ("subscriptionCap", "50"),
                ("managementMode", "ServiceManaged"),
                ("accessLevels", "reader,admin"),
                ("useApplyForFlow", "false"),
                ("modifiedDateTime", 1750062614L),
                ("prefix", "DEMO"),
                ("modifiedBy", TestUserId),
                ("pk", TenantId),
                ("createDateTime", 1734437626L),
                ("entityType", "tenantService"),
                ("createdBy", TestUserId),
                ("pricingTier", "standard"),
                ("serviceId", "1deb5f9f-f438-4665-9f11-62e86737f7d1"),
                ("orderNumber", "ORD-ELEMENT-001"),
                ("name", "Service Element Test Service")
            )),

            // Test User Profile - matches TestsBase user ID
            new ScenarioVertexDefinition(TestProfileId, "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", 1734437623L),
                ("entityType", "profile"),
                ("createdBy", TestUserId),
                ("email", "test@veracity.com"),
                ("principalId", TestUserId), // MUST match TestsBase user ID
                ("isServicePrincipal", "false"),
                ("name", "Test User"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", 1734437623L)
            )),

            // Additional profile for testing (used in AddRight test)
            new ScenarioVertexDefinition("4e6e48bf-5df2-4fd4-b5c2-c44b4737f510", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", 1734437624L),
                ("entityType", "profile"),
                ("createdBy", TestUserId),
                ("email", "test2@veracity.com"),
                ("principalId", "3ab05279-159c-4961-84db-e373421fd7af"),
                ("isServicePrincipal", "false"),
                ("name", "Additional Test User"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", 1734437624L)
            )),

            // Second test profile for invalid member type tests
            new ScenarioVertexDefinition(TestProfile2Id, "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", 1734437625L),
                ("entityType", "profile"),
                ("createdBy", TestUser2Id),
                ("email", "test3@veracity.com"),
                ("principalId", "2bc04168-048b-3850-73ca-d262310ec6ae"),
                ("isServicePrincipal", "false"),
                ("name", "Test User 3"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", 1734437625L)
            )),

            // User Groups
            new ScenarioVertexDefinition("d57a0003-83ad-4ace-9150-57bc5d5f1e74", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", 1734437620L),
                ("entityType", "userGroup"),
                ("name", "TenantAdmins"),
                ("builtIn", true), // Must be boolean true
                ("createdBy", TestUserId),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", 1734437620L)
            )),

            new ScenarioVertexDefinition("ff18913e-39a0-423b-aeb7-1a28c64c24bf", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", 1734437620L),
                ("entityType", "userGroup"),
                ("name", "UserAdmins"),
                ("builtIn", true), // Must be boolean true
                ("createdBy", TestUserId),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", 1734437620L)
            )),

            // Service Elements (Assets)
            new ScenarioVertexDefinition("6000ba9f-2ade-4412-8c82-e33d32a725cf", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", DateTime.UtcNow.AddDays(-10).ToEpoch()),
                ("entityType", "asset"),
                ("createdBy", TestUserId),
                ("name", "Main Asset Container"),
                ("description", "Main asset container for testing"),
                ("ownerServiceId", "1deb5f9f-f438-4665-9f11-62e86737f7d1"),
                ("ownerTenantServiceId", "b43d002f-a372-431a-8104-e3df7dd5734f"),
                ("assetExternalId", "MAIN_ASSET_001"),
                ("assetTypeName", "Container"),
                ("assetTypeIcon", "https://example.com/container-icon.png"),
                ("assetLevel", 0),
                ("capability", "[{\"CapabilityName\":\"storage\",\"HasAccessLevel\":true,\"Capacity\":100,\"CapacityUnit\":\"GB\"}]"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", DateTime.UtcNow.AddDays(-5).ToEpoch())
            )),

            new ScenarioVertexDefinition("6001ba9f-2ade-4412-8c82-e33d32a725cf", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", DateTime.UtcNow.AddDays(-8).ToEpoch()),
                ("entityType", "asset"),
                ("createdBy", TestUserId),
                ("name", "Secondary Asset"),
                ("description", "Secondary asset for testing"),
                ("ownerServiceId", "1deb5f9f-f438-4665-9f11-62e86737f7d1"),
                ("ownerTenantServiceId", "b43d002f-a372-431a-8104-e3df7dd5734f"),
                ("assetExternalId", "SEC_ASSET_001"),
                ("assetTypeName", "Resource"),
                ("assetTypeIcon", "https://example.com/resource-icon.png"),
                ("assetLevel", 0),
                ("capability", "[{\"CapabilityName\":\"processing\",\"HasAccessLevel\":true,\"Capacity\":50,\"CapacityUnit\":\"Units\"}]"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", DateTime.UtcNow.AddDays(-3).ToEpoch())
            )),

            // Child Elements
            new ScenarioVertexDefinition("6200ba9f-2ade-4412-8c82-e33d32a725cf", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", DateTime.UtcNow.AddDays(-7).ToEpoch()),
                ("entityType", "asset"),
                ("createdBy", TestUserId),
                ("name", "Child Asset 1"),
                ("description", "Child asset within main container"),
                ("ownerServiceId", "1deb5f9f-f438-4665-9f11-62e86737f7d1"),
                ("ownerTenantServiceId", "b43d002f-a372-431a-8104-e3df7dd5734f"),
                ("parentAssetId", "6000ba9f-2ade-4412-8c82-e33d32a725cf"),
                ("assetExternalId", "CHILD_ASSET_001"),
                ("assetTypeName", "SubContainer"),
                ("assetTypeIcon", "https://example.com/subcontainer-icon.png"),
                ("assetLevel", 1),
                ("capability", "[{\"CapabilityName\":\"backup\",\"HasAccessLevel\":false}]"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", DateTime.UtcNow.AddDays(-2).ToEpoch())
            )),

            new ScenarioVertexDefinition("6201ba9f-2ade-4412-8c82-e33d32a725cf", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", DateTime.UtcNow.AddDays(-6).ToEpoch()),
                ("entityType", "asset"),
                ("createdBy", TestUserId),
                ("name", "Child Asset 2"),
                ("description", "Second child asset within main container"),
                ("ownerServiceId", "1deb5f9f-f438-4665-9f11-62e86737f7d1"),
                ("ownerTenantServiceId", "b43d002f-a372-431a-8104-e3df7dd5734f"),
                ("parentAssetId", "6000ba9f-2ade-4412-8c82-e33d32a725cf"),
                ("assetExternalId", "CHILD_ASSET_002"),
                ("assetTypeName", "SubResource"),
                ("assetTypeIcon", "https://example.com/subresource-icon.png"),
                ("assetLevel", 1),
                ("capability", "[{\"CapabilityName\":\"analytics\",\"HasAccessLevel\":true,\"Capacity\":25,\"CapacityUnit\":\"Queries\"}]"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", DateTime.UtcNow.AddDays(-1).ToEpoch())
            )),

            // Grandchild Element
            new ScenarioVertexDefinition("6202ba9f-2ade-4412-8c82-e33d32a725cf", "tenantEntity", Props(
                ("pk", TenantId),
                ("createDateTime", DateTime.UtcNow.AddDays(-5).ToEpoch()),
                ("entityType", "asset"),
                ("createdBy", TestUserId),
                ("name", "Grandchild Asset"),
                ("description", "Third level nested asset"),
                ("ownerServiceId", "1deb5f9f-f438-4665-9f11-62e86737f7d1"),
                ("ownerTenantServiceId", "b43d002f-a372-431a-8104-e3df7dd5734f"),
                ("parentAssetId", "6200ba9f-2ade-4412-8c82-e33d32a725cf"),
                ("assetExternalId", "GRANDCHILD_001"),
                ("assetTypeName", "Component"),
                ("assetTypeIcon", "https://example.com/component-icon.png"),
                ("assetLevel", 2),
                ("capability", "[{\"CapabilityName\":\"monitoring\",\"HasAccessLevel\":false}]"),
                ("modifiedBy", TestUserId),
                ("modifiedDateTime", DateTime.UtcNow.ToEpoch())
            ))
        };

        var edges = new ScenarioEdgeDefinition[]
        {
            // BIDIRECTIONAL EDGES: Test User Profile membership in TenantAdmins (gives admin rights)
            new ScenarioEdgeDefinition($"members{TestProfileId}d57a0003-83ad-4ace-9150-57bc5d5f1e74", "members", "d57a0003-83ad-4ace-9150-57bc5d5f1e74", TestProfileId, Props(
                ("memberType", "profileMember"),
                ("name", "Test User"),
                ("createDateTime", 1734437634L),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", 1734393600L),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", false),
                ("isAdmin", true)
            )),

            new ScenarioEdgeDefinition($"memberOfd57a0003-83ad-4ace-9150-57bc5d5f1e74{TestProfileId}", "memberOf", TestProfileId, "d57a0003-83ad-4ace-9150-57bc5d5f1e74", Props(
                ("memberType", "profileMember"),
                ("name", "Test User"),
                ("createDateTime", 1734437634L),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", 1734393600L),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", false),
                ("isAdmin", true)
            )),

            // BIDIRECTIONAL EDGES: Test User Profile membership in UserAdmins
            new ScenarioEdgeDefinition($"members{TestProfileId}ff18913e-39a0-423b-aeb7-1a28c64c24bf", "members", "ff18913e-39a0-423b-aeb7-1a28c64c24bf", TestProfileId, Props(
                ("memberType", "profileMember"),
                ("name", "Test User"),
                ("createDateTime", 1734437635L),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", 1734393600L),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", false),
                ("isAdmin", true)
            )),

            new ScenarioEdgeDefinition($"memberOfff18913e-39a0-423b-aeb7-1a28c64c24bf{TestProfileId}", "memberOf", TestProfileId, "ff18913e-39a0-423b-aeb7-1a28c64c24bf", Props(
                ("memberType", "profileMember"),
                ("name", "Test User"),
                ("createDateTime", 1734437635L),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", 1734393600L),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", false),
                ("isAdmin", true)
            )),

            // BIDIRECTIONAL EDGES: Test User subscription to main asset
            // Edge ID pattern: {label}{ToVertexId}{FromVertexId}
            new ScenarioEdgeDefinition($"members{TestProfileId}6000ba9f-2ade-4412-8c82-e33d32a725cf", "members", "6000ba9f-2ade-4412-8c82-e33d32a725cf", TestProfileId, Props(
                ("memberType", "profileSubscription"),
                ("name", "Test User"),
                ("createDateTime", DateTime.UtcNow.AddDays(-9).ToEpoch()),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", DateTime.UtcNow.AddDays(-9).ToEpoch()),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", "false"),
                ("accessLevel", "admin"),
                ("isAdmin", true),
                ("subscriptionState", "subscribing"),
                ("capabilityRighs", "")
            )),

            new ScenarioEdgeDefinition($"memberOf6000ba9f-2ade-4412-8c82-e33d32a725cf{TestProfileId}", "memberOf", TestProfileId, "6000ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "profileSubscription"),
                ("name", "Test User"),
                ("createDateTime", DateTime.UtcNow.AddDays(-9).ToEpoch()),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", DateTime.UtcNow.AddDays(-9).ToEpoch()),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", "false"),
                ("accessLevel", "admin"),
                ("isAdmin", true),
                ("subscriptionState", "subscribing"),
                ("capabilityRighs", "")
            )),

            // BIDIRECTIONAL EDGES: Test User subscription to the tenant service
            new ScenarioEdgeDefinition($"members{TestProfileId}b43d002f-a372-431a-8104-e3df7dd5734f", "members", "b43d002f-a372-431a-8104-e3df7dd5734f", TestProfileId, Props(
                ("memberType", "profileSubscription"),
                ("name", "Test User"),
                ("createDateTime", DateTime.UtcNow.AddDays(-10).ToEpoch()),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", DateTime.UtcNow.AddDays(-10).ToEpoch()),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", "false"),
                ("accessLevel", "admin"),
                ("isAdmin", true),
                ("subscriptionState", "subscribing")
            )),

            new ScenarioEdgeDefinition($"memberOfb43d002f-a372-431a-8104-e3df7dd5734f{TestProfileId}", "memberOf", TestProfileId, "b43d002f-a372-431a-8104-e3df7dd5734f", Props(
                ("memberType", "profileSubscription"),
                ("name", "Test User"),
                ("createDateTime", DateTime.UtcNow.AddDays(-10).ToEpoch()),
                ("userId", TestUserId),
                ("createdBy", TestUserId),
                ("addedDate", DateTime.UtcNow.AddDays(-10).ToEpoch()),
                ("email", "test@veracity.com"),
                ("isServicePrincipal", "false"),
                ("accessLevel", "admin"),
                ("isAdmin", true),
                ("subscriptionState", "subscribing")
            )),

            // BIDIRECTIONAL EDGES: UserGroup right to secondary asset
            new ScenarioEdgeDefinition("membersff18913e-39a0-423b-aeb7-1a28c64c24bf6001ba9f-2ade-4412-8c82-e33d32a725cf", "members", "6001ba9f-2ade-4412-8c82-e33d32a725cf", "ff18913e-39a0-423b-aeb7-1a28c64c24bf", Props(
                ("memberType", "groupSubscription"),
                ("name", "UserAdmins"),
                ("createDateTime", DateTime.UtcNow.AddDays(-7).ToEpoch()),
                ("createdBy", TestUserId),
                ("addedDate", DateTime.UtcNow.AddDays(-7).ToEpoch()),
                ("accessLevel", "reader"),
                ("isAdmin", false),
                ("subscriptionState", "subscribing")
            )),

            new ScenarioEdgeDefinition("memberOf6001ba9f-2ade-4412-8c82-e33d32a725cfff18913e-39a0-423b-aeb7-1a28c64c24bf", "memberOf", "ff18913e-39a0-423b-aeb7-1a28c64c24bf", "6001ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "groupSubscription"),
                ("name", "UserAdmins"),
                ("createDateTime", DateTime.UtcNow.AddDays(-7).ToEpoch()),
                ("createdBy", TestUserId),
                ("addedDate", DateTime.UtcNow.AddDays(-7).ToEpoch()),
                ("accessLevel", "reader"),
                ("isAdmin", false),
                ("subscriptionState", "subscribing")
            )),

            // Asset structure relationships (parts/partsOf for parent-child relationships)
            new ScenarioEdgeDefinition("parts6200ba9f-2ade-4412-8c82-e33d32a725cf6000ba9f-2ade-4412-8c82-e33d32a725cf", "parts", "6000ba9f-2ade-4412-8c82-e33d32a725cf", "6200ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "assetStructure")
            )),
            new ScenarioEdgeDefinition("partsOf6000ba9f-2ade-4412-8c82-e33d32a725cf6200ba9f-2ade-4412-8c82-e33d32a725cf", "partsOf", "6200ba9f-2ade-4412-8c82-e33d32a725cf", "6000ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "assetStructure")
            )),

            new ScenarioEdgeDefinition("parts6201ba9f-2ade-4412-8c82-e33d32a725cf6000ba9f-2ade-4412-8c82-e33d32a725cf", "parts", "6000ba9f-2ade-4412-8c82-e33d32a725cf", "6201ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "assetStructure")
            )),
            new ScenarioEdgeDefinition("partsOf6000ba9f-2ade-4412-8c82-e33d32a725cf6201ba9f-2ade-4412-8c82-e33d32a725cf", "partsOf", "6201ba9f-2ade-4412-8c82-e33d32a725cf", "6000ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "assetStructure")
            )),

            new ScenarioEdgeDefinition("parts6202ba9f-2ade-4412-8c82-e33d32a725cf6200ba9f-2ade-4412-8c82-e33d32a725cf", "parts", "6200ba9f-2ade-4412-8c82-e33d32a725cf", "6202ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "assetStructure")
            )),
            new ScenarioEdgeDefinition("partsOf6200ba9f-2ade-4412-8c82-e33d32a725cf6202ba9f-2ade-4412-8c82-e33d32a725cf", "partsOf", "6202ba9f-2ade-4412-8c82-e33d32a725cf", "6200ba9f-2ade-4412-8c82-e33d32a725cf", Props(
                ("memberType", "assetStructure")
            ))
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Add any custom query responses here if needed
        base.ConfigureCustomResponses(database);
    }
}