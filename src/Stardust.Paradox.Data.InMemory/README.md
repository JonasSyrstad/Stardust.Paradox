# Stardust.Paradox.Data.InMemory

[![NuGet Version](https://img.shields.io/nuget/v/Stardust.Paradox.Data.InMemory?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.InMemory/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Stardust.Paradox.Data.InMemory?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.InMemory/)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue?style=flat-square)](https://github.com/JonasSyrstad/Stardust.Paradox/blob/main/LICENSE)

A **production-ready**, high-performance in-memory Gremlin graph database implementation for testing, development, and production debugging. Perfect for unit testing, integration testing, rapid prototyping, and creating safe local environments for troubleshooting production issues.

## ✨ Features

- ⚡ **High Performance**: Lightning-fast in-memory graph operations with optimized query execution
- 🎭 **Scenario Framework**: Pre-built test data scenarios for common domains plus production data import
- 🔄 **Gremlin Compatible**: Full support for Gremlin traversal language and TinkerPop 3.x protocol
- 🔌 **Easy Integration**: Seamless integration with existing Paradox applications and standard Gremlin.Net clients
- ✅ **Test Friendly**: Designed specifically for testing scenarios with comprehensive validation
- 🎯 **Fluent API**: Intuitive, chainable configuration methods
- 📊 **Performance Tracking**: Built-in query performance monitoring and statistics
- 🚀 **Zero Dependencies**: No external database setup required for development
- 🔍 **Production Debugging**: Export real database scenarios for local debugging
- 🖥️ **Server Mode**: Built-in Gremlin server with WebSocket and TCP support
- 📥 **Live Data Import**: Import scenarios directly from CosmosDB and other Gremlin databases
- 🌐 **TinkerPop Compatible**: Full compatibility with Apache TinkerPop clients and tools
- 🏗️ **Entity Framework Style**: Full support for GraphContextBase-based applications

## 📦 Installation

### Package Manager
```powershell
Install-Package Stardust.Paradox.Data.InMemory
```

### .NET CLI
```bash
dotnet add package Stardust.Paradox.Data.InMemory
```

### PackageReference
```xml
<PackageReference Include="Stardust.Paradox.Data.InMemory" Version="1.0.0" />
```

## 🚀 Quick Start

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

## 🏗️ Testing with GraphContextBase (Entity Framework Style)

The **GraphContextBase** is the core of the Stardust.Paradox.Data framework, providing an Entity Framework-like experience for graph databases. The InMemory connector seamlessly integrates with GraphContextBase applications, making it perfect for testing your existing Paradox-based applications.

### Entity Definition

Define your graph entities using interfaces with proper attributes:

```csharp
using Stardust.Paradox.Data.Annotations;

[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }
    string FirstName { get; set; }
    string LastName { get; set; }
    string Email { get; set; }
    EpochDateTime Birthday { get; set; }
    
    // Navigation properties for graph relationships
    IEdgeCollection<IPerson> Friends { get; }
    IEdgeCollection<ICompany> Employers { get; }
    
    [InLabel("city")]
    IEdgeReference<ICity> HomeCity { get; }
}

[VertexLabel("company")]
public interface ICompany : IVertex
{
    string Id { get; }
    string Name { get; set; }
    string Industry { get; set; }
    
    [OutLabel("employer")]
    IEdgeCollection<IPerson> Employees { get; }
}

[VertexLabel("city")]
public interface ICity : IVertex
{
    string Id { get; }
    string Name { get; set; }
    string Country { get; set; }
    
    [OutLabel("city")]
    IEdgeCollection<IPerson> Residents { get; }
}

[EdgeLabel("employment")]
public interface IEmployment : IEdge<IPerson, ICompany>
{
    string Id { get; }
    EpochDateTime HiredDate { get; set; }
    string Position { get; set; }
    decimal Salary { get; set; }
}
```

### GraphContext Implementation

Create a GraphContext that inherits from GraphContextBase:

```csharp
using Stardust.Paradox.Data;
using Microsoft.Extensions.DependencyInjection;

public class TestGraphContext : GraphContextBase
{
    static TestGraphContext()
    {
        PartitionKeyName = "pk"; // For CosmosDB compatibility
    }

    public TestGraphContext(IGremlinLanguageConnector connector) 
        : base(connector, CreateServiceProvider())
    {
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Register entity implementations (auto-generated or manual)
        services.AddEntityBinding((entity, implementation) => 
        {
            services.AddTransient(entity, implementation);
        });
        
        return services.BuildServiceProvider();
    }

    // Graph sets for entity operations
    public IGraphSet<IPerson> Persons => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    public IGraphSet<ICity> Cities => GraphSet<ICity>();
    public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        try
        {
            // Configure entity relationships
            configuration
                .ConfigureCollection<IPerson>()
                    .Out(p => p.Friends, "knows").In(p => p.Friends) // Bidirectional friendship
                    .Out(p => p.Employers, "employer").In<ICompany>(c => c.Employees)
                    .Out(p => p.HomeCity, "lives_in").In<ICity>(c => c.Residents)
                .ConfigureCollection<ICompany>()
                    .In(c => c.Employees, "employer").Out<IPerson>(p => p.Employers)
                .ConfigureCollection<ICity>()
                    .In(c => c.Residents, "lives_in").Out<IPerson>(p => p.HomeCity)
                .ConfigureCollection<IEmployment>();

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Binding already exists, skip
            return false;
        }
    }

    // Optional: Seed test data
    protected override async Task SeedAsync()
    {
        // Add any initial test data here
        await Task.CompletedTask;
    }
}
```

### Unit Testing with InMemory GraphContext

#### Basic Unit Test Setup

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stardust.Paradox.Data.InMemory;

[TestClass]
public class GraphContextTests
{
    private TestGraphContext _context;

    [TestInitialize]
    public void Setup()
    {
        // Create InMemory connector with optional scenario
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();
        _context = new TestGraphContext(connector);
    }

    [TestMethod]
    public async Task TestCreateAndRetrievePerson()
    {
        // Arrange
        var personId = "test-person-1";
        var person = _context.CreateEntity<IPerson>(personId);
        person.FirstName = "John";
        person.LastName = "Doe";
        person.Email = "john.doe@example.com";
        person.Birthday = EpochDateTime.FromDateTime(new DateTime(1990, 5, 15));

        // Act
        await _context.SaveChangesAsync();
        var retrievedPerson = await _context.VAsync<IPerson>(personId);

        // Assert
        Assert.IsNotNull(retrievedPerson);
        Assert.AreEqual("John", retrievedPerson.FirstName);
        Assert.AreEqual("Doe", retrievedPerson.LastName);
        Assert.AreEqual("john.doe@example.com", retrievedPerson.Email);
    }

    [TestMethod]
    public async Task TestPersonCompanyRelationship()
    {
        // Arrange
        var person = _context.CreateEntity<IPerson>("person-1");
        person.FirstName = "Alice";
        person.LastName = "Smith";

        var company = _context.CreateEntity<ICompany>("company-1");
        company.Name = "TechCorp";
        company.Industry = "Technology";

        // Create employment relationship
        var employment = _context.Employments.Create(person, company);
        employment.HiredDate = EpochDateTime.FromDateTime(DateTime.UtcNow.AddYears(-2));
        employment.Position = "Software Engineer";
        employment.Salary = 85000m;

        // Act
        await _context.SaveChangesAsync();

        // Assert - Test navigation properties
        var employerCompanies = await person.Employers.ToVerticesAsync();
        Assert.AreEqual(1, employerCompanies.Count());
        Assert.AreEqual("TechCorp", employerCompanies.First().Name);

        var employees = await company.Employees.ToVerticesAsync();
        Assert.AreEqual(1, employees.Count());
        Assert.AreEqual("Alice", employees.First().FirstName);
    }

    [TestMethod]
    public async Task TestComplexGraphQuery()
    {
        // Arrange - Create a network of people and companies
        await SetupTestNetwork();

        // Act - Find all people who work at tech companies
        var techWorkers = await _context.VAsync<IPerson>(g => 
            g.V().HasLabel("person")
             .Out("employer")
             .Has("industry", "Technology")
             .In("employer"));

        // Assert
        Assert.IsTrue(techWorkers.Any());
        
        // Verify using GraphContext query methods
        var directQuery = await _context.ExecuteAsync<IPerson>(g =>
            g.V().Has("industry", "Technology").In("employer"));
        
        Assert.IsTrue(directQuery.Any());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context?.Dispose();
    }

    private async Task SetupTestNetwork()
    {
        // Create test people
        var alice = _context.CreateEntity<IPerson>("alice");
        alice.FirstName = "Alice";
        alice.LastName = "Johnson";

        var bob = _context.CreateEntity<IPerson>("bob");
        bob.FirstName = "Bob";
        bob.LastName = "Wilson";

        // Create test companies
        var techCorp = _context.CreateEntity<ICompany>("techcorp");
        techCorp.Name = "TechCorp";
        techCorp.Industry = "Technology";

        var retailCorp = _context.CreateEntity<ICompany>("retailcorp");
        retailCorp.Name = "RetailCorp";
        retailCorp.Industry = "Retail";

        // Create relationships
        var aliceEmployment = _context.Employments.Create(alice, techCorp);
        aliceEmployment.Position = "Developer";

        var bobEmployment = _context.Employments.Create(bob, retailCorp);
        bobEmployment.Position = "Manager";

        await _context.SaveChangesAsync();
    }
}
```

#### Integration Testing with Scenarios

```csharp
[TestClass]
public class GraphContextIntegrationTests
{
    [TestMethod]
    public async Task TestWithPredefinedScenario()
    {
        // Arrange - Use built-in scenario
        var connector = InMemoryConnectorFactory.CreateSocialNetwork();
        using var context = new TestGraphContext(connector);

        // Act - Query predefined data
        var allPeople = await context.Persons.ToListAsync();
        var friendships = await context.ExecuteAsync<IPerson>(g =>
            g.V().HasLabel("person").Out("knows"));

        // Assert
        Assert.IsTrue(allPeople.Any());
        Assert.IsTrue(friendships.Any());
    }

    [TestMethod]
    public async Task TestChangeTracking()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        using var context = new TestGraphContext(connector);

        var person = context.CreateEntity<IPerson>("tracking-test");
        person.FirstName = "Original";
        await context.SaveChangesAsync();

        // Act - Modify entity
        person.FirstName = "Modified";
        await context.SaveChangesAsync();

        // Assert - Verify change was persisted
        context.Clear(); // Clear context cache
        var reloadedPerson = await context.VAsync<IPerson>("tracking-test");

        // Verify that the FirstName property was updated
        Assert.AreEqual("Modified", reloadedPerson.FirstName);
    }

    [TestMethod]
    public async Task TestTransactionBehavior()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        using var context = new TestGraphContext(connector);

        var person1 = context.CreateEntity<IPerson>("txn-1");
        person1.FirstName = "Person1";

        var person2 = context.CreateEntity<IPerson>("txn-2");
        person2.FirstName = "Person2";

        // Act & Assert - All changes saved together
        await context.SaveChangesAsync();

        // Query the number of vertices with the label "person"
        var count = await context.ExecuteAsync<IPerson>(g => 
            g.V().HasLabel("person").Count());
        
        // Verify that the count is 2
        Assert.AreEqual(2, count.First());
    }
}
```

### Advanced GraphContext Testing Patterns

#### Testing Dynamic Properties

```csharp
[TestMethod]
public async Task TestDynamicProperties()
{
    // For entities implementing IDynamicGraphEntity
    var connector = InMemoryGremlinLanguageConnector.Create();
    using var context = new TestGraphContext(connector);

    var person = context.CreateEntity<IPerson>("dynamic-test");
    person.FirstName = "John";
    
    // Set dynamic properties (if IPerson implements IDynamicGraphEntity)
    if (person is IDynamicGraphEntity dynamicPerson)
    {
        dynamicPerson.SetProperty("customField", "customValue");
        dynamicPerson.SetProperty("rating", 4.5);
    }

    await context.SaveChangesAsync();

    // Verify dynamic properties persist
    var reloaded = await context.VAsync<IPerson>("dynamic-test");
    if (reloaded is IDynamicGraphEntity reloadedDynamic)
    {
        Assert.AreEqual("customValue", reloadedDynamic.GetProperty("customField"));
        Assert.AreEqual(4.5, reloadedDynamic.GetProperty("rating"));
    }
}
```

#### Testing Complex Queries and Aggregations

```csharp
[TestMethod]
public async Task TestComplexAggregationQueries()
{
    var connector = InMemoryConnectorFactory.CreateECommerce();
    using var context = new TestGraphContext(connector);

    // Test aggregation using GremlinContext
    var companySizeStats = await context.ExecuteAsync<dynamic>(g =>
        g.V().HasLabel("company")
         .Group()
         .By("industry")
         .By(__.Out("employer").Count()));

    Assert.IsTrue(companySizeStats.Any());

    // Test using GraphSet operations
    var topCompanies = await context.Companies
        .Where(c => c.Industry == "Technology")
        .ToListAsync();

    Assert.IsTrue(topCompanies.Any());
}
```

#### Performance Testing

```csharp
[TestMethod]
public async Task TestQueryPerformance()
{
    var connector = InMemoryConnectorFactory.CreateSocialNetwork();
    using var context = new TestGraphContext(connector);

    var stopwatch = Stopwatch.StartNew();

    // Test complex traversal performance
    var results = await context.ExecuteAsync<IPerson>(g =>
        g.V().HasLabel("person")
         .Out("knows").Out("knows") // Friends of friends
         .Dedup()
         .Limit(100));

    stopwatch.Stop();

    Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000); // Should be fast in-memory
    Assert.IsTrue(results.Any());

    // Check RU consumption (compatibility with CosmosDB patterns)
    Assert.IsTrue(context.ConsumedRU >= 0);
}
```

### Testing Edge Cases and Error Handling

```csharp
[TestMethod]
public async Task TestConcurrentModification()
{
    var connector = InMemoryGremlinLanguageConnector.Create();
    using var context1 = new TestGraphContext(connector);
    using var context2 = new TestGraphContext(connector);

    // Create entity in first context
    var person1 = context1.CreateEntity<IPerson>("concurrent-test");
    person1.FirstName = "Context1";
    await context1.SaveChangesAsync();

    // Modify in second context
    var person2 = await context2.VAsync<IPerson>("concurrent-test");
    person2.FirstName = "Context2";
    await context2.SaveChangesAsync();

    // Verify final state
    context1.Clear(); // Clear cache
    var final = await context1.VAsync<IPerson>("concurrent-test");
    Assert.AreEqual("Context2", final.FirstName);
}

