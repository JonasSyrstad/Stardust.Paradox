---
name: stardust-linq-gremlin
description: >
  Use Stardust.Paradox.Data.Linq to write C# LINQ queries that are automatically
  translated to Gremlin traversals. Use when the user wants type-safe, IntelliSense-
  friendly graph queries instead of raw Gremlin strings. Covers: AsQueryable on
  IGraphSet, standard LINQ operators (Where, Select, OrderBy, Skip, Take,
  GroupBy, aggregations), async operations (ToListAsync, FirstAsync, CountAsync,
  AnyAsync), graph traversals via LINQ (Out, In, OutE, InE, As/Select step labels),
  predicates (P.Gt, P.Lt, P.Within), debugging with ToGremlinQuery, and setting
  up the InMemory connector for LINQ tests.
license: MIT
compatibility: Requires Stardust.Paradox.Data.Linq NuGet package. .NET Standard 2.0 / .NET 8.
metadata:
  author: stardust-paradox
  version: "1.0"
---

# Stardust.Paradox LINQ-to-Gremlin Skill

This skill teaches you how to use **`Stardust.Paradox.Data.Linq`** to write
C# LINQ queries that are automatically translated to Gremlin traversals
and executed against the graph database through a `GraphContextBase` subclass.

Think of it as LINQ-to-SQL but for graph databases.

---

## When to Use

- The user has a `GraphContextBase` subclass and wants to query with LINQ instead
  of raw Gremlin strings.
- The user wants type-safe, compile-time-checked graph queries with IntelliSense.
- The user wants filtering, ordering, paging, projections, aggregations, or
  graph traversals expressed as C# lambda expressions.
- The user wants to write tests for LINQ-to-Gremlin queries using the InMemory
  connector.

## Prerequisites

1. A project referencing **`Stardust.Paradox.Data.Linq`**.
2. A `GraphContextBase` subclass with `IGraphSet<T>` / `IEdgeGraphSet<T>` properties.
3. Entity interfaces annotated with `[VertexLabel]`, `[EdgeLabel]`, etc.
4. For testing: `Stardust.Paradox.Data.InMemory` + `GremlinFactory.SetActivatorFactory`.

---

## Core Concepts

### 1. Creating a Queryable from a GraphSet

The entry point is `AsQueryable()` on any `IGraphSet<T>` or `IEdgeGraphSet<T>`:

```csharp
// From a vertex graph set
IQueryable<IPerson> people = context.People.AsQueryable();

// From an edge graph set
IQueryable<IEmployment> employments = context.Employments.AsQueryable();
```

This returns an `IQueryable<T>` backed by a `GremlinQueryProvider` that
translates LINQ expressions into Gremlin traversal strings.

### 2. Standard LINQ Operators

All standard LINQ operators are translated to Gremlin server-side:

#### Filtering (Where)

```csharp
// Simple equality
var alice = people.Where(p => p.Name == "Alice Johnson").ToList();

// Comparison operators
var seniors = people.Where(p => p.Age > 30).ToList();

// Boolean properties
var active = people.Where(p => p.IsActive).ToList();

// Combined conditions (AND)
var activeAdults = people.Where(p => p.Age > 25 && p.IsActive).ToList();

// String operations
var gmails = people.Where(p => p.Email.Contains("@gmail.com")).ToList();
var aNames = people.Where(p => p.Name.StartsWith("A")).ToList();
var dotCom = people.Where(p => p.Email.EndsWith(".com")).ToList();
```

#### Projections (Select)

```csharp
// Single property
var names = people.Select(p => p.Name).ToList();           // List<string>
var ages = people.Select(p => p.Age).ToList();              // List<int>

// Anonymous type
var dtos = people.Select(p => new { p.Name, p.Age, p.Email }).ToList();
```

#### Ordering

```csharp
var byAge = people.OrderBy(p => p.Age).ToList();
var byAgeDesc = people.OrderByDescending(p => p.Age).ToList();

// Multi-column sort
var sorted = people
    .OrderBy(p => p.City)
    .ThenBy(p => p.Age)
    .ToList();
```

#### Paging (Skip / Take)

```csharp
var page2 = people
    .OrderBy(p => p.Name)
    .Skip(10)
    .Take(5)
    .ToList();
```

#### Aggregations

```csharp
var count = people.Count();
var filteredCount = people.Where(p => p.IsActive).Count();
var totalAge = people.Sum(p => p.Age);
var avgAge = people.Average(p => p.Age);
var minAge = people.Min(p => p.Age);
var maxAge = people.Max(p => p.Age);
var anyActive = people.Any(p => p.IsActive);
```

#### Distinct

```csharp
var uniqueCities = people.Select(p => p.City).Distinct().ToList();
```

