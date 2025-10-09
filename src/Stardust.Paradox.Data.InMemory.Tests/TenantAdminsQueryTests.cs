using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Scenarios;
using Stardust.Paradox.Data.InMemory.Extensions;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Comprehensive test suite for the TenantAdmins query pattern:
/// g.V().has('pk', {networkId}).has('builtIn', 'true').has('name', 'TenantAdmins').as('a').out().has('principalId', {userId}).select('a')
/// 
/// This query pattern is used to:
/// 1. Find a built-in group by partition key (pk) and name
/// 2. Navigate to members of that group
/// 3. Filter members by principalId
/// 4. Return the original group if the user is a member
/// 
/// This is a critical authorization pattern used in multi-tenant applications.
/// </summary>
public class TenantAdminsQueryTests
{
    #region Test Data Constants
    
    private const string NetworkId = "550e8401-e29b-41d4-a716-446655440001";
    private const string AdminUserId = "550e8400-e29b-41d4-a716-446655440000";
    private const string RegularUserId = "550e8400-e29b-41d4-a716-446655440001";
    private const string NonMemberUserId = "550e8400-e29b-41d4-a716-446655440999";
    
    #endregion

    #region Core Functionality Tests

    [Fact]
    public async Task TenantAdminsQuery_WithValidAdminUser_ShouldReturnGroup()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
        var group = result.First();
        ((string)group.properties.name).Should().Be("TenantAdmins");
        ((string)group.properties.builtIn).Should().Be("true");
    }

    [Fact]
    public async Task TenantAdminsQuery_WithNonMemberUser_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", NonMemberUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantAdminsQuery_WithWrongNetworkId_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var wrongNetworkId = "550e8401-e29b-41d4-a716-446655440999";
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", wrongNetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantAdminsQuery_WithWrongGroupName_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "WrongGroupName" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Parameterization Tests

    [Fact]
    public async Task TenantAdminsQuery_WithAllParameters_ShouldSubstituteCorrectly()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task TenantAdminsQuery_WithMissingParameter_ShouldThrowOrReturnEmpty()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            // Missing __p8 (userId)
            { "__p9", "a" }
        };

        // Act & Assert
        // Should either throw an exception or return empty results
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);
        
        // The query should not succeed without all required parameters
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantAdminsQuery_WithDifferentParameterNames_ShouldWork()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "p0", "pk" },
            { "p1", NetworkId },
            { "p2", "builtIn" },
            { "p3", "true" },
            { "p4", "name" },
            { "p5", "TenantAdmins" },
            { "p6", "a" },
            { "p7", "principalId" },
            { "p8", AdminUserId },
            { "p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(p0,p1).has(p2,p3).has(p4,p5).as(p6).out().has(p7,p8).select(p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region Multiple Members Tests

    [Fact]
    public async Task TenantAdminsQuery_WithMultipleMembers_ShouldReturnGroupForEachMember()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        
        // Query for first admin user
        var parameters1 = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Query for second admin user
        var parameters2 = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", RegularUserId },
            { "__p9", "a" }
        };

        // Act
        var result1 = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters1);

        var result2 = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters2);

        // Assert
        result1.Should().HaveCount(1);
        result2.Should().HaveCount(1);
        
        var group1 = result1.First();
        var group2 = result2.First();
        
        ((string)group1.properties.name).Should().Be("TenantAdmins");
        ((string)group2.properties.name).Should().Be("TenantAdmins");
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public async Task TenantAdminsQuery_WithEmptyPrincipalId_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", "" },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantAdminsQuery_WithNullPrincipalId_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", null },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantAdminsQuery_WithCaseInsensitiveGroupName_ShouldHandleCorrectly()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "tenantadmins" }, // lowercase
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        // Should return empty as group name is case-sensitive
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantAdminsQuery_WithBooleanBuiltInValue_ShouldNotMatch()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", true }, // boolean instead of string "true"
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        // Should handle type conversion or return empty
        result.Should().BeEmpty();
    }

    #endregion

    #region Alternative Query Patterns

    [Fact]
    public async Task TenantAdminsQuery_WithDirectValues_ShouldWork()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();

        // Act
        var result = await connector.ExecuteAsync(
            $"g.V().has('pk','{NetworkId}').has('builtIn','true').has('name','TenantAdmins').as('a').out().has('principalId','{AdminUserId}').select('a')",
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var group = result.First();
        ((string)group.properties.name).Should().Be("TenantAdmins");
    }

    [Fact]
    public async Task TenantAdminsQuery_WithChainedHasSteps_ShouldBeEquivalent()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task TenantAdminsQuery_WithOutEInV_ShouldBeEquivalent()
    {
        // Arrange
        var connector = await CreateConnectorWithTestData();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act - Using outE().inV() instead of out()
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).outE().inV().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region Multiple Groups Tests

    [Fact]
    public async Task TenantAdminsQuery_WithMultipleBuiltInGroups_ShouldOnlyReturnCorrectGroup()
    {
        // Arrange
        var connector = await CreateConnectorWithMultipleGroups();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
        var group = result.First();
        ((string)group.properties.name).Should().Be("TenantAdmins");
    }

    [Fact]
    public async Task TenantAdminsQuery_ForDifferentGroup_ShouldReturnCorrectGroup()
    {
        // Arrange
        var connector = await CreateConnectorWithMultipleGroups();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "CommunityMods" }, // Different group
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", RegularUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
        var group = result.First();
        ((string)group.properties.name).Should().Be("CommunityMods");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task TenantAdminsQuery_WithLargeNumberOfMembers_ShouldPerformEfficiently()
    {
        // Arrange
        var connector = await CreateConnectorWithManyMembers();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "TenantAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", AdminUserId },
            { "__p9", "a" }
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(1);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete in less than 1 second
    }

    #endregion

    #region Test Data Setup Methods

    private async Task<InMemoryGremlinLanguageConnector> CreateConnectorWithTestData()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create TenantAdmins group
        await connector.ExecuteAsync(
            $"g.addV('socialEntity').property('id', 'group1').property('pk', '{NetworkId}').property('name', 'TenantAdmins').property('builtIn', 'true').property('entityType', 'interestGroup')",
            new Dictionary<string, object>());

        // Create admin person
        await connector.ExecuteAsync(
            $"g.addV('socialEntity').property('id', 'person1').property('pk', '{NetworkId}').property('name', 'Admin User').property('principalId', '{AdminUserId}').property('entityType', 'person')",
            new Dictionary<string, object>());

        // Create second admin person
        await connector.ExecuteAsync(
            $"g.addV('socialEntity').property('id', 'person2').property('pk', '{NetworkId}').property('name', 'Regular User').property('principalId', '{RegularUserId}').property('entityType', 'person')",
            new Dictionary<string, object>());

        // Create membership edge from group to admin
        await connector.ExecuteAsync(
            "g.V('group1').addE('members').to(g.V('person1')).property('principalId', '" + AdminUserId + "')",
            new Dictionary<string, object>());

        // Create membership edge from group to regular user
        await connector.ExecuteAsync(
            "g.V('group1').addE('members').to(g.V('person2')).property('principalId', '" + RegularUserId + "')",
            new Dictionary<string, object>());

        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateConnectorWithMultipleGroups()
    {
        var connector = await CreateConnectorWithTestData();
        
        // Create CommunityMods group
        await connector.ExecuteAsync(
            $"g.addV('socialEntity').property('id', 'group2').property('pk', '{NetworkId}').property('name', 'CommunityMods').property('builtIn', 'true').property('entityType', 'interestGroup')",
            new Dictionary<string, object>());

        // Create membership edge from CommunityMods to regular user
        await connector.ExecuteAsync(
            "g.V('group2').addE('members').to(g.V('person2')).property('principalId', '" + RegularUserId + "')",
            new Dictionary<string, object>());

        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateConnectorWithManyMembers()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create TenantAdmins group
        await connector.ExecuteAsync(
            $"g.addV('socialEntity').property('id', 'group1').property('pk', '{NetworkId}').property('name', 'TenantAdmins').property('builtIn', 'true').property('entityType', 'interestGroup')",
            new Dictionary<string, object>());

        // Create 100 person vertices
        for (int i = 0; i < 100; i++)
        {
            var personId = $"person{i}";
            var principalId = $"550e8400-e29b-41d4-a716-{i:D12}";
            
            await connector.ExecuteAsync(
                $"g.addV('socialEntity').property('id', '{personId}').property('pk', '{NetworkId}').property('name', 'User {i}').property('principalId', '{principalId}').property('entityType', 'person')",
                new Dictionary<string, object>());

            await connector.ExecuteAsync(
                $"g.V('group1').addE('members').to(g.V('{personId}')).property('principalId', '{principalId}')",
                new Dictionary<string, object>());
        }

        // Create the specific admin user we're looking for
        await connector.ExecuteAsync(
            $"g.addV('socialEntity').property('id', 'admin_person').property('pk', '{NetworkId}').property('name', 'Admin User').property('principalId', '{AdminUserId}').property('entityType', 'person')",
            new Dictionary<string, object>());

        await connector.ExecuteAsync(
            $"g.V('group1').addE('members').to(g.V('admin_person')).property('principalId', '{AdminUserId}')",
            new Dictionary<string, object>());

        return connector;
    }

    #endregion

    #region Integration Tests with Scenario

    [Fact]
    public async Task TenantAdminsQuery_WithSocialNetworkScenario_ShouldWork()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        connector.WithScenario<SocialNetworkTestScenario>();
        
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "NetworkAdmins" }, // Use NetworkAdmins from scenario
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", "550e8400-e29b-41d4-a716-446655440000" }, // Default test user from scenario
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().HaveCount(1);
        var group = result.First();
        ((string)group.properties.name).Should().Be("NetworkAdmins");
    }

    [Fact]
    public async Task TenantAdminsQuery_WithScenario_ShouldReturnEmptyForNonMember()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        connector.WithScenario<SocialNetworkTestScenario>();
        
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pk" },
            { "__p1", NetworkId },
            { "__p2", "builtIn" },
            { "__p3", "true" },
            { "__p4", "name" },
            { "__p5", "NetworkAdmins" },
            { "__p6", "a" },
            { "__p7", "principalId" },
            { "__p8", NonMemberUserId },
            { "__p9", "a" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Documentation Tests

    [Fact]
    public async Task TenantAdminsQuery_DocumentedExample_ShouldWork()
    {
        // This test documents the exact query pattern used in production
        // Query: g.V().has('pk', networkId).has('builtIn', 'true').has('name', 'TenantAdmins')
        //          .as('a').out().has('principalId', userId).select('a')
        
        // Arrange
        var connector = await CreateConnectorWithTestData();
        
        // Act
        var result = await connector.ExecuteAsync(
            "g.V().has(__p0,__p1).has(__p2,__p3).has(__p4,__p5).as(__p6).out().has(__p7,__p8).select(__p9)",
            new Dictionary<string, object>
            {
                { "__p0", "pk" },
                { "__p1", NetworkId },
                { "__p2", "builtIn" },
                { "__p3", "true" },
                { "__p4", "name" },
                { "__p5", "TenantAdmins" },
                { "__p6", "a" },
                { "__p7", "principalId" },
                { "__p8", AdminUserId },
                { "__p9", "a" }
            });

        // Assert
        result.Should().HaveCount(1, 
            "the query should return the group if the user is a member");
        
        var group = result.First();
        ((string)group.properties.name).Should().Be("TenantAdmins",
            "the returned group should be the TenantAdmins group");
        ((string)group.properties.builtIn).Should().Be("true",
            "the group should be marked as built-in");
    }

    #endregion
}
