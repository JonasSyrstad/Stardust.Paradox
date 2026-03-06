---
name: stardust-graphcontext-setup
description: >
  Set up a GraphContextBase-derived context for Stardust.Paradox.Data projects.
  Use when the user needs to define entity interfaces, configure edge/vertex
  relationships via fluent configuration in InitializeModel, wire up dependency
  injection with AddEntityBinding, and ensure thread-safe model initialization
  in test scenarios. Covers: IVertex/IEdge interfaces, VertexLabel/EdgeLabel
  attributes, fluent In/Out edge configuration, custom Gremlin queries on
  properties, partition key setup, and the critical _modelInitialized guard
  pattern for safe re-entrant initialization.
license: MIT
compatibility: Requires Stardust.Paradox.Data NuGet package. .NET Standard 2.0+ / .NET 6+.
metadata:
  author: stardust-paradox
  version: "1.0"
---

# Stardust.Paradox GraphContext Setup Skill

This skill teaches you how to create a **`GraphContextBase`-derived context**
with fluent entity configuration for any project using `Stardust.Paradox.Data`.

`GraphContextBase` is the central ORM context — analogous to EF Core's
`DbContext` — that maps interface-based entity definitions to Gremlin graph
traversals via runtime code generation.

---

## When to Use

- The user needs to create a new graph context for a Stardust.Paradox project.
- The user needs to define vertex/edge entity interfaces.
- The user needs to configure edge relationships using the fluent API in `InitializeModel`.
- The user is setting up test contexts that may be instantiated multiple times.
- The user asks about partition keys, custom Gremlin queries on properties, or
  inline serialization.

## Prerequisites

1. NuGet reference to **`Stardust.Paradox.Data`**.
2. NuGet reference to **`Stardust.Paradox.Data.Annotations`** (usually pulled transitively).
3. `Microsoft.Extensions.DependencyInjection` for `IServiceProvider` / `AddEntityBinding`.

---

## Core Concepts

### 1. Entity Interface Definitions

Entities are **interfaces**, not classes. The framework generates implementations
at runtime.

#### Vertex entities

```csharp
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }           // Required — maps to the vertex id
    string Name { get; set; }
    int Age { get; set; }
    string Pk { get; set; }      // Partition key property (if used)
}
```

#### Edge entities (typed edges with properties)

```csharp
[EdgeLabel("employer")]
public interface IEmployment : IEdge<IPerson, ICompany>
{
    string Id { get; }
    DateTime HiredDate { get; set; }
    string Manager { get; set; }
}
```

#### Navigation properties on vertices

| Property Type | Direction | Cardinality |
|--------------|-----------|-------------|
| `IEdgeCollection<T>` | In or Out | Many |
| `IEdgeReference<T>` | In or Out | One |
| `ICollection<T>` | Eager-loaded | Many (loads immediately) |

### 2. Creating the GraphContext Subclass

```csharp
public class MyGraphContext : GraphContextBase
{
    public MyGraphContext(IGremlinLanguageConnector connector, IServiceProvider sp)
        : base(connector, sp) { }

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        configuration.ConfigureCollection<IPerson>();
        configuration.ConfigureCollection<ICompany>();
        return true;
    }

    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
}
```

### 3. Fluent Edge Configuration in InitializeModel

The fluent API configures how navigation properties map to Gremlin edge
traversals. This is where the **real power** lives.

#### Pattern: In/Out pair (preferred modern API)

```csharp
configuration.ConfigureCollection<IPerson>()
    .In(p => p.Parents, "parent")    // IPerson.Parents navigates IN on "parent" edge
    .Out(p => p.Children);           // The reverse: IProfile.Children navigates OUT
```

**How this reads:** "For `IPerson`, the `Parents` property traverses **incoming**
`parent` edges. The reverse side — `Children` on the target type — traverses
**outgoing** `parent` edges."

#### Pattern: Out/In pair

```csharp
configuration.ConfigureCollection<ICompany>()
    .Out(c => c.Employees, "employer")  // ICompany.Employees navigates OUT on "employer"
    .In(p => p.Employers);              // IPerson.Employers navigates IN on "employer"
```

#### Pattern: Custom Gremlin query on a property

```csharp
configuration.ConfigureCollection<IPerson>()
    .AddQuery(p => p.AllSiblings,
        g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());
```

Or with a raw string:

```csharp
// On the interface directly:
[GremlinQuery("g.V('{id}').as('s').in('parent').out('parent').where(without('s')).dedup()")]
IEdgeCollection<IPerson> Siblings { get; }
```

#### Pattern: Edge entity (typed edge with properties)

```csharp
configuration.ConfigureCollection<IEmployment>();
// No further config needed — the [EdgeLabel] attribute + IEdge<TIn,TOut> provides everything
```

#### Chaining multiple entities

```csharp
protected override bool InitializeModel(IGraphConfiguration configuration)
{
    configuration.ConfigureCollection<IPerson>()
        .In(p => p.Parents, "parent").Out(p => p.Children)
        .AddQuery(p => p.AllSiblings,
            g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());

    configuration.ConfigureCollection<ICompany>()
        .Out(c => c.Employees, "employer").In(p => p.Employers);

    configuration.ConfigureCollection<IEmployment>();

    return true;
}
```

### 4. ?? Thread-Safe Model Initialization (CRITICAL for Tests)

