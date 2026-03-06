# Full Examples — Stardust.Paradox InMemory Scenarios

## Example 1: Custom Scenario for a Domain-Specific Context

Suppose the consuming project has:

```csharp
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }
    string FirstName { get; set; }
    string LastName { get; set; }
    string Email { get; set; }
    bool VerifiedEmail { get; set; }

    [EdgeLabel("parent")]
    IEdgeCollection<IPerson> Parents { get; }
    IEdgeCollection<IPerson> Children { get; }

    [Eager]
    ICollection<ICompany> Employers { get; }
}

[VertexLabel("company")]
public interface ICompany : IVertex
{
    string Id { get; }
    string Name { get; set; }
    IEdgeCollection<IPerson> Employees { get; }
}

[EdgeLabel("employer")]
public interface IEmployment : IEdge<IPerson, ICompany>
{
    string Id { get; }
    DateTime HiredDate { get; set; }
}

public class MyContext : GraphContextBase
{
    public MyContext(IGremlinLanguageConnector connector, IServiceProvider sp)
        : base(connector, sp) { }

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        configuration.ConfigureCollection<IPerson>()
            .In(p => p.Parents, "parent").Out(p => p.Children);
        configuration.ConfigureCollection<ICompany>()
            .Out(c => c.Employees, "employer").In(p => p.Employers);
        configuration.ConfigureCollection<IEmployment>();
        return true;
    }

    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
}
```

### Step 1 — Create the Scenario

```csharp
using Stardust.Paradox.Data.InMemory.Scenarios;

public class EmploymentScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "Employment";
    public override string Description => "People, companies, and employment relationships";

    protected override (ScenarioVertexDefinition[] vertices,
                        ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new ScenarioVertexDefinition("alice", "person", Props(
                ("firstName", "Alice"),
                ("lastName", "Anderson"),
                ("email", "alice@example.com"),
                ("verifiedEmail", true)
            )),
            new ScenarioVertexDefinition("bob", "person", Props(
                ("firstName", "Bob"),
                ("lastName", "Baker"),
                ("email", "bob@example.com"),
                ("verifiedEmail", false)
            )),
            new ScenarioVertexDefinition("acme", "company", Props(
                ("name", "Acme Corp")
            ))
        };

        var edges = new ScenarioEdgeDefinition[]
        {
            // Alice works at Acme — edge label must match [EdgeLabel("employer")]
            new ScenarioEdgeDefinition("employer", "alice", "acme", Props(
                ("hiredDate", DateTime.UtcNow.AddYears(-3).ToString("o"))
            )),
            // Alice is Bob's parent
            new ScenarioEdgeDefinition("parent", "alice", "bob")
        };

        return (vertices, edges);
    }
}
```

### Step 2 — Write Tests

```csharp
using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;

public class EmploymentTests
{
    private static InMemoryGremlinLanguageConnector CreateConnector()
    {
        return InMemoryGremlinLanguageConnector.Create()
            .WithScenario<EmploymentScenario>();
    }

    [Fact]
    public async Task People_ShouldContainAliceAndBob()
    {
        // Arrange
        var connector = CreateConnector();

        // Act
        var result = await connector.ExecuteAsync(
            "g.V().hasLabel('person')",
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task AliceOutEmployer_ShouldReturnAcme()
    {
        // Arrange
        var connector = CreateConnector();

        // Act
        var result = await connector.ExecuteAsync(
            "g.V('alice').out('employer')",
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("acme");
    }

    [Fact]
    public async Task AliceChildren_ShouldReturnBob()
    {
        // Arrange — "parent" edge goes alice -> bob, so
        //          alice.out('parent') = bob
        var connector = CreateConnector();

        // Act
        var result = await connector.ExecuteAsync(
            "g.V('alice').out('parent')",
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("bob");
    }
}
```

---

## Example 2: Using GraphContextBase in Tests