[TestMethod]
public async Task TestErrorHandling()
{
    var connector = InMemoryGremlinLanguageConnector.Create();
    using var context = new TestGraphContext(connector);

    // Test handling of non-existent entities
    var nonExistent = await context.VAsync<IPerson>("does-not-exist");
    Assert.IsNull(nonExistent);

    // Test exception during save
    var person = context.CreateEntity<IPerson>("error-test");
    person.FirstName = "Test";
    
    // Simulate error condition (implementation specific)
    // await Assert.ThrowsExceptionAsync<GraphExecutionException>(() => context.SaveChangesAsync());
}
```

### Advantages of InMemory Testing with GraphContextBase

#### 🚀 **Performance Benefits**
- **Instant Setup**: No database startup time or connection delays
- **Fast Execution**: All operations run in memory at native speed
- **Parallel Testing**: Run multiple test suites simultaneously without conflicts

#### 🔄 **Development Workflow**
- **Rapid Iteration**: Make changes and test immediately
- **No External Dependencies**: Tests run anywhere without database setup
- **Consistent Environment**: Same test results across all machines

#### ✅ **Testing Reliability**
- **Isolated Tests**: Each test gets a fresh database state
- **Predictable Data**: Built-in scenarios provide consistent test data
- **Change Tracking**: Full support for GraphContextBase change tracking
- **Transaction Support**: Test complete save operations

#### 🔧 **Flexibility**
- **Custom Scenarios**: Create domain-specific test data
- **Production Debugging**: Import real data for local testing
- **Multiple Frameworks**: Works with MSTest, NUnit, xUnit, etc.
- **Easy Mocking**: Perfect for integration testing

## 🖥️ Setting up InMemoryGremlinServer for Testing with Gremlin.Net

The InMemoryGremlinServer provides a complete Gremlin server implementation that's compatible with standard Gremlin.Net clients. This is perfect for integration testing, development, and debugging scenarios where you need to test against a real Gremlin server without external dependencies.

### Basic Server Setup

#### Simple Development Server
```csharp
using Stardust.Paradox.Data.InMemory.Management;
using Stardust.Paradox.Data.InMemory.Server;

