---
name: stardust-inmemory-scenarios
description: >
  Create InMemory graph database test scenarios for projects that use Stardust.Paradox.Data
  with a GremlinContext-derived class. Use when the user wants to write integration tests,
  unit tests, or demo scenarios that substitute the real Gremlin connector
  (Cosmos DB / TinkerPop) with the in-memory provider from Stardust.Paradox.Data.InMemory.
  Covers: creating scenario providers, wiring InMemoryGremlinLanguageConnector into a
  GraphContextBase-derived context, populating vertices and edges, writing xUnit tests,
  using the scenario registry, and configuring custom responses.
license: MIT
compatibility: Requires Stardust.Paradox.Data.InMemory NuGet package. .NET Standard 2.0+ / .NET 6+.
metadata:
  author: stardust-paradox
  version: "1.0"
---

# Stardust.Paradox InMemory Scenario Skill

This skill teaches you how to create **in-memory graph database test scenarios**
for any project that references `Stardust.Paradox.Data` and uses a class derived
from `GraphContextBase` (or uses `GremlinContext` directly).

The `Stardust.Paradox.Data.InMemory` package provides `InMemoryGremlinLanguageConnector`
— a drop-in replacement for real Gremlin connectors — so integration tests run
without a live database.

---

## When to Use

- The solution has a `GraphContextBase` subclass (the ORM context for graph entities).
- The user wants to write tests that exercise Gremlin traversals without Cosmos DB / TinkerPop.
- The user wants repeatable, isolated test data (scenarios).
- The user wants to verify entity CRUD, edge traversals, or custom Gremlin queries.

## Prerequisites

1. A project referencing **`Stardust.Paradox.Data`** (contains `GraphContextBase`, `IGraphSet<T>`, entity interfaces).
2. Add NuGet reference to **`Stardust.Paradox.Data.InMemory`** in the test project.
3. A test framework — typically **xUnit** with **FluentAssertions** (matches the existing repo conventions).

---

## Core Concepts

### 1. InMemoryGremlinLanguageConnector

The central class. It implements `IGremlinLanguageConnector` and keeps all
vertices/edges in memory. Three ways to create one:

```csharp
// Empty connector
var connector = InMemoryGremlinLanguageConnector.Create();

// With options
var connector = InMemoryGremlinLanguageConnector.Create(options =>
{
    options.EnableQueryLogging = true;
    options.EnableDebugLogging = true;
});

// With a pre-built scenario
var connector = InMemoryConnectorFactory.CreateWithScenario("BasicSocialNetwork");
```

### 2. Plugging InMemory into a GraphContextBase Subclass

Every `GraphContextBase` subclass accepts an `IGremlinLanguageConnector` in its
constructor. To test, pass the in-memory connector:

```csharp
var connector = InMemoryGremlinLanguageConnector.Create();
var services = new ServiceCollection();
services.AddEntityBinding((entity, impl) => services.AddTransient(entity, impl));
var sp = services.BuildServiceProvider();

var context = new MyGraphContext(connector, sp);
```

> **Key point:** `AddEntityBinding` registers the code-generated implementations
> for each entity interface (e.g., `IPerson`). Without it, `GraphSet<T>` cannot
> resolve entities.

### 3. Scenario Providers

A scenario is a reusable data-setup class. Inherit from
`InMemoryScenarioProviderBase` and override `GetScenarioData()`:

```csharp
public class MyTestScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "MyTest";
    public override string Description => "Scenario for testing X";

    protected override (ScenarioVertexDefinition[] vertices,
                        ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new ScenarioVertexDefinition("v1", "person", Props(
                ("name", "Alice"), ("age", 30)
            )),
            new ScenarioVertexDefinition("v2", "person", Props(
                ("name", "Bob"), ("age", 25)
            ))
        };

        var edges = new ScenarioEdgeDefinition[]
        {
            new ScenarioEdgeDefinition("knows", "v1", "v2")
        };

        return (vertices, edges);
    }
}
```

### 4. Loading Scenarios

```csharp
// By type (no registry needed)
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario<MyTestScenario>();

// By name (requires registration)
InMemoryScenarioRegistry.Register(new MyTestScenario());
var connector = InMemoryScenarioExtensions.CreateWithScenario("MyTest");

// Via factory
var connector = InMemoryConnectorFactory.CreateWithScenario("MyTest");

// Multiple scenarios
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario<ScenarioA>()
    .WithScenario<ScenarioB>();
```

### 5. Direct Database Population (Without Scenarios)

```csharp
var connector = InMemoryGremlinLanguageConnector.Create();

// Direct API
var v = connector.Database.AddVertex("person", "alice");
v.SetProperty("name", "Alice");
var v2 = connector.Database.AddVertex("person", "bob");
connector.Database.AddEdge("knows", "alice", "bob");

// Or via Gremlin strings
await connector.ExecuteAsync(
    "g.addV('person').property('id','alice').property('name','Alice')",
    new Dictionary<string, object>());
```

### 6. Custom Response Registration

