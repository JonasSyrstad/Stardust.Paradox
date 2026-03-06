# Full Examples — Stardust.Paradox.Data.Linq

## Example 1: Complete Test Setup with InMemory

### Entity Interfaces

```csharp
using Stardust.Paradox.Data.Annotations;

[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Name { get; set; }
    string Email { get; set; }
    int Age { get; set; }
    bool IsActive { get; set; }
    decimal Score { get; set; }
    string City { get; set; }

    [OutLabel("worksAt")]
    IEdgeCollection<ICompany> Companies { get; }

    [OutLabel("friendsWith")]
    IEdgeCollection<IPerson> Friends { get; }

    [OutLabel("hasSkill")]
    IEdgeCollection<ISkill> Skills { get; }
}

[VertexLabel("company")]
public interface ICompany : IVertex
{
    string Name { get; set; }
    string Industry { get; set; }
    int EmployeeCount { get; set; }

    [InLabel("worksAt")]
    IEdgeCollection<IPerson> Employees { get; }
}

[VertexLabel("skill")]
public interface ISkill : IVertex
{
    string Name { get; set; }
    string Category { get; set; }

    [InLabel("hasSkill")]
    IEdgeCollection<IPerson> Practitioners { get; }
}

[InLabel("hasSkill")]
public interface IUserSkill : IEdge<IPerson, ISkill>
{
    int YearsExperience { get; set; }
    int Proficiency { get; set; }
}
```

### GraphContext with _modelInitialized Guard

```csharp
using Microsoft.Extensions.DependencyInjection;

public class LinqTestContext : GraphContextBase
{
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();

    public LinqTestContext(IGremlinLanguageConnector connector)
        : base(connector, CreateServiceProvider()) { }

    static LinqTestContext()
    {
        PartitionKeyName = "pk";
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityBinding((entity, impl) => services.AddTransient(entity, impl));
        return services.BuildServiceProvider();
    }

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        if (_isInitialized) return true;
        lock (_lock)
        {
            if (_isInitialized) return true;

            configuration.ConfigureCollection<IPerson>()
                .AddOutEdge(p => p.Companies, "worksAt");
            configuration.ConfigureCollection<IPerson>()
                .AddOutEdge(p => p.Friends, "friendsWith");
            configuration.ConfigureCollection<IPerson>()
                .Out(p => p.Skills, "hasSkill").In(s => s.Practitioners);
            configuration.ConfigureCollection<ICompany>()
                .AddInEdge(c => c.Employees, "worksAt");
            configuration.ConfigureCollection<ISkill>();
            configuration.ConfigureCollection<IUserSkill>();

            _isInitialized = true;
        }
        return true;
    }

    public IGraphSet<IPerson> People => GraphSet<IPerson>();
    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    public IGraphSet<ISkill> Skills => GraphSet<ISkill>();
    public IEdgeGraphSet<IUserSkill> UserSkills => EdgeGraphSet<IUserSkill>();
}
```

### Test Scenario (InMemory Data)

```csharp
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Scenarios;

public class LinqTestScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "LinqTest";
    public override string Description => "Test data for LINQ queries";

    protected override (ScenarioVertexDefinition[] vertices,
                        ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new("user1", "person", Props(
                ("pk", "test"), ("name", "Alice Johnson"),
                ("email", "alice@example.com"), ("age", 30),
                ("isActive", true), ("score", 95.5m), ("city", "Seattle"))),
            new("user2", "person", Props(
                ("pk", "test"), ("name", "Bob Smith"),
                ("email", "bob@example.com"), ("age", 25),
                ("isActive", true), ("score", 88.0m), ("city", "Portland"))),
            new("user3", "person", Props(
                ("pk", "test"), ("name", "Charlie Brown"),
                ("email", "charlie@example.com"), ("age", 35),
                ("isActive", false), ("score", 92.3m), ("city", "Seattle"))),

            new("company1", "company", Props(
                ("pk", "test"), ("name", "TechCorp"),
                ("industry", "Technology"), ("employeeCount", 500))),

            new("skill1", "skill", Props(
                ("pk", "test"), ("name", "C#"),
                ("category", "Programming"))),
        };

        var edges = new ScenarioEdgeDefinition[]
        {
            new("worksAt", "user1", "company1", Props(("role", "Senior Dev"))),
            new("worksAt", "user2", "company1", Props(("role", "Developer"))),
            new("hasSkill", "user1", "skill1", Props(
                ("yearsExperience", 8), ("proficiency", 9))),
            new("hasSkill", "user2", "skill1", Props(
                ("yearsExperience", 3), ("proficiency", 6))),
        };

        return (vertices, edges);
    }
}
```

