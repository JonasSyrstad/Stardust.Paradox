---
name: stardust-gremlin-traversals
description: >
  Build Gremlin traversal queries programmatically using the Stardust.Paradox
  fluent GremlinQuery API. Use when the user needs to construct raw Gremlin
  queries in C# with compile-time safety, parameterized values, and direct
  execution against the graph database. Covers: GremlinFactory, GremlinContext,
  GremlinQuery composition, vertex/edge steps (Out, In, OutE, InE), filtering
  (Has, HasLabel, HasId, Not, And, Or), mutations (AddV, AddE, Property, Drop),
  paging (Skip, Range, Limit), ordering (Order, By), aggregation (Count, Sum,
  Min, Max, Mean, Fold, Unfold, Group, GroupCount), looping (Repeat, Until,
  Times, Coalesce, Choose), path traversals (Path, SimplePath, CyclicPath),
  step labels (As, Select, Where), and predicates (Eq, Neq, Gt, Lt, Gte, Lte,
  Within, Without, Between). This is the lower-level API compared to LINQ —
  use it for queries that LINQ cannot express or for custom AddQuery properties.
license: MIT
compatibility: Stardust.Paradox.Data (namespace Stardust.Paradox.Data.Traversals). .NET Standard 2.0+.
metadata:
  author: stardust-paradox
  version: "1.0"
---

# Stardust.Paradox GremlinQuery Traversal Builder

The `GremlinQuery` fluent API lets you build Gremlin traversal queries in C#
with automatic parameter binding and compile-time safety. This is the **raw
Gremlin builder** — use it when LINQ-to-Gremlin cannot express your query,
when defining `AddQuery` properties on entity interfaces, or when you need
full Gremlin control.

---

## When to Use

- The user needs to build a Gremlin query that LINQ cannot express (complex
  graph patterns, recursive traversals, conditional branching).
- The user is implementing `AddQuery` on an entity configuration in
  `InitializeModel`.
- The user needs direct query execution via `ExecuteAsync`.
- The user is writing Gremlin queries for Cosmos DB or TinkerPop that require
  parameterized values for injection safety.

## Prerequisites

- Reference to `Stardust.Paradox.Data`.
- Namespace: `using Stardust.Paradox.Data.Traversals;`

---

## Core Concepts

### 1. Entry Points

#### GremlinFactory (static access)

```csharp
using Stardust.Paradox.Data.Traversals;

// GremlinFactory.G returns the current IGremlinLanguageConnector
// Set it once at startup or in test setup
GremlinFactory.SetActivatorFactory(() => myConnector);

// Build queries starting from GremlinFactory.G
var query = GremlinFactory.G.V().HasLabel("person").Has("age", p => p.Gt(30));
```

#### GremlinContext (instance-based, used in AddQuery)

```csharp
// In InitializeModel — the lambda receives a GremlinContext
configuration.ConfigureCollection<IProfile>()
    .AddQuery(p => p.AllSiblings,
        g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());
```

The `g` parameter is a `GremlinContext` with methods:
- `g.V()` — all vertices
- `g.V(id)` — vertex by id
- `g.V(id, partitionKey)` — vertex by id + partition key
- `g.E()` — all edges
- `g.E(id)` — edge by id

### 2. Query Composition

All step methods return a new `GremlinQuery`, enabling fluent chaining:

```csharp
var query = g.V()
    .HasLabel("person")
    .Has("age", p => p.Gt(25))
    .Out("worksAt")
    .HasLabel("company")
    .Values("name");

// Compile to string
string gremlin = query.ToString();
// ? g.V().hasLabel('person').has('age',gt(25)).out('worksAt').hasLabel('company').values('name')

// Execute
var results = await query.ExecuteAsync();
```

### 3. Parameter Binding

Values are automatically parameterized when the connector supports it
(`CanParameterizeQueries == true`). This prevents Gremlin injection:

```csharp
// With parameterization: g.V().has('name', __p0) + { __p0: "Alice" }
// Without: g.V().has('name', 'Alice')
var query = g.V().Has("name", "Alice");
var results = await query.ExecuteAsync();
```

### 4. Vertex & Edge Steps