// Quick start with default settings (localhost:8182)
var server = await GremlinDatabase.StartWebSocketDevServerAsync();

Console.WriteLine($"Server running on {server.Options.Host}:{server.Options.Port}");
Console.WriteLine($"WebSocket endpoint: ws://{server.Options.Host}:{server.Options.GetHttpPort()}/gremlin");

// Server is now ready for Gremlin.Net clients
```

#### Custom Configuration
```csharp
using Stardust.Paradox.Data.InMemory.Server;
using Stardust.Paradox.Data.InMemory.Core;

// Create server with custom options
var options = new InMemoryGremlinServerOptions
{
    Host = "localhost",
    Port = 8182,                    // TCP port for Gremlin protocol
    HttpPort = 8183,               // WebSocket/HTTP port  
    EnableWebSocket = true,        // Enable WebSocket support (.NET 8+ only)
    EnableTcp = true,              // Enable TCP support
    EnableHttp = true,             // Enable HTTP support
    MaxConnections = 50,           // Maximum concurrent connections
    ConnectionTimeoutSeconds = 30, // Connection timeout
    QueryTimeoutSeconds = 60,      // Query execution timeout
    EnableLogging = true,          // Enable request/response logging
    EnableDebugLogging = false,    // Enable detailed debug logging
    
    // Database configuration
    DatabaseOptions = new InMemoryDatabaseOptions
    {
        EnableQueryLogging = true,
        TrackStatistics = true,
        AutoGenerateIds = true,
        ValidateEdgeVertices = true
    }
};

