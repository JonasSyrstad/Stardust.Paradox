# API Reference — Stardust.Paradox.Data Graph Context Configuration

## GraphContextBase

**Namespace:** `Stardust.Paradox.Data`

Abstract base class for all graph contexts. Analogous to EF Core's `DbContext`.

### Constructors

```csharp
protected GraphContextBase(IGremlinLanguageConnector connector, IServiceProvider serviceProvider)
protected GraphContextBase(IGremlinLanguageConnector connector, IServiceProvider serviceProvider, ILogging logger)
```

### Static Properties

```csharp
// Set in static constructor to enable partition-key-aware operations
protected internal static string PartitionKeyName { get; set; }
```

### Instance Properties / Methods

```csharp
double ConsumedRU { get; }  // Total consumed Request Units

// Override in subclass — configure all entity collections here
protected abstract bool InitializeModel(IGraphConfiguration configuration);

// Override to seed data after model initialization (called once via .Wait())
protected virtual Task SeedAsync();
```

### Entity Operations

```csharp
// Vertex lookup
Task<T> VAsync<T>(string id) where T : IVertex
Task<T> VAsync<T>(string id, string partitionKey) where T : IVertex
Task<IEnumerable<T>> VAsync<T>(GremlinQuery g) where T : IVertex
Task<IEnumerable<T>> VAsync<T>(Func<GremlinContext, GremlinQuery> g) where T : IVertex

// Create / Get-or-Create
T CreateEntity<T>(string id) where T : IGraphEntity
T CreateEntity<T>(string id, string partitionKey) where T : IGraphEntity
Task<T> GetOrCreate<T>(string id) where T : IVertex
Task<T> GetOrCreate<T>(string id, string partitionKey) where T : IVertex

// Typed queries
GremlinQuery V<T>() where T : IVertex         // g.V().hasLabel('label')
GremlinQuery V<T>(string id) where T : IVertex // g.V(id).hasLabel('label')

// Mutation
void Delete<T>(T toBeDeleted) where T : IGraphEntity
void ResetChanges<T>(T entityToReset) where T : IGraphEntity
Task SaveChangesAsync()

// Graph sets
IGraphSet<T> GraphSet<T>() where T : IVertex
IEdgeGraphSet<T> EdgeGraphSet<T>() where T : IEdgeEntity
```

---

## IGraphConfiguration (root)

**Namespace:** `Stardust.Paradox.Data`

Entry point returned when `InitializeModel` is called.

```csharp
public interface IGraphConfiguration
{
    // Begin configuring an entity — label inferred from [VertexLabel] / [EdgeLabel]
    IGraphConfiguration<T> ConfigureCollection<T>() where T : IGraphEntity;

    // Begin configuring with an explicit label override
    IGraphConfiguration<T> ConfigureCollection<T>(string label) where T : IGraphEntity;
}
```

---

## IGraphConfiguration&lt;T&gt; (entity-level fluent API)

Returned by `ConfigureCollection<T>()`. All methods return `IGraphConfiguration<T>`
for chaining unless noted.

### Modern In/Out API (preferred)

```csharp
// Navigate INCOMING edges — returns InReverse<TReverse, T> for chaining .Out()
InReverse<TReverse, T> In<TReverse>(
    Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda)
    where TReverse : IVertex;

InReverse<TReverse, T> In<TReverse>(
    Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda, string label)
    where TReverse : IVertex;

// Navigate OUTGOING edges — returns OutReverse<TReverse, T> for chaining .In()
OutReverse<TReverse, T> Out<TReverse>(
    Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda)
    where TReverse : IVertex;

OutReverse<TReverse, T> Out<TReverse>(
    Expression<Func<T, IEdgeNavigation<TReverse>>> inPropertyLambda, string label)
    where TReverse : IVertex;
```

### InReverse&lt;T, TParent&gt; (returned by `.In()`)

```csharp
// Complete the pair: on the reverse type, this property traverses OUT
IGraphConfiguration<TParent> Out(Expression<Func<T, object>> func);
```

### OutReverse&lt;T, TParent&gt; (returned by `.Out()`)

```csharp
// Complete the pair: on the reverse type, this property traverses IN
IGraphConfiguration<TParent> In(Expression<Func<T, object>> func);
```

### Legacy Edge API

```csharp
IInEdgeConfiguration<T> AddInEdge(Expression<Func<T, object>> inPropertyLambda);
IInEdgeConfiguration<T> AddInEdge(Expression<Func<T, object>> inPropertyLambda, string label);
IInEdgeConfiguration<T> AddInEdge(Expression<Func<T, object>> inPropertyLambda, string label, bool eagerLoading);

IOutEdgeConfiguration<T> AddOutEdge(Expression<Func<T, object>> outPropertyLambda);
IOutEdgeConfiguration<T> AddOutEdge(Expression<Func<T, object>> outPropertyLambda, string label);
```

### Custom Queries

