# Full Examples — Stardust.Paradox.Data GraphContext Setup

## Example 1: Complete Test Context (from TestContext.cs)

This is the canonical example of a graph context safe for use in xUnit tests.

### Entity Interfaces

```csharp
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;

[VertexLabel("person")]
public interface IProfile : IVertex, IDynamicGraphEntity
{
    string Id { get; }
    string FirstName { get; set; }
    string LastName { get; set; }
    string Email { get; set; }
    bool VerifiedEmail { get; set; }
    string Name { get; set; }
    string Ocupation { get; set; }
    DateTime LastUpdated { get; set; }

    // Navigations configured fluently in InitializeModel
    IEdgeCollection<IProfile> Parents { get; }
    IEdgeCollection<IProfile> Children { get; }

    // Attribute-based navigation
    [ToWayEdgeLabel("spouce")]
    IEdgeReference<IProfile> Spouce { get; }

    // Eager-loaded (loads with parent vertex)
    [Eager]
    ICollection<ICompany> Employers { get; }

    // Custom Gremlin query via attribute
    [GremlinQuery("g.V('{id}').as('s').in('parent').out('parent').where(without('s')).dedup()")]
    IEdgeCollection<IProfile> Siblings { get; }

    // Custom Gremlin query configured fluently (no attribute needed)
    IEdgeCollection<IProfile> AllSiblings { get; set; }

    // Inline serialized collection (stored as vertex property)
    [InlineSerialization(SerializationType.ClearText)]
    ICollection<string> ProgramingLanguages { get; }

    string Pk { get; set; }
}

[VertexLabel("company")]
public interface ICompany : IVertex
{
    string Id { get; }
    string Name { get; set; }

    [OutLabel("division")]
    IEdgeReference<ICompany> Parent { get; }

    [InLabel("division")]
    IEdgeCollection<ICompany> Divisions { get; }

    // Configured fluently in InitializeModel
    IEdgeCollection<IProfile> Employees { get; }

    // Custom Gremlin queries via attributes
    [GremlinQuery("g.V('{id}').out('division').tail(1)")]
    IEdgeReference<ICompany> Group { get; }

    [GremlinQuery("g.V('{id}').emit().repeat(inE('division').outV()).out('employer')")]
    IEdgeCollection<IProfile> AllEmployees { get; }

    [InlineSerialization(SerializationType.Base64)]
    IInlineCollection<string> EmailDomains { get; }

    string Pk { get; set; }
}

// Typed edge entity — has its own properties
[EdgeLabel("employer")]
public interface IEmployment : IEdge<IProfile, ICompany>, IDynamicGraphEntity
{
    string Id { get; }
    DateTime HiredDate { get; set; }
    string Manager { get; set; }

    [InlineSerialization(SerializationType.ClearText)]
    ICollection<string> TestInline { get; set; }
}
```

### The GraphContext Subclass

