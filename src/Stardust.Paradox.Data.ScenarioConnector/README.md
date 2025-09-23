# Stardust Paradox Scenario Connector

A command-line tool for exporting graph data from CosmosDB to InMemory database scenario files. This tool allows you to extract subsets of your CosmosDB graph database and convert them into scenario files that can be used with the Stardust.Paradox.Data.InMemory testing framework.

## Features

- 🔐 **Secure Connection Storage**: Connection strings are encrypted and stored securely using Windows Data Protection API
- 🔍 **Flexible Export Options**: Export by Gremlin query or vertex IDs
- 📁 **Multiple Output Formats**: Generate both JSON scenario files and C# class files
- 🔗 **Edge Discovery**: Automatically finds all edges between exported vertices
- 📊 **Read-Only Operations**: Only performs read operations on your CosmosDB database
- 🎯 **CosmosDB Optimized**: Specifically designed for Azure CosmosDB Gremlin API

## Installation

Build the project using .NET 8:

```bash
dotnet build
dotnet run
```

## Usage

### 1. Start the Application

```bash
dotnet run
```

You'll be presented with the main menu:

```
=== Stardust Paradox Scenario Connector ===
Export scenarios from CosmosDB to InMemory database format

Main Menu:
1. (C)onnect to database and export scenarios
2. (A)dd new connection
3. (R)emove connection
4. (L)ist all connections
5. (Q)uit

Enter your choice:
```

### 2. Add a Connection

Choose option 2 to add a new CosmosDB connection:

```
Add New CosmosDB Connection
Connection name: MyProductionDB
Hostname (e.g., myaccount.gremlin.cosmosdb.azure.com): myaccount.gremlin.cosmosdb.azure.com
Database name: MyGraphDatabase
Graph name: MyGraph
Access key: ********************************
```

The access key is entered securely (hidden) and encrypted before storage.

### 3. Export Scenarios

Choose option 1 to connect and export scenarios. You'll have two export options:

#### Export by Query

Enter a Gremlin query to find the vertices you want to export:

```
Examples:
- g.V().hasLabel('person').limit(100)
- g.V().has('category', 'product').has('active', true)
- g.V().has('department', 'engineering').out('manages')
```

#### Export by Vertex IDs

Provide a list of specific vertex IDs to export:

```
vertex1, vertex2, vertex3
user123
product456
END
```

### 4. Output Files

The tool generates two files for each export:

1. **JSON File** (`ScenarioName.json`): Raw scenario data that can be loaded programmatically
2. **C# Class File** (`ScenarioName.cs`): Ready-to-use scenario provider class

