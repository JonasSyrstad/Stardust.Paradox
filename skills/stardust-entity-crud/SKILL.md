---
name: stardust-entity-crud
description: >
  Perform CRUD operations on graph entities (vertices and edges) using
  Stardust.Paradox. Use when the user needs to create, read, update, or delete
  vertices/edges, navigate edge collections, manage entity tracking, persist
  changes with SaveChangesAsync, load related entities, or set up provider
  connections (CosmosDB, TinkerPop). Covers: IGraphSet CRUD, IEdgeCollection
  edge navigation, CreateEntity, VAsync, SaveChangesAsync, Delete, Attach,
  GetOrCreate, change tracking, partition keys, provider connectors
  (GremlinNetLanguageConnector for CosmosDB / TinkerPop), and the IGraphContext
  API. This is the primary skill for data access patterns in Stardust.Paradox.
license: MIT
compatibility: Stardust.Paradox.Data / Stardust.Paradox.Data.Providers.Gremlin. .NET Standard 2.0+.
metadata:
  author: stardust-paradox
  version: "1.0"
---

# Stardust.Paradox Entity CRUD & Data Access

This skill covers creating, reading, updating, and deleting graph entities
(vertices and edges) through the `GraphContextBase` API, navigating edge
collections, persisting changes, and configuring provider connections.

---

## When to Use

- The user needs to create, read, update, or delete vertices/edges.
- The user is working with `IGraphSet<T>` or `IEdgeGraphSet<T>` properties.
- The user needs to navigate edge collections (`IEdgeCollection<T>`).
- The user needs to call `SaveChangesAsync` to persist changes.
- The user is setting up a `GremlinNetLanguageConnector` for CosmosDB or
  TinkerPop.
- The user needs entity tracking, partition keys, or eager/lazy loading.

## Prerequisites

- A `GraphContextBase` subclass (see `stardust-graphcontext-setup` skill).
- Entity interfaces with `[VertexLabel]` and edge labels configured.
- A connector: `GremlinNetLanguageConnector` (production) or
  `InMemoryGremlinLanguageConnector` (testing).

---

## Core Concepts

### 1. IGraphSet — Repository-Style Access

Each `IGraphSet<T>` property on your context provides CRUD operations:

```csharp
public class MyContext : GraphContextBase
{
    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
}
```

#### Create

```csharp
// Create with auto-generated ID
var person = context.People.Create();

// Create with specific ID
var person = context.People.Create("person-123");

// Create with ID + initializer
var person = context.People.Create("person-123", p =>
{
    p.Name = "Alice Johnson";
    p.Age = 30;
    p.Email = "alice@example.com";
    return p;
});

// Create with partition key
var person = context.CreateEntity<IPerson>("person-123", "partition-1");
```

#### Read

```csharp
// Get by ID
var person = await context.People.GetAsync("person-123");

// Get by ID + partition key (CosmosDB)
var person = await context.People.GetAsync("person-123", "partition-1");

// Get by ID + partition key tuple
var person = await context.People.GetAsync(("person-123", "partition-1"));

// Get all
var allPeople = await context.People.AllAsync();

// Get all with paging
var page2 = await context.People.AllAsync(page: 1, pageSize: 20);

// Filter by property
var alices = await context.People.FilterAsync(p => p.Name, "Alice");

// Filter with paging
var result = await context.People.FilterAsync(p => p.City, "Seattle", page: 0, pageSize: 10);
```

#### Read with Custom Gremlin Query

```csharp
// Execute a custom Gremlin query and materialize as entities
var seniors = await context.VAsync<IPerson>(
    g => g.V().HasLabel("person").Has("age", p => p.Gt(30)));

// Execute directly and get raw results
var results = await context.ExecuteAsync<IPerson>(
    g => g.V().HasLabel("person").Count());
```

#### Update

Just modify properties and call `SaveChangesAsync`:

```csharp
var person = await context.People.GetAsync("person-123");
person.Name = "Alice Smith";  // changed
person.Age = 31;              // changed
await context.SaveChangesAsync();
```

#### Delete

```csharp
// Delete by entity
var person = await context.People.GetAsync("person-123");
context.Delete(person);
await context.SaveChangesAsync();

// Delete by ID (loads then deletes)
await context.People.DeleteAsync("person-123");
await context.SaveChangesAsync();

// Delete by ID + partition key
await context.People.DeleteAsync("person-123", "partition-1");
await context.SaveChangesAsync();
```

#### GetOrCreate

