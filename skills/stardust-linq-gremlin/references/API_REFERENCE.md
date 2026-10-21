# API Reference — Stardust.Paradox.Data.Linq

## Namespace: `Stardust.Paradox.Data.Linq`

---

## GraphSetLinqExtensions (entry point)

### AsQueryable

```csharp
/// Creates an IQueryable from a vertex graph set
static IQueryable<T> AsQueryable<T>(this IGraphSet<T> graphSet) where T : IVertex;

/// Creates an IQueryable from an edge graph set
static IQueryable<T> AsQueryable<T>(this IEdgeGraphSet<T> graphSet) where T : IEdgeEntity;
```

### Has (Gremlin-style filter)

```csharp
/// Filters using a predicate expression — translates to Gremlin has() step
static IQueryable<T> Has<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate);
```

### Select by Step Label

```csharp
/// Selects a previously labeled step (from .As()) and casts to target type
static IQueryable<TResult> Select<TResult>(this IQueryable source, string label)
    where TResult : IGraphEntity;
```

### Edge Traversals on IQueryable

```csharp
/// Traverses outgoing edges by edge entity type (label inferred from type)
static IQueryable<TEdge> OutE<TVertex, TEdge>(this IQueryable<TVertex> source)
    where TVertex : IVertex where TEdge : IEdgeEntity;

/// Traverses outgoing edges using a lambda selector
static IQueryable<TEdge> OutE<TVertex, TEdge>(
    this IQueryable<TVertex> source,
    Expression<Func<TVertex, IEdgeCollection<IVertex>>> edgeSelector)
    where TVertex : IVertex where TEdge : IEdgeEntity;

/// Traverses incoming edges by edge entity type
static IQueryable<TEdge> InE<TVertex, TEdge>(this IQueryable<TVertex> source)
    where TVertex : IVertex where TEdge : IEdgeEntity;

/// Traverses incoming edges using a lambda selector
static IQueryable<TEdge> InE<TVertex, TEdge>(
    this IQueryable<TVertex> source,
    Expression<Func<TVertex, IEdgeCollection<IVertex>>> edgeSelector)
    where TVertex : IVertex where TEdge : IEdgeEntity;

/// Traverses both incoming and outgoing edges
static IQueryable<TEdge> BothE<TVertex, TEdge>(this IQueryable<TVertex> source)
    where TVertex : IVertex where TEdge : IEdgeEntity;

/// Traverses both edges using a lambda selector
static IQueryable<TEdge> BothE<TVertex, TEdge>(
    this IQueryable<TVertex> source,
    Expression<Func<TVertex, IEdgeCollection<IVertex>>> edgeSelector)
    where TVertex : IVertex where TEdge : IEdgeEntity;

/// Shorthand: outgoing edges (source type inferred)
static IQueryable<TEdge> OutE<TEdge>(this IQueryable source) where TEdge : IEdgeEntity;

/// Shorthand: incoming edges (source type inferred)
static IQueryable<TEdge> InE<TEdge>(this IQueryable source) where TEdge : IEdgeEntity;
```

### Async Terminal Operations

```csharp
static Task<List<T>> ToListAsync<T>(this IQueryable<T> source);
static Task<T> FirstAsync<T>(this IQueryable<T> source);
static Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> source);
static Task<int> CountAsync<T>(this IQueryable<T> source);
static Task<bool> AnyAsync<T>(this IQueryable<T> source);
```

---

## GraphTraversalExtensions (vertex-to-vertex / edge navigation)

### Vertex Traversals

```csharp
/// Traverse outgoing edges to target vertices
static IQueryable<TTarget> Out<TSource, TTarget>(
    this IQueryable<TSource> source,
    Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
    where TSource : IVertex where TTarget : IVertex;

/// Traverse incoming edges from source vertices
static IQueryable<TSource> In<TSource, TTarget>(
    this IQueryable<TTarget> source,
    Expression<Func<TTarget, IEdgeCollection<TSource>>> edgeSelector)
    where TSource : IVertex where TTarget : IVertex;
```

### Edge Traversals