Files are saved to: `Documents\StardustParadox\ExportedScenarios\`

## Using Exported Scenarios

### Option 1: Using the C# Class

1. Copy the generated `.cs` file to your test project
2. Register the scenario:
   ```csharp
   InMemoryScenarioRegistry.Register(new MyExportedScenario());
   ```
3. Use in tests:
   ```csharp
   var connector = InMemoryConnectorFactory.CreateWithScenario("MyExportedScenario");
   ```

### Option 2: Loading JSON Dynamically

```csharp
var scenario = await ScenarioExporter.LoadScenarioAsync("path/to/scenario.json");
var provider = ScenarioConverter.ConvertToInMemoryScenario(scenario);
var connector = InMemoryGremlinLanguageConnector.Create().WithScenario(provider);
```

## Example Workflow

1. **Development**: Use production data to create realistic test scenarios
2. **Testing**: Import subsets of production data for integration tests
3. **CI/CD**: Use exported scenarios for consistent, fast tests
4. **Debugging**: Export problematic data patterns for investigation

## Security

- Connection strings are encrypted using Windows DPAPI (Data Protection API)
- Only the current user can decrypt stored connections
- The tool only performs read operations on your database
- Access keys are never displayed in plain text after entry

## Example Export Scenarios

### User Social Network
```gremlin
g.V().hasLabel('user').has('verified', true).limit(50)
```

### Product Catalog Subset
```gremlin
g.V().hasLabel('product').has('category', 'electronics').has('inStock', true)
```

### Organization Hierarchy
```gremlin
g.V().hasLabel('employee').has('department', 'engineering').out('reportsTo').path()
```

## Generated Files Structure

### JSON Format
```json
{
  "Name": "MyScenario",
  "Description": "Exported scenario description",
  "ExportedAt": "2025-01-XX",
  "SourceConnection": "MyProductionDB",
  "Vertices": [...],
  "Edges": [...],
  "Metadata": {...}
}
```

### C# Class Template
```csharp
public class MyScenarioProvider : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "MyScenario";
    public override string Description => "Exported scenario description";
    
    protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        // Generated vertices and edges
    }
}
```

## Troubleshooting

### Connection Issues
- Verify hostname format: `account.gremlin.cosmosdb.azure.com`
- Ensure access key has read permissions
- Check firewall settings for CosmosDB

### Export Issues
- Verify Gremlin query syntax
- Check vertex IDs exist in database
- Ensure sufficient permissions for read operations

### File Access Issues
- Check write permissions to Documents folder
- Ensure disk space is available
- Verify .NET 8 runtime is installed

## Requirements

- .NET 8.0 or later
- Windows (for secure credential storage)
- Azure CosmosDB with Gremlin API
- Read access to target CosmosDB database

## Related Projects

- [Stardust.Paradox.Data](../Stardust.Paradox.Data/) - Core graph database framework
- [Stardust.Paradox.Data.InMemory](../Stardust.Paradox.Data.InMemory/) - InMemory testing database
- [Stardust.Paradox.Data.Providers.Gremlin](../Stardust.Paradox.Data.Providers.Gremlin/) - CosmosDB provider

## CosmosDB Partition Key Support

The tool is designed to work with CosmosDB databases that use partition keys. CosmosDB requires special handling for composite keys (partition key + vertex ID).

### Partition Key Formats

When exporting by vertex IDs, use these formats:

**Simple Vertex IDs:**
```
vertex1
vertex2
vertex3
```

**Composite Keys (Partition Key + Vertex ID):**
```
partitionKey|vertexId
user|123
product|456
order|789
```

### Query Limitations

Due to CosmosDB's partition key requirements, avoid these query patterns:
- `g.V(['id1', 'id2'])` - Array syntax not supported
- Complex multi-vertex queries with ID arrays

Use these instead:
- `g.V().hasLabel('person').limit(100)` - Label-based queries
- `g.V().has('category', 'product')` - Property-based queries
- `g.V('singleId')` - Single vertex queries

### Edge Discovery Strategy

The tool uses a safe approach for edge discovery:
1. **Primary Method**: Individual vertex queries (`g.V('id').bothE()`)
2. **Fallback Method**: Query all edges and filter in memory
3. **Error Handling**: Graceful degradation with warnings

This ensures compatibility with both partitioned and non-partitioned CosmosDB instances.

## CosmosDB Compatibility

The tool is specifically designed for Azure CosmosDB Gremlin API and uses only supported Gremlin methods:

### Supported Operations
- ✅ `valueMap(true)` - Vertex properties
- ✅ `valueMap()` - Edge properties  
- ✅ `bothE()`, `outE()`, `inE()` - Edge traversals
- ✅ `hasLabel()`, `has()` - Filtering
- ✅ `limit()` - Result limiting

### Avoided Operations
- ❌ `elementMap()` - Not supported in CosmosDB
- ❌ Complex array syntax - Composite key issues
- ❌ Advanced Gremlin 3.5+ methods

### Property Handling
The tool uses a two-step approach for complete data capture:
1. **Structure First**: Get vertex/edge basic information
2. **Properties Second**: Fetch properties separately when available

This ensures compatibility while maintaining data fidelity.