```csharp
// Vertex traversals
g.V().Out("knows")          // outgoing "knows" edges ? target vertices
g.V().In("knows")           // incoming "knows" edges ? source vertices
g.V().Both("knows")         // both directions
g.V().OutE("knows")         // outgoing edges (as edge objects)
g.V().InE("knows")          // incoming edges (as edge objects)
g.V().BothE("knows")        // both directions (as edge objects)

// Edge ? vertex
query.InV()                 // edge ? incoming vertex
query.OutV()                // edge ? outgoing vertex
query.OtherV()              // edge ? the other vertex
query.BothV()               // edge ? both vertices
```

### 5. Filtering

```csharp
// By property
g.V().Has("name", "Alice")
g.V().Has("age", 30)
g.V().Has("active", true)
g.V().Has("name")               // has property "name" (any value)
g.V().HasNot("deleted")          // does NOT have property

// By label or id
g.V().HasLabel("person")
g.V().HasLabel("person", "company")  // multiple labels (OR)
g.V().HasId("id1", "id2")

// With predicates
g.V().Has("age", p => p.Gt(30))
g.V().Has("age", p => p.Gte(18))
g.V().Has("age", p => p.Lt(65))
g.V().Has("age", p => p.Lte(100))
g.V().Has("name", p => p.Neq("deleted"))
g.V().Has("status", p => p.Within("active", "pending"))
g.V().Has("status", p => p.Without("deleted", "archived"))
g.V().Has("age", p => p.Between(18, 65))
g.V().Has("age", p => p.Inside(17, 66))    // exclusive bounds
g.V().Has("age", p => p.Outside(0, 18))    // outside range

// Logical combinators
g.V().And(
    p => p.__().Has("age", p2 => p2.Gt(18)),
    p => p.__().Has("active", true))
g.V().Or(
    p => p.__().Has("city", "Seattle"),
    p => p.__().Has("city", "Portland"))
g.V().Not(p => p.__().Has("deleted"))

// Scalar filtering
query.Is(42)
query.Is(p => p.Gt(100))
```

### 6. Mutations

```csharp
// Add vertex
g.V().AddV("person")
    .Property("name", "Alice")
    .Property("age", 30)
    .Property("active", true)

// Add edge
g.V("id1").AddE("knows").To(g.V("id2"))
    .Property("since", "2020")

// Update properties
g.V("id1").Property("name", "Bob")

// Delete
g.V("id1").Drop()         // delete vertex + all edges
g.E("edge1").Drop()        // delete edge
g.V().Has("deleted", true).Drop()  // bulk delete
```

### 7. Ordering & Paging

```csharp
// Ordering
g.V().HasLabel("person").Order().By("name", OrderingTypes.Incr)
g.V().HasLabel("person").Order().By("age", OrderingTypes.Decr)

// Paging
g.V().HasLabel("person").Limit(10)           // first 10
g.V().HasLabel("person").Range(20, 30)       // items 20-29
g.V().HasLabel("person").Skip(20)            // skip first 20
g.V().HasLabel("person").SkipTake(20, 10)    // skip 20, take 10
g.V().HasLabel("person").Tail()              // last element
g.V().HasLabel("person").Tail(5)             // last 5 elements
```

### 8. Aggregation

```csharp
g.V().HasLabel("person").Count()
g.V().HasLabel("person").Values("age").Sum()
g.V().HasLabel("person").Values("age").Min()
g.V().HasLabel("person").Values("age").Max()
g.V().HasLabel("person").Values("age").Mean()

// Grouping
g.V().HasLabel("person").Group().By("city")
g.V().HasLabel("person").GroupCount().By("city")

// Fold / Unfold
g.V().HasLabel("person").Fold()     // collect into list
query.Unfold()                       // expand list back
```

### 9. Step Labels & Selection

```csharp
// Label a step
g.V("{id}").As("start")
    .Out("knows")
    .As("friend")
    .Out("knows")
    .Where("friend", p => p.Neq("start"))
    .Select("start", "friend")

// Path
g.V("{id}").Repeat(p => p.__().Out("knows"))
    .Until(p => p.__().Has("name", "Bob"))
    .Path()
    .SimplePath()   // no repeated vertices
```