var server = new InMemoryGremlinServer(options);
await server.StartAsync();
```

### Integration with Gremlin.Net Clients

#### Standard Gremlin.Net Client Connection
```csharp
using Gremlin.Net.Driver;
using Gremlin.Net.Driver.Remote;
using Gremlin.Net.Process.Traversal;

// Connect to the InMemoryGremlinServer
var gremlinServer = new GremlinServer("localhost", 8182);
var gremlinClient = new GremlinClient(gremlinServer);

// Execute queries using standard Gremlin.Net API
var result = await gremlinClient.SubmitAsync("g.V().count()");
Console.WriteLine($"Vertex count: {result.First()}");

// Or use traversal approach
var connection = new DriverRemoteConnection(gremlinClient);
var g = AnonymousTraversalSource.Traversal().WithRemote(connection);

var vertices = await g.V().ToListAsync();
var edges = await g.E().ToListAsync();
```

#### WebSocket Connection (.NET 8+ only)
```csharp
using Gremlin.Net.Driver;

// Connect via WebSocket (more efficient for frequent queries)
var gremlinServer = new GremlinServer("localhost", 8183, enableSsl: false);
var gremlinClient = new GremlinClient(gremlinServer);

// Same API as TCP connection
var people = await gremlinClient.SubmitAsync("g.V().hasLabel('person').values('name')");
foreach (var person in people)
{
    Console.WriteLine($"Person: {person}");
}
```

### Testing Scenarios with Server

#### Unit Test Setup
```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stardust.Paradox.Data.InMemory.Management;
using Gremlin.Net.Driver;

