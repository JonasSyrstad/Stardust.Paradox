# Stardust.Paradox.Data

An Entity Framework-styled tool for accessing Gremlin-based graph databases like Azure Cosmos DB and Apache TinkerPop.

## Overview

Stardust.Paradox.Data provides a modern, type-safe approach to working with graph databases using Gremlin traversals. It offers an intuitive API similar to Entity Framework for relational databases, but designed specifically for graph data structures.

## Key Features

- **Entity Framework-like API** for graph databases
- **Multi-targeting support** for .NET Standard 2.0, .NET 6, 7, 8, and 9
- **Type-safe Gremlin queries** with LINQ-style syntax
- **Support for Azure Cosmos DB** and Apache TinkerPop
- **Vertex and Edge modeling** with attributes and relationships
- **Code generation** for graph entity classes
- **Dependency injection** integration
- **Change tracking** and batch operations
- **Fluent configuration** for relationships
- **In-memory testing** with comprehensive scenario framework

## Installation

```bash
dotnet add package Stardust.Paradox.Data
```

## Quick Start

### 1. Define Your Graph Entities

```csharp
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;

[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }
    string FirstName { get; set; }
    string LastName { get; set; }
    string Email { get; set; }
    string Name { get; set; }
    bool Adult { get; set; }
    DateTime LastUpdated { get; set; }
    EpochDateTime LastUpdatedEpoch { get; set; }
    
    // Navigation properties for graph relationships
    IEdgeCollection<IPerson> Parents { get; }
    IEdgeCollection<IPerson> Children { get; }
    
    [ToWayEdgeLabel("spouse")]
    IEdgeReference<IPerson> Spouse { get; }
    
    IEdgeCollection<ICompany> Employers { get; }
    
    // Custom traversal query
    [GremlinQuery("g.V('{id}').as('s').in('parent').out('parent').where(without('s')).dedup()")]
    IEdgeCollection<IPerson> Siblings { get; }
    
    // Inline serialized collections
    [InlineSerialization(SerializationType.ClearText)]
    ICollection<string> ProgrammingLanguages { get; }
}

[VertexLabel("company")]
public interface ICompany : IVertex
{
    string Id { get; }
    string Name { get; set; }
    string Industry { get; set; }
    
    // Reverse navigation property
    IEdgeCollection<IPerson> Employees { get; }
    
    [InlineSerialization(SerializationType.Base64)]
    ICollection<string> EmailDomains { get; }
}

[EdgeLabel("employment")]
public interface IEmployment : IEdge<IPerson, ICompany>, IDynamicGraphEntity
{
    string Id { get; }
    DateTime HiredDate { get; set; }
    string Manager { get; set; }
    
    [InlineSerialization(SerializationType.ClearText)]
    ICollection<string> Benefits { get; set; }
}
```

### 2. Configure Your Graph Context

```csharp
using Stardust.Paradox.Data;
using Microsoft.Extensions.DependencyInjection;

public class MyGraphContext : GraphContextBase
{
    static MyGraphContext()
    {
        PartitionKeyName = "pk"; // For CosmosDB compatibility
    }
    
    public MyGraphContext(IGremlinLanguageConnector connector) 
        : base(connector, CreateServiceProvider()) 
    { 
    }
    
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Register entity binding for code generation
        services.AddEntityBinding((entity, implementation) => 
        {
            services.AddTransient(entity, implementation);
        });
        
        return services.BuildServiceProvider();
    }
    
    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
    
    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        configuration
            .ConfigureCollection<IPerson>()
                .In(p => p.Parents, "parent").Out<IPerson>(p => p.Children)
                .Out(p => p.Employers, "employer").In<ICompany>(c => c.Employees)
            .ConfigureCollection<ICompany>()
                .In(c => c.Employees, "employer").Out<IPerson>(p => p.Employers)
            .ConfigureCollection<IEmployment>();
            
        return true;
    }
}
```

### 3. Use the Context

```csharp
// Dependency injection setup
services.AddSingleton<IGremlinLanguageConnector>(provider =>
    new GremlinNetLanguageConnector("hostname", "database", "collection", "key"));
services.AddScoped<MyGraphContext>();

// Usage in your application
using var context = serviceProvider.GetService<MyGraphContext>();

// Create and save new entities
var person = context.CreateEntity<IPerson>("person-1");
person.FirstName = "John";
person.LastName = "Doe";
person.Adult = true;

var company = context.CreateEntity<ICompany>("company-1");
company.Name = "Microsoft";
company.Industry = "Technology";

// Create relationships using EdgeGraphSet
var employment = context.Employments.Create(person, company);
employment.HiredDate = DateTime.UtcNow.AddYears(-2);
employment.Manager = "Jane Smith";

await context.SaveChangesAsync();

// Query entities using GraphSet methods
var retrievedPerson = await context.People.GetAsync("person-1");
var allPeople = await context.People.AllAsync();
var techWorkers = await context.PeopleProfiles.GetAsync(g=>g.V().Has("name","John");
```

