# InMemory Database Scenario Loader Framework

The InMemory Database Scenario Loader Framework provides a powerful way to set up well-defined test data scenarios for the Stardust.Paradox InMemory graph database. This framework allows you to quickly populate your database with realistic test data for various domains like social networks, e-commerce, organizations, and more.

## ?? Quick Start

### Using Built-in Scenarios

```csharp
using Stardust.Paradox.Data.InMemory;

// Create a connector with a social network scenario
var connector = InMemoryConnectorFactory.CreateSocialNetwork();

// Query the data
var people = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
var friendships = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());

Console.WriteLine($"Found {people.Count()} people and {friendships.Count()} friendships");
```

### List Available Scenarios

```csharp
var scenarios = InMemoryScenarioExtensions.GetAvailableScenarios();
foreach (var scenario in scenarios)
{
    Console.WriteLine($"{scenario.Key}: {scenario.Value}");
}
```

## ?? Built-in Scenarios

### 1. BasicSocialNetwork
**Purpose**: Social media platform testing  
**Contains**: Users, friendships, posts, likes  
**Use Cases**: Friend recommendations, social graphs, content discovery

```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork();
// Contains: john, jane, bob, alice with friendship connections and posts
```

### 2. SimpleECommerce
**Purpose**: E-commerce platform testing  
**Contains**: Customers, products, orders, purchases  
**Use Cases**: Product recommendations, order history, inventory management

```csharp
var connector = InMemoryConnectorFactory.CreateECommerce();
// Contains: customers, products (laptop, mouse, book), orders
```

### 3. OrganizationHierarchy
**Purpose**: Corporate structure testing  
**Contains**: Employees, departments, management relationships  
**Use Cases**: Org charts, reporting structures, department analytics

```csharp
var connector = InMemoryConnectorFactory.CreateOrganization();
// Contains: CEO, managers, developers with department assignments
```

### 4. UserRoleManagement
**Purpose**: Access control testing  
**Contains**: Users, roles, permissions, assignments  
**Use Cases**: RBAC testing, permission queries, user management

```csharp
var connector = InMemoryConnectorFactory.CreateUserManagement();
// Contains: admin, manager, developer users with role-based permissions
```

### 5. GraphTraversalTest
**Purpose**: Complex traversal testing  
**Contains**: Nodes with various connection patterns  
**Use Cases**: Path finding, complex queries, traversal algorithms

```csharp
var connector = InMemoryConnectorFactory.CreateWithScenario("GraphTraversalTest");
// Contains: test nodes with complex connection patterns
```

## ?? Factory Methods

### Single Scenario
```csharp
// Using specific factory methods
var socialConnector = InMemoryConnectorFactory.CreateSocialNetwork();
var ecommerceConnector = InMemoryConnectorFactory.CreateECommerce();
var orgConnector = InMemoryConnectorFactory.CreateOrganization();
var userMgmtConnector = InMemoryConnectorFactory.CreateUserManagement();

// Using generic factory method
var connector = InMemoryConnectorFactory.CreateWithScenario("BasicSocialNetwork");
```

### Multiple Scenarios
```csharp
// Combine multiple scenarios
var connector = InMemoryConnectorFactory.CreateWithScenarios(
    new[] { "BasicSocialNetwork", "SimpleECommerce", "UserRoleManagement" });

// With configuration
var connector = InMemoryConnectorFactory.CreateWithScenarios(
    new[] { "BasicSocialNetwork", "SimpleECommerce" }, 
    options =>
    {
        options.EnableDebugLogging = true;
        options.CaseSensitiveLabels = false;
    });
```

### Configuration Options
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;
    options.AutoGenerateIds = false;
    options.CaseSensitiveProperties = true;
    options.MaxVertexCount = 1000;
    options.TrackStatistics = true;
});
```

## ?? Fluent API

### Extension Methods
```csharp
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario("BasicSocialNetwork")
    .WithScenario("SimpleECommerce");

// Clear and apply new scenarios
connector.ClearScenarios()
        .WithScenarios("UserRoleManagement", "OrganizationHierarchy");
```

### Chaining Operations
```csharp
var stats = InMemoryGremlinLanguageConnector.Create()
    .WithScenario("BasicSocialNetwork")
    .WithScenario(new CustomLibraryScenario())
    .GetStatistics();