[TestClass]
public class GremlinIntegrationTests
{
    private InMemoryGremlinServer _server;
    private GremlinClient _client;

    [TestInitialize]
    public async Task Setup()
    {
        // Start server with test data
        _server = await GremlinDatabase.StartWebSocketDevServerAsync(options =>
        {
            options.Port = 8182;
            options.EnableLogging = false; // Quiet during tests
            options.DatabaseOptions.EnableQueryLogging = false;
        });

        // Load test scenario
        _server.Connector.WithScenario("BasicSocialNetwork");

        // Create client
        var gremlinServer = new GremlinServer("localhost", 8182);
        _client = new GremlinClient(gremlinServer);
    }

    [TestMethod]
    public async Task TestPersonQuery()
    {
        var result = await _client.SubmitAsync("g.V().hasLabel('person').count()");
        Assert.IsTrue(result.First<long>() > 0);
    }

    [TestMethod]
    public async Task TestFriendshipTraversal()
    {
        var friends = await _client.SubmitAsync("g.V().hasLabel('person').out('knows').values('name')");
        Assert.IsTrue(friends.Any());
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }
    }
}
```

#### Integration Test with Custom Data
```csharp
[TestMethod]
public async Task TestWithCustomData()
{
    // Clear existing data and add custom test data
    await _client.SubmitAsync("g.V().drop()");
    
    // Add test vertices
    await _client.SubmitAsync("g.addV('user').property('id', 'user1').property('name', 'Alice')");
    await _client.SubmitAsync("g.addV('user').property('id', 'user2').property('name', 'Bob')");
    await _client.SubmitAsync("g.addV('product').property('id', 'prod1').property('name', 'Widget')");
    
    // Add relationships
    await _client.SubmitAsync("g.V('user1').addE('purchased').to(g.V('prod1')).property('date', '2024-01-01')");
    
    // Test the data
    var purchases = await _client.SubmitAsync("g.V('user1').out('purchased').values('name')");
    Assert.AreEqual("Widget", purchases.First<string>());
}
```

### Advanced Configuration

#### Database Options
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

#### Fluent API
```csharp
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario("BasicSocialNetwork")
    .WithScenario("SimpleECommerce");