```csharp
/// Traverse outgoing edges — returns IEdge<TTarget>
static IQueryable<IEdge<TTarget>> OutE<TSource, TTarget>(
    this IQueryable<TSource> source,
    Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
    where TSource : IVertex where TTarget : IVertex;

/// Traverse incoming edges — returns IEdge<TSource>
static IQueryable<IEdge<TSource>> InE<TSource, TTarget>(
    this IQueryable<TTarget> source,
    Expression<Func<TTarget, IEdgeCollection<TSource>>> edgeSelector)
    where TSource : IVertex where TTarget : IVertex;

/// Traverse outgoing edges — returns typed TEdge
static IQueryable<TEdge> OutE<TSource, TTarget, TEdge>(
    this IQueryable<TSource> source,
    Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
    where TSource : IVertex where TTarget : IVertex where TEdge : IEdgeEntity;

/// Traverse incoming edges — returns typed TEdge
static IQueryable<TEdge> InE<TSource, TTarget, TEdge>(
    this IQueryable<TSource> source,
    Expression<Func<TSource, IEdgeCollection<TTarget>>> edgeSelector)
    where TSource : IVertex where TTarget : IVertex where TEdge : IEdgeEntity;
```

### Edge-to-Vertex Traversals

```csharp
/// From edges, traverse to outgoing vertices
static IQueryable<TVertex> OutV<TEdge, TVertex>(this IQueryable<TEdge> source)
    where TEdge : IEdgeEntity where TVertex : IVertex;

/// From edges, traverse to incoming vertices
static IQueryable<TVertex> InV<TEdge, TVertex>(this IQueryable<TEdge> source)
    where TEdge : IEdgeEntity where TVertex : IVertex;

/// Shorthand InV (edge type inferred)
static IQueryable<TVertex> InV<TVertex>(this IQueryable source) where TVertex : IVertex;

/// From edges, traverse to the other vertex (opposite end)
static IQueryable<TVertex> OtherV<TEdge, TVertex>(this IQueryable<TEdge> source)
    where TEdge : IEdgeEntity where TVertex : IVertex;

/// Shorthand OtherV (edge type inferred)
static IQueryable<TVertex> OtherV<TVertex>(this IQueryable source) where TVertex : IVertex;
```

### Step Labeling

```csharp
/// Label the current traversal step for later reference with Select
static IQueryable<T> As<T>(this IQueryable<T> source, string label)
    where T : IGraphEntity;
```

### Path

```csharp
/// Retrieve the full path traversed
static IQueryable<IGraphPath> Path<T>(this IQueryable<T> source) where T : IVertex;
```

---

## GraphTraversalLambdaExtensions (fluent builder API)

Extensions on `GraphTraversal<T>` for building traversals with lambda expressions:

```csharp
static GraphTraversal<T> OutE<T>(this GraphTraversal<T> t, Expression<Func<T, object>> edgeProperty);
static GraphTraversal<T> InE<T>(this GraphTraversal<T> t, Expression<Func<T, object>> edgeProperty);
static GraphTraversal<T> Out<T>(this GraphTraversal<T> t, Expression<Func<T, object>> edgeProperty);
static GraphTraversal<T> In<T>(this GraphTraversal<T> t, Expression<Func<T, object>> edgeProperty);
```

---

## GraphTraversalEntityExtensions (fluent builder — string-based)

Extensions on `GraphTraversal<T>` for Gremlin steps:

### Filtering

```csharp
static GraphTraversal<T> Has<T>(this GraphTraversal<T> t, string key, object value);
static GraphTraversal<T> Has<T>(this GraphTraversal<T> t, string key);
static GraphTraversal<T> Has<T>(this GraphTraversal<T> t, Expression<Func<T, bool>> predicate);
static GraphTraversal<T> HasLabel<T>(this GraphTraversal<T> t, params string[] labels);
static GraphTraversal<T> HasId<T>(this GraphTraversal<T> t, params string[] ids);
```

### Vertex Traversal

```csharp
static GraphTraversal<T> Out<T>(this GraphTraversal<T> t, params string[] labels);
static GraphTraversal<T> In<T>(this GraphTraversal<T> t, params string[] labels);
static GraphTraversal<T> Both<T>(this GraphTraversal<T> t, params string[] labels);
```

### Edge Traversal

```csharp
static GraphTraversal<T> OutE<T>(this GraphTraversal<T> t, params string[] labels);
static GraphTraversal<T> InE<T>(this GraphTraversal<T> t, params string[] labels);
static GraphTraversal<T> BothE<T>(this GraphTraversal<T> t, params string[] labels);
static GraphTraversal<T> OutV<T>(this GraphTraversal<T> t);
static GraphTraversal<T> InV<T>(this GraphTraversal<T> t);
static GraphTraversal<T> OtherV<T>(this GraphTraversal<T> t);
static GraphTraversal<T> BothV<T>(this GraphTraversal<T> t);
```

