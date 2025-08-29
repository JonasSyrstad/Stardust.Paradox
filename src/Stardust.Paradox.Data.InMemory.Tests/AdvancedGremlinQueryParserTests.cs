using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for the advanced Gremlin query parser
/// </summary>
public class AdvancedGremlinQueryParserTests
{
    [Fact]
    public void ParseAndExecute_SimpleQuery_ShouldReturnCorrectResult()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        connector.AddVertex("person", "p1");
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V()", new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAndExecute_ParameterizedQuery_ShouldSubstituteParameters()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        connector.AddVertex("person", "p1");
        var vertex = connector.GetVertex("p1");
        vertex!.Properties["name"] = "John";
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V().has('name', p0)", 
            new Dictionary<string, object> { { "p0", "John" } });

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAndExecute_ComplexQuery_ShouldExecuteAllSteps()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V().hasLabel('person').out('works_for').hasLabel('company')", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAndExecute_InvalidQuery_ShouldFallBackToSimpleParser()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act & Assert - Should not throw
        var result = parser.ParseAndExecute("invalid query syntax", new Dictionary<string, object>());
        result.Should().NotBeNull();
    }

    [Fact]
    public void ParseAndExecute_MultiStepTraversal_ShouldExecuteInOrder()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V('p1').out('works_for').in('works_for')", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1); // Should return p1 (the original person)
    }

    [Fact]
    public void ParseAndExecute_MaxStepsQuery_ShouldHandleUpTo10Steps()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupAdvancedTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act - 9-step query (within limit)
        var result = parser.ParseAndExecute(
            "g.V().hasLabel('person').out('works_for').in('works_for').hasLabel('person').out('assigned_to').hasLabel('project').in('assigned_to').dedup()", 
            new Dictionary<string, object>());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void ParseAndExecute_AggregationQuery_ShouldReturnAggregatedResult()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupNumericTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V().hasLabel('person').values('age').sum()", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((double)result.First()).Should().Be(93.0); // Aggregation test
    }

    [Fact]
    public void ParseAndExecute_FilteringQuery_ShouldApplyFilters()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V().hasLabel('person').has('age')", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAndExecute_PropertyOperations_ShouldReturnProperties()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V('p1').valueMap()", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        var valueMap = result.First() as Dictionary<string, object>;
        valueMap.Should().NotBeNull();
    }

    [Fact]
    public void ParseAndExecute_EdgeNavigation_ShouldNavigateEdges()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V('p1').outE('works_for').inV()", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
        ((string)result.First().label).Should().Be("company"); // Edge navigation test
    }

    [Fact]
    public void ParseAndExecute_OrderingAndLimiting_ShouldApplyCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V().hasLabel('person').order().limit(1)", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAndExecute_PathQuery_ShouldReturnPath()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        SetupTestGraph(connector);
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V('p1').out('works_for').path()", 
            new Dictionary<string, object>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseAndExecute_NullParameters_ShouldHandleGracefully()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        connector.AddVertex("person", "p1");
        
        var database = connector.Database;
        var parser = new AdvancedGremlinQueryParser(database);

        // Act
        var result = parser.ParseAndExecute("g.V().count()", null);

        // Assert
        result.Should().HaveCount(1);
        ((long)result.First()).Should().Be(1L); // Count test
    }

    private static void SetupTestGraph(InMemoryGremlinLanguageConnector connector)
    {
        var person = connector.AddVertex("person", "p1");
        person.Properties["name"] = "John";
        person.Properties["age"] = 30;

        var company = connector.AddVertex("company", "c1");
        company.Properties["name"] = "Tech Corp";

        connector.AddEdge("works_for", "p1", "c1");
    }

    private static void SetupAdvancedTestGraph(InMemoryGremlinLanguageConnector connector)
    {
        // Create people
        var john = connector.AddVertex("person", "john");
        john.Properties["name"] = "John";
        var jane = connector.AddVertex("person", "jane");
        jane.Properties["name"] = "Jane";

        // Create company
        var company = connector.AddVertex("company", "tech_corp");
        company.Properties["name"] = "Tech Corp";

        // Create project
        var project = connector.AddVertex("project", "proj1");
        project.Properties["name"] = "Project Alpha";

        // Create relationships
        connector.AddEdge("works_for", "john", "tech_corp");
        connector.AddEdge("works_for", "jane", "tech_corp");
        connector.AddEdge("assigned_to", "john", "proj1");
        connector.AddEdge("assigned_to", "jane", "proj1");
    }

    private static void SetupNumericTestGraph(InMemoryGremlinLanguageConnector connector)
    {
        var john = connector.AddVertex("person", "john");
        john.Properties["age"] = 30;

        var jane = connector.AddVertex("person", "jane");
        jane.Properties["age"] = 28;

        var bob = connector.AddVertex("person", "bob");
        bob.Properties["age"] = 35;
    }
}