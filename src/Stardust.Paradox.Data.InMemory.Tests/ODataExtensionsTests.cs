using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.OData;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Extensions;
using Stardust.Paradox.Data.OData;
using Stardust.Paradox.Data.Traversals;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class ODataExtensionsTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private static InMemoryGremlinLanguageConnector _sharedConnector;
        private static readonly object _lock = new object();
        private static bool _scenarioApplied = false;
        
        public void Dispose()
        {
            // Don't dispose the shared connector as it's used across all tests
            // The connector will be reused by all test instances
        }

        public ODataExtensionsTests(ITestOutputHelper output)
        {
            _output = output;
            // Ensure we have a shared connector for all tests
            lock (_lock)
            {
                if (_sharedConnector == null)
                {
                    _sharedConnector = new InMemoryGremlinLanguageConnector(new InMemoryDatabaseOptions { EnableDebugLogging = true, EnableQueryLogging = true });
                }
                // Apply scenario once for all tests
                if (!_scenarioApplied)
                {
                    Scenarios.InMemoryScenarioRegistry.ApplyScenario(_sharedConnector.Database, "OdataTestScenario");
                    _scenarioApplied = true;
                }
            }
        }
        [Fact]
        public async Task ApplyODataQuery_WithFilter_ShouldFilterResults()
        {
            // Arrange
            var connector = _sharedConnector;

            var context = new TestGraphContext(connector);

            var options = new ODataSearchOptions
            {
                Filter = "age eq 30"
            };

            // Act
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options)).ConfigureAwait(false);

            // Assert
            var list = results.ToList();
            list.Should().HaveCount(1);
            list[0].Name.Should().Be("Alice");
        }

        [Fact]
        public async Task ApplyODataQuery_WithMultipleFilters_ShouldFilterResults()
        {
            // Arrange
            var connector = _sharedConnector;

            var context = new TestGraphContext(connector);

            var options = new ODataSearchOptions
            {
                Filter = "age gt 25 and active eq true"
            };

            // Act
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options)).ConfigureAwait(false);

            // Assert
            var list = results.ToList();
            // Eve (28), Alice (30), and Charlie (35) all have age > 25 AND active = true
            list.Should().HaveCount(3);
            list.Should().Contain(p => p.Name == "Eve");
            list.Should().Contain(p => p.Name == "Alice");
            list.Should().Contain(p => p.Name == "Charlie");
        }

        [Fact]
        public async Task ApplyODataQuery_WithContainsFilter_ShouldFilterResults()
        {
            // Arrange
            var connector = _sharedConnector;

            var context = new TestGraphContext(connector);

            var options = new ODataSearchOptions
            {
                Filter = "email contains 'example'"
            };

            // Act
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options)).ConfigureAwait(false);

            // Assert
            var list = results.ToList();
            list.Should().HaveCount(2);
            list.Should().Contain(p => p.Name == "Alice");
            list.Should().Contain(p => p.Name == "Charlie");
        }

        [Fact]
        public async Task ApplyODataQuery_WithOrderByAscending_ShouldOrderResults()
        {
            // Arrange
            var connector = _sharedConnector;

            var context = new TestGraphContext(connector);

            var options = new ODataSearchOptions
            {
                OrderBy = new OrderingOptions 
                { 
                    PropertyName = "age", 
                    Ordering = OrderingType.Ascending 
                }
            };

            // Act
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options)).ConfigureAwait(false);

            // Assert
            var list = results.ToList();
            list.Should().HaveCount(5); // All 5 people
            list[0].Name.Should().Be("Frank"); // age 22
            list[1].Name.Should().Be("Bob");   // age 25
            list[2].Name.Should().Be("Eve");   // age 28
            list[3].Name.Should().Be("Alice"); // age 30
            list[4].Name.Should().Be("Charlie"); // age 35
        }

        [Fact]
        public async Task ApplyODataQuery_WithTop_ShouldLimitResults()
        {
            // Arrange
            var connector = _sharedConnector;

            var context = new TestGraphContext(connector);

            var options = new ODataSearchOptions
            {
                Top = 2
            };

            // Act
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options)).ConfigureAwait(false);

            // Assert
            var list = results.ToList();
            list.Should().HaveCount(2);
        }

        [Fact]
        public async Task ApplyODataQuery_WithCombinedOptions_ShouldApplyAllFilters()
        {
            // Arrange
            var connector = _sharedConnector;

            var context = new TestGraphContext(connector);

            var options = new ODataSearchOptions
            {
                Filter = "active eq true",
                OrderBy = new OrderingOptions 
                { 
                    PropertyName = "age", 
                    Ordering = OrderingType.Ascending 
                },
                Top = 2
            };

            // Act
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options)).ConfigureAwait(false);

            // Assert
            var list = results.ToList();
            list.Should().HaveCount(2);
            list[0].Name.Should().Be("Bob");  // age 25, active true
            list[1].Name.Should().Be("Eve");  // age 28, active true
        }

        [Fact]
        public async Task ApplyODataQuery_WithNullOptions_ShouldReturnAllResults()
        {
            // Arrange
            var connector = _sharedConnector;
            var context = new TestGraphContext(connector);

            // Act
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(null)).ConfigureAwait(false);

            // Assert
            var list = results.ToList();
            list.Should().HaveCount(5); // All 5 people from the scenario
        }

        // Test helper interfaces
        [VertexLabel("person")]
        public interface ITestPerson : IVertex
        {
            string Name { get; set; }
            int Age { get; set; }
            bool Active { get; set; }
            string Email { get; set; }
            string Description { get; set; }
        }

        [VertexLabel("product")]
        public interface ITestProduct : IVertex
        {
            string Name { get; set; }
            decimal Price { get; set; }
        }

        private class TestGraphContext : GraphContextBase
        {
            public TestGraphContext(IGremlinLanguageConnector connector) : base(connector, CreateServiceProvider())
            {
            }
    
            public IGraphSet<ITestPerson> People { get; }

            
            public IGraphSet<ITestProduct> Products { get; }
            private static IServiceProvider CreateServiceProvider()
            {
                var services = new ServiceCollection();

                // Set up the entity binding for code generation
                services.AddEntityBinding((entity, implementation) =>
                {
                    services.AddTransient(entity, implementation);
                });

                return services.BuildServiceProvider();
            }

            protected override bool InitializeModel(IGraphConfiguration configuration)
            {
                configuration.ConfigureCollection<ITestPerson>().ConfigureCollection<ITestProduct>();
                // No additional configuration needed for tests
                return true;
            }
        }
    }
}
