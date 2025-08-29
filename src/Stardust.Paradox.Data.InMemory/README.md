# Stardust.Paradox.Data.InMemory

[![NuGet Version](https://img.shields.io/nuget/v/Stardust.Paradox.Data.InMemory?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.InMemory/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Stardust.Paradox.Data.InMemory?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.InMemory/)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue?style=flat-square)](https://github.com/JonasSyrstad/Stardust.Paradox/blob/main/LICENSE)

A high-performance, feature-rich in-memory Gremlin graph database implementation for testing and development. Perfect for unit testing, integration testing, and rapid prototyping with graph data.

## ? Features

- ?? **High Performance**: Lightning-fast in-memory graph operations
- ?? **Scenario Framework**: Pre-built test data scenarios for common domains
- ?? **Gremlin Compatible**: Full support for Gremlin traversal language
- ?? **Easy Integration**: Seamless integration with existing Paradox applications
- ?? **Test Friendly**: Designed specifically for testing scenarios
- ?? **Fluent API**: Intuitive, chainable configuration methods
- ?? **Performance Tracking**: Built-in query performance monitoring
- ?? **Zero Dependencies**: No external database setup required

## ?? Installation

### Package Manager
```powershell
Install-Package Stardust.Paradox.Data.InMemory -Version 1.0.0-preview.1
```

### .NET CLI
```bash
dotnet add package Stardust.Paradox.Data.InMemory --version 1.0.0-preview.1
```

### PackageReference
```xml
<PackageReference Include="Stardust.Paradox.Data.InMemory" Version="1.0.0-preview.1" />
```

## ?? Quick Start

### Basic Usage
```csharp
using Stardust.Paradox.Data.InMemory;

// Create an empty in-memory database
var connector = InMemoryGremlinLanguageConnector.Create();

// Add some data
await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());
await connector.ExecuteAsync("g.addV('person').property('name', 'Jane')", new Dictionary<string, object>());
await connector.ExecuteAsync("g.V().has('name', 'John').addE('knows').to(g.V().has('name', 'Jane'))", new Dictionary<string, object>());

// Query the data
var people = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
var friendships = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());
```

### Using Built-in Scenarios
```csharp
using Stardust.Paradox.Data.InMemory;

// Create with pre-built social network data
var connector = InMemoryConnectorFactory.CreateSocialNetwork();

// Query immediately - data is already there!
var friends = await connector.ExecuteAsync("g.V('john').out('knows')", new Dictionary<string, object>());
var mutualFriends = await connector.ExecuteAsync("g.V('john').out('knows').where(in('knows').hasId('jane'))", new Dictionary<string, object>());
```

## ?? Built-in Scenarios

The package includes several pre-built scenarios for common testing needs:

| Scenario | Description | Vertex Types | Edge Types |
|----------|-------------|--------------|------------|
| **BasicSocialNetwork** | Social media platform | person, post | knows, authored, likes |
| **SimpleECommerce** | E-commerce platform | customer, product, order | purchased, contains, placed |
| **OrganizationHierarchy** | Corporate structure | employee, department | manages, works_for, assigned_to |
| **UserRoleManagement** | RBAC system | user, role, permission | has_role, has_permission |
| **GraphTraversalTest** | Complex patterns | node, typeA, typeB | connects, links, self |

### Scenario Factory Methods
```csharp
// Convenience methods for common scenarios
var socialConnector = InMemoryConnectorFactory.CreateSocialNetwork();
var ecommerceConnector = InMemoryConnectorFactory.CreateECommerce();
var orgConnector = InMemoryConnectorFactory.CreateOrganization();
var userMgmtConnector = InMemoryConnectorFactory.CreateUserManagement();

// Generic method
var connector = InMemoryConnectorFactory.CreateWithScenario("BasicSocialNetwork");

// Multiple scenarios combined
var multiConnector = InMemoryConnectorFactory.CreateWithScenarios(
    new[] { "BasicSocialNetwork", "SimpleECommerce" });
```

## ?? Advanced Configuration

### Database Options
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;        // Log all queries
    options.AutoGenerateIds = true;          // Auto-generate vertex/edge IDs
    options.ValidateEdgeVertices = true;     // Ensure vertices exist for edges
    options.CaseSensitiveLabels = false;     // Case-insensitive labels
    options.MaxVertexCount = 10000;          // Limit number of vertices
    options.TrackStatistics = true;          // Enable performance tracking
});
```

### Fluent API
```csharp
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario("BasicSocialNetwork")
    .WithScenario("SimpleECommerce");

