# Stardust.Paradox.Data.Linq

[![NuGet Version](https://img.shields.io/nuget/v/Stardust.Paradox.Data.Linq?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.Linq/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Stardust.Paradox.Data.Linq?style=flat-square)](https://www.nuget.org/packages/Stardust.Paradox.Data.Linq/)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue?style=flat-square)](https://github.com/JonasSyrstad/Stardust.Paradox/blob/main/LICENSE)

**Powerful LINQ to Gremlin query provider** for Stardust.Paradox graph database framework. Write natural C# LINQ queries that are automatically translated to efficient Gremlin traversals for CosmosDB, TinkerPop, and other Gremlin-compatible graph databases.

## ? Features

- ?? **Full LINQ Support**: Where, Select, OrderBy, GroupBy, Join, and more
- ?? **Type-Safe Queries**: Strong typing with IntelliSense support
- ?? **Graph Traversals**: Native support for graph operations (Out, In, OutE, InE, etc.)
- ? **Query Optimization**: Automatic translation to efficient Gremlin queries
- ?? **Lambda Expressions**: Type-safe lambda expressions for graph traversals
- ?? **Aggregations**: Sum, Count, Average, Min, Max with full LINQ syntax
- ?? **Pattern Matching**: Match and select operations for complex graph patterns
- ?? **Set Operations**: Union, Intersect, Except with proper deduplication
- ?? **Branching**: Union and repeat operations with type preservation
- ?? **Async Support**: Full async/await pattern support
- ?? **Performance**: Optimized query generation for minimal server roundtrips

## ?? Installation

### Package Manager
```powershell
Install-Package Stardust.Paradox.Data.Linq
```

### .NET CLI
```bash
dotnet add package Stardust.Paradox.Data.Linq
```

### PackageReference
```xml
<PackageReference Include="Stardust.Paradox.Data.Linq" Version="1.0.0" />
```

## ?? Quick Start

### Basic LINQ Queries

```csharp
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Linq;

public class MyGraphContext : GraphContextBase
{
    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    
    // Constructor and configuration...
}

// Basic where clause
var activePeople = context.People
    .AsQueryable()
 .Where(p => p.IsActive)
    .ToList();

// Multiple conditions
var seniorDevelopers = context.People
    .AsQueryable()
    .Where(p => p.Age > 30 && p.Role == "Developer")
    .ToList();

// String operations
var searchResults = context.People
    .AsQueryable()
    .Where(p => p.Email.Contains("@example.com"))
    .ToList();

// Ordering
var sortedPeople = context.People
.AsQueryable()
    .OrderBy(p => p.LastName)
    .ThenBy(p => p.FirstName)
    .ToList();
```

### Projections and Selections

```csharp
// Select single property
var names = context.People
    .AsQueryable()
    .Select(p => p.Name)
    .ToList();

// Select anonymous type
var personInfo = context.People
    .AsQueryable()
    .Where(p => p.Age > 25)
    .Select(p => new { p.Name, p.Age, p.Email })
    .ToList();

// Complex projection
var detailedInfo = context.People
    .AsQueryable()
    .Where(p => p.IsActive)
    .OrderBy(p => p.Score)
    .Select(p => new 
    {
        FullName = p.Name,
   Contact = p.Email,
        Demographics = new { p.Age, p.City },
        IsHighScorer = p.Score > 90
    })
    .ToList();
```

### Aggregations

```csharp
// Count
var totalPeople = context.People
    .AsQueryable()
.Count();

var activePeopleCount = context.People
  .AsQueryable()
    .Count(p => p.IsActive);

// Average, Sum, Min, Max
var averageAge = context.People
    .AsQueryable()
 .Average(p => p.Age);

var totalScore = context.People
    .AsQueryable()
    .Sum(p => p.Score);

var youngestAge = context.People
    .AsQueryable()
  .Min(p => p.Age);

var highestScore = context.People
    .AsQueryable()
    .Max(p => p.Score);

// Any and All
var hasActiveUsers = context.People
    .AsQueryable()
    .Any(p => p.IsActive);

var allAdults = context.People
    .AsQueryable()
    .All(p => p.Age >= 18);
```

### GroupBy Operations

```csharp
// Simple grouping
var peopleByCity = context.People
    .AsQueryable()
  .GroupBy(p => p.City)
    .ToList();

// Grouping with aggregation
var cityStatistics = context.People
    .AsQueryable()
    .GroupBy(p => p.City)
.Select(g => new 
    {
        City = g.Key,
 Count = g.Count(),
        AverageAge = g.Average(p => p.Age),
        TotalScore = g.Sum(p => p.Score)
    })
    .ToList();

// Multiple grouping keys
var roleAndDepartmentGroups = context.People
    .AsQueryable()
    .GroupBy(p => new { p.Role, p.Department })
    .Select(g => new 
{
   g.Key.Role,
      g.Key.Department,
        Count = g.Count()
    })
    .ToList();
```

### Paging and Limiting

```csharp
// Take first N results
var topTen = context.People
    .AsQueryable()
    .OrderByDescending(p => p.Score)
    .Take(10)
    .ToList();

// Skip and Take (pagination)
var page2 = context.People
    .AsQueryable()
    .OrderBy(p => p.Name)
    .Skip(20)
    .Take(10)
    .ToList();

// First, Single, FirstOrDefault, SingleOrDefault
var firstPerson = context.People
    .AsQueryable()
    .First();

var specificPerson = context.People
    .AsQueryable()
    .Single(p => p.Email == "john@example.com");

var maybeExists = context.People
  .AsQueryable()
    .FirstOrDefault(p => p.Name == "Unknown");
```

## ?? Graph Traversal Operations

### Type-Safe Graph Navigation

```csharp
using Stardust.Paradox.Data.Linq;

// Define your graph entities
[VertexLabel("person")]
public interface IPerson : IVertex
{
 string Name { get; set; }
    int Age { get; set; }
    
    // Navigation properties
    IEdgeCollection<IPerson> Friends { get; }
  IEdgeCollection<ICompany> Employers { get; }
}

[VertexLabel("company")]
public interface ICompany : IVertex
{
    string Name { get; set; }
    IEdgeCollection<IPerson> Employees { get; }
}
```

### Out, In, OutE, InE Operations

```csharp
// Navigate outgoing edges
var friends = context.People
    .AsQueryable()
  .Where(p => p.Name == "Alice")
    .SelectMany(p => p.Friends.ToVertices())
    .ToList();

// Find friends of friends
var extendedNetwork = context.People
    .AsQueryable()
    .Where(p => p.Name == "Alice")
    .SelectMany(p => p.Friends.ToVertices())
    .SelectMany(friend => friend.Friends.ToVertices())
    .Distinct()
    .ToList();

// Incoming relationships
var popularPeople = context.People
    .AsQueryable()
  .Where(p => p.Friends.Count() > 10)
    .ToList();

// Edge filtering
var recentFriendships = context.People
    .AsQueryable()
    .Where(p => p.Name == "Alice")
    .SelectMany(p => p.Friends.Where(e => e.CreatedDate > DateTime.UtcNow.AddYears(-1)))
    .ToList();
```

### Lambda Expression Support for Graph Traversals

```csharp
using Stardust.Paradox.Data.Linq;

// Create a typed traversal
var traversal = new GraphTraversal<IPerson>();

// Has with lambda expressions
var result = traversal
    .Has(p => p.Age > 25)
    .Has(p => p.IsActive == true)
    .Has(p => p.Name != "Bob");

// Navigate using lambda expressions
var friends = traversal
    .Out(p => p.Friends)
    .Has(p => p.Age >= 21);

// Edge traversal with lambda
var employmentEdges = traversal
    .OutE(p => p.Employers)
    .Has("startDate", P.Gt(new DateTime(2020, 1, 1)));

// Complex chain with multiple filters
var complexQuery = traversal
    .Has(p => p.Age > 50)
    .Has(p => p.IsActive == true)
  .Out(p => p.Companies)
    .Dedup();

// Generate Gremlin query
string gremlinQuery = complexQuery.ToGremlinQuery();
// Result: "has('age', P.gt(50)).has('isActive', true).out('companies').dedup()"
```

### Gremlin Predicates

```csharp
using Stardust.Paradox.Data.Linq;

// P class for predicates (Gremlin-style)
var query = traversal
    .Has("age", P.Gt(25))              // Greater than
    .Has("age", P.Gte(18))             // Greater than or equal
    .Has("age", P.Lt(65))              // Less than
    .Has("age", P.Lte(30))     // Less than or equal
    .Has("age", P.Between(18, 65))    // Between (inclusive)
    .Has("age", P.Inside(18, 65))     // Inside (exclusive)
    .Has("age", P.Outside(18, 65))    // Outside
    .Has("name", P.Neq("Unknown"))    // Not equal
    .Has("city", P.Within("Seattle", "Portland", "Boston"))  // In list
    .Has("status", P.Without("inactive", "deleted"))        // Not in list
    .Has("email", P.Containing("@example.com"))   // String contains
    .Has("name", P.StartingWith("A"))  // String starts with
    .Has("name", P.EndingWith("son"))             // String ends with
    .Has("score", P.Not(P.Lt(50)));       // Negation
```

### Repeat and Loop Operations

```csharp
// Repeat traversal
var hierarchyTraversal = new GraphTraversal<IPerson>()
 .Repeat(t => t.Out("manages"))
    .Times(3)  // Repeat exactly 3 times
    .Emit();   // Emit intermediate results

// Until condition
var pathToRoot = new GraphTraversal<IPerson>()
    .Repeat(t => t.In("reportsTo"))
    .Until(t => t.Has("isRoot", true));

// Emit with filter
var organizationTree = new GraphTraversal<IPerson>()
    .Repeat(t => t.Out("manages"))
    .Emit(t => t.Has("level", P.Lte(5)))
    .Times(10);
```

## ?? Advanced LINQ Operations

### Set Operations

```csharp
// Union - Combine two queries
var techAndManagers = context.People
    .AsQueryable()
    .Where(p => p.Department == "Technology")
    .Union(
     context.People.AsQueryable().Where(p => p.Role == "Manager")
    )
    .ToList();

// Intersect - Find common elements
var seniorManagers = context.People
    .AsQueryable()
    .Where(p => p.Age > 40)
    .Intersect(
context.People.AsQueryable().Where(p => p.Role == "Manager")
    )
    .ToList();

// Except - Find difference
var nonManagers = context.People
    .AsQueryable()
    .Except(
        context.People.AsQueryable().Where(p => p.Role == "Manager")
  )
    .ToList();

// Concat - Simple concatenation (with duplicates)
var allEmployees = context.People
    .AsQueryable()
    .Where(p => p.Department == "Sales")
    .Concat(
     context.People.AsQueryable().Where(p => p.Department == "Marketing")
    )
    .ToList();
```

### Pattern Matching with Match

```csharp
// Match complex graph patterns
var matchResult = context.People
    .AsQueryable()
    .Match(
 p => p.Out("knows").As("friend"),
        p => p.As("friend").Out("worksAt").As("company"),
  p => p.As("company").In("worksAt").As("colleague")
    )
    .Select("friend", "company", "colleague")
    .ToList();
```

### Branch and Choose Operations

```csharp
// Conditional branching
var categorizedPeople = new GraphTraversal<IPerson>()
    .Choose(
      p => p.Has("age", P.Gt(30)),
        seniorPath => seniorPath.Values("name", "email"),
        juniorPath => juniorPath.Values("name")
    );

// Coalesce - First matching path
var contactInfo = new GraphTraversal<IPerson>()
    .Coalesce(
  t => t.Values("primaryEmail"),
        t => t.Values("secondaryEmail"),
        t => t.Constant("no-email@unknown.com")
    );
```

### Local Scoping

```csharp
// Local aggregation
var localMax = new GraphTraversal<IPerson>()
    .Out("friends")
    .Local(
        t => t.Values("age").Max()
    );

// Local ordering within groups
var topFriendsByAge = new GraphTraversal<IPerson>()
    .Out("friends")
    .Local(
  t => t.Order().By("age", Order.Desc).Limit(5)
    );
```

### Deduplication

```csharp
// Simple dedup
var uniquePeople = context.People
    .AsQueryable()
    .SelectMany(p => p.Friends.ToVertices())
    .Distinct()
    .ToList();

// Dedup by property
var distinctCities = new GraphTraversal<IPerson>()
    .Dedup()
    .By("city")
    .Values("city");
```

## ?? Async Operations

```csharp
// All LINQ operations support async
var activeUsersAsync = await context.People
    .AsQueryable()
    .Where(p => p.IsActive)
    .ToListAsync();

var countAsync = await context.People
 .AsQueryable()
    .CountAsync();

var firstAsync = await context.People
    .AsQueryable()
    .FirstOrDefaultAsync(p => p.Email == "test@example.com");

// Async enumeration
await foreach (var person in context.People
    .AsQueryable()
    .Where(p => p.Age > 25)
    .AsAsyncEnumerable())
{
    Console.WriteLine(person.Name);
}
```

## ?? Type Preservation and Casting

```csharp
// Type preservation through operations
IQueryable<IPerson> query = context.People.AsQueryable();

var result = query
    .Where(p => p.Age > 25)     // Still IQueryable<IPerson>
    .OrderBy(p => p.Name)        // Still IQueryable<IPerson>
    .Take(10)        // Still IQueryable<IPerson>
    .ToList();           // List<IPerson>

// Explicit typing after projection
var projections = context.People
    .AsQueryable()
    .Select(p => new PersonDto 
    { 
   Name = p.Name, 
        Age = p.Age 
    })
    .ToList();  // List<PersonDto>
```

## ?? Query Translation Details

### Supported LINQ Operators

| LINQ Method | Gremlin Translation | Description |
|-------------|-------------------|-------------|
| `Where(predicate)` | `has(property, value)` | Filter vertices/edges |
| `Select(selector)` | `project().by()` or `values()` | Project properties |
| `OrderBy(keySelector)` | `order().by(property)` | Sort ascending |
| `OrderByDescending(keySelector)` | `order().by(property, desc)` | Sort descending |
| `ThenBy(keySelector)` | Additional `by()` modulator | Secondary sort |
| `GroupBy(keySelector)` | `group().by()` | Group vertices/edges |
| `Count()` | `count()` | Count elements |
| `Sum(selector)` | `sum()` | Sum values |
| `Average(selector)` | `mean()` | Average values |
| `Min(selector)` | `min()` | Minimum value |
| `Max(selector)` | `max()` | Maximum value |
| `Take(n)` | `limit(n)` | Limit results |
| `Skip(n)` | `skip(n)` | Skip results |
| `First()` | `limit(1)` | Get first element |
| `FirstOrDefault()` | `limit(1).fold()` | Get first or null |
| `Single()` | `limit(2)` + validation | Get single element |
| `Distinct()` | `dedup()` | Remove duplicates |
| `Union(other)` | `union()` | Set union |
| `Intersect(other)` | Custom implementation | Set intersection |
| `Except(other)` | Custom implementation | Set difference |
| `Concat(other)` | `union()` | Concatenation |
| `Any()` | `hasNext()` | Check if any exist |
| `All(predicate)` | `not(has(!predicate))` | Check if all match |
| `SelectMany(selector)` | `flatMap()` or `out()/in()` | Flatten collections |

### Comparison Operators

| C# Operator | Gremlin Predicate | Example |
|-------------|------------------|---------|
| `==` | Value or `eq()` | `p => p.Age == 30` ? `has('age', 30)` |
| `!=` | `neq()` | `p => p.Age != 30` ? `has('age', P.neq(30))` |
| `>` | `gt()` | `p => p.Age > 30` ? `has('age', P.gt(30))` |
| `>=` | `gte()` | `p => p.Age >= 30` ? `has('age', P.gte(30))` |
| `<` | `lt()` | `p => p.Age < 30` ? `has('age', P.lt(30))` |
| `<=` | `lte()` | `p => p.Age <= 30` ? `has('age', P.lte(30))` |

### String Operations

| C# Method | Gremlin Translation | Example |
|-----------|-------------------|---------|
| `Contains(value)` | `containing()` | `p => p.Email.Contains("@")` |
| `StartsWith(value)` | `startingWith()` | `p => p.Name.StartsWith("A")` |
| `EndsWith(value)` | `endingWith()` | `p => p.Name.EndsWith("son")` |

### Boolean Operations

| C# Operator | Gremlin Translation | Example |
|-------------|-------------------|---------|
| `&&` | Chained `has()` | `p => p.Age > 25 && p.IsActive` |
| `\|\|` | `or()` step | `p => p.Age < 18 \|\| p.Age > 65` |
| `!` | `not()` step | `p => !p.IsActive` ? `not(has('isActive', true))` |

## ?? Best Practices

### Query Optimization

```csharp
// ? Good: Filter early
var result = context.People
    .AsQueryable()
    .Where(p => p.IsActive)           // Filter first
    .Where(p => p.Age > 25)         // Additional filter
    .OrderBy(p => p.Name)              // Then sort
    .Take(10)             // Limit results
    .ToList();

// ? Avoid: Late filtering
var badResult = context.People
    .AsQueryable()
    .OrderBy(p => p.Name)     // Sorting all records
    .ToList()        // Materializing all
    .Where(p => p.IsActive)   // Filtering in memory
    .Take(10)
    .ToList();

// ? Good: Use Count() for existence checks
var hasActive = context.People
    .AsQueryable()
    .Any(p => p.IsActive);

// ? Avoid: Materializing for count
var badCount = context.People
    .AsQueryable()
    .ToList()         // Fetches all data
    .Count(p => p.IsActive);           // Counts in memory
```

### Async Patterns

```csharp
// ? Good: Use async throughout
public async Task<List<IPerson>> GetActivePeopleAsync()
{
    return await context.People
        .AsQueryable()
        .Where(p => p.IsActive)
    .ToListAsync();
}

// ? Good: Async enumeration for large datasets
public async IAsyncEnumerable<IPerson> StreamPeopleAsync()
{
    await foreach (var person in context.People
        .AsQueryable()
        .AsAsyncEnumerable())
    {
      yield return person;
    }
}
```

### Type Safety

```csharp
// ? Good: Use strongly typed entities
var people = context.People
    .AsQueryable()
    .Where(p => p.Age > 25)  // IntelliSense and compile-time checking
  .ToList();

// ? Good: Use projection for DTOs
var dtos = context.People
.AsQueryable()
    .Select(p => new PersonDto 
    { 
   Name = p.Name,
        Age = p.Age,
Contact = p.Email
    })
    .ToList();
```

## ?? Debugging and Troubleshooting

### View Generated Gremlin Queries

```csharp
// For IQueryable
var queryable = context.People
    .AsQueryable()
    .Where(p => p.Age > 25)
    .OrderBy(p => p.Name);

// Cast to GraphQueryable to see the query
if (queryable is GraphQueryable<IPerson> graphQuery)
{
    string gremlinQuery = graphQuery.ToString();
    Console.WriteLine($"Generated Gremlin: {gremlinQuery}");
}

// For GraphTraversal
var traversal = new GraphTraversal<IPerson>()
    .Has(p => p.Age > 25)
    .Out(p => p.Friends);

string query = traversal.ToGremlinQuery();
Console.WriteLine($"Gremlin Query: {query}");
```

### Testing with InMemory Provider

```csharp
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.Linq;

[TestClass]
public class LinqQueryTests
{
    private TestGraphContext _context;

    [TestInitialize]
    public void Setup()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        _context = new TestGraphContext(connector);
        
  // Add test data
        var person = _context.CreateEntity<IPerson>("test1");
        person.Name = "Test Person";
   person.Age = 30;
      _context.SaveChangesAsync().Wait();
    }

    [TestMethod]
    public void TestLinqQuery()
    {
        // Act
        var result = _context.People
        .AsQueryable()
     .Where(p => p.Age > 25)
     .ToList();

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Test Person", result[0].Name);
    }
}
```

## ?? Integration with Stardust.Paradox.Data

### GraphContext Integration

```csharp
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Linq;

public class MyGraphContext : GraphContextBase
{
    public MyGraphContext(IGremlinLanguageConnector connector, IServiceProvider serviceProvider)
   : base(connector, serviceProvider)
  {
    }

    // Define your graph sets
    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    public IGraphSet<IProject> Projects => GraphSet<IProject>();

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
     configuration
            .ConfigureCollection<IPerson>()
        .Out(p => p.Friends, "knows")
     .Out(p => p.Employers, "worksAt")
 .ConfigureCollection<ICompany>()
                .In(c => c.Employees, "worksAt");

     return true;
    }
}

// Usage
var connector = /* your connector */;
var context = new MyGraphContext(connector, serviceProvider);

// Now use LINQ
var result = context.People
    .AsQueryable()
    .Where(p => p.IsActive)
    .OrderBy(p => p.Name)
    .ToList();
```

### Combining with Direct Gremlin Queries

```csharp
// Use LINQ for simple queries
var linqResult = context.People
    .AsQueryable()
  .Where(p => p.Age > 25)
    .ToList();

// Use direct Gremlin for complex graph patterns
var gremlinResult = await context.ExecuteAsync<IPerson>(g =>
    g.V().HasLabel("person")
     .Out("knows").Out("knows")  // Friends of friends
     .Dedup()
     .Limit(10));

// Combine approaches
var peopleIds = context.People
.AsQueryable()
    .Where(p => p.Department == "Engineering")
  .Select(p => p.Id)
    .ToList();

var detailedResults = await context.ExecuteAsync<IPerson>(g =>
    g.V(peopleIds.ToArray())
     .Out("knows")
     .HasLabel("person"));
```

## ?? Additional Resources

### Related Packages

- **Stardust.Paradox.Data**: Core graph database framework
- **Stardust.Paradox.Data.Annotations**: Entity annotations and interfaces
- **Stardust.Paradox.Data.InMemory**: In-memory graph database for testing
- **Stardust.Paradox.Data.Providers.Gremlin**: Gremlin.Net provider
- **Stardust.Paradox.Data.Providers.CosmosDb**: Azure CosmosDB provider

### Documentation

- [GitHub Repository](https://github.com/JonasSyrstad/Stardust.Paradox)
- [API Documentation](https://github.com/JonasSyrstad/Stardust.Paradox/wiki)
- [Sample Applications](https://github.com/JonasSyrstad/Stardust.Paradox/tree/main/samples)

### Support

- **Issues**: [GitHub Issues](https://github.com/JonasSyrstad/Stardust.Paradox/issues)
- **Discussions**: [GitHub Discussions](https://github.com/JonasSyrstad/Stardust.Paradox/discussions)
- **License**: Apache 2.0

## ??? Architecture

### Query Translation Pipeline

```
LINQ Expression Tree
       ?
Expression Visitor Pattern
       ?
Gremlin Query Builder
       ?
Gremlin Query String
       ?
IGremlinLanguageConnector
       ?
Graph Database (CosmosDB, TinkerPop, etc.)
```

### Key Components

- **GremlinQueryProvider**: IQueryProvider implementation for LINQ
- **GremlinQueryTranslator**: Translates LINQ expressions to Gremlin
- **GraphQueryable<T>**: IQueryable<T> implementation for graph entities
- **ExpressionVisitor**: Visitor pattern for expression tree traversal
- **GraphTraversal<T>**: Fluent API for building Gremlin traversals

## ?? Examples

### Real-World Scenarios

#### Social Network Queries

```csharp
// Find mutual friends
var mutualFriends = context.People
  .AsQueryable()
    .Where(p => p.Name == "Alice")
    .SelectMany(p => p.Friends.ToVertices())
    .Intersect(
        context.People
.AsQueryable()
        .Where(p => p.Name == "Bob")
        .SelectMany(p => p.Friends.ToVertices())
    )
    .ToList();

// Find people within 2 degrees of separation
var network = new GraphTraversal<IPerson>()
    .Has("name", "Alice")
    .Repeat(t => t.Out("knows"))
    .Times(2)
    .Emit()
    .Dedup();

// Most connected people
var influencers = context.People
    .AsQueryable()
    .OrderByDescending(p => p.Friends.Count())
    .Take(10)
    .ToList();
```

#### Organization Hierarchy

```csharp
// Find all reports (direct and indirect)
var allReports = new GraphTraversal<IPerson>()
    .Has("name", "CEO")
    .Repeat(t => t.Out("manages"))
    .Emit()
    .Dedup();

// Find managers with most direct reports
var topManagers = context.People
    .AsQueryable()
    .Where(p => p.Role == "Manager")
    .OrderByDescending(p => p.DirectReports.Count())
    .Select(p => new 
    {
        p.Name,
        DirectReportCount = p.DirectReports.Count()
    })
    .Take(10)
    .ToList();

// Organization statistics by level
var levelStats = context.People
    .AsQueryable()
    .GroupBy(p => p.Level)
    .Select(g => new 
    {
        Level = g.Key,
        Count = g.Count(),
        AverageTenure = g.Average(p => p.YearsInRole)
    })
    .OrderBy(x => x.Level)
    .ToList();
```

#### E-Commerce Recommendations

```csharp
// Find recommended products (collaborative filtering)
var recommendations = context.Products
    .AsQueryable()
    .Where(p => p.Category == targetProduct.Category)
    .SelectMany(p => p.PurchasedBy.ToVertices())
    .SelectMany(customer => customer.Purchases.ToVertices())
    .Where(p => p.Id != targetProduct.Id)
    .GroupBy(p => p.Id)
    .OrderByDescending(g => g.Count())
    .Select(g => g.First())
    .Take(5)
    .ToList();

// Customer purchase patterns
var patterns = context.Customers
    .AsQueryable()
    .Select(c => new 
    {
        CustomerId = c.Id,
        TotalPurchases = c.Purchases.Count(),
  AveragePrice = c.Purchases.Average(p => p.Price),
  Categories = c.Purchases
 .Select(p => p.Category)
            .Distinct()
            .ToList()
    })
    .ToList();
```

## ?? Performance Tips

### Index Usage

```csharp
// ? Good: Uses index on IsActive
var active = context.People
    .AsQueryable()
    .Where(p => p.IsActive)  // Indexed property
    .ToList();

// Ensure your graph database has appropriate indexes:
// - CosmosDB: Automatic indexing on all properties
// - TinkerPop: Configure composite indexes for frequently queried properties
```

### Projection for Large Datasets

```csharp
// ? Good: Only fetch needed properties
var lightweightData = context.People
    .AsQueryable()
    .Select(p => new { p.Id, p.Name })
    .ToList();

// ? Avoid: Fetching full entities when not needed
var heavyData = context.People
    .AsQueryable()
    .ToList();  // Loads all properties
```

### Batching

```csharp
// ? Good: Process in batches
var pageSize = 100;
var page = 0;

while (true)
{
 var batch = context.People
  .AsQueryable()
        .OrderBy(p => p.Id)
      .Skip(page * pageSize)
      .Take(pageSize)
        .ToList();

if (!batch.Any()) break;

    // Process batch
    await ProcessBatchAsync(batch);
    page++;
}
```

## ?? Version Compatibility

- **.NET Standard 2.0**: Full support
- **C# 7.3+**: Required for language features
- **Stardust.Paradox.Data**: 2.3.0 or higher recommended
- **Gremlin.Net**: Compatible with TinkerPop 3.x protocol
- **Azure CosmosDB**: Gremlin API support

## ?? License

Copyright © Stardust 2025

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

---

Made with ?? by the Stardust.Paradox team
