using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests for g.V(id, partitionKey) syntax and complex traversals with partition keys
    /// These tests document expected behavior that should work in both in-memory and CosmosDB implementations
    /// </summary>
    public class PartitionKeyVertexLookupTests
    {
        private readonly ITestOutputHelper _output;

        public PartitionKeyVertexLookupTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task VWithPartitionKey_SingleId_ShouldReturnVertex()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('service').property('id', 'service1').property('pk', 'tenant1').property('name', 'TestService')", null);

            // Act - Single ID lookup
            var result1 = await connector.ExecuteAsync("g.V('service1')", null);

            // Assert
            result1.Should().HaveCount(1);
            ((string)result1.First().id).Should().Be("service1");
            ((string)result1.First().properties.name).Should().Be("TestService");
        }

        [Fact]
        public async Task VWithPartitionKey_ArraySyntax_ShouldReturnVertex()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('service').property('id', 'service1').property('pk', 'tenant1').property('name', 'TestService')", null);

            // Act - Array syntax [partitionKey, id]
            var result = await connector.ExecuteAsync("g.V(['tenant1','service1'])", null);

            // Assert
            result.Should().HaveCount(1);
            ((string)result.First().id).Should().Be("service1");
            ((string)result.First().properties.name).Should().Be("TestService");
        }

        [Fact]
        public async Task VWithPartitionKey_TwoParameters_ShouldReturnVertex()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('service').property('id', 'service1').property('pk', 'tenant1').property('name', 'TestService')", null);

            // Act - Two parameter syntax V(id, partitionKey)
            var result = await connector.ExecuteAsync("g.V('service1', 'tenant1')", null);

            // Assert
            result.Should().HaveCount(1);
            ((string)result.First().id).Should().Be("service1");
            ((string)result.First().properties.name).Should().Be("TestService");
        }

        [Fact]
        public async Task VWithPartitionKey_ComplexTraversalPattern_ShouldWork()
        {
            // Arrange - This is the production scenario
            var connector = InMemoryGremlinLanguageConnector.Create();
            var db = connector.Database;

            // Create service with partition key
            await connector.ExecuteAsync(
                "g.addV('service').property('id', 'service1').property('pk', 'tenant1').property('entityType', 'service').property('name', 'TestService')", 
                null);

            // Create profiles with partition keys
            await connector.ExecuteAsync(
                "g.addV('profile').property('id', 'profile1').property('pk', 'tenant1').property('entityType', 'profile').property('name', 'User1')", 
                null);
            await connector.ExecuteAsync(
                "g.addV('profile').property('id', 'profile2').property('pk', 'tenant1').property('entityType', 'profile').property('name', 'User2')", 
                null);

            // Add edges
            await connector.ExecuteAsync("g.V('service1').addE('members').to(g.V('profile1'))", null);
            await connector.ExecuteAsync("g.V('service1').addE('members').to(g.V('profile2'))", null);

            _output.WriteLine("=== Verify edges were created ===");
            var allEdges = db.GetAllEdges();
            _output.WriteLine($"Total edges in database: {allEdges.Count()}");
            allEdges.Should().HaveCount(2);

            // Act - Complex traversal with partition key
            var result = await connector.ExecuteAsync(
                "g.V('service1', 'tenant1').emit().repeat(__.out('members').dedup()).until(__.loops().is(7)).has('entityType', 'profile').dedup()",
                null);

            // Assert
            _output.WriteLine($"Found {result.Count()} profiles");
            result.Should().HaveCount(2);
            
            var ids = result.Select(r => (string)r.id).OrderBy(id => id).ToList();
            ids.Should().Contain("profile1");
            ids.Should().Contain("profile2");
        }

        [Fact]
        public async Task VWithPartitionKey_ParameterizedQuery_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync(
                "g.addV('service').property('id', 'service1').property('pk', 'tenant1').property('entityType', 'service')", 
                null);
            await connector.ExecuteAsync(
                "g.addV('profile').property('id', 'profile1').property('pk', 'tenant1').property('entityType', 'profile')", 
                null);
            await connector.ExecuteAsync("g.V('service1').addE('members').to(g.V('profile1'))", null);

            // Act - Parameterized query
            var parameters = new Dictionary<string, object>
            {
                ["serviceId"] = "service1",
                ["tenantId"] = "tenant1"
            };

            var result = await connector.ExecuteAsync(
                "g.V(serviceId, tenantId).emit().repeat(__.out('members').dedup()).until(__.loops().is(7)).has('entityType', 'profile').dedup()",
                parameters);

            // Assert
            result.Should().HaveCount(1);
            ((string)result.First().id).Should().Be("profile1");
        }

        [Fact]
        public async Task EdgeCreation_ImmediateVisibility_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            var db = connector.Database;

            await connector.ExecuteAsync("g.addV('service').property('id', 'service1').property('pk', 'tenant1')", null);
            await connector.ExecuteAsync("g.addV('profile').property('id', 'profile1').property('pk', 'tenant1')", null);

            _output.WriteLine("=== Before edge creation ===");
            var edgesBefore = db.GetAllEdges();
            _output.WriteLine($"Edges: {edgesBefore.Count()}");
            edgesBefore.Should().BeEmpty();

            // Act - Create edge
            await connector.ExecuteAsync("g.V('service1').addE('members').to(g.V('profile1'))", null);

            // Assert - Edge should be immediately visible
            _output.WriteLine("\n=== After edge creation ===");
            var edgesAfter = db.GetAllEdges();
            _output.WriteLine($"Edges: {edgesAfter.Count()}");
            edgesAfter.Should().HaveCount(1);

            // Edge should be discoverable via traversal
            var traversalResult = await connector.ExecuteAsync("g.V('service1').out('members')", null);
            traversalResult.Should().HaveCount(1);
            ((string)traversalResult.First().id).Should().Be("profile1");
        }

        [Fact]
        public async Task EdgeCreation_WithPropertyIndexing_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            var db = connector.Database;

            await connector.ExecuteAsync("g.addV('service').property('id', 'service1').property('pk', 'tenant1')", null);
            await connector.ExecuteAsync("g.addV('profile').property('id', 'profile1').property('pk', 'tenant1')", null);

            // Act - Create edge with properties
            await connector.ExecuteAsync(
                "g.V('service1').addE('members').to(g.V('profile1')).property('principalId', 'user123').property('role', 'admin')", 
                null);

            // Assert - Edge properties should be indexed
            var edgesByProperty = db.GetEdgesByProperty("principalId", "user123");
            edgesByProperty.Should().HaveCount(1);

            var edge = edgesByProperty.First();
            edge.Label.Should().Be("members");
            edge.GetProperty<string>("principalId", null).Should().Be("user123");
            edge.GetProperty<string>("role", null).Should().Be("admin");
        }

        [Fact]
        public async Task MultipleEdgeCreations_AllShouldBeVisible()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            var db = connector.Database;

            await connector.ExecuteAsync("g.addV('service').property('id', 'service1').property('pk', 'tenant1')", null);
            
            for (int i = 1; i <= 5; i++)
            {
                await connector.ExecuteAsync($"g.addV('profile').property('id', 'profile{i}').property('pk', 'tenant1')", null);
            }

            // Act - Create multiple edges
            for (int i = 1; i <= 5; i++)
            {
                await connector.ExecuteAsync($"g.V('service1').addE('members').to(g.V('profile{i}'))", null);
            }

            // Assert - All edges should be visible
            var allEdges = db.GetAllEdges();
            _output.WriteLine($"Total edges: {allEdges.Count()}");
            allEdges.Should().HaveCount(5);

            // All should be discoverable via traversal
            var traversalResult = await connector.ExecuteAsync("g.V('service1').out('members')", null);
            _output.WriteLine($"Profiles via traversal: {traversalResult.Count()}");
            traversalResult.Should().HaveCount(5);
        }

        [Fact]
        public async Task ComplexScenario_ProductionPattern_ShouldWork()
        {
            // This test documents the exact production scenario that was failing
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            var tenantId = "tenant-abc-123";
            var serviceId = "service-xyz-456";

            // Create service with partition key
            await connector.ExecuteAsync(
                $"g.addV('service').property('id', '{serviceId}').property('pk', '{tenantId}').property('entityType', 'service').property('name', 'ProductionService')",
                null);

            // Create multiple profiles
            for (int i = 1; i <= 10; i++)
            {
                await connector.ExecuteAsync(
                    $"g.addV('profile').property('id', 'profile{i}').property('pk', '{tenantId}').property('entityType', 'profile').property('name', 'User{i}')",
                    null);

                // Add edge immediately after creating profile
                await connector.ExecuteAsync(
                    $"g.V('{serviceId}').addE('members').to(g.V('profile{i}'))",
                    null);
            }

            // Act - Use the production query pattern
            var result = await connector.ExecuteAsync(
                $"g.V('{serviceId}', '{tenantId}').emit().repeat(__.out('members').dedup()).until(__.loops().is(7)).has('entityType', 'profile').dedup()",
                null);

            // Assert
            _output.WriteLine($"Found {result.Count()} profiles");
            result.Should().HaveCount(10);

            // Verify all profiles are found
            var profileIds = result.Select(r => (string)r.id).OrderBy(id => id).ToList();
            for (int i = 1; i <= 10; i++)
            {
                profileIds.Should().Contain($"profile{i}");
            }
        }
    }
}