### Test Base Class

```csharp
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Extensions;
using Stardust.Paradox.Data.InMemory.Factory;
using Stardust.Paradox.Data.Traversals;

public abstract class LinqTestBase : IDisposable
{
    protected InMemoryGremlinLanguageConnector Connector { get; private set; } = null!;
    protected InMemoryGraphDatabase Database { get; private set; } = null!;
    protected LinqTestContext Context { get; private set; } = null!;

    protected LinqTestBase()
    {
        Database = new InMemoryGraphDatabase();

        var scenario = new LinqTestScenario();
        scenario.ConfigureScenario(Database);

        Connector = InMemoryConnectorFactory.Create(Database);

        // ?? REQUIRED — enables LINQ query execution
        GremlinFactory.SetActivatorFactory(() => Connector);

        Context = new LinqTestContext(Connector);
    }

    public void Dispose()
    {
        Context?.Dispose();
        Connector?.Dispose();
    }
}
```

---

## Example 2: Filtering, Ordering, and Paging

```csharp
public class QueryTests : LinqTestBase
{
    [Fact]
    public async Task FilterByEquality()
    {
        var alice = await Context.People.AsQueryable()
            .Where(p => p.Name == "Alice Johnson")
            .FirstAsync();

        alice.Should().NotBeNull();
        alice.Name.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task FilterByComparison()
    {
        var seniors = await Context.People.AsQueryable()
            .Where(p => p.Age > 30)
            .ToListAsync();

        seniors.Should().OnlyContain(p => p.Age > 30);
    }

    [Fact]
    public async Task CombinedFilters()
    {
        var result = await Context.People.AsQueryable()
            .Where(p => p.IsActive && p.Score > 90)
            .OrderByDescending(p => p.Score)
            .ToListAsync();

        result.Should().OnlyContain(p => p.IsActive && p.Score > 90);
        result.Should().BeInDescendingOrder(p => p.Score);
    }

    [Fact]
    public async Task StringContains()
    {
        var result = await Context.People.AsQueryable()
            .Where(p => p.Email.Contains("@example.com"))
            .ToListAsync();

        result.Should().OnlyContain(p => p.Email.Contains("@example.com"));
    }

    [Fact]
    public async Task Paging()
    {
        var page = await Context.People.AsQueryable()
            .OrderBy(p => p.Name)
            .Skip(1)
            .Take(2)
            .ToListAsync();

        page.Should().HaveCount(2);
        page.Should().BeInAscendingOrder(p => p.Name);
    }
}
```

---

## Example 3: Projections

```csharp
public class ProjectionTests : LinqTestBase
{
    [Fact]
    public void SelectSingleProperty()
    {
        var names = Context.People.AsQueryable()
            .Select(p => p.Name)
            .ToList();

        names.Should().AllBeOfType<string>();
        names.Should().Contain("Alice Johnson");
    }

    [Fact]
    public void SelectAnonymousType()
    {
        var dtos = Context.People.AsQueryable()
            .Select(p => new { p.Name, p.Age, p.Email })
            .ToList();

        dtos.First().Name.Should().NotBeEmpty();
        dtos.First().Age.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectWithFilter()
    {
        var result = Context.People.AsQueryable()
            .Where(p => p.IsActive)
            .Select(p => new { p.Name, p.Score })
            .OrderByDescending(p => p.Score)
            .ToList();

        result.Should().BeInDescendingOrder(p => p.Score);
    }
}
```

---

## Example 4: Aggregations