#### GroupBy

```csharp
var byCity = people
    .GroupBy(p => p.City)
    .ToList();

var cityCounts = people
    .GroupBy(p => p.City)
    .Select(g => new { City = g.Key, Count = g.Count() })
    .ToList();
```

### 3. Async Operations

All terminal operations have async variants:

```csharp
var list = await people.ToListAsync();
var first = await people.FirstAsync();
var firstOrNull = await people.FirstOrDefaultAsync();
var count = await people.CountAsync();
var exists = await people.AnyAsync();
```

These can be combined with any preceding LINQ chain:

```csharp
var result = await people
    .Where(p => p.Age >= 25)
    .OrderByDescending(p => p.Score)
    .Skip(1)
    .Take(2)
    .ToListAsync();
```

### 4. Graph Traversals (Out / In / OutE / InE)

LINQ-to-Gremlin extends standard LINQ with graph-specific operators.

#### Vertex-to-Vertex traversals

```csharp
// Traverse OUTGOING edges using a lambda
var companies = people
    .Out(p => p.Companies)    // person ? worksAt ? company
    .ToList();

// Traverse INCOMING edges
var employees = companies
    .In(c => c.Employees)     // company ? worksAt ? person
    .ToList();
```

#### Edge traversals (OutE / InE)

```csharp
// Get edge entities (typed)
var employments = people
    .OutE<IPerson, IUserSkill>(p => p.Skills)
    .ToList();

// Shorthand — infer edge type from label
var userSkills = people.OutE<IUserSkill>().ToList();
var skillEdges = skills.InE<IUserSkill>().ToList();
```

#### Edge-to-Vertex traversals (InV / OutV / OtherV)

```csharp
// Start from Skills, get incoming hasSkill edges, then traverse to the people
var people = Context.Skills.AsQueryable()
    .Where(s => s.Name == "C#")
    .InE<IUserSkill>()
    .InV<IUserSkill, IPerson>()
    .ToList();
```

#### Vertex-level edge traversal (on single entity in Select)

```csharp
// Get edges from each vertex in a projection
var edges = (from p in Context.People.AsQueryable()
             select p.OutE(person => person.Skills)
                     .Cast<IUserSkill>()
            ).ToListAsync();
```

### 5. Step Labels (As / Select)

Label a traversal step and select back to it later:

```csharp
// Label step "a", traverse out, filter, then select back to the labeled step
var people = Context.People.AsQueryable()
    .As("a")
    .Out(p => p.Skills)
    .Has(s => s.Name == "C#")
    .Select<IPerson>("a")
    .ToList();
```

This translates to: `g.V().hasLabel('person').as('a').out('hasSkill').has('name','C#').select('a')`

### 6. Gremlin Predicates (P class)

For `GraphTraversal<T>` queries, use the `P` class for Gremlin predicates:

```csharp
traversal.Has("age", P.Gt(30))
traversal.Has("age", P.Between(20, 40))
traversal.Has("name", P.Within("Alice", "Bob", "Charlie"))
traversal.Has("status", P.Neq("deleted"))
```

Available predicates: `P.Gt`, `P.Lt`, `P.Gte`, `P.Lte`, `P.Eq`, `P.Neq`,
`P.Within`, `P.Without`, `P.Between`, `P.Inside`, `P.Outside`.

### 7. LINQ Query Syntax (from … select)

LINQ query comprehension syntax works too:

```csharp
// Simple query
var people = await (from p in Context.People.AsQueryable()
                    select p).ToListAsync();

// With filter and projection
var result = await (from p in Context.People.AsQueryable()
                    where p.Age > 20 && p.Age < 30
                    select new { p.Age, p.Name }).ToListAsync();

// With graph traversal
var skills = await (from p in Context.People.AsQueryable()
                        .As("a")
                        .Out(t => t.Skills)
                        .Has(t => t.Name == "C#")
                        .Select<IPerson>("a")
                    select p).ToListAsync();
```

### 8. Debugging: ToGremlinQuery()

Cast to `GraphQueryable<T>` to see the generated Gremlin:

```csharp
var queryable = Context.People.AsQueryable()
    .Where(p => p.Age > 30)
    .OrderBy(p => p.Name);

// Inspect the generated Gremlin query
var gremlin = ((GraphQueryable<IPerson>)queryable).ToGremlinQuery();
// ? "g.V().hasLabel('person').has('age', gt(30)).order().by('name', asc)"

// Or just call ToString() on the queryable
Console.WriteLine(queryable.ToString());
```

### 9. Has() Extension (Gremlin-style filter)

In addition to `Where()`, you can use `Has()` for Gremlin-native filtering:

