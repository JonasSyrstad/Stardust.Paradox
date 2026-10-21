# Copilot Custom Instructions for Stardust.Paradox

This document provides context and guidelines for GitHub Copilot when working with the Stardust.Paradox codebase.

## Project Overview

**Stardust.Paradox** is an Entity Framework-styled ORM for Gremlin-based graph databases (Azure Cosmos DB Gremlin API, Apache TinkerPop). It provides type-safe access to graph databases using interface-based entity modeling with runtime code generation.

### Core Packages

| Package | Purpose | Target Frameworks |
|---------|---------|-------------------|
| `Stardust.Paradox.Data.Annotations` | Attributes, interfaces (`IVertex`, `IEdgeEntity`, etc.) | .NET Standard 2.0 |
| `Stardust.Paradox.Data` | Core ORM: `GraphContextBase`, `IGraphSet<T>`, traversals, code generation | .NET Standard 2.0, .NET 6/7/8/9 |
| `Stardust.Paradox.Data.Linq` | LINQ-to-Gremlin query provider | .NET Standard 2.0, .NET 8 |
| `Stardust.Paradox.Data.InMemory` | In-memory graph database for testing | .NET Standard 2.0, .NET 8 |
| `Stardust.Paradox.Data.Providers.Gremlin` | Gremlin.Net connector for Cosmos DB/TinkerPop | .NET Standard 2.0 |
| `Stardust.Paradox.Data.Providers.CosmosDb` | Cosmos DB DocumentClient connector (deprecated in favor of Gremlin provider) | .NET Standard 2.0 |

### Test Projects

| Project | Target Framework | Test Framework |
|---------|------------------|----------------|
| `Stardust.Paradox.Data.UnitTests` | .NET Core 2.1 | xUnit 2.4.1 |
| `Stardust.Paradox.Data.InMemory.Tests` | .NET 8 | xUnit 2.5.3, FluentAssertions |
| `Stardust.Paradox.Data.Linq.Tests` | .NET 8 | xUnit 2.5.3, FluentAssertions |
| `Stardust.Paradox.Data.CosmosDbTests` | .NET 8 | Integration tests against Cosmos DB |

## Architecture Patterns

### Entity Definition Pattern

Entities are defined as **interfaces** decorated with attributes. Runtime code generation creates concrete implementations:

```csharp
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }
    string FirstName { get; set; }
    
    // Navigation properties use IEdgeCollection<T> or IEdgeReference<T>
    [EdgeLabel("knows")]
    IEdgeCollection<IPerson> Friends { get; }
    
    [ToWayEdgeLabel("spouse")]
    IEdgeReference<IPerson> Spouse { get; }
    
    // Inline serialized collections
    [InlineSerialization(SerializationType.ClearText)]
    ICollection<string> Skills { get; }
}

[EdgeLabel("employment")]
public interface IEmployment : IEdge<IPerson, ICompany>
{
    string Id { get; }
    DateTime HiredDate { get; set; }
}
```

### Graph Context Pattern

Inherit from `GraphContextBase` and configure relationships in `InitializeModel`:

```csharp
public class MyContext : GraphContextBase
{
    public MyContext(IGremlinLanguageConnector connector, IServiceProvider serviceProvider) 
        : base(connector, serviceProvider) { }
    
    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
    
    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        configuration.ConfigureCollection<IPerson>()
            .Out(p => p.Friends, "knows").In<IPerson>(p => p.Friends)
            .Out(p => p.Employers, "employer").In<ICompany>(c => c.Employees);
        return true;
    }
}
```

### Gremlin Query Building

The `GremlinQuery` class and extension methods in `Stardust.Paradox.Data.Traversals` build Gremlin strings:

```csharp
// Direct traversal building
var query = GremlinFactory.G.V().HasLabel("person").Has("name", "John").Out("knows");

// Context-based queries  
var results = await context.VAsync<IPerson>(g => 
    g.V().HasLabel("person").Has("adult", true).Limit(10));
```

### LINQ Provider Architecture

The LINQ provider in `Stardust.Paradox.Data.Linq` uses a visitor pattern with a plugin-based architecture:

- `GremlinQueryProvider` - IQueryProvider implementation
- `GraphQueryable<T>` - IQueryable implementation  
- `GremlinQueryTranslator` - Main expression visitor implementing `IVisitorContext`
- `VisitorRegistry` - Centralized registry for LINQ operator visitors
- Individual visitors in `Visitors/` folder for each LINQ operator (inherit from `GraphTraversalVisitorBase`)
- `IExpressionVisitor` interface for visitor plugins

