using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;
using Stardust.Paradox.Data.InMemory.Scenarios;

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
    public async Task ComplexeQueryWithRepeatUntilPathUnfold()
    {
        try
        {
            InMemoryScenarioRegistry.Register(new ServiceElementScenario());
        }
        catch (Exception e)
        {
        }
        string tenantId = "31729bb9-5b5a-423b-b8ff-1cb81dfbb5b3";
        string tenantServiceId = "b43d002f-a372-431a-8104-e3df7dd5734f";
        string serviceId = "1deb5f9f-f438-4665-9f11-62e86737f7d1";
        string mainAssetId = "6000ba9f-2ade-4412-8c82-e33d32a725cf";
        string secondaryAssetId = "6001ba9f-2ade-4412-8c82-e33d32a725cf";
        string childAsset1Id = "6200ba9f-2ade-4412-8c82-e33d32a725cf";
        string childAsset2Id = "6201ba9f-2ade-4412-8c82-e33d32a725cf";
        string grandchildAssetId = "6202ba9f-2ade-4412-8c82-e33d32a725cf";
        string testProfileId = "74f0fc83-51e3-48af-ac58-25176301cd6f";
        string testProfile2Id = "74f0fcaa-51e3-48af-ac58-25176301cd6f";
        string testUserId = "550e8400-e29b-41d4-a716-446655440000";
        string userAdminsGroupId = "ff18913e-39a0-423b-aeb7-1a28c64c24bf";
        var connector = InMemoryGremlinLanguageConnector.Create(options =>
            {
                options.LogQueries = true;
                options.EnableDebugLogging = true;
                options.EnableQueryLogging = true;
            });
        // Apply the scenario with the correct name
        Scenarios.InMemoryScenarioRegistry.ApplyScenario(connector.Database, "ServiceElementScenario");
        
        // Test simpler queries first to understand what's happening
        var step1 = await connector.ExecuteAsync("g.V('" + mainAssetId + "')", new Dictionary<string, object>());
        Assert.NotEmpty(step1); // Verify vertex exists
        
        var step2 = await connector.ExecuteAsync("g.V('" + mainAssetId + "').outE('members')", new Dictionary<string, object>());
        Assert.NotEmpty(step2); // Verify edges exist
        
        // Test repeat/until without path
        var step3Query = "g.V('" + mainAssetId + "').repeat(outE('members').as('e').otherV().simplePath()).until(or(has('entityType','profile'),has('entityType','userGroup')))";
        var step3 = await connector.ExecuteAsync(step3Query, new Dictionary<string, object>());
        Assert.NotEmpty(step3); // Should reach a profile or userGroup
        
        // Test with path
        var step4Query = "g.V('" + mainAssetId + "').repeat(outE('members').as('e').otherV().simplePath()).until(or(has('entityType','profile'),has('entityType','userGroup'))).path()";
        var step4 = await connector.ExecuteAsync(step4Query, new Dictionary<string, object>());
        Assert.NotEmpty(step4); // Should return paths
        Console.WriteLine($"Step 4 returned {step4.Count()} path(s)");
        
        // Test with path().unfold()
        var step5Query = "g.V('" + mainAssetId + "').repeat(outE('members').as('e').otherV().simplePath()).until(or(has('entityType','profile'),has('entityType','userGroup'))).path().unfold()";
        var step5 = await connector.ExecuteAsync(step5Query, new Dictionary<string, object>());
        Assert.NotEmpty(step5); // Should return unfolded path elements
        Console.WriteLine($"Step 5 returned {step5.Count()} unfolded element(s)");
        
        // Test where clause separately
        var step6Query = "g.V('" + mainAssetId + "').repeat(outE('members').as('e').otherV().simplePath()).until(or(has('entityType','profile'),has('entityType','userGroup'))).path().unfold().where(select('e').not(has('memberType','assetStructure')))";

        Console.WriteLine($"Step 6 query: {step6Query}");
        var step6 = await connector.ExecuteAsync(step6Query, new Dictionary<string, object>());
        Console.WriteLine($"Step 6 (with where filter) returned {step6.Count()} element(s)");
        
        // Full complex query - but split to debug
        var preSelectQuery = "g.V().has('id',within('" + mainAssetId + "', '" + testProfileId + "'))\n  .has('pk','" + tenantId + "')\n  .as('a')\n  .repeat(outE('members').as('e').otherV().simplePath())\n  .until(or(has('entityType','profile'),has('entityType','userGroup')))\n  .path().unfold()\n  .where(select('e').not(has('memberType','assetStructure')))\n  .limit(1)";
        var preSelect = await connector.ExecuteAsync(preSelectQuery, new Dictionary<string, object>());
        Console.WriteLine($"Pre-select query returned {preSelect.Count()} element(s)");
        if (preSelect.Any())
        {
            var first = preSelect.First();
            Console.WriteLine($"First element type: {first.GetType().Name}");
        }
        
        // Full complex query
        var finalQuery = "g.V().has('id',within('" + mainAssetId + "', '" + testProfileId + "'))\n  .has('pk','" + tenantId + "')\n  .as('a')\n  .repeat(outE('members').as('e').otherV().simplePath())\n  .until(or(has('entityType','profile'),has('entityType','userGroup')))\n  .path().unfold()\n  .where(select('e').not(has('memberType','assetStructure')))\n  .limit(1)\n  .select('e')";
        var result = await connector.ExecuteAsync(finalQuery, new Dictionary<string, object>());
        Console.WriteLine($"Final query returned {result.Count()} result(s)");
        Assert.NotEmpty(result);

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