```csharp
public class AggregationTests : LinqTestBase
{
    [Fact]
    public void Count()
    {
        var count = Context.People.AsQueryable().Count();
        count.Should().Be(3);
    }

    [Fact]
    public void CountWithFilter()
    {
        var activeCount = Context.People.AsQueryable()
            .Where(p => p.IsActive)
            .Count();

        activeCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SumAndAverage()
    {
        var totalAge = Context.People.AsQueryable().Sum(p => p.Age);
        var avgAge = Context.People.AsQueryable().Average(p => p.Age);

        totalAge.Should().BeGreaterThan(0);
        avgAge.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MinAndMax()
    {
        var youngest = Context.People.AsQueryable().Min(p => p.Age);
        var oldest = Context.People.AsQueryable().Max(p => p.Age);

        youngest.Should().BeLessThan(oldest);
    }

    [Fact]
    public void GroupBy()
    {
        var byCity = Context.People.AsQueryable()
            .GroupBy(p => p.City)
            .Select(g => new { City = g.Key, Count = g.Count() })
            .ToList();

        byCity.Should().Contain(g => g.City == "Seattle");
    }

    [Fact]
    public async Task AnyAsync()
    {
        var hasActive = await Context.People.AsQueryable()
            .Where(p => p.IsActive)
            .AnyAsync();

        hasActive.Should().BeTrue();

        var hasOld = await Context.People.AsQueryable()
            .Where(p => p.Age > 100)
            .AnyAsync();

        hasOld.Should().BeFalse();
    }
}
```

---

## Example 5: Graph Traversals with LINQ

```csharp
public class TraversalTests : LinqTestBase
{
    [Fact]
    public async Task OutTraversal_PersonToCompany()
    {
        // Person ? worksAt ? Company
        var companies = await Context.People.AsQueryable()
            .Where(p => p.Name == "Alice Johnson")
            .Out(p => p.Companies)
            .ToListAsync();

        companies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InTraversal_CompanyToEmployees()
    {
        // Company ? worksAt ? Person
        var employees = await Context.Companies.AsQueryable()
            .Where(c => c.Name == "TechCorp")
            .In(c => c.Employees)
            .ToListAsync();

        employees.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OutE_GetEdgeEntities()
    {
        // Get the IUserSkill edge entities
        var userSkills = await Context.People.AsQueryable()
            .OutE<IUserSkill>()
            .ToListAsync();

        userSkills.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EdgeToVertex_InV()
    {
        // Skills ? inE(hasSkill) ? inV() ? People
        var people = await Context.Skills.AsQueryable()
            .Where(s => s.Name == "C#")
            .InE<IUserSkill>()
            .InV<IUserSkill, IPerson>()
            .ToListAsync();

        people.Should().NotBeEmpty();
    }

    [Fact]
    public async Task VertexLevelEdgeTraversal_InSelect()
    {
        // Get edges from each vertex using Select projection
        var edges = await (
            from p in Context.People.AsQueryable()
            select p.OutE(person => person.Skills)
                    .Cast<IUserSkill>()
        ).ToListAsync();

        edges.Should().NotBeEmpty();
    }
}
```

---

## Example 6: As / Select Step Labels

```csharp
public class StepLabelTests : LinqTestBase
{
    [Fact]
    public async Task AsAndSelect_ReturnLabeledStep()
    {
        // Label people as "a", traverse to skills, filter, select back to "a"
        var people = await Context.People.AsQueryable()
            .As("a")
            .Out(p => p.Skills)
            .Has(s => s.Name == "C#")
            .Select<IPerson>("a")
            .ToListAsync();

        // Generated: g.V().hasLabel('person').as('a').out('hasSkill')
        //            .has('name','C#').select('a')
        people.Should().NotBeEmpty();
        people.First().Should().BeAssignableTo<IPerson>();
    }

    [Fact]
    public async Task AsAndSelect_WithEdges()
    {
        // Label edges, traverse to vertex, then select back to edge
        var edges = await Context.Skills.AsQueryable()
            .InE(s => s.Practitioners).Cast<IUserSkill>()
            .As("a")
            .OtherV<ISkill>()
            .Has(s => s.Name == "C#")
            .Select<IUserSkill>("a")
            .ToListAsync();

        if (edges.Any())
        {
            edges.First().Should().BeAssignableTo<IUserSkill>();
        }
    }
}
```

---

## Example 7: LINQ Query Syntax (from ... select)