Console.WriteLine($"Total: {stats.VertexCount} vertices, {stats.EdgeCount} edges");
```

## ?? Creating Custom Scenarios

### Basic Custom Scenario

```csharp
public class LibraryManagementScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "LibraryManagement";
    public override string Description => "Library system with books, authors, and borrowing";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new InMemoryVertexDefinition[]
        {
            new InMemoryVertexDefinition("tolkien", "author", Props(
                ("name", "J.R.R. Tolkien"),
                ("birthYear", 1892)
            )),
            new InMemoryVertexDefinition("lotr", "book", Props(
                ("title", "The Lord of the Rings"),
                ("isbn", "978-0544003415")
            ))
        };

        var edges = new InMemoryEdgeDefinition[]
        {
            new InMemoryEdgeDefinition("wrote", "tolkien", "lotr")
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Custom query responses
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('author'\)\.count\(\)", 
            (query, parameters) => new dynamic[] { 1L });
    }
}
```

### Register and Use Custom Scenarios

```csharp
// Register the custom scenario
InMemoryScenarioRegistry.Register(new LibraryManagementScenario());

// Use it
var connector = InMemoryConnectorFactory.CreateWithScenario("LibraryManagement");

// Or apply it to existing connector
connector.WithScenario(new LibraryManagementScenario());
```

## ?? Unit Testing with Scenarios

### Test Setup
```csharp
public class SocialNetworkTests
{
    [Fact]
    public async Task Should_FindMutualFriends()
    {
        // Arrange
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();

        // Act
        var mutualFriends = await connector.ExecuteAsync(
            "g.V('john').out('knows').where(in('knows').hasId('jane'))", 
            new Dictionary<string, object>());

        // Assert
        mutualFriends.Should().NotBeEmpty();
    }
}
```

### Test Isolation
```csharp
public class IsolatedTests
{
    [Fact]
    public async Task Test1_Should_BeIsolated()
    {
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();
        // Each test gets its own database instance
    }

    [Fact]
    public async Task Test2_Should_BeIsolated()
    {
        var connector = InMemoryConnectorFactory.CreateECommerce();
        // Independent of Test1
    }
}
```

### Parameterized Tests
```csharp
[Theory]
[InlineData("BasicSocialNetwork")]
[InlineData("SimpleECommerce")]
[InlineData("UserRoleManagement")]
public async Task Scenario_Should_HaveVertices(string scenarioName)
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateWithScenario(scenarioName);

    // Act
    var vertices = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());

    // Assert
    ((long)vertices.First()).Should().BeGreaterThan(0);
}
```

## ?? Advanced Usage

### Scenario Information
```csharp
// List all available scenarios
var scenarioNames = InMemoryScenarioExtensions.ListAvailableScenarios();

// Get scenario descriptions
var scenarioDetails = InMemoryScenarioExtensions.GetAvailableScenarios();

// Check if scenario exists
bool exists = InMemoryScenarioRegistry.IsRegistered("BasicSocialNetwork");

// Get specific scenario
var scenario = InMemoryScenarioRegistry.GetScenario("BasicSocialNetwork");
```

### Database Statistics
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork();
var stats = connector.GetStatistics();

Console.WriteLine($"Vertices: {stats.VertexCount}");
Console.WriteLine($"Edges: {stats.EdgeCount}");
Console.WriteLine($"Consumed RU: {connector.ConsumedRU}");
```

### Custom Responses and Complex Queries
```csharp
public class AdvancedScenario : InMemoryScenarioProviderBase
{
    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Shortest path query
        database.RegisterCustomResponse(@"shortestPath\('(\w+)', '(\w+)'\)", 
            (query, parameters) =>
        {
            // Custom shortest path implementation
            return CalculateShortestPath(match.Groups[1].Value, match.Groups[2].Value);
        });

        // Complex aggregation
        database.RegisterCustomResponse(@"popularityScore\('(\w+)'\)", 
            (query, parameters) =>
        {
            // Custom popularity calculation
            return CalculatePopularityScore(match.Groups[1].Value);
        });
    }
}
```

## ?? Debugging and Troubleshooting

### Enable Debug Logging
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;
});

// Queries will now be logged to console
var result = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
```

### Inspect Scenario Data
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork();

// Check what data was loaded
var allVertices = connector.GetAllVertices();
var allEdges = connector.GetAllEdges();

foreach (var vertex in allVertices)
{
    Console.WriteLine($"Vertex: {vertex.Id} ({vertex.Label})");
    foreach (var prop in vertex.Properties)
    {
        Console.WriteLine($"  {prop.Key}: {prop.Value}");
    }
}
```