## Supported Databases

- **Azure Cosmos DB** (Gremlin API)
- **Apache TinkerPop** compatible databases  
- **In-memory graph database** (for testing via Stardust.Paradox.Data.InMemory)

## Advanced Features

### Custom Traversals with GraphContext

```csharp
// Complex traversal queries using VAsync
var friendsOfFriends = await context.VAsync<IPerson>(g =>
    g.V().HasLabel("person")
     .Has("firstName", "John")
     .Out("knows")
     .Out("knows") // Friends of friends
     .Dedup()
     .Limit(10));

// Aggregation queries using ExecuteAsync
var companySizes = await context.ExecuteAsync<dynamic>(g =>
    g.V().HasLabel("company")
     .Group()
     .By("industry")
     .By(__.In("employer").Count()));

// Direct vertex queries by ID
var specificPerson = await context.VAsync<IPerson>("person-1");

// Partition key support for Cosmos DB
var personWithPK = await context.VAsync<IPerson>("person-1", "partition-key");
```

### IGraphSet<T> Operations

```csharp
// Get by ID
var person = await context.People.GetAsync("person-1");

// Get by ID with partition key (Cosmos DB)
var person = await context.People.GetAsync("person-1", "partition-key");

// Filter operations  
var techWorkers = await context.People.FilterAsync(p => p.FirstName, "John");

// Get all with pagination
var allPeople = await context.People.AllAsync();
var pagedPeople = await context.People.GetAsync(page: 0, pageSize: 20);

// Create operations
var newPerson = context.People.Create(); // Auto-generated ID
var specificPerson = context.People.Create("specific-id");
var initializedPerson = context.People.Create("id", p => {
    p.FirstName = "John";
    p.LastName = "Doe";
    return p;
});

// Delete operations
await context.People.DeleteAsync("person-1");
await context.People.DeleteAsync("person-1", "partition-key");

// Attach existing entities
context.People.Attach(existingPerson);
```

### IEdgeGraphSet<T> Operations

```csharp
// Create edges between vertices
var employment = context.Employments.Create(person, company);

// Get edge by vertices
var employment = await context.Employments.GetAsync("person-1", "company-1");

// Get edges by vertex ID
var personEmployments = await context.Employments.GetByInIdAsync("person-1");
var companyEmployments = await context.Employments.GetByOutIdAsync("company-1");
```

### Change Tracking and Events

```csharp
// Event handling
context.SavingChanges += (sender, args) => {
    Console.WriteLine($"Saving {args.TrackedItems.Count()} items");
};

context.ChangesSaved += (sender, args) => {
    Console.WriteLine($"Saved successfully. RU consumed: {context.ConsumedRU}");
};

context.SaveChangesError += (sender, args) => {
    Console.WriteLine($"Save failed: {args.Error.Message}");
    Console.WriteLine($"Failed query: {args.FailedUpdateStatement}");
};

// Automatic change tracking
var person = await context.VAsync<IPerson>("person-1");
person.FirstName = "Jane"; // Change is tracked automatically
await context.SaveChangesAsync(); // Only changed properties are updated
```

### Dynamic Properties

```csharp
// For entities implementing IDynamicGraphEntity
public interface IPerson : IVertex, IDynamicGraphEntity
{
    // ... other properties
}

// Usage
if (person is IDynamicGraphEntity dynamicPerson)
{
    dynamicPerson.SetProperty("customField", "customValue");
    dynamicPerson.SetProperty("rating", 4.5);
    
    await context.SaveChangesAsync();
    
    var customValue = dynamicPerson.GetProperty("customField");
    var propertyNames = dynamicPerson.DynamicPropertyNames;
}
```

## Configuration Examples

### Azure Cosmos DB Connection

```csharp
using Stardust.Paradox.Data.Providers.Gremlin;

// Cosmos DB connection
var connector = new GremlinNetLanguageConnector(
    gremlinHostname: "your-account.gremlin.cosmos.azure.com",
    databaseName: "your-database",
    graphName: "your-collection", 
    accessKey: "your-primary-key");
```