```csharp
// On IQueryable
var filtered = people.Has(p => p.IsActive && p.Age > 25).ToList();

// On GraphTraversal (fluent builder)
traversal.Has("name", "Alice")
         .Has("age", P.Gt(25))
         .HasLabel("person")
         .HasId("user1", "user2");
```

---

## Setting Up LINQ Tests with InMemory

### Test Infrastructure

```csharp
public abstract class LinqTestBase : IDisposable
{
    protected InMemoryGremlinLanguageConnector Connector { get; private set; } = null!;
    protected InMemoryGraphDatabase Database { get; private set; } = null!;
    protected LinqTestContext Context { get; private set; } = null!;

    protected LinqTestBase()
    {
        // Create in-memory database
        Database = new InMemoryGraphDatabase();

        // Apply scenario data
        var scenario = new MyTestScenario();
        scenario.ConfigureScenario(Database);

        // Create connector
        Connector = InMemoryConnectorFactory.Create(Database);

        // ?? REQUIRED: Initialize GremlinFactory for LINQ queries
        GremlinFactory.SetActivatorFactory(() => Connector);

        // Create context
        Context = new LinqTestContext(Connector);
    }

    public void Dispose()
    {
        Context?.Dispose();
        Connector?.Dispose();
    }
}
```

> **Key point:** `GremlinFactory.SetActivatorFactory(() => Connector)` is
> **required** for LINQ queries to work. The `GremlinQueryProvider` uses
> `GremlinFactory` internally to execute translated Gremlin strings.

### Writing a LINQ Test

```csharp
public class PeopleQueryTests : LinqTestBase
{
    [Fact]
    public async Task FilterByAge_ReturnsCorrectPeople()
    {
        // Arrange
        var queryable = Context.People.AsQueryable();

        // Act
        var result = await queryable
            .Where(p => p.Age > 30)
            .OrderBy(p => p.Name)
            .ToListAsync();

        // Assert
        result.Should().OnlyContain(p => p.Age > 30);
        result.Should().BeInAscendingOrder(p => p.Name);
    }

    [Fact]
    public async Task GraphTraversal_FindColleagues()
    {
        var colleagues = await Context.People.AsQueryable()
            .Where(p => p.Name == "Alice Johnson")
            .Out(p => p.Companies)
            .In(c => c.Employees)
            .ToListAsync();

        colleagues.Should().NotBeEmpty();
    }
}
```

---

## How Translation Works

```
C# LINQ                              Gremlin
?????????????????????????????????????????????????????????
.AsQueryable()                    ?  g.V().hasLabel('person')
.Where(p => p.Age > 30)          ?  .has('age', gt(30))
.Where(p => p.Name == "Alice")   ?  .has('name', 'Alice')
.Where(p => p.IsActive)          ?  .has('isActive', true)
.OrderBy(p => p.Name)            ?  .order().by('name', asc)
.OrderByDescending(p => p.Age)   ?  .order().by('age', desc)
.Skip(10)                        ?  .skip(10)
.Take(5)                         ?  .limit(5)
.Count()                         ?  .count()
.First()                         ?  .limit(1)
.Any()                           ?  .limit(1).count()
.Distinct()                      ?  .dedup()
.Select(p => p.Name)             ?  .values('name')
.Out(p => p.Companies)           ?  .out('worksAt')
.In(c => c.Employees)            ?  .in('worksAt')
.OutE<IUserSkill>()              ?  .outE('hasSkill')
.InE<IUserSkill>()               ?  .inE('hasSkill')
.InV<IPerson>()                  ?  .inV()
.As("a")                         ?  .as('a')
.Select<IPerson>("a")            ?  .select('a')
```

---

## Common Pitfalls

- **Missing `GremlinFactory.SetActivatorFactory`**: LINQ queries will fail at
  execution time because the provider cannot resolve a connector. Always call
  this in test setup.
- **Projection + traversal ordering**: Some complex query chains (e.g.,
  `Select().OrderBy().GroupBy()`) may require client-side evaluation. The
  provider handles this automatically but be aware of performance for large
  result sets.
- **Edge traversal type mismatch**: When using `OutE<TEdge>()`, the edge label
  on `TEdge` must match the label configured in `InitializeModel` or via
  `[InLabel]` / `[OutLabel]` attributes.
- **`As`/`Select` label scope**: Labels are step-scoped. A `Select<T>("a")`
  can only retrieve the type that was at step `"a"` — the generic parameter
  must match.
- **Query caching**: The translator caches query plans by expression tree hash.
  This improves performance but means the first execution of each query shape
  is slightly slower.

---

## Reference Files

For full API details, use `read_skill_resource` to load:
- `references/API_REFERENCE.md` — Complete extension method signatures
- `references/FULL_EXAMPLES.md` — End-to-end code examples with test patterns