```csharp
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Traversals;
using Microsoft.Extensions.DependencyInjection;

public class TestContext : GraphContextBase
{
    // ================================================================
    // ?? CRITICAL: Thread-safe initialization guard
    // ================================================================
    // These MUST be static. In test runners (xUnit, NUnit), each test
    // creates a new TestContext instance. Without this guard, the second
    // instance throws ArgumentOutOfRangeException because edge bindings
    // are registered in static code generator dictionaries.
    //
    // The lock ensures exactly-once initialization. The catch block is
    // a safety net for race conditions where the base class's own
    // ConcurrentDictionary<string,bool> state gets out of sync.
    // ================================================================
    private static bool _modelInitialized = false;
    private static readonly object _lockObject = new object();

    public TestContext(IGremlinLanguageConnector connector)
        : base(connector, CreateServiceProvider()) { }

    /// <summary>
    /// Creates and configures the service provider with entity bindings.
    /// AddEntityBinding hooks the code generator so that generated types
    /// are registered as transient services.
    /// </summary>
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddEntityBinding((entity, implementation) =>
        {
            services.AddTransient(entity, implementation);
        });

        return services.BuildServiceProvider();
    }

    // Partition key — set once in static constructor
    static TestContext()
    {
        PartitionKeyName = "pk";
    }

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        lock (_lockObject)
        {
            if (_modelInitialized)
                return false; // Already initialized — skip

            try
            {
                // IPerson edges:
                //   .In(Parents, "parent") = Parents navigates INCOMING "parent" edges
                //   .Out(Children)         = reverse side on the target type
                configuration.ConfigureCollection<IProfile>()
                    .In(t => t.Parents, "parent").Out(t => t.Children)
                    .AddQuery(t => t.AllSiblings,
                        g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());

                // ICompany edges:
                //   .Out(Employees, "employer") = Employees navigates OUTGOING "employer" edges
                //   .In(Employers)              = reverse: IProfile.Employers navigates IN
                configuration.ConfigureCollection<ICompany>()
                    .Out(t => t.Employees, "employer").In(t => t.Employers);

                // Typed edge entity — label comes from [EdgeLabel("employer")]
                configuration.ConfigureCollection<IEmployment>();

                _modelInitialized = true;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Binding already exists — another instance beat us to it
                _modelInitialized = true;
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Model initialization warning: {ex.Message}");
                _modelInitialized = true;
                return false;
            }
        }
    }

    // Vertex graph sets
    public IGraphSet<IProfile> Profiles => GraphSet<IProfile>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();

    // Edge graph set (typed edges)
    public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
}
```

---

## Example 2: Minimal Production Context

```csharp
[VertexLabel("user")]
public interface IUser : IVertex
{
    string Id { get; }
    string DisplayName { get; set; }
    string Email { get; set; }
}

[VertexLabel("role")]
public interface IRole : IVertex
{
    string Id { get; }
    string Name { get; set; }
    IEdgeCollection<IUser> Members { get; }
}

public class AppGraphContext : GraphContextBase
{
    public AppGraphContext(IGremlinLanguageConnector connector, IServiceProvider sp)
        : base(connector, sp) { }

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        configuration.ConfigureCollection<IUser>();
        configuration.ConfigureCollection<IRole>()
            .Out(r => r.Members, "member");

        return true;
    }

    public IGraphSet<IUser> Users => GraphSet<IUser>();
    public IGraphSet<IRole> Roles => GraphSet<IRole>();
}
```

### ASP.NET Core Registration

```csharp
// In Startup.cs / Program.cs
services.AddParadox<AppGraphContext>(sp =>
    new GremlinNetLanguageConnector(
        hostname,    // e.g. "mydb.gremlin.cosmos.azure.com"
        database,    // e.g. "graphdb"
        collection,  // e.g. "people"
        accessKey));
```

---

## Example 3: Using with InMemory Connector in Tests

```csharp
using Stardust.Paradox.Data.InMemory;

public class ProfileTests
{
    [Fact]
    public async Task CanCreateAndRetrieveProfile()
    {
        // Arrange — InMemory connector replaces Cosmos DB
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Seed data
        var v = connector.Database.AddVertex("person", "p1");
        v.SetProperty("firstName", "Alice");
        v.SetProperty("lastName", "Smith");
        v.SetProperty("pk", "partition1");

        using var ctx = new TestContext(connector);

        // Act
        var profile = await ctx.Profiles.GetAsync("p1");

        // Assert
        profile.Should().NotBeNull();
        profile.FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task CanCreateNewProfile()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        using var ctx = new TestContext(connector);

        // Act
        var profile = ctx.CreateEntity<IProfile>("p-new");
        profile.FirstName = "Bob";
        profile.LastName = "Jones";
        profile.Pk = "partition1";
        await ctx.SaveChangesAsync();

        // Assert — verify it was persisted
        var loaded = await ctx.Profiles.GetAsync("p-new");
        loaded.Should().NotBeNull();
        loaded.FirstName.Should().Be("Bob");
    }

    [Fact]
    public async Task CanFilterProfiles()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();

        connector.Database.AddVertex("person", "p1").SetProperty("email", "alice@test.com");
        connector.Database.AddVertex("person", "p2").SetProperty("email", "bob@test.com");

        using var ctx = new TestContext(connector);

        // Act
        var results = await ctx.Profiles.FilterAsync(p => p.Email, "alice@test.com");

        // Assert
        results.Should().ContainSingle();
    }
}
```

