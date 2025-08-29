# Stardust.Paradox.Data.InMemory Testing Guide

A comprehensive guide for using the Stardust.Paradox.Data.InMemory package in your test projects for fast, reliable graph database testing.

## ?? Installation

### NuGet Package Manager
```powershell
Install-Package Stardust.Paradox.Data.InMemory -Version 1.0.0-preview.1
```

### Package Manager Console
```powershell
dotnet add package Stardust.Paradox.Data.InMemory --version 1.0.0-preview.1
```

### PackageReference (in .csproj)
```xml
<PackageReference Include="Stardust.Paradox.Data.InMemory" Version="1.0.0-preview.1" />
```

## ?? Quick Start

### Basic Setup
```csharp
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Scenarios;

// Create an empty in-memory database
var connector = InMemoryGremlinLanguageConnector.Create();

// Or use a pre-built scenario
var connector = InMemoryConnectorFactory.CreateSocialNetwork();
```

### Simple Test Example
```csharp
[Test]
public async Task Should_CreateAndRetrieveVertex()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateForTesting();
    
    // Act
    await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());
    var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
    
    // Assert
    result.Should().HaveCount(1);
    ((string)result.First().properties.name).Should().Be("John");
}
```

## ??? Project Setup

### Test Project Configuration

#### 1. Create Test Project Structure
```
YourProject.Tests/
??? YourProject.Tests.csproj
??? TestBase.cs
??? Scenarios/
?   ??? CustomTestScenario.cs
?   ??? ComplexTestScenario.cs
??? UnitTests/
?   ??? UserServiceTests.cs
?   ??? GraphTraversalTests.cs
??? IntegrationTests/
    ??? ApiIntegrationTests.cs
    ??? WorkflowTests.cs
```

#### 2. Test Project File (.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Stardust.Paradox.Data.InMemory" Version="1.0.0-preview.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\YourProject\YourProject.csproj" />
  </ItemGroup>

</Project>
```

#### 3. Base Test Class
```csharp
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Scenarios;

public abstract class GraphTestBase
{
    protected InMemoryGremlinLanguageConnector CreateEmptyDatabase()
    {
        return InMemoryConnectorFactory.CreateForTesting(options =>
        {
            options.EnableDebugLogging = true;
            options.TrackStatistics = true;
        });
    }

    protected InMemoryGremlinLanguageConnector CreateWithScenario(string scenarioName)
    {
        return InMemoryConnectorFactory.CreateWithScenario(scenarioName, options =>
        {
            options.EnableDebugLogging = true;
        });
    }

    protected static Dictionary<string, object> NoParams => new();
}
```

## ?? Using Built-in Scenarios

The InMemory package comes with several pre-built scenarios for common testing needs:

### Available Built-in Scenarios

| Scenario | Description | Use Cases |
|----------|-------------|-----------|
| `BasicSocialNetwork` | Users, friendships, posts, likes | Social features, friend recommendations |
| `SimpleECommerce` | Customers, products, orders, purchases | Shopping, order processing, recommendations |
| `OrganizationHierarchy` | Employees, departments, management | Org charts, reporting, permissions |
| `UserRoleManagement` | Users, roles, permissions, RBAC | Authentication, authorization testing |
| `GraphTraversalTest` | Complex graph patterns | Algorithm testing, path finding |

### Using Built-in Scenarios

```csharp
[TestClass]
public class SocialNetworkTests : GraphTestBase
{
    [Test]
    public async Task Should_FindMutualFriends()
    {
        // Arrange
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();
        
        // Act
        var mutualFriends = await connector.ExecuteAsync(
            "g.V('john').out('knows').where(in('knows').hasId('jane'))", 
            NoParams);
        
        // Assert
        mutualFriends.Should().NotBeEmpty();
    }

    [Test]
    public async Task Should_FindPopularPosts()
    {
        // Arrange
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();
        
        // Act
        var popularPosts = await connector.ExecuteAsync(
            "g.V().hasLabel('post').order().by('likes', desc).limit(3)", 
            NoParams);
        
        // Assert
        popularPosts.Should().HaveCount(2); // Based on built-in data
    }
}
```

### E-Commerce Testing
```csharp
[TestClass]
public class ECommerceTests : GraphTestBase
{
    [Test]
    public async Task Should_FindCustomerPurchaseHistory()
    {
        // Arrange
        var connector = InMemoryConnectorFactory.CreateECommerce();
        
        // Act
        var purchases = await connector.ExecuteAsync(
            "g.V('customer1').out('purchased')", 
            NoParams);
        
        // Assert
        purchases.Should().NotBeEmpty();
    }