### Apache TinkerPop Connection

```csharp
// TinkerPop Server connection
var connector = new GremlinNetLanguageConnector(
    gremlinHostname: "localhost", 
    username: "", 
    password: "",
    port: 8182,
    enableSsl: false);
```

### In-Memory Testing

```csharp
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Factory;

// In-memory connector for testing
var connector = InMemoryConnectorFactory.CreateForTesting();

// Or with predefined scenarios
var connector = InMemoryConnectorFactory.CreateSocialNetwork();
using var context = new MyGraphContext(connector);

// Perfect for unit testing - no external dependencies!
```

## Testing with InMemory Provider

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stardust.Paradox.Data.InMemory.Factory;

[TestClass]
public class GraphTests
{
    private MyGraphContext _context;
    
    [TestInitialize]
    public void Setup()
    {
        var connector = InMemoryConnectorFactory.CreateForTesting();
        _context = new MyGraphContext(connector);
    }
    
    [TestMethod]
    public async Task TestPersonCreation()
    {
        // Arrange
        var person = _context.CreateEntity<IPerson>("test-1");
        person.FirstName = "John";
        person.Adult = true;
        
        // Act
        await _context.SaveChangesAsync();
        var retrieved = await _context.VAsync<IPerson>("test-1");
        
        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual("John", retrieved.FirstName);
        Assert.IsTrue(retrieved.Adult);
    }
    
    [TestMethod]
    public async Task TestRelationshipCreation()
    {
        // Arrange
        var person = _context.CreateEntity<IPerson>("person-1");
        person.FirstName = "Alice";
        
        var company = _context.CreateEntity<ICompany>("company-1");
        company.Name = "TechCorp";
        
        // Create employment edge
        var employment = _context.Employments.Create(person, company);
        employment.Manager = "Bob";
        
        // Act
        await _context.SaveChangesAsync();
        
        // Assert - Test navigation
        var employers = await person.Employers.ToVerticesAsync();
        Assert.AreEqual(1, employers.Count());
        Assert.AreEqual("TechCorp", employers.First().Name);
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        _context?.Dispose();
    }
}
```

## Performance and Monitoring

```csharp
// Monitor Request Units (Cosmos DB)
Console.WriteLine($"RU consumed: {context.ConsumedRU}");

// Enable parallel save execution
GremlinContext.ParallelSaveExecution = true;

// Performance tracking with events
context.SavingChanges += (sender, args) => {
    var stopwatch = Stopwatch.StartNew();
    // Track save performance
};
```

## Error Handling

```csharp
try
{
    await context.SaveChangesAsync();
}
catch (GraphExecutionException ex)
{
    Console.WriteLine($"Save failed: {ex.Message}");
    Console.WriteLine($"Failed query: {ex.SaveEventArgs.FailedUpdateStatement}");
    
    // Inspect tracked entities that failed
    foreach (var item in ex.SaveEventArgs.TrackedItems)
    {
        Console.WriteLine($"Failed item: {item.EntityKey}");
    }
}
```

## Advanced Configuration

### Fluent Model Configuration

```csharp
protected override bool InitializeModel(IGraphConfiguration configuration)
{
    configuration
        .ConfigureCollection<IPerson>()
            // Bidirectional friendship
            .Out(p => p.Friends, "knows").In<IPerson>(p => p.Friends)
            // Employment relationship  
            .Out(p => p.Employers, "employer").In<ICompany>(c => c.Employees)
            // Custom queries
            .AddQuery(p => p.Siblings, g => 
                g.V("{id}").As("s").In("parent").Out("parent").Where(p.Without("s")).Dedup())
        .ConfigureCollection<ICompany>()
            .In(c => c.Employees, "employer").Out<IPerson>(p => p.Employers)
        .ConfigureCollection<IEmployment>();
        
    return true;
}
```

### Special Data Types

```csharp
using Stardust.Paradox.Data.Annotations.DataTypes;

[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id { get; }
    string Name { get; set; }
    
    // Epoch timestamp support
    EpochDateTime CreatedAt { get; set; }
    
    // Inline serialized collections
    [InlineSerialization(SerializationType.ClearText)]
    ICollection<string> Skills { get; set; }
    
    // Complex properties
    MyComplexProperty Address { get; set; }
}

public class MyComplexProperty : IComplexProperty
{
    private string _street;
    
    public string Street
    {
        get => _street;
        set
        {
            if (value == _street) return;
            _street = value;
            OnPropertyChanged();
        }
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public void StartNotifications() { }
    public void EndNotifications() { }
}
```