---

## Example 4: Complex Edge Configuration Patterns

### Bidirectional edges

```csharp
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }
    string Name { get; set; }

    // Bidirectional — traverses "friend" edges in both directions
    [ToWayEdgeLabel("friend")]
    IEdgeCollection<IPerson> Friends { get; }
}
```

### Self-referencing hierarchy with fluent config

```csharp
[VertexLabel("department")]
public interface IDepartment : IVertex
{
    string Id { get; }
    string Name { get; set; }
    IEdgeReference<IDepartment> Parent { get; }
    IEdgeCollection<IDepartment> SubDepartments { get; }
}

protected override bool InitializeModel(IGraphConfiguration configuration)
{
    configuration.ConfigureCollection<IDepartment>()
        .In(d => d.Parent, "reports_to").Out(d => d.SubDepartments);

    return true;
}
```

### Multiple edge types between the same vertex types

```csharp
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }

    IEdgeCollection<IPerson> ManagedBy { get; }      // "manages" IN
    IEdgeCollection<IPerson> DirectReports { get; }   // "manages" OUT
    IEdgeCollection<IPerson> MentorOf { get; }        // "mentors" OUT
    IEdgeCollection<IPerson> MentoredBy { get; }      // "mentors" IN
}

protected override bool InitializeModel(IGraphConfiguration configuration)
{
    configuration.ConfigureCollection<IPerson>()
        .In(p => p.ManagedBy, "manages").Out(p => p.DirectReports)
        .In(p => p.MentoredBy, "mentors").Out(p => p.MentorOf);

    return true;
}
```

---

## Example 5: Helper Methods on the Context

```csharp
public class TestContext : GraphContextBase
{
    // ... constructor, InitializeModel, etc.

    // Expose an event for disposal tracking in tests
    public Action<TestContext> OnDisposing { get; set; }

    protected override void Dispose(bool disposing)
    {
        OnDisposing?.Invoke(this);
        base.Dispose(disposing);
    }

    // Utility methods for partition key manipulation in tests
    public void NullPkName() => PartitionKeyName = null;
    public void ResetPkName() => PartitionKeyName = "pk";
}
```

---

## The _modelInitialized Guard — Why It Exists

### The Problem

```
Test1 creates TestContext  ?  InitializeModel runs  ?  bindings registered  ?
Test2 creates TestContext  ?  InitializeModel runs  ?  "binding already added" ??
```

The code generator stores fluent edge bindings in `static Dictionary<Type, Dictionary<MemberInfo, FluentConfig>>`.
Calling `.In(t => t.Parents, "parent").Out(t => t.Children)` a second time for
the same type throws `ArgumentOutOfRangeException`.

### The Solution

```csharp
private static bool _modelInitialized = false;          // ? static: shared across instances
private static readonly object _lockObject = new object(); // ? static: one lock for all

protected override bool InitializeModel(IGraphConfiguration configuration)
{
    lock (_lockObject)                    // 1. Serialize access
    {
        if (_modelInitialized)            // 2. Skip if already done
            return false;

        try
        {
            // ... configuration ...
            _modelInitialized = true;     // 3. Mark done
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            _modelInitialized = true;     // 4. Mark done even on conflict
            return false;
        }
    }
}
```

### Why not rely on `GraphContextBase`'s built-in check?

`GraphContextBase` has its own `Initialized` flag backed by
`ConcurrentDictionary<string, bool>`. However:

1. It checks **before** calling `InitializeModel`, not inside it.
2. In multi-threaded test runners, a timing window exists where two threads
   pass the base class check before either completes initialization.
3. The base class sets `Initialized = true` **after** `InitializeModel`
   returns, but the code generator's static dictionaries are populated
   **during** the call — creating a window for duplicates.

The user-space `lock` + `_modelInitialized` flag closes this gap completely.