    [Test]
    public async Task Should_RecommendProductsBasedOnPurchases()
    {
        // Arrange
        var connector = InMemoryConnectorFactory.CreateECommerce();
        
        // Act - Find products bought by similar customers
        var recommendations = await connector.ExecuteAsync(
            "g.V('customer1').out('purchased').in('purchased').out('purchased').dedup()", 
            NoParams);
        
        // Assert
        recommendations.Should().NotBeEmpty();
    }
}
```

## ?? Creating Custom Scenarios

### Simple Custom Scenario
```csharp
public class BlogScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "BlogPlatform";
    public override string Description => "Blog platform with authors, posts, comments, and tags";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new InMemoryVertexDefinition[]
        {
            // Authors
            new InMemoryVertexDefinition("author1", "author", Props(
                ("name", "Alice Writer"),
                ("email", "alice@blog.com"),
                ("joinDate", DateTime.UtcNow.AddYears(-2))
            )),
            new InMemoryVertexDefinition("author2", "author", Props(
                ("name", "Bob Blogger"),
                ("email", "bob@blog.com"),
                ("joinDate", DateTime.UtcNow.AddMonths(-6))
            )),

            // Posts
            new InMemoryVertexDefinition("post1", "post", Props(
                ("title", "Getting Started with Graph Databases"),
                ("content", "Graph databases are powerful..."),
                ("publishDate", DateTime.UtcNow.AddDays(-10)),
                ("views", 150)
            )),
            new InMemoryVertexDefinition("post2", "post", Props(
                ("title", "Advanced Gremlin Queries"),
                ("content", "Learn advanced traversal patterns..."),
                ("publishDate", DateTime.UtcNow.AddDays(-5)),
                ("views", 89)
            )),

            // Comments
            new InMemoryVertexDefinition("comment1", "comment", Props(
                ("text", "Great article!"),
                ("author", "Reader1"),
                ("timestamp", DateTime.UtcNow.AddDays(-9))
            )),

            // Tags
            new InMemoryVertexDefinition("tag1", "tag", Props(("name", "database"))),
            new InMemoryVertexDefinition("tag2", "tag", Props(("name", "tutorial"))),
            new InMemoryVertexDefinition("tag3", "tag", Props(("name", "gremlin")))
        };

        var edges = new InMemoryEdgeDefinition[]
        {
            // Author relationships
            new InMemoryEdgeDefinition("authored", "author1", "post1"),
            new InMemoryEdgeDefinition("authored", "author2", "post2"),

            // Comment relationships
            new InMemoryEdgeDefinition("commented_on", "comment1", "post1"),

            // Tag relationships
            new InMemoryEdgeDefinition("tagged_with", "post1", "tag1"),
            new InMemoryEdgeDefinition("tagged_with", "post1", "tag2"),
            new InMemoryEdgeDefinition("tagged_with", "post2", "tag1"),
            new InMemoryEdgeDefinition("tagged_with", "post2", "tag3"),

            // Following relationships
            new InMemoryEdgeDefinition("follows", "author2", "author1")
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Custom query for popular posts
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('post'\)\.order\(\)\.by\('views', desc\)", 
            (query, parameters) =>
        {
            var post1 = database.GetVertex("post1")?.ToGremlinResponse();
            var post2 = database.GetVertex("post2")?.ToGremlinResponse();
            return new[] { post1, post2 }.Where(x => x != null);
        });

        // Custom query for posts by tag
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('tag'\)\.has\('name', '(.+)'\)\.in\('tagged_with'\)", 
            (query, parameters) =>
        {
            // Extract tag name from query and return matching posts
            return new[] { database.GetVertex("post1")?.ToGremlinResponse() }.Where(x => x != null);
        });
    }
}
```

### Using Custom Scenarios
```csharp
[TestClass]
public class BlogPlatformTests : GraphTestBase
{
    public BlogPlatformTests()
    {
        // Register custom scenario
        InMemoryScenarioRegistry.Register(new BlogScenario());
    }

    [Test]
    public async Task Should_FindPostsByAuthor()
    {
        // Arrange
        var connector = InMemoryConnectorFactory.CreateWithScenario("BlogPlatform");
        
        // Act
        var posts = await connector.ExecuteAsync(
            "g.V('author1').out('authored')", 
            NoParams);
        
        // Assert
        posts.Should().HaveCount(1);
        ((string)posts.First().properties.title).Should().Be("Getting Started with Graph Databases");
    }