// Clear and apply different scenarios
connector.ClearScenarios()
        .WithScenarios("UserRoleManagement", "OrganizationHierarchy");
```

## ?? Custom Scenarios

Create your own scenarios for domain-specific testing:

```csharp
public class BlogScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "BlogPlatform";
    public override string Description => "Blog platform with authors, posts, and comments";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new InMemoryVertexDefinition[]
        {
            new InMemoryVertexDefinition("author1", "author", Props(
                ("name", "Alice Writer"),
                ("email", "alice@blog.com")
            )),
            new InMemoryVertexDefinition("post1", "post", Props(
                ("title", "Getting Started"),
                ("content", "Welcome to our blog!")
            ))
        };

        var edges = new InMemoryEdgeDefinition[]
        {
            new InMemoryEdgeDefinition("authored", "author1", "post1")
        };

        return (vertices, edges);
    }
}

// Register and use
InMemoryScenarioRegistry.Register(new BlogScenario());
var connector = InMemoryConnectorFactory.CreateWithScenario("BlogPlatform");
```

## ?? Testing Integration

### Unit Testing
```csharp
[Test]
public async Task Should_FindUserFriends()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    
    // Act
    var friends = await connector.ExecuteAsync("g.V('john').out('knows')", new Dictionary<string, object>());
    
    // Assert
    friends.Should().HaveCount(2);
}
```

### Integration Testing
```csharp
[Test]
public async Task Should_TestComplexWorkflow()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateECommerce();
    
    // Act - Test a complete purchase workflow
    var customer = await connector.ExecuteAsync("g.V('customer1')", new Dictionary<string, object>());
    var cart = await connector.ExecuteAsync("g.V('customer1').out('purchased')", new Dictionary<string, object>());
    
    // Assert
    customer.Should().HaveCount(1);
    cart.Should().NotBeEmpty();
}
```

### Performance Testing
```csharp
[Test]
public async Task Should_PerformWell()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
    {
        options.TrackStatistics = true;
    });
    
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    await connector.ExecuteAsync("g.V().out('knows').out('knows').dedup().count()", new Dictionary<string, object>());
    
    stopwatch.Stop();
    
    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    connector.ConsumedRU.Should().BeGreaterThan(0);
}
```

## ?? Performance & Statistics

Monitor query performance and database usage:

```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.TrackStatistics = true;
    options.EnableDebugLogging = true;
});

// Execute queries
await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());

// Check statistics
var stats = connector.GetStatistics();
Console.WriteLine($"Vertices: {stats.VertexCount}, Edges: {stats.EdgeCount}");
Console.WriteLine($"Consumed RU: {connector.ConsumedRU}");
```

## ?? Debugging & Troubleshooting

### Enable Query Logging
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;
});

// All queries will be logged to console
var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
```

### Inspect Database State
```csharp
// Examine all vertices and edges
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

## ?? Documentation

- **[Complete Testing Guide](docs/TESTING_GUIDE.md)** - Comprehensive guide for using in test projects
- **[Scenario Framework Guide](docs/SCENARIO_FRAMEWORK_README.md)** - Detailed scenario creation and usage
- **[Implementation Summary](docs/SCENARIO_FRAMEWORK_IMPLEMENTATION_SUMMARY.md)** - Technical implementation details

## ?? Use Cases

- **Unit Testing**: Test graph algorithms and traversals without external dependencies
- **Integration Testing**: Test complete workflows with realistic graph data
- **Rapid Prototyping**: Quickly build and test graph-based applications
- **Educational**: Learn Gremlin queries and graph concepts
- **CI/CD**: Fast, reliable tests in continuous integration pipelines

## ?? Compatibility

- **.NET Standard 2.0+**: Compatible with .NET Framework, .NET Core, and .NET 5+
- **Gremlin Language**: Supports most common Gremlin traversal operations
- **Testing Frameworks**: Works with xUnit, NUnit, MSTest, and others
- **Stardust.Paradox**: Full compatibility with existing Paradox applications

## ?? Contributing

Contributions are welcome! Please feel free to submit pull requests, report bugs, or suggest new features.

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## ?? License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/JonasSyrstad/Stardust.Paradox/blob/main/LICENSE) file for details.

## ?? Related Packages

- **[Stardust.Paradox.Data](https://www.nuget.org/packages/Stardust.Paradox.Data/)** - Core Paradox graph database framework
- **[Stardust.Paradox.Data.Mocker](https://www.nuget.org/packages/Stardust.Paradox.Data.Mocker/)** - Mocking framework for Paradox applications

---

Made with ?? by the Stardust team