```csharp
// Returns existing entity or creates a new one
var person = await context.GetOrCreate<IPerson>("person-123");
person.Name = "Alice";
await context.SaveChangesAsync();

// With partition key
var person = await context.GetOrCreate<IPerson>("person-123", "partition-1");
```

### 2. Edge Navigation (IEdgeCollection)

Edge collections are lazy-loaded and provide navigation between vertices:

```csharp
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Name { get; set; }

    [OutLabel("worksAt")]
    IEdgeCollection<ICompany> Companies { get; }

    [OutLabel("friendsWith")]
    IEdgeCollection<IPerson> Friends { get; }
}
```

#### Loading Edges

```csharp
var person = await context.People.GetAsync("person-123");

// Explicit load (required before accessing)
await person.Companies.LoadAsync();

// Load with filtering
await person.Friends.LoadAsync(
    filterSelector: f => f.City,
    value: "Seattle",
    orderSelector: f => f.Name,
    isDesc: false,
    pageNumber: 0,
    pageSize: 20);

// Convert to vertices list
var companies = await person.Companies.ToVerticesAsync();

// Convert with filtering/paging
var friends = await person.Friends.ToVerticesAsync(
    filterSelector: f => f.IsActive,
    value: true,
    orderSelector: f => f.Name);

// Get as edge objects (includes edge properties)
var edges = await person.Companies.ToEdgesAsync();
```

#### Adding Edges

```csharp
var person = await context.People.GetAsync("person-123");
var company = await context.Companies.GetAsync("company-456");

// Add edge (creates edge on SaveChangesAsync)
person.Companies.Add(company);

// Add edge with properties
person.Companies.Add(company, new Dictionary<string, object>
{
    ["role"] = "Developer",
    ["startDate"] = "2024-01-15",
    ["salary"] = 120000
});

// Add bidirectional edge
person.Friends.AddDual(otherPerson);

await context.SaveChangesAsync();
```

#### Removing Edges

```csharp
var person = await context.People.GetAsync("person-123");
await person.Companies.LoadAsync();

var company = (await person.Companies.ToVerticesAsync()).First();
person.Companies.Remove(company);

await context.SaveChangesAsync();
```

### 3. SaveChangesAsync — Persisting All Changes

`SaveChangesAsync` is the Unit of Work commit. It:
1. Saves new/modified/deleted **vertices** first.
2. Saves new/modified/deleted **edges** second.
3. Processes edge collection changes.
4. Resets dirty tracking on all entities.

```csharp
// Create and modify multiple entities
var alice = context.People.Create("alice", p => { p.Name = "Alice"; return p; });
var bob = context.People.Create("bob", p => { p.Name = "Bob"; return p; });
alice.Friends.Add(bob);

// One call persists everything
await context.SaveChangesAsync();
```

#### Events

```csharp
context.SavingChanges += (sender, args) =>
{
    Console.WriteLine($"Saving {args.TrackedItems.Count()} entities...");
};

context.ChangesSaved += (sender, args) =>
{
    Console.WriteLine("Changes saved successfully.");
};

context.SaveChangesError += (sender, args) =>
{
    Console.WriteLine($"Save failed: {args.Error.Message}");
    Console.WriteLine($"Failed statement: {args.FailedUpdateStatement}");
};
```

#### Parallel Execution

For high-throughput writes:

```csharp
// Enable parallel saves (default is sequential)
GremlinContext.ParallelSaveExecution = true;
```

### 4. Change Tracking

The context tracks entities automatically:

```csharp
// Entity is tracked after Get
var person = await context.People.GetAsync("id");  // tracked

// Entity is tracked after Create
var newPerson = context.People.Create("id");        // tracked

// Manually attach an entity
context.Attach(detachedPerson);

// Reset changes on an entity (undo modifications)
context.ResetChanges(person);

// Clear all tracked entities
context.Clear();
```

### 5. Partition Keys

For CosmosDB, set the partition key name in the context:

```csharp
public class MyContext : GraphContextBase
{
    static MyContext()
    {
        PartitionKeyName = "pk";  // or "/pk" depending on provider
    }
}

// Create entity with partition key
var person = context.CreateEntity<IPerson>("person-123", "my-partition");
```

### 6. Tree Traversals

Load hierarchical data as a tree:

```csharp
// Get a tree starting from rootId, following "parent" edges
var tree = await context.GetTreeAsync<IDepartment>("root-id", "parent");

// Using a property expression
var tree = await context.GetTreeAsync<IDepartment>(
    "root-id",
    d => d.Children,
    incommingEdge: false);
```

### 7. Edge Entities (Typed Edges)

Access edge properties through typed edge interfaces:

```csharp
// Get typed edge entities
var edge = await context.EAsync<IEmployment>("edge-id");
edge.Role = "Senior Developer";
edge.Salary = 150000;
await context.SaveChangesAsync();

// Query edges with custom Gremlin
var edges = await context.EAsync<IEmployment>(
    g => g.V("person-123").OutE("worksAt"));
```

### 8. Request Units (CosmosDB)

Track consumed RUs:

```csharp
var people = await context.People.AllAsync();
Console.WriteLine($"Consumed RUs: {context.ConsumedRU}");
```

---

## Provider Setup

### CosmosDB (Gremlin API)

```csharp
using Stardust.Paradox.Data.Providers.Gremlin;

// Package: Stardust.Paradox.Data.Providers.Gremlin
var connector = new GremlinNetLanguageConnector(
    gremlinHostname: "myaccount.gremlin.cosmosdb.azure.com",
    databaseName: "mydb",
    graphName: "mygraph",
    accessKey: "your-primary-key");

var context = new MyContext(connector, serviceProvider);
```

### Apache TinkerPop (Gremlin Server)

```csharp
var connector = new GremlinNetLanguageConnector(
    gremlinHostname: "localhost",
    username: "/dbs/mydb/colls/mygraph",
    password: "",
    port: 8182,
    enableSsl: false);

var context = new MyContext(connector, serviceProvider);
```

### Connection Pool Tuning

```csharp
// Configure before creating any connectors
GremlinNetLanguageConnector.ConnectionPoolSettings = new ConnectionPoolSettings
{
    MaxInProcessPerConnection = 32,
    PoolSize = 4,
    ReconnectionAttempts = 4,
    ReconnectionBaseDelay = TimeSpan.FromSeconds(1)
};
```

### InMemory (Testing)

```csharp
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Factory;

var database = new InMemoryGraphDatabase();
var connector = InMemoryConnectorFactory.Create(database);

// Required for LINQ queries
GremlinFactory.SetActivatorFactory(() => connector);

var context = new MyContext(connector, serviceProvider);
```

---

## Complete CRUD Example

```csharp
public class PersonService
{
    private readonly MyContext _context;

    public PersonService(MyContext context)
    {
        _context = context;
    }

    public async Task<IPerson> CreatePersonAsync(string name, string email)
    {
        var person = _context.People.Create(Guid.NewGuid().ToString(), p =>
        {
            p.Name = name;
            p.Email = email;
            p.IsActive = true;
            return p;
        });

        await _context.SaveChangesAsync();
        return person;
    }

    public async Task<IPerson> GetPersonAsync(string id)
    {
        return await _context.People.GetAsync(id);
    }

    public async Task<IEnumerable<IPerson>> GetActivePeopleAsync(int page)
    {
        return await _context.VAsync<IPerson>(
            g => g.V().HasLabel("person")
                  .Has("isActive", true)
                  .SkipTake(page * 20, 20));
    }

    public async Task UpdatePersonAsync(string id, string newName)
    {
        var person = await _context.People.GetAsync(id);
        person.Name = newName;
        await _context.SaveChangesAsync();
    }

    public async Task DeletePersonAsync(string id)
    {
        await _context.People.DeleteAsync(id);
        await _context.SaveChangesAsync();
    }

    public async Task AssignToCompanyAsync(string personId, string companyId, string role)
    {
        var person = await _context.People.GetAsync(personId);
        var company = await _context.Companies.GetAsync(companyId);

        person.Companies.Add(company, new Dictionary<string, object>
        {
            ["role"] = role,
            ["startDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
        });

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ICompany>> GetCompaniesForPersonAsync(string personId)
    {
        var person = await _context.People.GetAsync(personId);
        return await person.Companies.ToVerticesAsync();
    }
}
```

---

## Common Pitfalls

- **Forgetting `SaveChangesAsync`**: Property changes, creates, and deletes are
  only tracked in memory. Call `SaveChangesAsync` to commit to the database.
- **Edges not loaded**: Edge collections are lazy. Call `LoadAsync()` or
  `ToVerticesAsync()` before iterating. Without loading, the collection is empty.
- **Duplicate tracking**: Don't call `CreateEntity` with the same ID twice.
  Use `GetOrCreate` for upsert patterns.
- **Partition key required**: On CosmosDB, reads by ID often require the
  partition key. Use the overloads that accept `partitionKey`.
- **Context lifetime**: Dispose the context when done. Tracked entities are not
  valid after disposal.
- **Missing `GremlinFactory` setup**: When using LINQ queries alongside direct
  `VAsync` calls, ensure `GremlinFactory.SetActivatorFactory` is configured.