For queries the in-memory parser cannot execute natively, register a regex pattern:

```csharp
connector.RegisterCustomResponse(
    @"g\.V\(\)\.hasLabel\('person'\)\.has\('verified', true\)",
    (query, parameters) =>
    {
        var alice = connector.Database.GetVertex("alice")?.ToGremlinResponse();
        return new[] { alice }.Where(x => x != null);
    });
```

### 7. Writing the xUnit Test

```csharp
public class PersonTraversalTests
{
    [Fact]
    public async Task Friends_ShouldReturnConnectedPeople()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create()
            .WithScenario<MyTestScenario>();

        // Act
        var result = await connector.ExecuteAsync(
            "g.V('v1').out('knows')",
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().id).Should().Be("v2");
    }
}
```

### 8. Testing Through a Full GraphContextBase Subclass

```csharp
public class MyContextTests
{
    [Fact]
    public async Task GetProfile_ShouldReturnEntity()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        connector.Database.AddVertex("person", "p1");
        connector.Database.GetVertex("p1").SetProperty("name", "Alice");

        var services = new ServiceCollection();
        services.AddEntityBinding((e, i) => services.AddTransient(e, i));
        var sp = services.BuildServiceProvider();

        using var ctx = new MyGraphContext(connector, sp);

        // Act
        var profile = await ctx.VAsync<IPerson>("p1");

        // Assert
        profile.Should().NotBeNull();
    }
}
```

---

## Step-by-Step: Creating a Scenario for an Existing GremlinContext Subclass

1. **Identify the entity interfaces** used in the `GraphContextBase.InitializeModel`
   (e.g., `IProfile`, `ICompany`). Note vertex labels (`[VertexLabel("person")]`)
   and edge labels configured via `configuration.ConfigureCollection<T>()`.

2. **Create a scenario class** inheriting `InMemoryScenarioProviderBase`.
   - Use `ScenarioVertexDefinition` with labels matching `[VertexLabel]`.
   - Use `ScenarioEdgeDefinition` with labels matching edge configuration.
   - Use the `Props(...)` helper for properties.

3. **Create the test class**:
   - Instantiate `InMemoryGremlinLanguageConnector`.
   - Apply the scenario with `.WithScenario<T>()`.
   - Construct the `GraphContextBase` subclass passing the connector.
   - Call context methods (`VAsync`, `GraphSet`, etc.) and assert results.

4. **For custom Gremlin queries** that the parser may not handle, register
   custom responses with `RegisterCustomResponse`.

5. **Run tests** with `dotnet test` — no database required.

---

## Configuration Options

`InMemoryDatabaseOptions` controls connector behavior:

| Option | Default | Purpose |
|--------|---------|---------|
| `EnableQueryLogging` | `false` | Log queries to console |
| `EnableDebugLogging` | `false` | Verbose debug output |
| `SimulatedRUPerQuery` | `1.0` | Simulated RU per query |
| `QueryTimeout` | `30s` | Query execution timeout |
| `AutoGenerateIds` | `true` | Auto-generate IDs |
| `ValidateEdgeVertices` | `true` | Validate vertex existence when creating edges |
| `CaseSensitiveProperties` | `false` | Property name matching |
| `CaseSensitiveLabels` | `false` | Label matching |
| `CascadeDeleteEdges` | `true` | Delete edges when vertex is removed |

---

## Common Pitfalls

- **Missing `AddEntityBinding`**: Without it, `GraphSet<T>` throws because
  the code-generated entity type is not registered in DI.
- **Label mismatch**: Vertex labels in the scenario must match `[VertexLabel]`
  attributes exactly. Edge labels must match `[EdgeLabel]` or the fluent
  configuration string.
- **Partition key**: If the real context sets `PartitionKeyName`, set the same
  property on scenario vertices (e.g., `("pk", someValue)`).
- **Double initialization**: `GraphContextBase.InitializeModel` uses a static
  lock and caches. In tests, guard with a `_modelInitialized` flag or reset
  between test classes.
- **Custom responses vs. parser**: The TinkerGraph parser handles most standard
  Gremlin. Only register custom responses for queries that fail at runtime.

---

## Available Built-In Scenarios

| Name | Description |
|------|-------------|
| `BasicSocialNetwork` | Users, friendships, posts |
| `SimpleECommerce` | Products, customers, orders |
| `OrganizationHierarchy` | Employees, departments, reports-to |
| `UserRoleManagement` | Users, roles, permissions |
| `GraphTraversalTest` | General traversal testing |
| `SocialNetworkTest` | Social network with admin groups |
| `LibraryManagement` | Books, authors, borrowers |
| `ODataTest` | OData-style filtering scenarios |

Use `InMemoryScenarioExtensions.ListAvailableScenarios()` to discover all at runtime.

---

## Reference Files

For full API details, use `read_skill_resource` to load:
- `references/API_REFERENCE.md` — Complete API surface for all InMemory classes
- `references/FULL_EXAMPLES.md` — End-to-end code examples with GraphContextBase