### 10. Looping & Branching

```csharp
// Repeat/Until — find path to "Bob"
g.V("{id}")
    .Repeat(p => p.__().Out("knows"))
    .Until(p => p.__().Has("name", "Bob"))
    .Times(5)  // max depth

// Emit — emit vertices during traversal
g.V("{id}").Emit().Repeat(p => p.__().Out("parent"))

// Coalesce — try alternatives in order
g.V("{id}").Coalesce(
    q => q.Out("preferred").CompileQuery(),
    q => q.Out("fallback").CompileQuery())

// Choose — conditional branching
g.V("{id}").Choose(
    q => q.Has("type", "premium").CompileQuery(),
    q => q.Out("premiumService").CompileQuery(),
    q => q.Out("basicService").CompileQuery())

// Optional — return identity if no result
g.V("{id}").Optional(p => p.__().Out("spouse"))
```

### 11. Map & Transform

```csharp
g.V().HasLabel("person").Values("name")        // get property values
g.V().HasLabel("person").Value()               // single value
g.V().HasLabel("person").ValueMap()            // all properties as map
g.V().HasLabel("person").ValueMap(true)        // include id and label
g.V().HasLabel("person").ValueMap("name", "age")  // specific properties
g.V().HasLabel("person").Label()               // get vertex labels
g.V().HasLabel("person").Properties("name")    // get property objects
```

### 12. Modulation & Deduplication

```csharp
g.V().Out("knows").Dedup()          // remove duplicates
g.V().Out("knows").SimplePath()     // no cycles in path
g.V().Out("knows").CyclicPath()     // only cyclic paths
g.V().HasLabel("person").Sample(5)  // random sample of 5
g.V().HasLabel("person").Coin(0.5)  // 50% chance per traverser
```

### 13. Special Steps

```csharp
// Barrier — force all traversers to complete before continuing
g.V().HasLabel("person").Order().By("name").Barrier()

// Explain — get query plan (not supported by all databases)
g.V().HasLabel("person").Explain()

// Identity — pass through unchanged
p.__().Identity()

// PageRank — compute page rank
g.V().PageRank()

// PeerPressure — community detection
g.V().PeerPressure()

// SubGraph — extract subgraph
g.V().OutE("knows").SubGraph("sg")
```

---

## Using with AddQuery in InitializeModel

The most common use of the traversal builder is defining custom query
properties on entity interfaces:

```csharp
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }
    string Name { get; set; }

    // This property is backed by a custom Gremlin traversal
    IEdgeCollection<IPerson> AllSiblings { get; set; }
}

protected override bool InitializeModel(IGraphConfiguration configuration)
{
    configuration.ConfigureCollection<IPerson>()
        .AddQuery(p => p.AllSiblings,
            g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());

    return true;
}
```

The `{id}` placeholder is replaced with the current vertex's ID at runtime.

---

## Executing Queries Directly

```csharp
// Build query
var query = GremlinFactory.G.V()
    .HasLabel("person")
    .Has("age", p => p.Gt(30))
    .Values("name");

// Execute and get results
IEnumerable<dynamic> results = await query.ExecuteAsync();

// Access parameters (for debugging)
var parameters = query.Parameters;
// ? { "__p0": "person", "__p1": 30 }
```

---

## Common Pitfalls

- **Missing GremlinFactory setup**: Call `GremlinFactory.SetActivatorFactory`
  or `SetServiceProvider` before building queries. Without it, `G` returns null.
- **Parameter ordering**: Parameters are numbered `__p0`, `__p1`, etc. in the
  order `ComposeParameter` is called. Don't reuse query objects.
- **String escaping**: When `CanParameterizeQueries` is false, strings are
  auto-escaped with `EscapeGremlinString()`. But prefer parameterized queries.
- **Cosmos DB limitations**: Some steps (`Explain`, `PeerPressure`, `PageRank`)
  are not supported by Cosmos DB. Check the compatibility matrix.
- **Immutable composition**: Each step returns a **new** `ComposedGremlinQuery`.
  The original query is not modified.
