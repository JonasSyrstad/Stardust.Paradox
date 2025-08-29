# Stardust.Paradox.Data.InMemory - Complete Package Guide

## ?? Package Overview

**Stardust.Paradox.Data.InMemory** is a comprehensive, high-performance in-memory Gremlin graph database implementation designed specifically for testing and development. It provides a complete testing ecosystem with pre-built scenarios, fluent APIs, and extensive configuration options.

### ?? Package Information
- **Package ID**: `Stardust.Paradox.Data.InMemory`
- **Version**: `1.0.0-preview.1` (Pre-release)
- **Target Framework**: .NET Standard 2.0
- **License**: Apache-2.0
- **Repository**: https://github.com/JonasSyrstad/Stardust.Paradox

## ?? Quick Installation & Usage

### Installation
```bash
dotnet add package Stardust.Paradox.Data.InMemory --version 1.0.0-preview.1
```

### Basic Usage
```csharp
using Stardust.Paradox.Data.InMemory;

// Create with built-in social network scenario
var connector = InMemoryConnectorFactory.CreateSocialNetwork();

// Query immediately - data is ready!
var friends = await connector.ExecuteAsync("g.V('john').out('knows')", new Dictionary<string, object>());
```

## ?? Built-in Scenarios

| Scenario | Command | Description |
|----------|---------|-------------|
| Social Network | `CreateSocialNetwork()` | Users, friendships, posts, likes |
| E-Commerce | `CreateECommerce()` | Customers, products, orders, purchases |
| Organization | `CreateOrganization()` | Employees, departments, management hierarchy |
| User Management | `CreateUserManagement()` | Users, roles, permissions, RBAC |
| Graph Traversal | `CreateWithScenario("GraphTraversalTest")` | Complex patterns for algorithm testing |

## ?? Testing Integration Examples

### Unit Testing
```csharp
[Test]
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
```

### Integration Testing
```csharp
[Test] 
public async Task Should_ProcessECommerceWorkflow()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateECommerce();
    
    // Act - Test complete purchase workflow
    var customer = await connector.ExecuteAsync("g.V('customer1')", new Dictionary<string, object>());
    var purchases = await connector.ExecuteAsync("g.V('customer1').out('purchased')", new Dictionary<string, object>());
    
    // Assert
    customer.Should().HaveCount(1);
    purchases.Should().NotBeEmpty();
}
```

### Custom Scenarios
```csharp
public class BlogScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "Blog";
    public override string Description => "Blog platform with authors and posts";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new InMemoryVertexDefinition[]
        {
            new InMemoryVertexDefinition("author1", "author", Props(("name", "Alice"))),
            new InMemoryVertexDefinition("post1", "post", Props(("title", "Hello World")))
        };

        var edges = new InMemoryEdgeDefinition[]
        {
            new InMemoryEdgeDefinition("wrote", "author1", "post1")
        };

        return (vertices, edges);
    }
}

// Register and use
InMemoryScenarioRegistry.Register(new BlogScenario());
var connector = InMemoryConnectorFactory.CreateWithScenario("Blog");
```

## ?? Configuration Options

```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;        // Log all queries
    options.AutoGenerateIds = true;          // Auto-generate IDs
    options.ValidateEdgeVertices = true;     // Validate edge endpoints
    options.TrackStatistics = true;          // Monitor performance
    options.MaxVertexCount = 10000;          // Set limits
    options.CaseSensitiveLabels = false;     // Case sensitivity
});
```

## ?? Performance & Monitoring

```csharp
// Enable performance tracking
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.TrackStatistics = true;
    options.EnableDebugLogging = true;
});

// Execute queries
await connector.ExecuteAsync("g.V().out('knows').count()", new Dictionary<string, object>());

// Check performance metrics
var stats = connector.GetStatistics();
Console.WriteLine($"Vertices: {stats.VertexCount}, Edges: {stats.EdgeCount}");
Console.WriteLine($"Consumed RU: {connector.ConsumedRU}");
```

## ?? Fluent API

```csharp
// Chain multiple scenarios
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario("BasicSocialNetwork")
    .WithScenario("SimpleECommerce");

// Clear and apply different scenarios
connector.ClearScenarios()
        .WithScenarios("UserRoleManagement", "OrganizationHierarchy");
```

## ?? Debugging Support