## Coding Conventions

### C# Version and Language Features

- **Stardust.Paradox.Data.Linq**: Uses C# 7.3 explicitly (set via `<LangVersion>7.3</LangVersion>` for .NET Standard 2.0 compatibility)
- **Stardust.Paradox.Data.InMemory**: Uses `<LangVersion>latest</LangVersion>`
- **Other projects**: Use latest stable C# features where supported
- Use `nameof()` for property references
- Prefer expression-bodied members for simple getters
- Use nullable reference types in test projects (`.NET 8` with `<Nullable>enable</Nullable>`)

### Naming Conventions

```csharp
// Interfaces for entities always start with 'I'
public interface IPerson : IVertex { }

// Private fields use underscore prefix
private readonly IGremlinLanguageConnector _connector;
private static readonly ConcurrentDictionary<Type, string> _cache;

// Internal fields without underscore are rare but exist in generated code
internal string _entityKey;

// Constants use PascalCase
private const int MaxRetries = 5;

// Edge labels use camelCase in Gremlin
[EdgeLabel("worksFor")]  // NOT "WorksFor" or "works_for"
```

### XML Documentation

Public APIs should have XML documentation:

```csharp
/// <summary>
/// Creates a LINQ-queryable interface for a vertex graph set
/// </summary>
/// <typeparam name="T">The vertex type</typeparam>
/// <param name="graphSet">The graph set</param>
/// <returns>An IQueryable for LINQ operations</returns>
public static IQueryable<T> AsQueryable<T>(this IGraphSet<T> graphSet)
    where T : IVertex
```

### Async Patterns

- All database operations should be async
- Use `ConfigureAwait(false)` in library code
- Return `Task<T>` not `ValueTask<T>` for consistency

```csharp
public async Task<T> VAsync<T>(string id) where T : IVertex
{
    return await ConvertTo<T>(await _connector.V(id).ExecuteAsync())
        .ConfigureAwait(false);
}
```

### Null Checking

- Use `ArgumentNullException` for public method parameters
- Use null-conditional operators where appropriate

```csharp
public static IQueryable<T> Has<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate)
{
    if (source == null) throw new ArgumentNullException(nameof(source));
    if (predicate == null) throw new ArgumentNullException(nameof(predicate));
    // ...
}
```

## Build and Test Commands

### Building

```bash
# Build entire solution (from src directory)
dotnet build Stardust.Paradox.sln

# Build specific project
dotnet build Stardust.Paradox.Data/Stardust.Paradox.Data.csproj

# Build for specific framework
dotnet build -f net8.0
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test Stardust.Paradox.Data.InMemory.Tests/Stardust.Paradox.Data.InMemory.Tests.csproj
dotnet test Stardust.Paradox.Data.Linq.Tests/Stardust.Paradox.Data.Linq.Tests.csproj

# Run tests with filter
dotnet test --filter "FullyQualifiedName~TinkerGraphBasicTests"

# Run with verbosity
dotnet test -v normal
```

### Test Framework

- **xUnit** for all test projects
- **FluentAssertions** for assertions (newer test projects)
- **Moq** for mocking (where needed)
- Use `[Fact]` for standard tests, `[Theory]` for parameterized tests

```csharp
[Fact]
public async Task SimpleVertexQuery_ShouldReturnAllVertices()
{
    // Arrange
    var connector = InMemoryGremlinLanguageConnector.Create();
    
    // Act
    var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
    
    // Assert
    result.Should().HaveCount(1);
}
```

## Key Interfaces and Classes

### Core Interfaces

| Interface | Purpose |
|-----------|---------|
| `IVertex` | Marker interface for vertex entities |
| `IEdgeEntity` | Marker interface for edge entities |
| `IEdge<TIn, TOut>` | Typed edge between two vertex types |
| `IGraphEntity` | Base interface for all graph entities |
| `IEdgeCollection<T>` | Navigation property for multiple edges |
| `IEdgeReference<T>` | Navigation property for single edge |
| `IGremlinLanguageConnector` | Database connection abstraction |
| `IGraphContext` | Unit of work pattern for graph operations |
| `IGraphSet<T>` | Repository pattern for vertices |
| `IEdgeGraphSet<T>` | Repository pattern for edges |

### Important Attributes