    [Test]
    public async Task Should_FindPopularPostsByViews()
    {
        // Arrange
        var connector = InMemoryConnectorFactory.CreateWithScenario("BlogPlatform");
        
        // Act
        var popularPosts = await connector.ExecuteAsync(
            "g.V().hasLabel('post').order().by('views', desc)", 
            NoParams);
        
        // Assert
        popularPosts.Should().HaveCount(2);
        ((int)popularPosts.First().properties.views).Should().Be(150);
    }
}
```

## ?? Advanced Configuration

### Database Options
```csharp
var connector = InMemoryConnectorFactory.CreateForTesting(options =>
{
    options.EnableDebugLogging = true;         // Log all queries
    options.AutoGenerateIds = false;          // Manual ID management
    options.ValidateEdgeVertices = true;      // Ensure vertices exist for edges
    options.CaseSensitiveLabels = false;      // Case-insensitive labels
    options.CaseSensitiveProperties = false;  // Case-insensitive properties
    options.MaxVertexCount = 10000;           // Limit vertices
    options.MaxEdgeCount = 50000;             // Limit edges
    options.TrackStatistics = true;           // Enable performance tracking
    options.VertexIdPrefix = "v_";            // Custom vertex ID prefix
    options.EdgeIdPrefix = "e_";              // Custom edge ID prefix
    options.AllowDuplicateEdges = false;      // Prevent duplicate edges
    options.CascadeDeleteEdges = true;        // Auto-delete connected edges
});
```

### Fluent Configuration
```csharp
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario("BasicSocialNetwork")
    .WithScenario("SimpleECommerce")
    .WithScenario(new BlogScenario());

// Clear and apply different scenarios
connector.ClearScenarios()
        .WithScenarios("UserRoleManagement", "OrganizationHierarchy");
```

### Performance Monitoring
```csharp
[Test]
public async Task Should_TrackQueryPerformance()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
    {
        options.TrackStatistics = true;
        options.EnableDebugLogging = true;
    });
    
    // Act
    await connector.ExecuteAsync("g.V().out('knows').out('knows').dedup().count()", NoParams);
    
    // Assert
    var stats = connector.GetStatistics();
    stats.VertexCount.Should().BeGreaterThan(0);
    stats.EdgeCount.Should().BeGreaterThan(0);
    
    connector.ConsumedRU.Should().BeGreaterThan(0);
}
```

## ?? Common Testing Patterns

### 1. Arrange-Act-Assert with Scenarios
```csharp
[Test]
public async Task Should_FollowAAA_Pattern()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    var userId = "john";
    
    // Act
    var friendsCount = await connector.ExecuteAsync(
        $"g.V('{userId}').out('knows').count()", NoParams);
    
    // Assert
    ((long)friendsCount.First()).Should().BeGreaterThan(0);
}
```

### 2. Parameterized Queries
```csharp
[Test]
public async Task Should_UseParameterizedQueries()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateECommerce();
    var parameters = new Dictionary<string, object>
    {
        ["customerId"] = "customer1",
        ["minPrice"] = 50.0
    };
    
    // Act
    var expensivePurchases = await connector.ExecuteAsync(
        "g.V(customerId).out('purchased').has('price', gte(minPrice))", 
        parameters);
    
    // Assert
    expensivePurchases.Should().NotBeEmpty();
}
```

### 3. Data Setup in Tests
```csharp
[Test]
public async Task Should_SetupCustomDataInTest()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateForTesting();
    
    // Setup test data
    await connector.ExecuteAsync("g.addV('user').property('id', 'testuser')", NoParams);
    await connector.ExecuteAsync("g.addV('product').property('id', 'testproduct')", NoParams);
    await connector.ExecuteAsync("g.V('testuser').addE('likes').to(g.V('testproduct'))", NoParams);
    
    // Act
    var likes = await connector.ExecuteAsync("g.V('testuser').out('likes')", NoParams);
    
    // Assert
    likes.Should().HaveCount(1);
}
```

### 4. Testing Graph Algorithms
```csharp
[Test]
public async Task Should_TestShortestPath()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateWithScenario("GraphTraversalTest");
    
    // Act - Find shortest path between v1 and v4
    var path = await connector.ExecuteAsync(
        "g.V('v1').repeat(out('connects')).until(hasId('v4')).path()", 
        NoParams);
    
    // Assert
    path.Should().NotBeEmpty();
}
```

### 5. Testing Complex Traversals
```csharp
[Test]
public async Task Should_TestComplexTraversal()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    
    // Act - Find friends of friends who like the same posts
    var recommendations = await connector.ExecuteAsync(@"
        g.V('john')
         .out('knows')
         .out('knows')
         .where(neq('john'))
         .where(out('likes').in('likes').hasId('john'))
         .dedup()", 
        NoParams);
    
    // Assert
    recommendations.Should().NotBeNull();
}
```

## ?? Debugging and Troubleshooting

### Enable Debug Logging
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;
});

// This will log all queries to console
var result = await connector.ExecuteAsync("g.V().count()", NoParams);
```

