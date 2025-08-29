using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Comprehensive examples demonstrating the InMemory scenario loader framework
    /// </summary>
    public class ScenarioUsageExamples
    {
        /// <summary>
        /// Example 1: Using built-in scenarios
        /// </summary>
        public static async Task BuiltInScenariosExample()
        {
            Console.WriteLine("=== Built-in Scenarios Example ===");

            // List all available scenarios
            var scenarios = InMemoryScenarioExtensions.GetAvailableScenarios();
            Console.WriteLine("Available scenarios:");
            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"  - {scenario.Key}: {scenario.Value}");
            }

            // Create connector with social network scenario
            var socialConnector = InMemoryConnectorFactory.CreateSocialNetwork();
            
            // Query some data
            var people = await socialConnector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
            Console.WriteLine($"Found {people.Count()} people in social network");

            // Test a friendship traversal
            var friends = await socialConnector.ExecuteAsync("g.V('john').out('knows')", new Dictionary<string, object>());
            Console.WriteLine($"John knows {friends.Count()} people");

            // Create connector with e-commerce scenario  
            var ecommerceConnector = InMemoryConnectorFactory.CreateECommerce();
            
            var products = await ecommerceConnector.ExecuteAsync("g.V().hasLabel('product')", new Dictionary<string, object>());
            Console.WriteLine($"Found {products.Count()} products in e-commerce scenario");
        }

        /// <summary>
        /// Example 2: Creating custom scenarios
        /// </summary>
        public static async Task CustomScenarioExample()
        {
            Console.WriteLine("=== Custom Scenario Example ===");

            // Register a custom scenario
            InMemoryScenarioRegistry.Register(new LibraryManagementScenario());

            // Use the custom scenario
            var connector = InMemoryConnectorFactory.CreateWithScenario("LibraryManagement");
            
            var books = await connector.ExecuteAsync("g.V().hasLabel('book')", new Dictionary<string, object>());
            Console.WriteLine($"Found {books.Count()} books in library");

            var authors = await connector.ExecuteAsync("g.V().hasLabel('author')", new Dictionary<string, object>());
            Console.WriteLine($"Found {authors.Count()} authors in library");
        }

        /// <summary>
        /// Example 3: Combining multiple scenarios
        /// </summary>
        public static async Task MultipleScenarioExample()
        {
            Console.WriteLine("=== Multiple Scenarios Example ===");

            // Create connector with multiple scenarios
            var connector = InMemoryConnectorFactory.CreateWithScenarios(
                new[] { "BasicSocialNetwork", "SimpleECommerce", "UserRoleManagement" });

            // Now we have data from all three scenarios combined
            var allVertices = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            Console.WriteLine($"Total vertices across all scenarios: {allVertices.First()}");

            var vertexTypes = await connector.ExecuteAsync("g.V().label().dedup()", new Dictionary<string, object>());
            Console.WriteLine($"Vertex types: {string.Join(", ", vertexTypes.Cast<string>())}");
        }

        /// <summary>
        /// Example 4: Using extension methods for fluent configuration
        /// </summary>
        public static async Task FluentConfigurationExample()
        {
            Console.WriteLine("=== Fluent Configuration Example ===");

            // Create a connector and chain scenario applications
            var connector = InMemoryGremlinLanguageConnector.Create()
                .WithScenario("BasicSocialNetwork")
                .WithScenario(new LibraryManagementScenario());

            var stats = connector.GetStatistics();
            Console.WriteLine($"Database contains {stats.VertexCount} vertices and {stats.EdgeCount} edges");

            // Clear and apply different scenarios
            connector.ClearScenarios()
                    .WithScenarios("UserRoleManagement", "OrganizationHierarchy");

            var newStats = connector.GetStatistics();
            Console.WriteLine($"After clearing and applying new scenarios: {newStats.VertexCount} vertices, {newStats.EdgeCount} edges");
        }

        /// <summary>
        /// Example 5: Testing with scenarios in unit tests
        /// </summary>
        public static async Task UnitTestingExample()
        {
            Console.WriteLine("=== Unit Testing Example ===");

            // This is how you'd use scenarios in unit tests
            await TestSocialNetworkFunctionality();
            await TestECommerceFunctionality();
            await TestUserManagementFunctionality();
        }

        private static async Task TestSocialNetworkFunctionality()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
            {
                options.EnableDebugLogging = true;
            });

            // Act
            var mutualFriends = await connector.ExecuteAsync(
                "g.V('john').out('knows').where(in('knows').hasId('jane'))", 
                new Dictionary<string, object>());

            // Assert (in real unit test, you'd use assertion framework)
            Console.WriteLine($"Mutual friends test: Found {mutualFriends.Count()} mutual connections");
        }

        private static async Task TestECommerceFunctionality()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateECommerce();

            // Act
            var customerPurchases = await connector.ExecuteAsync(
                "g.V('customer1').out('purchased')", 
                new Dictionary<string, object>());

            // Assert
            Console.WriteLine($"Customer purchases test: Customer1 purchased {customerPurchases.Count()} items");
        }

        private static async Task TestUserManagementFunctionality()
        {
            // Arrange
            var connector = InMemoryConnectorFactory.CreateUserManagement();

            // Act
            var adminPermissions = await connector.ExecuteAsync(
                "g.V('admin').out('has_role').out('has_permission')", 
                new Dictionary<string, object>());

            // Assert
            Console.WriteLine($"User management test: Admin has {adminPermissions.Count()} permissions");
        }

        /// <summary>
        /// Example 6: Advanced scenario configuration with custom responses
        /// </summary>
        public static async Task AdvancedScenarioExample()
        {
            Console.WriteLine("=== Advanced Scenario Configuration ===");

            var connector = InMemoryConnectorFactory.CreateWithScenario("GraphTraversalTest");

            // Test the custom responses configured in the scenario
            var pathQuery = await connector.ExecuteAsync(
                "g.V('v1').out('connects').out('connects').out('connects')", 
                new Dictionary<string, object>());

            Console.WriteLine($"Path traversal result: {pathQuery.Count()} vertices found");

            // Test another custom response
            var intermediateNodes = await connector.ExecuteAsync(
                "g.V().has('type', 'intermediate')", 
                new Dictionary<string, object>());

            Console.WriteLine($"Intermediate nodes: {intermediateNodes.Count()} found");
        }

        /// <summary>
        /// Run all examples
        /// </summary>
        public static async Task RunAllExamples()
        {
            await BuiltInScenariosExample();
            Console.WriteLine();
            
            await CustomScenarioExample();
            Console.WriteLine();
            
            await MultipleScenarioExample();
            Console.WriteLine();
            
            await FluentConfigurationExample();
            Console.WriteLine();
            
            await UnitTestingExample();
            Console.WriteLine();
            
            await AdvancedScenarioExample();
        }
    }
}