### Validate Scenario Loading
```csharp
[Fact]
public void Scenario_Should_LoadCorrectly()
{
    // Arrange & Act
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    
    // Assert data was loaded
    var stats = connector.GetStatistics();
    stats.VertexCount.Should().BeGreaterThan(0);
    
    // Assert specific entities exist
    connector.GetVertex("john").Should().NotBeNull();
    connector.GetVertex("jane").Should().NotBeNull();
    
    // Assert relationships exist
    var johnsFriends = connector.Database.GetOutVertices("john", "knows");
    johnsFriends.Should().NotBeEmpty();
}
```

## ?? Best Practices

### 1. **Scenario Design**
- Keep scenarios focused on specific domains
- Use realistic data that reflects your application's use cases
- Include both positive and edge cases
- Document expected query patterns

### 2. **Testing Strategy**
```csharp
// ? DO: Use appropriate scenarios for your tests
[Fact]
public async Task SocialFeature_Should_Work()
{
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    // Test social network features
}

// ? DON'T: Use oversized scenarios for simple tests
[Fact]
public async Task SimpleCount_Should_Work()
{
    // Overkill for a simple count test
    var connector = InMemoryConnectorFactory.CreateWithScenarios(
        new[] { "BasicSocialNetwork", "SimpleECommerce", "OrganizationHierarchy" });
}
```

### 3. **Performance Considerations**
```csharp
// ? DO: Reuse scenarios within test classes when possible
public class SocialNetworkTestSuite
{
    private readonly InMemoryGremlinLanguageConnector _connector;
    
    public SocialNetworkTestSuite()
    {
        _connector = InMemoryConnectorFactory.CreateSocialNetwork();
    }
    
    // Multiple tests can use the same connector if they don't modify data
}

// ? DO: Use minimal scenarios for performance-critical tests
var connector = InMemoryConnectorFactory.CreateForTesting(); // Empty database
// Add only the specific data you need
```

### 4. **Maintenance**
```csharp
// ? DO: Version your scenarios when making breaking changes
public class SocialNetworkV2Scenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "BasicSocialNetworkV2";
    // Updated data model
}

// ? DO: Use descriptive scenario names and documentation
public class FinancialServicesCompleteScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "FinancialServicesComplete";
    public override string Description => "Complete financial services domain including customers, accounts, transactions, and regulatory data for compliance testing";
}
```

## ?? Configuration Reference

### InMemoryDatabaseOptions
```csharp
var options = new InMemoryDatabaseOptions
{
    EnableDebugLogging = true,      // Log queries to console
    AutoGenerateIds = true,         // Auto-generate IDs if not specified
    ValidateEdgeVertices = true,    // Validate vertices exist when creating edges
    CaseSensitiveProperties = false, // Property name matching
    CaseSensitiveLabels = false,    // Label matching
    MaxVertexCount = 10000,         // Limit vertices (0 = unlimited)
    MaxEdgeCount = 50000,           // Limit edges (0 = unlimited)
    TrackStatistics = true,         // Track execution statistics
    VertexIdPrefix = "v",          // Prefix for auto-generated vertex IDs
    EdgeIdPrefix = "e",            // Prefix for auto-generated edge IDs
    AllowDuplicateEdges = true,    // Allow multiple edges between same vertices
    CascadeDeleteEdges = true      // Auto-delete edges when vertices are removed
};
```

## ?? Performance Tips

1. **Scenario Size**: Keep scenarios reasonably sized for your test needs
2. **Reuse Connectors**: Within test classes, reuse connectors when tests don't modify data
3. **Selective Loading**: Use `CreateForTesting()` and manually add data for very specific tests
4. **Custom Responses**: Use custom responses for complex queries instead of traversing large graphs
5. **Statistics Tracking**: Enable statistics tracking only when needed for performance analysis

## ?? Contributing

To contribute a new scenario:

1. Implement `InMemoryScenarioProviderBase`
2. Add comprehensive test coverage
3. Document the scenario's purpose and use cases
4. Submit a pull request with examples

---

This scenario framework makes it easy to set up realistic test data for your graph database applications, enabling comprehensive testing and rapid development iteration.