```csharp
public class MyContextIntegrationTests
{
    private static (MyContext context, InMemoryGremlinLanguageConnector connector) CreateTestContext()
    {
        var connector = InMemoryGremlinLanguageConnector.Create()
            .WithScenario<EmploymentScenario>();

        var services = new ServiceCollection();
        services.AddEntityBinding((entity, impl) => services.AddTransient(entity, impl));
        var sp = services.BuildServiceProvider();

        var context = new MyContext(connector, sp);
        return (context, connector);
    }

    [Fact]
    public async Task VAsync_ShouldResolvePersonById()
    {
        // Arrange
        var (context, _) = CreateTestContext();

        // Act
        var alice = await context.VAsync<IPerson>("alice");

        // Assert
        alice.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEntity_ShouldTrackNewPerson()
    {
        // Arrange
        var (context, connector) = CreateTestContext();

        // Act
        var charlie = context.CreateEntity<IPerson>("charlie");
        charlie.FirstName = "Charlie";
        charlie.LastName = "Clark";
        await context.SaveChangesAsync();

        // Assert
        var result = await connector.ExecuteAsync(
            "g.V('charlie')",
            new Dictionary<string, object>());
        result.Should().HaveCount(1);
    }
}
```

---

## Example 3: Direct Database Population Without Scenarios

```csharp
[Fact]
public async Task DirectPopulation_ShouldWorkForSimpleCases()
{
    // Arrange
    var connector = InMemoryGremlinLanguageConnector.Create();

    var alice = connector.Database.AddVertex("person", "alice");
    alice.SetProperty("name", "Alice");
    alice.SetProperty("age", 30);

    var bob = connector.Database.AddVertex("person", "bob");
    bob.SetProperty("name", "Bob");
    bob.SetProperty("age", 25);

    connector.Database.AddEdge("knows", "alice", "bob");

    // Act
    var friends = await connector.ExecuteAsync(
        "g.V('alice').out('knows').values('name')",
        new Dictionary<string, object>());

    // Assert
    friends.Should().HaveCount(1);
}
```

---

## Example 4: Using Gremlin Strings to Populate

```csharp
[Fact]
public async Task GremlinStringPopulation_ShouldCreateGraph()
{
    // Arrange
    var connector = InMemoryGremlinLanguageConnector.Create();
    var emptyParams = new Dictionary<string, object>();

    await connector.ExecuteAsync(
        "g.addV('person').property('id','alice').property('name','Alice')", emptyParams);
    await connector.ExecuteAsync(
        "g.addV('person').property('id','bob').property('name','Bob')", emptyParams);
    await connector.ExecuteAsync(
        "g.V('alice').addE('knows').to(g.V('bob'))", emptyParams);

    // Act
    var result = await connector.ExecuteAsync("g.V('alice').out('knows')", emptyParams);

    // Assert
    result.Should().HaveCount(1);
    ((string)result.First().id).Should().Be("bob");
}
```

---

## Example 5: Custom Responses for Complex Queries

```csharp
public class AnalyticsScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "Analytics";
    public override string Description => "Scenario with custom response for analytics queries";

    protected override (ScenarioVertexDefinition[] vertices,
                        ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new ScenarioVertexDefinition("u1", "user", Props(("name", "User 1"), ("score", 85))),
            new ScenarioVertexDefinition("u2", "user", Props(("name", "User 2"), ("score", 92))),
            new ScenarioVertexDefinition("u3", "user", Props(("name", "User 3"), ("score", 78)))
        };

        return (vertices, null);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Custom aggregation response
        database.RegisterCustomResponse(
            @"g\.V\(\)\.hasLabel\('user'\)\.values\('score'\)\.mean\(\)",
            (query, parameters) => new dynamic[] { 85.0 });

        // Custom group-by response
        database.RegisterCustomResponse(
            @"g\.V\(\)\.hasLabel\('user'\)\.group\(\)\.by\('score'\)",
            (query, parameters) => new dynamic[]
            {
                new Dictionary<string, object>
                {
                    { "85", new[] { "u1" } },
                    { "92", new[] { "u2" } },
                    { "78", new[] { "u3" } }
                }
            });
    }
}
```

---

## Example 6: Tenant/Service Authorization Scenario

This pattern mirrors real-world usage where a service vertex has admin edges:

```csharp
public class ServiceAdminScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "ServiceAdmin";
    public override string Description => "Service with administrator edges for authorization testing";

    private readonly string _testUserId = "550e8400-e29b-41d4-a716-446655440000";
    private readonly string _serviceId = "12345678-1234-5678-9abc-123456789abc";

    protected override (ScenarioVertexDefinition[] vertices,
                        ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new ScenarioVertexDefinition(_testUserId, "user", Props(
                ("name", "Test User"),
                ("email", "test@example.com"),
                ("pk", _testUserId)
            )),
            new ScenarioVertexDefinition(_serviceId, "serviceDefinition", Props(
                ("name", "Test Service"),
                ("pk", _serviceId),
                ("productionService", true)
            ))
        };

        var edges = new ScenarioEdgeDefinition[]
        {
            new ScenarioEdgeDefinition("administrators", _serviceId, _testUserId, Props(
                ("roles", "ADMIN"),
                ("isOwner", true)
            ))
        };

        return (vertices, edges);
    }
}
```

---

## Example 7: Composing Multiple Scenarios

```csharp
[Fact]
public async Task MultipleScenarios_ShouldMergeData()
{
    // Arrange
    var connector = InMemoryGremlinLanguageConnector.Create()
        .WithScenario<EmploymentScenario>()
        .WithScenario<AnalyticsScenario>();

    // Act — data from both scenarios is present
    var people = await connector.ExecuteAsync(
        "g.V().hasLabel('person')", new Dictionary<string, object>());
    var users = await connector.ExecuteAsync(
        "g.V().hasLabel('user')", new Dictionary<string, object>());

    // Assert
    people.Should().HaveCount(2);  // From EmploymentScenario
    users.Should().HaveCount(3);   // From AnalyticsScenario
}
```

---

## Example 8: Debugging Failed Queries

```csharp
[Fact]
public async Task DebugMode_ShouldLogQueries()
{
    // Arrange — enable debug logging
    var connector = InMemoryGremlinLanguageConnector.Create(options =>
    {
        options.EnableQueryLogging = true;
        options.EnableDebugLogging = true;
    });

    connector.Database.AddVertex("person", "test");

    // Act — queries will be logged to console
    var result = await connector.ExecuteAsync(
        "g.V('test')", new Dictionary<string, object>());

    // Assert
    result.Should().HaveCount(1);

    // Use ToDebugString() to inspect database state
    var debugInfo = connector.ToDebugString();
    debugInfo.Should().Contain("test");
    debugInfo.Should().Contain("person");
}
```

---

## Matching Entity Labels to Scenario Definitions

When building scenarios for an existing `GraphContextBase` subclass, labels must
match exactly:

| Entity Attribute | Scenario Definition |
|-----------------|---------------------|
| `[VertexLabel("person")]` | `new ScenarioVertexDefinition("id", "person", ...)` |
| `[VertexLabel("company")]` | `new ScenarioVertexDefinition("id", "company", ...)` |
| `[EdgeLabel("employer")]` | `new ScenarioEdgeDefinition("employer", outV, inV, ...)` |
| `.In(p => p.Parents, "parent")` | `new ScenarioEdgeDefinition("parent", outV, inV, ...)` |
| `[ToWayEdgeLabel("spouce")]` | `new ScenarioEdgeDefinition("spouce", outV, inV, ...)` |

The edge direction matters: `OutVertexId` is the source vertex, `InVertexId` is
the target. This matches Gremlin's `g.V(outV).addE(label).to(g.V(inV))`.

---

## Test Project Setup Checklist

1. **Add NuGet packages:**
   - `Stardust.Paradox.Data.InMemory`
   - `xunit`
   - `xunit.runner.visualstudio`
   - `Microsoft.NET.Test.Sdk`
   - `FluentAssertions` (recommended)

2. **Add project reference** to the project containing entity interfaces and the
   `GraphContextBase` subclass.

3. **Create scenario class** matching the entity labels.

4. **Create test class** — instantiate connector with scenario, construct context,
   exercise API, assert results.

5. **Run:** `dotnet test`
