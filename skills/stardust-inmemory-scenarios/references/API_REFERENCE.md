# API Reference — Stardust.Paradox.Data.InMemory

## InMemoryGremlinLanguageConnector

**Namespace:** `Stardust.Paradox.Data.InMemory`
**Implements:** `IGremlinLanguageConnector`, `IDisposable`

### Constructors

```csharp
// Default (empty database, default options)
new InMemoryGremlinLanguageConnector()

// With options
new InMemoryGremlinLanguageConnector(InMemoryDatabaseOptions options)

// With shared database instance
new InMemoryGremlinLanguageConnector(InMemoryGraphDatabase database)

// With shared database and options
new InMemoryGremlinLanguageConnector(InMemoryGraphDatabase database, InMemoryDatabaseOptions options)
```

### Factory Methods (static)

```csharp
InMemoryGremlinLanguageConnector.Create()
InMemoryGremlinLanguageConnector.Create(InMemoryDatabaseOptions options)
InMemoryGremlinLanguageConnector.Create(Action<InMemoryDatabaseOptions> configure)
InMemoryGremlinLanguageConnector.CreateOptimized()
InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator()
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Database` | `InMemoryGraphDatabase` | Direct access to the in-memory graph |
| `CanParameterizeQueries` | `bool` | Always `true` |
| `ConsumedRU` | `double` | Total simulated Request Units consumed |
| `Options` | `InMemoryDatabaseOptions` | The configuration options |

### Key Methods

```csharp
// Execute Gremlin query
Task<IEnumerable<dynamic>> ExecuteAsync(string query, Dictionary<string, object> parametrizedValues)

// Register custom response for regex-matched queries
void RegisterCustomResponse(string queryPattern, Func<string, Dictionary<string, object>, IEnumerable<dynamic>> responseFunc)

// Clear all data
void Clear()
```

---

## InMemoryGraphDatabase

**Namespace:** `Stardust.Paradox.Data.InMemory.Core`

### Key Methods

```csharp
// Vertex operations
InMemoryVertex AddVertex(string label, string id)
InMemoryVertex GetVertex(string id)
IEnumerable<InMemoryVertex> GetVertices()
IEnumerable<InMemoryVertex> GetVerticesByLabel(string label)
bool RemoveVertex(string id)

// Edge operations
InMemoryEdge AddEdge(string label, string outVertexId, string inVertexId, string id = null)
InMemoryEdge GetEdge(string id)
IEnumerable<InMemoryEdge> GetEdges()
bool RemoveEdge(string id)

// Traversal helpers
IEnumerable<InMemoryVertex> GetOutVertices(string vertexId, string edgeLabel)
IEnumerable<InMemoryVertex> GetInVertices(string vertexId, string edgeLabel)

// Custom responses
void RegisterCustomResponse(string pattern, Func<string, Dictionary<string, object>, IEnumerable<dynamic>> func)
```

---

## InMemoryVertex

**Namespace:** `Stardust.Paradox.Data.InMemory.Core`

```csharp
string Id { get; }
string Label { get; }
Dictionary<string, object> Properties { get; }

void SetProperty(string key, object value)
T GetProperty<T>(string key)
dynamic ToGremlinResponse()  // Convert to the dynamic format returned by ExecuteAsync
```

---

## InMemoryEdge

**Namespace:** `Stardust.Paradox.Data.InMemory.Core`

```csharp
string Id { get; }
string Label { get; }
string OutVertexId { get; }
string InVertexId { get; }
Dictionary<string, object> Properties { get; }

dynamic ToGremlinResponse()
```

---

## Scenario Framework

### IInMemoryScenarioProvider

```csharp
public interface IInMemoryScenarioProvider
{
    string ScenarioName { get; }
    string Description { get; }
    void ConfigureScenario(InMemoryGraphDatabase database);
}
```

### InMemoryScenarioProviderBase

Abstract base class implementing `IInMemoryScenarioProvider`.

```csharp
public abstract class InMemoryScenarioProviderBase : IInMemoryScenarioProvider
{
    public abstract string ScenarioName { get; }
    public abstract string Description { get; }

    // Override to return vertex/edge definitions
    protected virtual (ScenarioVertexDefinition[] vertices,
                       ScenarioEdgeDefinition[] edges) GetScenarioData()

    // Override for custom response patterns
    protected virtual void ConfigureCustomResponses(InMemoryGraphDatabase database)

    // Helper to create property KeyValuePairs
    protected KeyValuePair<string, object> Prop(string key, object value)

    // Helper to create property arrays from tuples
    protected KeyValuePair<string, object>[] Props(params (string key, object value)[] properties)
}
```

### ScenarioVertexDefinition

```csharp
public class ScenarioVertexDefinition
{
    public string Id { get; set; }
    public string Label { get; set; }
    public KeyValuePair<string, object>[] Properties { get; set; }

    public ScenarioVertexDefinition(string id, string label,
        params KeyValuePair<string, object>[] properties)
}
```

### ScenarioEdgeDefinition

```csharp
public class ScenarioEdgeDefinition
{
    public string Id { get; set; }
    public string Label { get; set; }
    public string OutVertexId { get; set; }
    public string InVertexId { get; set; }
    public KeyValuePair<string, object>[] Properties { get; set; }

    // Without explicit ID (auto-generated)
    public ScenarioEdgeDefinition(string label, string outVertexId, string inVertexId,
        params KeyValuePair<string, object>[] properties)

    // With explicit ID
    public ScenarioEdgeDefinition(string id, string label, string outVertexId, string inVertexId,
        params KeyValuePair<string, object>[] properties)
}
```

### InMemoryScenarioRegistry (static)

