using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stardust.Paradox.Data.MockTester.Models;
using Stardust.Paradox.Data.Mocker;
using Stardust.Paradox.Data.Annotations.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.MockTester
{
    /// <summary>
    /// Comprehensive unit tests for MockTestContext that demonstrate proper IoC container usage
    /// and initial state population for each test scenario.
    /// </summary>
    public class MockTestContextTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHost _host;

        public MockTestContextTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Build host with default .NET IoC container
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
            
            _serviceProvider = _host.Services;
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register MockGremlinLanguageConnector as a singleton to maintain graph state
            services.AddSingleton<MockGremlinLanguageConnector>(provider =>
            {
                return MockGremlinConnectorFactory.CreateForTesting();
            });

            // Register test data seeding service
            services.AddTransient<ITestDataSeeder, TestDataSeeder>();
        }

        #region MockConnector Integration Tests (Simplified)

        [Fact]
        public void MockConnector_Should_BeAvailable_FromIoCContainer()
        {
            // Arrange & Act
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();

            // Assert
            Assert.NotNull(connector);
            Assert.True(connector.CanParameterizeQueries);
        }

        [Fact]
        public async Task MockConnector_Should_ExecuteBasicQueries_WithIoC()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            var seeder = _serviceProvider.GetRequiredService<ITestDataSeeder>();
            
            // Populate initial state
            await seeder.SeedBasicConnectorDataAsync(connector);

            // Act
            var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            var vertex = result.First();
            Assert.Equal("person", vertex.label);
        }

        [Fact]
        public async Task MockConnector_Should_HandleParameterizedQueries_WithIoC()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            var seeder = _serviceProvider.GetRequiredService<ITestDataSeeder>();
            
            await seeder.SeedParameterizedQueryDataAsync(connector);

            // Act
            var parameters = new Dictionary<string, object> { { "__p0", "TestUser" } };
            var result = await connector.ExecuteAsync("g.V().has('name', __p0)", parameters);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        #endregion

        #region Mock Configuration Tests

        [Fact]
        public async Task MockConnector_Should_HandleJsonConfiguration_WithIoC()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            connector.ConfigureJsonResponse(@"g\.V\('test'\)", 
                @"[{""id"": ""test"", ""label"": ""person"", ""type"": ""vertex"", ""properties"": {""name"": ""TestPerson""}}]");

            // Act
            var result = await connector.ExecuteAsync("g.V('test')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            var vertex = result.First();
            Assert.Equal("test", vertex.id.ToString());
            Assert.Equal("person", vertex.label.ToString());
        }

        [Fact]
        public async Task MockConnector_Should_HandleFunctionConfiguration_WithIoC()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.count\(\)", (query, parameters) =>
            {
                return new[] { new { result = 5 } };
            });

            // Act
            var result = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            var countResult = result.First();
            Assert.Equal(5, countResult.result);
        }

        #endregion

        #region Complex Scenario Tests

        [Fact]
        public async Task ComplexScenario_Should_HandleUserAndCompanyData_WithIoC()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            var seeder = _serviceProvider.GetRequiredService<ITestDataSeeder>();
            
            // Populate comprehensive initial state
            await seeder.SeedCompleteScenarioAsync(connector);

            // Act - Create user
            var userResult = await connector.ExecuteAsync("g.addV('person').property('name', 'John Doe')", 
                new Dictionary<string, object>());

            // Act - Create company
            var companyResult = await connector.ExecuteAsync("g.addV('company').property('name', 'TechCorp')", 
                new Dictionary<string, object>());

            // Act - Query all persons
            var allPersons = await connector.ExecuteAsync("g.V().hasLabel('person')", 
                new Dictionary<string, object>());

            // Assert
            Assert.NotNull(userResult);
            Assert.Single(userResult);
            
            Assert.NotNull(companyResult);
            Assert.Single(companyResult);
            
            Assert.NotNull(allPersons);
            Assert.NotEmpty(allPersons);
        }

        [Fact]
        public async Task ComplexScenario_Should_HandleEmploymentRelationships_WithIoC()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            var seeder = _serviceProvider.GetRequiredService<ITestDataSeeder>();
            
            await seeder.SeedEmploymentScenarioAsync(connector);

            // Act - Create employment edge
            var employmentResult = await connector.ExecuteAsync(
                "g.V('person1').addE('employer').to(g.V('company1'))", 
                new Dictionary<string, object>());

            // Query employment relationships
            var relationships = await connector.ExecuteAsync("g.E().hasLabel('employer')", 
                new Dictionary<string, object>());

            // Assert
            Assert.NotNull(employmentResult);
            Assert.NotEmpty(employmentResult);
            
            Assert.NotNull(relationships);
            Assert.NotEmpty(relationships);
        }

        #endregion

        #region Performance and State Tests

        [Fact]
        public async Task MockConnector_Should_TrackRequestUnits_WithIoC()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            var initialRU = connector.ConsumedRU;

            // Act - Perform operations that should consume RU
            await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());

            // Assert
            Assert.True(connector.ConsumedRU > initialRU);
            _output.WriteLine($"Consumed RU: {connector.ConsumedRU}");
        }

        [Fact]
        public async Task MockConnector_Should_ResetState_Properly()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            // Add some data
            await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());
            var beforeReset = connector.ConsumedRU;

            // Act
            connector.Reset();

            // Assert
            Assert.True(connector.ConsumedRU < beforeReset || connector.ConsumedRU == 0);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task MockConnector_Should_HandleInvalidQueries_Gracefully()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();

            // Act & Assert - Invalid queries should be handled gracefully
            var result = await connector.ExecuteAsync("invalid.query.syntax", new Dictionary<string, object>());
            Assert.NotNull(result); // Should return empty result, not throw
        }

        [Fact]
        public void ServiceProvider_Should_CreateConsistentConnectorInstances()
        {
            // Arrange & Act
            var connector1 = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            var connector2 = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();

            // Assert - Should be same instance (singleton)
            Assert.Same(connector1, connector2);
        }

        #endregion

        #region Integration with Multiple Services

        [Fact]
        public async Task MultipleServices_Should_ShareConnectorState()
        {
            // Arrange
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            var seeder1 = _serviceProvider.GetRequiredService<ITestDataSeeder>();
            var seeder2 = _serviceProvider.GetRequiredService<ITestDataSeeder>();

            // Act - Both seeders should work with the same connector
            await seeder1.SeedBasicConnectorDataAsync(connector);
            await seeder2.SeedBasicConnectorDataAsync(connector);

            var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        #endregion

        public void Dispose()
        {
            _host?.Dispose();
        }
    }

    #region Test Data Seeding Infrastructure

    /// <summary>
    /// Interface for test data seeding operations
    /// </summary>
    public interface ITestDataSeeder
    {
        Task SeedBasicConnectorDataAsync(MockGremlinLanguageConnector connector);
        Task SeedParameterizedQueryDataAsync(MockGremlinLanguageConnector connector);
        Task SeedCompleteScenarioAsync(MockGremlinLanguageConnector connector);
        Task SeedEmploymentScenarioAsync(MockGremlinLanguageConnector connector);
    }

    /// <summary>
    /// Implementation of test data seeding for MockGremlinLanguageConnector
    /// </summary>
    public class TestDataSeeder : ITestDataSeeder
    {
        public async Task SeedBasicConnectorDataAsync(MockGremlinLanguageConnector connector)
        {
            // Configure mock responses for basic operations
            connector.ConfigureFunctionResponse(@"g\.addV\('person'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "basic-person-" + Guid.NewGuid().ToString("N")[..8],
                        "person",
                        new Dictionary<string, object>
                        {
                            { "name", "Seeded Person" }
                        }
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"g\.addV\('company'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "basic-company-" + Guid.NewGuid().ToString("N")[..8],
                        "company",
                        new Dictionary<string, object>
                        {
                            { "name", "Seeded Company" }
                        }
                    )
                };
            });

            await Task.Delay(1); // Simulate async work
        }

        public async Task SeedParameterizedQueryDataAsync(MockGremlinLanguageConnector connector)
        {
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.has\('name'", (query, parameters) =>
            {
                var nameParam = parameters.FirstOrDefault(p => p.Key.Contains("p0"));
                var name = nameParam.Value?.ToString() ?? "Unknown";
                
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "param-user-" + Guid.NewGuid().ToString("N")[..8],
                        "person",
                        new Dictionary<string, object>
                        {
                            { "name", name }
                        }
                    )
                };
            });

            await Task.Delay(1);
        }

        public async Task SeedCompleteScenarioAsync(MockGremlinLanguageConnector connector)
        {
            // Configure responses for a complete user-company scenario
            connector.ConfigureFunctionResponse(@"g\.addV\('person'\)\.property\('name'", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "user-" + Guid.NewGuid().ToString("N")[..8],
                        "person",
                        new Dictionary<string, object>
                        {
                            { "name", "John Doe" }
                        }
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"g\.addV\('company'\)\.property\('name'", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "company-" + Guid.NewGuid().ToString("N")[..8],
                        "company",
                        new Dictionary<string, object>
                        {
                            { "name", "TechCorp" }
                        }
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("user1", "person", new Dictionary<string, object> { { "name", "John Doe" } }),
                    ("user2", "person", new Dictionary<string, object> { { "name", "Jane Smith" } })
                );
            });

            await Task.Delay(1);
        }

        public async Task SeedEmploymentScenarioAsync(MockGremlinLanguageConnector connector)
        {
            // Configure employment relationship mocks
            connector.ConfigureFunctionResponse(@"\.addE\('employer'\)\.to\(", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateEdgeResponse(
                        "employment-" + Guid.NewGuid().ToString("N")[..8],
                        "employer",
                        "person1",
                        "company1",
                        new Dictionary<string, object>
                        {
                            { "hiredDate", DateTime.UtcNow },
                            { "position", "Developer" }
                        }
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"g\.E\(\)\.hasLabel\('employer'\)", (query, parameters) =>
            {
                return MockExtensions.CreateEdgeCollection(
                    ("emp1", "employer", "person1", "company1", new Dictionary<string, object> 
                    { 
                        { "position", "Developer" } 
                    })
                );
            });

            await Task.Delay(1);
        }
    }

    #endregion
}