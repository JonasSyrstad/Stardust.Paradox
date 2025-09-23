# Stardust.Paradox.Data.InMemory

[![NuGet Version](https://img.shields.io/nuget/v/Stardust.Paradox.Data.InMemory?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.InMemory/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Stardust.Paradox.Data.InMemory?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.InMemory/)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue?style=flat-square)](https://github.com/JonasSyrstad/Stardust.Paradox/blob/main/LICENSE)

A high-performance, feature-rich in-memory Gremlin graph database implementation for testing, development, and production debugging. Perfect for unit testing, integration testing, rapid prototyping, and creating safe local environments for troubleshooting production issues.

## ? Features

- ?? **High Performance**: Lightning-fast in-memory graph operations with optimized query execution
- ?? **Scenario Framework**: Pre-built test data scenarios for common domains plus production data import
- ?? **Gremlin Compatible**: Full support for Gremlin traversal language and TinkerPop protocol
- ?? **Easy Integration**: Seamless integration with existing Paradox applications
- ??? **Test Friendly**: Designed specifically for testing scenarios with comprehensive validation
- ?? **Fluent API**: Intuitive, chainable configuration methods
- ?? **Performance Tracking**: Built-in query performance monitoring and statistics
- ?? **Zero Dependencies**: No external database setup required for development
- ?? **Production Debugging**: Export real database scenarios for local debugging
- ??? **Server Mode**: Built-in Gremlin server with WebSocket and TCP support
- ?? **Live Data Import**: Import scenarios directly from CosmosDB and other Gremlin databases

## ?? Installation

### Package Manager
```powershell
Install-Package Stardust.Paradox.Data.InMemory -Version 1.0.0-preview.9
```

### .NET CLI
```bash
dotnet add package Stardust.Paradox.Data.InMemory --version 1.0.0-preview.9
```

### PackageReference
```xml
<PackageReference Include="Stardust.Paradox.Data.InMemory" Version="1.0.0-preview.9" />
```

## ?? Quick Start

### Basic Usage
```csharp
using Stardust.Paradox.Data.InMemory;

// Create an empty in-memory database
var connector = InMemoryGremlinLanguageConnector.Create();

// Add some data
await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());
await connector.ExecuteAsync("g.addV('person').property('name', 'Jane')", new Dictionary<string, object>());
await connector.ExecuteAsync("g.V().has('name', 'John').addE('knows').to(g.V().has('name', 'Jane'))", new Dictionary<string, object>());

// Query the data
var people = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
var friendships = await connector.ExecuteAsync("g.E().hasLabel('knows')", new Dictionary<string, object>());
```

### Using Built-in Scenarios
```csharp
using Stardust.Paradox.Data.InMemory;

// Create with pre-built social network data
var connector = InMemoryConnectorFactory.CreateSocialNetwork();

// Query immediately - data is already there!
var friends = await connector.ExecuteAsync("g.V('john').out('knows')", new Dictionary<string, object>());
var mutualFriends = await connector.ExecuteAsync("g.V('john').out('knows').where(in('knows').hasId('jane'))", new Dictionary<string, object>());
```

## ?? Production Debugging with ScenarioConnector

The **ScenarioConnector** tool allows you to export real production data scenarios for safe local debugging:

### Overview
The ScenarioConnector is a command-line tool that connects to your production CosmosDB or other Gremlin databases and exports specific data subsets as InMemory scenarios. This enables you to:

- ?? **Debug Production Issues Locally**: Reproduce production problems in a safe, isolated environment
- ??? **Safe Data Access**: Read-only connections ensure no accidental modifications to production data
- ?? **Selective Export**: Export only the data you need via Gremlin queries or vertex IDs
- ?? **Repeatable Scenarios**: Save exported scenarios as reusable test fixtures
- ?? **Fast Iteration**: Debug and test fixes rapidly without production dependencies

### Installation and Usage

1. **Build the ScenarioConnector**:
   ```bash
   cd src/Stardust.Paradox.Data.ScenarioConnector
   dotnet build
   dotnet run
   ```

2. **Add a Production Connection**:
   ```
   Main Menu:
   2. (A)dd new connection
   
   Connection name: Production-Issues
   Hostname: myaccount.gremlin.cosmosdb.azure.com
   Database name: GraphDB
   Graph name: social
   Access key: [your-read-only-key]
   ```

3. **Export a Scenario**:
   ```
   1. (C)onnect to database and export scenarios
   
   Export Menu:
   1. Export by (Q)uery
   
   Enter Gremlin query: g.V().has('userId', 'problem-user-123').bothV().dedup().limit(50)
   Enter scenario name: ProblemUserNetwork
   Enter description: Network around problematic user for debugging
   ```

4. **Use in Your Tests**:
   ```csharp
   // Copy the generated .cs file to your test project
   InMemoryScenarioRegistry.Register(new ProblemUserNetworkScenario());
   
   // Create connector with production data
   var connector = InMemoryConnectorFactory.CreateWithScenario("ProblemUserNetwork");
   
   // Debug the issue locally
   var problematicData = await connector.ExecuteAsync(
       "g.V().has('userId', 'problem-user-123').out('relationship')", 
       new Dictionary<string, object>());
   ```

### ScenarioConnector Features

- **?? Secure Storage**: Connection strings are encrypted and stored locally
- **? Connection Testing**: Verify connections before use with read-only validation
- **?? Progress Tracking**: Visual progress bars for large exports
- **?? Complex Query Support**: Handles advanced Gremlin queries and transformations
- **?? Multiple Export Formats**: JSON scenarios and C# class files
- **??? Debug Logging**: Detailed logging for troubleshooting export issues
- **? Performance Monitoring**: Track export performance and database statistics

### Production Debugging Workflow

1. **Identify the Issue**: Determine which data subset is related to the production issue
2. **Export Scenario**: Use ScenarioConnector to export relevant vertices and edges
3. **Local Debugging**: Load the scenario in your development environment
4. **Reproduce Issue**: Run your application logic against the exported data
5. **Develop Fix**: Implement and test the fix locally
6. **Validate Solution**: Verify the fix works with the production scenario
7. **Deploy Confidently**: Deploy knowing the fix handles the specific production data

## ?? Built-in Scenarios

The package includes several pre-built scenarios for common testing needs:

| Scenario | Description | Vertex Types | Edge Types |
|----------|-------------|--------------|------------|
| **BasicSocialNetwork** | Social media platform | person, post | knows, authored, likes |
| **SimpleECommerce** | E-commerce platform | customer, product, order | purchased, contains, placed |
| **OrganizationHierarchy** | Corporate structure | employee, department | manages, works_for, assigned_to |
| **UserRoleManagement** | RBAC system | user, role, permission | has_role, has_permission |
| **GraphTraversalTest** | Complex patterns | node, typeA, typeB | connects, links, self |

### Scenario Factory Methods
```csharp
// Convenience methods for common scenarios
var socialConnector = InMemoryConnectorFactory.CreateSocialNetwork();
var ecommerceConnector = InMemoryConnectorFactory.CreateECommerce();
var orgConnector = InMemoryConnectorFactory.CreateOrganization();
var userMgmtConnector = InMemoryConnectorFactory.CreateUserManagement();

// Generic method
var connector = InMemoryConnectorFactory.CreateWithScenario("BasicSocialNetwork");

// Multiple scenarios combined
var multiConnector = InMemoryConnectorFactory.CreateWithScenarios(
    new[] { "BasicSocialNetwork", "SimpleECommerce" });
```

## ?? Advanced Configuration

### Database Options
```csharp
var connector = InMemoryConnectorFactory.CreateSocialNetwork(options =>
{
    options.EnableDebugLogging = true;        // Log all queries
    options.AutoGenerateIds = true;          // Auto-generate vertex/edge IDs
    options.ValidateEdgeVertices = true;     // Ensure vertices exist for edges
    options.CaseSensitiveLabels = false;     // Case-insensitive labels
    options.MaxVertexCount = 10000;          // Limit number of vertices
    options.TrackStatistics = true;          // Enable performance tracking
});
```

### Fluent API
```csharp
var connector = InMemoryGremlinLanguageConnector.Create()
    .WithScenario("BasicSocialNetwork")
    .WithScenario("SimpleECommerce");

// Clear and apply different scenarios
connector.ClearScenarios()
        .WithScenarios("UserRoleManagement", "OrganizationHierarchy");
```

### Server Mode Configuration
```csharp
// Start an in-memory Gremlin server
var server = new GremlinServer(options =>
{
    options.Port = 8182;
    options.EnableWebSockets = true;
    options.EnableTcp = true;
    options.GraphSONVersion = GraphSONVersion.V3_0;
});

await server.StartAsync();

// Connect with standard Gremlin clients
var client = new GremlinClient(new GremlinServer("localhost", 8182));