```csharp
static void Register(IInMemoryScenarioProvider scenario)
static void Register(params IInMemoryScenarioProvider[] scenarios)
static IInMemoryScenarioProvider GetScenario(string scenarioName)
static bool IsRegistered(string scenarioName)
static IReadOnlyDictionary<string, IInMemoryScenarioProvider> GetAllScenarios()
static IEnumerable<string> GetScenarioNames()
static bool Unregister(string scenarioName)
static void Clear()
static void EnsureBuiltInScenariosRegistered()
static bool ApplyScenario(InMemoryGraphDatabase database, string scenarioName)
static int ApplyScenarios(InMemoryGraphDatabase database, string[] scenarioNames)
```

---

## Extension Methods

### InMemoryScenarioExtensions

**Namespace:** `Stardust.Paradox.Data.InMemory.Extensions`

```csharp
// Static factory — create with scenario by name
static InMemoryGremlinLanguageConnector CreateWithScenario(string scenarioName, InMemoryDatabaseOptions options = null)
static InMemoryGremlinLanguageConnector CreateWithScenarios(string[] scenarioNames, InMemoryDatabaseOptions options = null)

// Fluent — apply scenario to existing connector
InMemoryGremlinLanguageConnector WithScenario(this ..., string scenarioName)
InMemoryGremlinLanguageConnector WithScenarios(this ..., params string[] scenarioNames)
InMemoryGremlinLanguageConnector WithScenario<TScenario>(this ...) where TScenario : IInMemoryScenarioProvider, new()
InMemoryGremlinLanguageConnector WithScenario(this ..., IInMemoryScenarioProvider scenario)
InMemoryGremlinLanguageConnector ClearScenarios(this ...)

// Discovery
static Dictionary<string, string> GetAvailableScenarios()
static string[] ListAvailableScenarios()
```

### InMemoryGremlinExtensions

```csharp
// Pre-built sample data
InMemoryGremlinLanguageConnector WithSampleData(this ...)
InMemoryGremlinLanguageConnector WithCommonResponses(this ...)

// Bulk loading
InMemoryGremlinLanguageConnector LoadData(this ...,
    IEnumerable<(string id, string label, Dictionary<string, object> properties)> vertices,
    IEnumerable<(string id, string label, string outV, string inV, Dictionary<string, object> properties)> edges = null)

// Performance testing
InMemoryGremlinLanguageConnector WithBulkData(this ..., int vertexCount = 1000, int edgeCount = 2000)
InMemoryGremlinLanguageConnector WithPerformanceSettings(this ...)

// Diagnostics
string ToDebugString(this ...)
bool IsEmpty(this ...)
```

### InMemoryConnectorFactory

**Namespace:** `Stardust.Paradox.Data.InMemory.Factory`

```csharp
static InMemoryGremlinLanguageConnector Create(InMemoryGraphDatabase database)
static InMemoryGremlinLanguageConnector Create(InMemoryGraphDatabase database, InMemoryDatabaseOptions options)
static InMemoryGremlinLanguageConnector CreateWithScenario(string scenarioName, Action<InMemoryDatabaseOptions> configure = null)
static InMemoryGremlinLanguageConnector CreateWithScenarios(string[] scenarioNames, Action<InMemoryDatabaseOptions> configure = null)
static InMemoryGremlinLanguageConnector CreateForTesting(Action<InMemoryDatabaseOptions> configure = null)
static InMemoryGremlinLanguageConnector CreateSocialNetwork(Action<InMemoryDatabaseOptions> configure = null)
static InMemoryGremlinLanguageConnector CreateECommerce(Action<InMemoryDatabaseOptions> configure = null)
```

---

## InMemoryDatabaseOptions

**Namespace:** `Stardust.Paradox.Data.InMemory.Core`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableQueryLogging` | `bool` | `false` | Log queries to console |
| `EnableDebugLogging` | `bool` | `false` | Verbose debug output |
| `SimulatedRUPerQuery` | `double` | `1.0` | Simulated RU per query |
| `QueryTimeout` | `TimeSpan` | `30s` | Query execution timeout |
| `AutoGenerateIds` | `bool` | `true` | Auto-generate IDs for vertices/edges |
| `ValidateEdgeVertices` | `bool` | `true` | Validate vertex existence for edges |
| `CaseSensitiveProperties` | `bool` | `false` | Property name matching |
| `CaseSensitiveLabels` | `bool` | `false` | Label matching |
| `MaxVertexCount` | `int` | `0` | Max vertices (0 = unlimited) |
| `MaxEdgeCount` | `int` | `0` | Max edges (0 = unlimited) |
| `TrackStatistics` | `bool` | `false` | Track database statistics |
| `AllowDuplicateEdges` | `bool` | `true` | Allow duplicate edges |
| `CascadeDeleteEdges` | `bool` | `true` | Delete edges on vertex removal |

---

## GraphContextBase Integration

**Namespace:** `Stardust.Paradox.Data`

Constructor signature used by all context subclasses:

```csharp
protected GraphContextBase(IGremlinLanguageConnector connector, IServiceProvider serviceProvider)
protected GraphContextBase(IGremlinLanguageConnector connector, IServiceProvider serviceProvider, ILogging logger)
```

Required DI setup for entity resolution:

```csharp
var services = new ServiceCollection();
services.AddEntityBinding((entity, implementation) =>
{
    services.AddTransient(entity, implementation);
});
var sp = services.BuildServiceProvider();
```

Key context members for tests:

```csharp
IGraphSet<T> GraphSet<T>()          // Repository for vertex type T
IEdgeGraphSet<T> EdgeGraphSet<T>()  // Repository for edge type T
T CreateEntity<T>(string id)        // Create a new tracked entity
Task<T> VAsync<T>(string id)        // Load vertex by ID
Task SaveChangesAsync()             // Persist all tracked changes
```