// Clear and apply different scenarios
connector.ClearScenarios()
        .WithScenarios("UserRoleManagement", "OrganizationHierarchy");
```

#### Server Mode Configuration
```csharp
// Start an in-memory Gremlin server
var server = new InMemoryGremlinServer(options =>
{
    options.Port = 8182;
    options.EnableWebSocket = true;        // .NET 8+ only
    options.EnableTcp = true;
    options.MaxConnections = 100;
    options.DatabaseOptions.TrackStatistics = true;
});

await server.StartAsync();

// Connect with standard Gremlin clients
var client = new GremlinClient(new GremlinServer("localhost", 8182));
```

## 📚 Documentation

Comprehensive documentation is included in the package:

- **README.md**: This comprehensive guide
- **SCENARIO_FRAMEWORK_README.md**: Detailed scenario framework documentation
- **TESTING_GUIDE.md**: Best practices for testing with InMemory
- **GREMLIN_SERVER_USAGE.md**: Server setup and configuration guide

### 🔧 ScenarioConnector Tool Reference

The ScenarioConnector project (`Stardust.Paradox.Data.ScenarioConnector`) provides tools for production debugging:

#### Core Classes

| Class | Purpose | Key Methods |
|-------|---------|-------------|
| **ScenarioExporter** | Export production data to scenarios | `ExportByQueryAsync()`, `ExportByIdsAsync()`, `TestVertexPropertyFetchAsync()` |
| **ExportedScenario** | Container for exported graph data | Properties: `Vertices`, `Edges`, `Metadata` |
| **ExportedVertex** | Represents a vertex in exported data | Properties: `Id`, `Label`, `Properties` |
| **ExportedEdge** | Represents an edge in exported data | Properties: `Id`, `Label`, `OutVertexId`, `InVertexId`, `Properties` |
| **ConsolidatedProgressBar** | Visual progress tracking for exports | `IncrementVertexProgress()`, `IncrementEdgeProgress()` |

#### Command-Line Tool Usage

```bash
# Build and run the ScenarioConnector
cd src/Stardust.Paradox.Data.ScenarioConnector
dotnet build
dotnet run

# Available menu options:
# 1. (C)onnect to database and export scenarios
# 2. (A)dd new connection  
# 3. (L)ist connections
# 4. (T)est connection
# 5. (D)elete connection
# 6. E(x)it
```

#### Programmatic Usage Examples

```csharp
// Create exporter
var exporter = new ScenarioExporter(gremlinConnector, "Production");