---

## VertexEdgeTraversalExtensions (instance-level edge access)

Used inside `Select()` projections to access edges from a vertex instance:

```csharp
/// Traverse outgoing edges from a vertex instance
static IEdgeTraversal<IEdgeEntity> OutE<TVertex, TTarget>(
    this TVertex vertex,
    Expression<Func<TVertex, IEdgeCollection<TTarget>>> edgeSelector)
    where TVertex : IVertex where TTarget : IVertex;

/// Traverse incoming edges to a vertex instance
static IEdgeTraversal<IEdgeEntity> InE<TVertex, TTarget>(
    this TVertex vertex,
    Expression<Func<TVertex, IEdgeCollection<TTarget>>> edgeSelector)
    where TVertex : IVertex where TTarget : IVertex;
```

---

## P (Gremlin Predicate Builder)

```csharp
static class P
{
    static GremlinPredicate Gt(object value);           // Greater than
    static GremlinPredicate Lt(object value);           // Less than
    static GremlinPredicate Gte(object value);          // Greater than or equal
    static GremlinPredicate Lte(object value);          // Less than or equal
    static GremlinPredicate Eq(object value);           // Equal
    static GremlinPredicate Neq(object value);          // Not equal
    static GremlinPredicate Within(params object[] v);  // In set
    static GremlinPredicate Without(params object[] v); // Not in set
    static GremlinPredicate Between(object s, object e);// Range [s, e)
    static GremlinPredicate Inside(object s, object e); // Range (s, e)
    static GremlinPredicate Outside(object s, object e);// Outside range
}
```

---

## GraphQueryable&lt;T&gt; (IQueryable implementation)

```csharp
internal class GraphQueryable<T> : IOrderedQueryable<T>
{
    /// Translates the current LINQ expression to a Gremlin query string
    string ToGremlinQuery();

    /// ToString() also returns the generated Gremlin (for debugging)
    override string ToString();
}
```

---

## Supported LINQ-to-Gremlin Translation Map

| LINQ | Gremlin |
|------|---------|
| `.Where(p => p.X == v)` | `.has('x', v)` |
| `.Where(p => p.X > v)` | `.has('x', gt(v))` |
| `.Where(p => p.X >= v)` | `.has('x', gte(v))` |
| `.Where(p => p.X < v)` | `.has('x', lt(v))` |
| `.Where(p => p.X != v)` | `.has('x', neq(v))` |
| `.Where(p => p.A && p.B)` | `.has('a', ...).has('b', ...)` |
| `.Where(p => p.S.Contains(v))` | `.has('s', containing(v))` |
| `.Where(p => p.S.StartsWith(v))` | `.has('s', startingWith(v))` |
| `.Where(p => p.S.EndsWith(v))` | `.has('s', endingWith(v))` |
| `.Select(p => p.X)` | `.values('x')` |
| `.Select(p => new { p.X, p.Y })` | `.project('x','y').by('x').by('y')` |
| `.OrderBy(p => p.X)` | `.order().by('x', asc)` |
| `.OrderByDescending(p => p.X)` | `.order().by('x', desc)` |
| `.ThenBy(p => p.Y)` | `.by('y', asc)` |
| `.Skip(n)` | `.skip(n)` |
| `.Take(n)` | `.limit(n)` |
| `.Count()` | `.count()` |
| `.First()` | `.limit(1)` |
| `.Any()` | `.limit(1).count()` |
| `.Distinct()` | `.dedup()` |
| `.Sum(p => p.X)` | `.values('x').sum()` |
| `.Average(p => p.X)` | `.values('x').mean()` |
| `.Min(p => p.X)` | `.values('x').min()` |
| `.Max(p => p.X)` | `.values('x').max()` |
| `.GroupBy(p => p.X)` | `.group().by('x')` |
| `.Out(p => p.E)` | `.out('label')` |
| `.In(p => p.E)` | `.in('label')` |
| `.OutE<TEdge>()` | `.outE('label')` |
| `.InE<TEdge>()` | `.inE('label')` |
| `.InV<T>()` | `.inV()` |
| `.OutV<T>()` | `.outV()` |
| `.OtherV<T>()` | `.otherV()` |
| `.As("x")` | `.as('x')` |
| `.Select<T>("x")` | `.select('x')` |