### Inspect Database State
```csharp
[Test]
public void Should_InspectDatabaseState()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    
    // Inspect vertices
    var allVertices = connector.GetAllVertices();
    foreach (var vertex in allVertices)
    {
        Console.WriteLine($"Vertex: {vertex.Id} ({vertex.Label})");
        foreach (var prop in vertex.Properties)
        {
            Console.WriteLine($"  {prop.Key}: {prop.Value}");
        }
    }
    
    // Inspect edges
    var allEdges = connector.GetAllEdges();
    foreach (var edge in allEdges)
    {
        Console.WriteLine($"Edge: {edge.Id} ({edge.Label}) from {edge.OutVertexId} to {edge.InVertexId}");
    }
}
```

### Custom Assertions
```csharp
public static class GraphAssertions
{
    public static void ShouldHaveVertex(this InMemoryGremlinLanguageConnector connector, 
        string id, string expectedLabel = null)
    {
        var vertex = connector.GetVertex(id);
        vertex.Should().NotBeNull($"Expected vertex with ID '{id}' to exist");
        
        if (expectedLabel != null)
        {
            vertex.Label.Should().Be(expectedLabel);
        }
    }
    
    public static void ShouldHaveEdge(this InMemoryGremlinLanguageConnector connector,
        string fromId, string toId, string expectedLabel = null)
    {
        var edges = connector.GetAllEdges()
            .Where(e => e.OutVertexId == fromId && e.InVertexId == toId);
            
        edges.Should().NotBeEmpty($"Expected edge from '{fromId}' to '{toId}' to exist");
        
        if (expectedLabel != null)
        {
            edges.Should().Contain(e => e.Label == expectedLabel);
        }
    }
}

// Usage
[Test]
public async Task Should_UseCustomAssertions()
{
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    
    connector.ShouldHaveVertex("john", "person");
    connector.ShouldHaveEdge("john", "jane", "knows");
}
```

## ?? Performance Testing

### Benchmark Tests
```csharp
[Test]
public async Task Should_PerformUnderLoad()
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    var stopwatch = Stopwatch.StartNew();
    
    // Act - Run 1000 queries
    var tasks = Enumerable.Range(0, 1000)
        .Select(i => connector.ExecuteAsync("g.V().count()", NoParams));
    
    await Task.WhenAll(tasks);
    stopwatch.Stop();
    
    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in < 5 seconds
}
```

### Memory Usage Testing
```csharp
[Test]
public void Should_ManageMemoryEfficiently()
{
    // Arrange
    var initialMemory = GC.GetTotalMemory(true);
    
    // Act - Create large scenario
    var connector = InMemoryConnectorFactory.CreateWithScenarios(
        new[] { "BasicSocialNetwork", "SimpleECommerce", "OrganizationHierarchy" });
    
    var stats = connector.GetStatistics();
    var finalMemory = GC.GetTotalMemory(true);
    
    // Assert
    var memoryUsed = finalMemory - initialMemory;
    Console.WriteLine($"Memory used: {memoryUsed / 1024 / 1024} MB for {stats.VertexCount} vertices, {stats.EdgeCount} edges");
    
    // Memory usage should be reasonable
    memoryUsed.Should().BeLessThan(100 * 1024 * 1024); // Less than 100MB
}
```

## ?? Best Practices

### 1. Test Organization
```csharp
// Group related tests
[TestClass]
public class UserManagementTests : GraphTestBase
{
    // Use consistent naming
    [Test]
    public async Task Should_CreateUser_When_ValidDataProvided() { }
    
    [Test]
    public async Task Should_ThrowException_When_UserAlreadyExists() { }
    
    [Test]
    public async Task Should_FindUser_When_SearchingByEmail() { }
}
```

### 2. Scenario Selection
```csharp
// ? Good - Use appropriate scenario for the test domain
[Test]
public async Task Should_TestSocialFeatures()
{
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    // Test social features
}

// ? Avoid - Using oversized scenarios for simple tests
[Test]
public async Task Should_CountVertices()
{
    // Overkill for a simple count test
    var connector = InMemoryConnectorFactory.CreateWithScenarios(
        new[] { "BasicSocialNetwork", "SimpleECommerce", "OrganizationHierarchy" });
}
```