| Attribute | Purpose |
|-----------|---------|
| `[VertexLabel("label")]` | Specifies vertex label in graph |
| `[EdgeLabel("label")]` | Specifies edge label |
| `[InLabel("label")]` | Incoming edge label (on property) |
| `[OutLabel("label")]` | Outgoing edge label (replaces deprecated `[ReverseEdgeLabel]`) |
| `[ToWayEdgeLabel("label")]` | Bidirectional edge (same label both directions) |
| `[ReverseEdgeLabel("label")]` | **Deprecated** - use `[OutLabel]` instead |
| `[GremlinQuery("query")]` | Custom traversal query with `{id}` placeholder |
| `[InlineSerialization(type)]` | Serialize collection inline (ClearText, Base64) |
| `[Eager]` | Eager load navigation property |

### Key Internal Classes

| Class | Purpose |
|-------|---------|
| `GraphContextBase` | Abstract base for graph contexts |
| `GraphDataEntity` | Base implementation for generated vertex entities |
| `EdgeDataEntity<TIn, TOut>` | Base for generated edge entities |
| `GremlinQuery` | Fluent Gremlin query builder |
| `CodeGenerator` | IL code generation for entity implementations |
| `GraphConfiguration` | Fluent configuration builder |
| `DualDictionary<T, U>` | Bidirectional dictionary for type/label mappings |

### Infrastructure Classes (Performance)

Located in `Stardust.Paradox.Data/Infrastructure/`:

| Class | Purpose |
|-------|---------|
| `ReflectionCache` | Caches reflection results for performance |
| `AttributeCache` | Caches attribute lookups |
| `TypeConversionCache` | Caches type conversion operations |
| `ExpressionCompiler` | Compiles and caches expression delegates |
| `PropertyTransferOptimizer` | Optimizes property transfer operations |
| `StringBuilderOptimizer` | StringBuilderPool for reducing allocations |

## InMemory Testing Provider

The `Stardust.Paradox.Data.InMemory` package provides a complete in-memory graph database:

```csharp
// Create connector for testing
var connector = InMemoryGremlinLanguageConnector.Create();

// Execute Gremlin queries directly
await connector.ExecuteAsync("g.addV('person').property('id', 'john')", params);
var result = await connector.ExecuteAsync("g.V('john').out('knows')", params);

// Access database directly for setup/verification
var database = connector.Database;
var vertex = database.AddVertex("person", "john");
vertex.SetProperty("name", "John Doe");
```

### Step Executors

Individual Gremlin steps are implemented in `ExecutionEngine/Steps/`:

- `VStepExecutor`, `EStepExecutor` - Vertex/Edge retrieval
- `HasStepExecutor`, `HasLabelStepExecutor` - Filtering
- `OutStepExecutor`, `InStepExecutor`, `BothStepExecutor` - Edge traversal
- `OutEStepExecutor`, `InEStepExecutor`, `BothEStepExecutor` - Edge entity traversal
- `AddVStepExecutor`, `AddEStepExecutor` - Creation
- `RepeatStepExecutor`, `UntilStepExecutor`, `EmitStepExecutor` - Loop control
- `TreeStepExecutor`, `PathStepExecutor` - Path operations
- etc.

All step executors inherit from `StepExecutorBase` and implement `IStepExecutor`.

When adding new Gremlin step support, create a new `*StepExecutor` class.

## Common Patterns to Follow

### Adding New Entity Properties

When adding properties to entity interfaces, ensure:
1. Property has getter (and setter if mutable)
2. Code generator handles the type (see `GraphContextBase.TransferData`)
3. Consider adding `[JsonProperty]` attribute if needed

### Adding New Gremlin Steps

1. Add extension method to `GremlinQuery` in appropriate file under `Traversals/`
2. Return new `GremlinQuery` with appended step string
3. For InMemory support, add `*StepExecutor` class in `ExecutionEngine/Steps/`
4. For LINQ support, add visitor in `Stardust.Paradox.Data.Linq/Visitors/`

### Adding New LINQ Operators

1. Create visitor class inheriting `GraphTraversalVisitorBase`
2. Implement `CanVisit` and `Visit` methods
3. The `VisitorRegistry` auto-discovers visitors via reflection
4. Add tests in `Stardust.Paradox.Data.Linq.Tests`

## Error Handling Guidelines

- Wrap database errors in `GraphExecutionException`
- Include query string in exceptions when possible
- Use retry logic for transient failures (see `GremlinNetLanguageConnector`)