```csharp
public class QuerySyntaxTests : LinqTestBase
{
    [Fact]
    public async Task SimpleQuerySyntax()
    {
        var people = await (
            from p in Context.People.AsQueryable()
            select p
        ).ToListAsync();

        people.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FilterAndProject()
    {
        var result = await (
            from p in Context.People.AsQueryable()
            where p.Age > 20 && p.Age < 30
            select new { p.Age, p.Name }
        ).ToListAsync();

        result.Should().OnlyContain(r => r.Age > 20 && r.Age < 30);
    }

    [Fact]
    public async Task WithGraphTraversal()
    {
        var people = await (
            from p in Context.People.AsQueryable()
                .As("a")
                .Out(t => t.Skills)
                .Has(t => t.Name == "C#")
                .Select<IPerson>("a")
            select p
        ).ToListAsync();

        people.Should().NotBeEmpty();
        people.First().Should().BeAssignableTo<IPerson>();
    }
}
```

---

## Example 8: Debugging LINQ Queries

```csharp
public class DebugTests : LinqTestBase
{
    [Fact]
    public void InspectGeneratedGremlin()
    {
        var queryable = Context.People.AsQueryable()
            .Where(p => p.Age > 30)
            .OrderBy(p => p.Name)
            .Take(10);

        // Method 1: Cast to GraphQueryable<T>
        var gremlin = ((GraphQueryable<IPerson>)queryable).ToGremlinQuery();
        // ? "g.V().hasLabel('person').has('age', gt(30)).order().by('name', asc).limit(10)"

        // Method 2: ToString() on queryable
        Console.WriteLine(queryable.ToString());

        gremlin.Should().Contain("hasLabel('person')");
        gremlin.Should().Contain("has('age'");
    }

    [Fact]
    public void InspectQueryLog()
    {
        // Execute a query
        var result = Context.People.AsQueryable()
            .Where(p => p.IsActive)
            .ToList();

        // Check the actual Gremlin that was sent to the connector
        var queryLog = Connector.GetQueryLog();
        var lastQuery = queryLog.First().Query;

        Console.WriteLine($"Executed: {lastQuery}");
        lastQuery.Should().Contain("hasLabel('person')");
    }
}
```

---

## Example 9: Complex Real-World Query

```csharp
public class RealWorldTests : LinqTestBase
{
    [Fact]
    public async Task FindActiveUsersWithSpecificSkill()
    {
        // Find all active people who have the C# skill,
        // ordered by score descending, take top 5
        var topCSharpDevelopers = await Context.People.AsQueryable()
            .Where(p => p.IsActive)
            .As("person")
            .Out(p => p.Skills)
            .Has(s => s.Name == "C#")
            .Select<IPerson>("person")
            .ToListAsync();

        topCSharpDevelopers.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task GetCompanyEmployeeStats()
    {
        // Count employees per company
        var companyCounts = Context.Companies.AsQueryable()
            .Select(c => new { c.Name, c.EmployeeCount })
            .OrderByDescending(c => c.EmployeeCount)
            .ToList();

        companyCounts.Should().BeInDescendingOrder(c => c.EmployeeCount);
    }

    [Fact]
    public async Task PaginatedSearch()
    {
        const int pageSize = 2;
        const int pageNumber = 0;

        var page = await Context.People.AsQueryable()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Skip(pageNumber * pageSize)
            .Take(pageSize)
            .ToListAsync();

        page.Should().HaveCountLessThanOrEqualTo(pageSize);
        page.Should().BeInAscendingOrder(p => p.Name);
    }
}
```

---

## GremlinFactory Setup — Why It Matters

The LINQ provider translates expressions into Gremlin strings and then calls
the connector to execute them. Internally, `GremlinQueryProvider` resolves the
connector through `GremlinFactory`, which is a static service locator.

```
LINQ expression
    ? GremlinQueryTranslator.Translate()
    ? "g.V().hasLabel('person').has('age', gt(30))"
    ? GremlinFactory ? IGremlinLanguageConnector.ExecuteAsync()
    ? Results ? mapped to entity instances
```

Without `GremlinFactory.SetActivatorFactory(() => connector)`, the execution
step fails because no connector is available. This call must happen **once**
before any LINQ query executes, typically in the test base class constructor.

In production ASP.NET Core apps using `AddParadox<TContext>()`, this is handled
automatically by the DI registration.