```csharp
// Enable query logging
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;
});

// Inspect database state
var allVertices = connector.GetAllVertices();
var allEdges = connector.GetAllEdges();

// Custom assertions
public static class GraphAssertions
{
    public static void ShouldHaveVertex(this InMemoryGremlinLanguageConnector connector, string id)
    {
        connector.GetVertex(id).Should().NotBeNull();
    }
}
```

## ?? Complete Documentation

### Included Documentation Files
1. **[README.md](README.md)** - Main package documentation with quick start guide
2. **[TESTING_GUIDE.md](TESTING_GUIDE.md)** - Comprehensive guide for using in test projects
3. **[SCENARIO_FRAMEWORK_README.md](SCENARIO_FRAMEWORK_README.md)** - Detailed scenario creation and usage
4. **[NUGET_PUBLISHING_GUIDE.md](NUGET_PUBLISHING_GUIDE.md)** - Publishing and CI/CD setup guide

### Key Features Covered
- ? Installation and setup
- ? Built-in scenarios usage
- ? Custom scenario creation
- ? Testing framework integration (xUnit, NUnit, MSTest)
- ? Performance testing and monitoring
- ? Debugging and troubleshooting
- ? Best practices and patterns
- ? Advanced configuration options
- ? Fluent API usage
- ? CI/CD integration

## ?? Use Cases

### Perfect For:
- **Unit Testing**: Test graph algorithms without external dependencies
- **Integration Testing**: Test workflows with realistic graph data
- **Rapid Prototyping**: Quick setup for graph-based application development
- **Education**: Learn Gremlin queries and graph database concepts
- **CI/CD Pipelines**: Fast, reliable tests in automated environments

### Framework Compatibility:
- ? .NET Framework 4.6.1+
- ? .NET Core 2.0+
- ? .NET 5, 6, 7, 8
- ? Xamarin
- ? Unity (with appropriate .NET Standard support)

## ?? Ecosystem Integration

### Works With:
- **Stardust.Paradox.Data**: Complete compatibility with main framework
- **xUnit, NUnit, MSTest**: All major testing frameworks
- **FluentAssertions**: Enhanced assertion syntax
- **Moq, NSubstitute**: Mocking frameworks for complex scenarios
- **Azure DevOps, GitHub Actions**: CI/CD pipeline integration

## ?? Getting Started Checklist

### For New Users:
1. ? Install the NuGet package
2. ? Review the [TESTING_GUIDE.md](TESTING_GUIDE.md)
3. ? Try built-in scenarios with your test framework
4. ? Explore scenario creation for your domain
5. ? Set up CI/CD integration

### For Existing Paradox Users:
1. ? Replace external database setup with InMemory connector
2. ? Use scenarios to speed up test data creation
3. ? Enable performance tracking for optimization
4. ? Leverage fluent API for complex test setups

## ?? Roadmap & Future Plans

### Version 1.0.0 (Stable Release Goals):
- [ ] Additional built-in scenarios based on community feedback
- [ ] Enhanced performance optimizations
- [ ] Extended Gremlin language support
- [ ] Visual Studio debugging improvements
- [ ] Additional testing framework integrations

### Community Contributions:
- Scenario contributions for specific domains
- Performance optimizations
- Documentation improvements
- Integration examples
- Bug reports and feature requests

## ?? Success Metrics (Preview Release)

### Package Quality:
- ? **247 Total Tests**: Comprehensive test coverage
- ? **22 Scenario Tests**: All scenario framework tests passing
- ? **Zero Dependencies**: No external database requirements
- ? **Complete Documentation**: 4 comprehensive guides
- ? **Multiple Examples**: Real-world usage patterns

### Community Goals:
- ?? 100+ downloads for preview release
- ?? Positive feedback on scenario framework
- ?? Community scenario contributions
- ?? Integration success stories

## ?? Contributing

We welcome contributions! Areas where help is needed:

1. **Domain-Specific Scenarios**: Healthcare, Finance, Gaming, etc.
2. **Performance Optimizations**: Query execution improvements
3. **Documentation**: Usage examples and tutorials
4. **Testing**: Edge cases and complex scenarios
5. **Integration Examples**: Different testing frameworks and CI/CD systems

## ?? Support

- **GitHub Issues**: https://github.com/JonasSyrstad/Stardust.Paradox/issues
- **Documentation**: Included guides and README files
- **Community**: Contribute scenarios and share experiences

---

**Ready to revolutionize your graph database testing?** Install `Stardust.Paradox.Data.InMemory` today and experience the power of scenario-driven testing! ??

```bash
dotnet add package Stardust.Paradox.Data.InMemory --version 1.0.0-preview.1
```