`GraphContextBase` uses an internal static `ConcurrentDictionary` keyed by
context type name to track whether `InitializeModel` has been called. However,
the fluent configuration registers bindings in static dictionaries inside the
code generator. If `InitializeModel` runs twice with the same bindings, it
throws `ArgumentOutOfRangeException` ("binding is already added").

**In test projects where you create multiple context instances across test
classes, you MUST guard `InitializeModel` with your own lock and flag:**

```csharp
public class TestContext : GraphContextBase
{
    // ?? These MUST be static — shared across all instances
    private static bool _modelInitialized = false;
    private static readonly object _lockObject = new object();

    public TestContext(IGremlinLanguageConnector connector)
        : base(connector, CreateServiceProvider()) { }

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        lock (_lockObject)
        {
            if (_modelInitialized)
                return false; // Skip — already configured in this AppDomain

            try
            {
                configuration.ConfigureCollection<IPerson>()
                    .In(p => p.Parents, "parent").Out(p => p.Children);
                configuration.ConfigureCollection<ICompany>()
                    .Out(c => c.Employees, "employer").In(p => p.Employers);
                configuration.ConfigureCollection<IEmployment>();

                _modelInitialized = true;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Binding already registered by another instance — safe to skip
                _modelInitialized = true;
                return false;
            }
        }
    }
}
```

**Why this matters:**
- xUnit creates a new test class instance per test method.
- Each instantiation creates a new `TestContext`, which calls `InitializeModel`.
- Without the guard, the second test throws because edge bindings already exist
  in the static code generator dictionaries.
- The `lock` + `_modelInitialized` flag ensures exactly-once initialization
  regardless of test execution order or parallelism.
- The `catch (ArgumentOutOfRangeException)` is a safety net for race conditions
  where `GraphContextBase`'s own initialization state gets out of sync.

### 5. Dependency Injection Setup

`GraphContextBase` requires an `IServiceProvider` that has entity bindings
registered. The `AddEntityBinding` call hooks into the code generator so that
when `BuildModel()` runs, each generated entity implementation type is
registered in DI:

```csharp
private static IServiceProvider CreateServiceProvider()
{
    var services = new ServiceCollection();

    // This MUST be called before constructing the context
    services.AddEntityBinding((entity, implementation) =>
    {
        services.AddTransient(entity, implementation);
    });

    return services.BuildServiceProvider();
}
```

For production ASP.NET Core apps, use the convenience method:

```csharp
services.AddParadox<MyGraphContext>(sp =>
    new GremlinNetLanguageConnector(host, database, collection, key));
```

### 6. Partition Key Configuration

Set the partition key property name in a static constructor:

```csharp
public class MyContext : GraphContextBase
{
    static MyContext()
    {
        PartitionKeyName = "pk";  // Must match the property name on your entities
    }
    // ...
}
```

When `PartitionKeyName` is set, operations like `VAsync<T>(id, partitionKey)`
become available and the partition key is included in vertex creation.

### 7. Exposing GraphSets

```csharp
// Vertex sets
public IGraphSet<IPerson> People => GraphSet<IPerson>();
public IGraphSet<ICompany> Companies => GraphSet<ICompany>();

// Edge sets (for typed edge entities)
public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
```

`IGraphSet<T>` provides:
- `GetAsync(id)` / `GetAsync(id, partitionKey)` — load by ID
- `AllAsync()` / `AllAsync(page, pageSize)` — list all
- `FilterAsync(property, value)` — filter by property
- `DeleteAsync(id)` — delete

---

## Attribute Quick Reference

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[VertexLabel("label")]` | Interface | Sets the Gremlin vertex label |
| `[EdgeLabel("label")]` | Interface/Property | Sets the edge label (obsolete — prefer `[InLabel]`) |
| `[InLabel("label")]` | Property | Incoming edge label on a navigation property |
| `[OutLabel("label")]` | Property | Outgoing edge label on a navigation property |
| `[ToWayEdgeLabel("label")]` | Property | Bidirectional edge (traverses both directions) |
| `[GremlinQuery("g.V(...)")]` | Property | Custom Gremlin traversal for a navigation property |
| `[Eager]` | Property | Load the collection eagerly (use with care) |
| `[InlineSerialization(type)]` | Property | Serialize collection inline as a property value |

---

## Common Pitfalls

- **Missing `AddEntityBinding`**: Context construction fails because the code
  generator cannot register entity types.
- **Double initialization in tests**: Always use the `_modelInitialized` +
  `lock` guard. Without it, the second test class instance throws
  `ArgumentOutOfRangeException`.
- **Edge direction confusion**: `In` = edges pointing **into** the current
  vertex. `Out` = edges pointing **out from** the current vertex. Think of it
  from the vertex's perspective.
- **Label mismatch**: The string in `.In(p => p.Parents, "parent")` must match
  the edge label in the database exactly. If you also use `[InLabel("parent")]`
  on the property, the fluent config takes precedence.
- **Partition key not set**: If your database uses partition keys, always set
  `PartitionKeyName` in the static constructor and include the `pk` property on
  entities.
- **`SeedAsync()` deadlock**: The base class calls `SeedAsync().Wait()` in the
  constructor. This is safe in .NET Core but can deadlock in .NET Framework.

---

## Reference Files

For full API details, use `read_skill_resource` to load:
- `references/API_REFERENCE.md` — Complete interface definitions and attribute reference
- `references/FULL_EXAMPLES.md` — End-to-end code examples with various patterns