### 3. Resource Management
```csharp
// Use using statements for large scenarios
[Test]
public async Task Should_ManageResourcesProperly()
{
    using var connector = InMemoryConnectorFactory.CreateECommerce();
    // Test logic here
    // Connector will be properly disposed
}
```

### 4. Test Data Isolation
```csharp
// Each test should use its own connector instance
[TestClass]
public class IsolatedTests
{
    [Test]
    public async Task Test1_Should_BeIsolated()
    {
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();
        // Test 1 logic
    }
    
    [Test]
    public async Task Test2_Should_BeIsolated()
    {
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();
        // Test 2 logic - independent of Test1
    }
}
```

### 5. Parameterized Testing
```csharp
[Theory]
[InlineData("BasicSocialNetwork")]
[InlineData("SimpleECommerce")]
[InlineData("UserRoleManagement")]
public async Task Should_HaveVertices_ForAllScenarios(string scenarioName)
{
    // Arrange
    var connector = InMemoryConnectorFactory.CreateWithScenario(scenarioName);
    
    // Act
    var vertices = await connector.ExecuteAsync("g.V().count()", NoParams);
    
    // Assert
    ((long)vertices.First()).Should().BeGreaterThan(0);
}
```

## ?? Integration with Testing Frameworks

### xUnit Integration
```csharp
public class GraphTestCollection : ICollectionFixture<GraphTestFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition] and all the ICollectionFixture<> interfaces.
}

public class GraphTestFixture : IDisposable
{
    public InMemoryGremlinLanguageConnector SocialNetworkConnector { get; }
    
    public GraphTestFixture()
    {
        SocialNetworkConnector = InMemoryConnectorFactory.CreateSocialNetwork();
    }
    
    public void Dispose()
    {
        SocialNetworkConnector?.Clear();
    }
}

[Collection("GraphTest")]
public class SocialNetworkTests
{
    private readonly GraphTestFixture _fixture;
    
    public SocialNetworkTests(GraphTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task Should_UseSameConnectorInstance()
    {
        var result = await _fixture.SocialNetworkConnector.ExecuteAsync("g.V().count()", NoParams);
        // Test logic
    }
}
```

### NUnit Integration
```csharp
[TestFixture]
public class GraphNUnitTests
{
    private InMemoryGremlinLanguageConnector _connector;
    
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _connector = InMemoryConnectorFactory.CreateSocialNetwork();
    }
    
    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _connector?.Clear();
    }
    
    [Test]
    public async Task Should_WorkWithNUnit()
    {
        var result = await _connector.ExecuteAsync("g.V().count()", NoParams);
        Assert.That(result, Is.Not.Empty);
    }
}
```

### MSTest Integration
```csharp
[TestClass]
public class GraphMSTests
{
    private static InMemoryGremlinLanguageConnector _connector;
    
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _connector = InMemoryConnectorFactory.CreateSocialNetwork();
    }
    
    [ClassCleanup]
    public static void ClassCleanup()
    {
        _connector?.Clear();
    }
    
    [TestMethod]
    public async Task Should_WorkWithMSTest()
    {
        var result = await _connector.ExecuteAsync("g.V().count()", NoParams);
        Assert.IsTrue(result.Any());
    }
}
```

## ?? Related Resources

- [Stardust.Paradox.Data Documentation](https://github.com/JonasSyrstad/Stardust.Paradox)
- [Gremlin Query Language Reference](https://tinkerpop.apache.org/docs/current/reference/#graph-traversal-steps)
- [Apache TinkerPop Documentation](https://tinkerpop.apache.org/docs/current/)
- [Graph Database Concepts](https://en.wikipedia.org/wiki/Graph_database)

## ?? Support & Troubleshooting

### Common Issues

1. **Scenario not found**: Ensure scenarios are registered before use
2. **Query parsing errors**: Check Gremlin syntax and supported operations
3. **Performance issues**: Use appropriate scenarios for your test size
4. **Memory usage**: Clear connectors after large test suites

### Getting Help

- GitHub Issues: [Report bugs and request features](https://github.com/JonasSyrstad/Stardust.Paradox/issues)
- Documentation: Check the included README and documentation files
- Examples: Review the built-in scenarios and usage examples

---

Happy testing with Stardust.Paradox.Data.InMemory! ??