// Export by query
var scenario = await exporter.ExportByQueryAsync(
    "g.V().has('userId', 'problem-user-123').bothV().dedup().limit(50)",
    "ProblemUserNetwork"
);

// Export by vertex IDs
var scenario2 = await exporter.ExportByIdsAsync(
    new[] { "user1", "user2", "user3" },
    "SpecificUsers"
);

// Save/load scenarios
await ScenarioExporter.SaveScenarioAsync(scenario, "users.json");
var loaded = await ScenarioExporter.LoadScenarioAsync("users.json");
```

For complete examples, see the `ScenarioConnector` project in the source code.

## 🔍 Production Debugging with ScenarioConnector

The **ScenarioConnector** tool allows you to export real production data scenarios for safe local debugging:

### Overview
The ScenarioConnector is a command-line tool that connects to your production CosmosDB or other Gremlin databases and exports specific data subsets as InMemory scenarios. This enables you to:

- 🔍 **Debug Production Issues Locally**: Reproduce production problems in a safe, isolated environment
- ✅ **Safe Data Access**: Read-only connections ensure no accidental modifications to production data
- 🎯 **Selective Export**: Export only the data you need via Gremlin queries or vertex IDs
- 📁 **Repeatable Scenarios**: Save exported scenarios as reusable test fixtures
- ⚡ **Fast Iteration**: Debug and test fixes rapidly without production dependencies

### Installation and Setup

#### Option 1: Build from Source

**PowerShell Script for Windows:**
```powershell
# Download and build ScenarioConnector tool
# Run this script from any directory

# Set variables
$RepoUrl = "https://github.com/JonasSyrstad/Stardust.Paradox.git"
$TempDir = "$env:TEMP\Stardust.Paradox"
$ProjectPath = "src\Stardust.Paradox.Data.ScenarioConnector"

Write-Host "🔄 Setting up Stardust.Paradox ScenarioConnector..." -ForegroundColor Cyan

# Clean up any existing temp directory
if (Test-Path $TempDir) {
    Write-Host "🧹 Cleaning up existing files..." -ForegroundColor Yellow
    Remove-Item $TempDir -Recurse -Force
}

# Clone the repository
Write-Host "📥 Cloning repository..." -ForegroundColor Green
git clone $RepoUrl $TempDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to clone repository" -ForegroundColor Red
    exit 1
}

# Navigate to project directory
$FullProjectPath = Join-Path $TempDir $ProjectPath
if (-not (Test-Path $FullProjectPath)) {
    Write-Host "❌ Project directory not found: $FullProjectPath" -ForegroundColor Red
    exit 1
}

Set-Location $FullProjectPath

# Build the project
Write-Host "🔨 Building ScenarioConnector..." -ForegroundColor Green
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

# Run the tool
Write-Host "🚀 Starting ScenarioConnector..." -ForegroundColor Green
Write-Host "📍 Project location: $FullProjectPath" -ForegroundColor Gray
Write-Host "💡 Tip: You can run 'dotnet run' from this directory anytime" -ForegroundColor Gray
Write-Host ""

dotnet run
```

#### Option 2: Manual Build

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/JonasSyrstad/Stardust.Paradox.git
   cd Stardust.Paradox/src/Stardust.Paradox.Data.ScenarioConnector
   ```

2. **Build and Run**:
   ```bash
   dotnet build
   dotnet run
   ```

### Basic Usage

1. **Add a Production Connection**:
   ```
   Main Menu:
   2. (A)dd new connection
   
   Connection name: Production-Issues
   Hostname: myaccount.gremlin.cosmosdb.azure.com
   Database name: GraphDB
   Graph name: social
   Access key: [your-read-only-key]
   ```

2. **Export a Scenario**:
   ```
   1. (C)onnect to database and export scenarios
   
   Export Menu:
   1. Export by (Q)uery
   
   Enter Gremlin query: g.V().has('userId', 'problem-user-123').bothV().dedup().limit(50)
   Enter scenario name: ProblemUserNetwork
   Enter description: Network around problematic user for debugging
   ```