```csharp
// Fluent Gremlin builder
IGraphConfiguration<T> AddQuery(
    Expression<Func<T, object>> inPropertyLambda,
    Func<GremlinContext, GremlinQuery> g);

// Raw Gremlin string (use {id} placeholder for current vertex id)
IGraphConfiguration<T> AddQuery(
    Expression<Func<T, object>> inPropertyLambda,
    string gremlinQuery);
```

### Inline Serialization

```csharp
IGraphConfiguration<T> AddInline(
    Expression<Func<T, object>> inPropertyLambda,
    SerializationType serialization);
```

### Chain to Next Entity

```csharp
IGraphConfiguration<Tn> ConfigureCollection<Tn>() where Tn : IGraphEntity;
IGraphConfiguration<Tn> ConfigureCollection<Tn>(string label) where Tn : IGraphEntity;
```

---

## Entity Interfaces

### IVertex

```csharp
namespace Stardust.Paradox.Data.Annotations;

public interface IGraphEntity
{
    string Label { get; }
    event PropertyChangedHandler PropertyChanged;
    event PropertyChangingHandler PropertyChanging;
}

public interface IVertex : IGraphEntity { }
```

### IEdge / IEdge&lt;TIn, TOut&gt;

```csharp
// Marker interface
public interface IEdge : IGraphEntity { }

// Single-end edge reference
public interface IEdge<T> : IEdge where T : IVertex
{
    T Vertex { get; set; }
    string EdgeType { get; }
    IDictionary<string, object> Properties { get; }
}

// Typed edge entity with both endpoints
public interface IEdge<TIn, TOut> : IEdgeEntity
    where TIn : IVertex
    where TOut : IVertex
{
    Task<TIn> InVAsync();
    string InVertexId { get; }
    string OutVertextId { get; }
    Task<TOut> OutVAsync();
}
```

### Navigation Property Types

```csharp
// Collection of edges (many)
public interface IEdgeCollection<T> : IEdgeNavigation<T> where T : IVertex
{
    Task<IEnumerable<T>> ToVerticesAsync();
    Task<IEnumerable<IEdge<T>>> ToEdgesAsync();
    void Add(T vertex, IDictionary<string, object> edgeProperties);
    void AddDual(T vertex); // Bidirectional
}

// Single edge reference (one)
public interface IEdgeReference<T> : IEdgeNavigation<T> where T : IVertex
{
    Task<T> ToVertexAsync();
    Task SetVertexAsync(T vertex);
    T Vertex { get; set; }
    IEdge<T> Edge { get; set; }
}

// Marker for In<>/Out<> fluent API
public interface IEdgeNavigation<T> where T : IVertex { }
```

### IDynamicGraphEntity

```csharp
public interface IDynamicGraphEntity
{
    object GetProperty(string propertyName);
    void SetProperty(string propertyName, object value);
    string[] DynamicPropertyNames { get; }
}
```

---

## IGraphSet&lt;T&gt;

```csharp
public interface IGraphSet<T> where T : IVertex
{
    Task<T> GetAsync(string id);
    Task<T> GetAsync(string id, string partitionKey);
    Task<IEnumerable<T>> AllAsync();
    Task<IEnumerable<T>> AllAsync(int page, int pageSize = 20);
    Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue);
    Task DeleteAsync(string id);
}
```

---

## Attributes Reference

| Attribute | Namespace | Target | Description |
|-----------|-----------|--------|-------------|
| `[VertexLabel("label")]` | `Annotations` | Interface | Gremlin vertex label |
| `[EdgeLabel("label")]` | `Annotations` | Interface/Property | Edge label (obsolete — use `[InLabel]`) |
| `[InLabel("label")]` | `Annotations` | Property | Incoming edge label |
| `[OutLabel("label")]` | `Annotations` | Property | Outgoing edge label |
| `[ToWayEdgeLabel("label")]` | `Annotations` | Property | Bidirectional edge label |
| `[GremlinQuery("query")]` | `Annotations` | Property | Custom Gremlin traversal (`{id}` = vertex id) |
| `[Eager]` | `Annotations` | Property | Load eagerly with parent vertex |
| `[InlineSerialization(type)]` | `Annotations` | Property | Serialize as vertex property |

### SerializationType enum

```csharp
public enum SerializationType
{
    ClearText,  // JSON string stored as-is
    Base64      // Base64-encoded JSON
}
```

---

## DependencyResolverAdapter

**Namespace:** `Stardust.Paradox.Data`

```csharp
// Register entity bindings (required before context construction)
T AddEntityBinding<T>(this T serviceCollection, Action<Type, Type> addToCollection);

// Convenience: register context + connector in one call (ASP.NET Core)
IServiceCollection AddParadox<TContext>(
    this IServiceCollection services,
    Func<IServiceProvider, IGremlinLanguageConnector> createDatabaseProvider)
    where TContext : class, IGraphContext;
```