```csharp
catch (GraphExecutionException ex)
{
    Console.WriteLine($"Query failed: {ex.SaveEventArgs.FailedUpdateStatement}");
    Console.WriteLine($"Error: {ex.SaveEventArgs.Error.Message}");
}
```

## Performance Considerations

- Use `ConcurrentDictionary` for caches with appropriate `concurrencyLevel` and `capacity`
- Cache reflection results (see `Infrastructure/ReflectionCache.cs`)
- Use compiled expressions for property access
- The code generator uses IL emit for performance-critical paths
- Use `StringBuilderPool` for string building operations

```csharp
// Example: ConcurrentDictionary with sizing
private static readonly ConcurrentDictionary<string, Type> _cache =
    new ConcurrentDictionary<string, Type>(
        concurrencyLevel: Environment.ProcessorCount,
        capacity: 128);
```

## Multi-Targeting Notes

When writing code that must work across all targets:

```csharp
// Use conditional compilation if needed
#if NETSTANDARD2_0
    // .NET Standard 2.0 specific code
#else
    // Modern .NET code
#endif

// Prefer APIs available in .NET Standard 2.0 when possible
// Avoid Span<T>, Index, Range without polyfills in netstandard2.0 projects
```

## Dependencies

### Core Dependencies (all projects)
- `Newtonsoft.Json` 13.0.1 - JSON serialization

### Additional by Project
- `Stardust.Paradox.Data`: `Stardust.Particles`, `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Stardust.Paradox.Data.InMemory`: `Gremlin.Net` 3.4.10
- `Stardust.Paradox.Data.Providers.Gremlin`: `Gremlin.Net` 3.4.10
- `Stardust.Paradox.Data.Providers.CosmosDb`: `Microsoft.Azure.DocumentDB`, `Microsoft.Azure.Graphs` (deprecated)

### Test Dependencies (newer projects)
- `xunit` 2.5.3
- `FluentAssertions` 6.12.0
- `Moq` 4.20.69
- `Microsoft.NET.Test.Sdk` 17.8.0
- `coverlet.collector` 6.0.0

## Git Workflow

- Main development branch: `V2.5`
- Repository: https://github.com/JonasSyrstad/Stardust.Paradox
- Use descriptive commit messages
- Create feature branches for significant changes

## Troubleshooting Common Issues

### "Type not registered" errors
- Ensure entity interface is configured in `InitializeModel`
- Check that `GraphConfiguration.BuildModel()` is called (happens automatically in base constructor)

### Edge traversal returns empty
- Verify edge labels match between configuration and attributes
- Check fluent configuration direction (In vs Out)
- Use `EdgeLabelResolver.GetAnyEdgeLabel()` to debug label resolution

### Query parameter issues
- Use `CanParameterizeQueries` check before parameterizing
- Parameters are named `__p0`, `__p1`, etc.
- Check `GremlinQueryTranslator.Parameters` dictionary

### LINQ query not translating correctly
- Check if visitor exists for the LINQ operator in `Visitors/` folder
- Verify visitor is registered in `VisitorRegistry`
- Enable debug output in `GremlinQueryProvider` to see generated Gremlin

### InMemory tests failing
- Check if step executor exists for the Gremlin step
- Verify `TinkerGraphQueryParser` can parse the query pattern
- Check `Traverser` context for step dependencies

## ?? Gremlin Studio Versioning — MANDATORY CHECK

**IMPORTANT:** When making ANY changes to the **Gremlin Studio** application (any file under `StardustGremlinStudio/`), you **MUST** ask the user whether the version number should be updated **before finishing your response**. This applies to code changes, XAML changes, new files, and configuration changes. Never skip this step.

The version is defined in `StardustGremlinStudio/Stardust.Paradox.GremlinStudio/Stardust.Paradox.GremlinStudio.csproj` in three properties that **must** be kept in sync:

```xml
<Version>1.3.0</Version>
<FileVersion>1.3.0.0</FileVersion>
<AssemblyVersion>1.3.0.0</AssemblyVersion>
```

- **Patch bump** (e.g., `1.3.0` to `1.3.1`): bug fixes, minor UI tweaks.
- **Minor bump** (e.g., `1.3.0` to `1.4.0`): new features, new panes, new commands.
- **Major bump** (e.g., `1.3.0` to `2.0.0`): breaking changes, major rewrites.

Always confirm with the user before changing the version. Do not bump automatically.