3. **Use in Your Tests**:
   ```csharp
   // Copy the generated .cs file to your test project
   InMemoryScenarioRegistry.Register(new ProblemUserNetworkScenario());
   
   // Create connector with production data
   var connector = InMemoryConnectorFactory.CreateWithScenario("ProblemUserNetwork");
   
   // Debug the issue locally
   var problematicData = await connector.ExecuteAsync(
       "g.V().has('userId', 'problem-user-123').out('relationship')", 
       new Dictionary<string, object>());
   ```

### ScenarioConnector Features

- **🔒 Secure Storage**: Connection strings are encrypted and stored locally
- **✅ Connection Testing**: Verify connections before use with read-only validation
- **📊 Progress Tracking**: Visual progress bars for large exports
- **🔧 Complex Query Support**: Handles advanced Gremlin queries and transformations
- **📁 Multiple Export Formats**: JSON scenarios and C# class files
- **🐛 Debug Logging**: Detailed logging for troubleshooting export issues
- **⚡ Performance Monitoring**: Track export performance and database statistics


### Production Debugging Workflow

1. **Identify the Issue**: Determine which data subset is related to the production issue
2. **Export Scenario**: Use ScenarioConnector to export relevant vertices and edges
3. **Local Debugging**: Load the scenario in your development environment
4. **Reproduce Issue**: Run your application logic against the exported data
5. **Develop Fix**: Implement and test the fix locally
6. **Validate Solution**: Verify the fix works with the production scenario
7. **Deploy Confidently**: Deploy knowing the fix handles the specific production data


#### Performance Optimization
```csharp
// Limit export size to manageable datasets
var optimizedQuery = @"
    g.V().has('issue_id', 'ISSUE-123')
          .bothE().bothV()
          .dedup()
          .limit(1000)"; // Always limit results

// Use specific queries rather than broad exports
var focusedQuery = @"
    g.V().has('user_id', within('user1', 'user2', 'user3'))
          .bothE('interacted_with', 'purchased', 'reviewed')
          .bothV()
          .dedup()";
```

#### Version Control Integration
```csharp
// Include scenario metadata for tracking
var scenario = new ExportedScenario
{
    Name = "BugFix_JIRA-1234",
    Description = "Data export for JIRA-1234: User authentication issue",
    Metadata = new Dictionary<string, object>
    {
        ["jira_ticket"] = "JIRA-1234",
        ["git_branch"] = "bugfix/auth-issue",
        ["developer"] = "john.doe@company.com",
        ["export_date"] = DateTime.UtcNow,
        ["production_version"] = "v2.3.1"
    }
};
```

### Troubleshooting Export Issues

#### Common Issues and Solutions

```csharp
// Issue: Complex queries not returning expected data
try
{
    var complexResult = await exporter.ExportByQueryAsync(complexQuery, "ComplexTest");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("complex query"))
{
    // Fallback: Use simpler query and post-process
    var simpleQuery = "g.V().has('type', 'target_type')";
    var simpleResult = await exporter.ExportByQueryAsync(simpleQuery, "SimpleTest");
    
    // Then filter results in code
    var filteredVertices = FilterVerticesInCode(simpleResult.Vertices);
}

// Issue: Properties not being extracted correctly
var exporter = new ScenarioExporter(connector, "Production");
exporter.EnableDebugLogging = true; // Enable debug output

// Test property extraction for specific vertices
await exporter.TestVertexPropertyFetchAsync("problematic-vertex-id");

// Issue: Large datasets causing timeout
var exportOptions = new ExportOptions
{
    MaxVertices = 5000,
    MaxEdges = 10000,
    TimeoutSeconds = 300
};
```

#### Debugging Property Extraction

```csharp
// Enable debug logging to see raw database responses
var debugExporter = new ScenarioExporter(connector, "Debug");
debugExporter.EnableDebugLogging = true;

// This will show:
// - Raw JSON structures from database
// - Property parsing attempts
// - Value extraction process
// - Type conversion details
var debugScenario = await debugExporter.ExportByQueryAsync("g.V().limit(1)", "Debug");
```