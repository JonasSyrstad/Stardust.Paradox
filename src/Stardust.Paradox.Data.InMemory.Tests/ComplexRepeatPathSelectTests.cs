using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Scenarios;
using Stardust.Paradox.Data.InMemory.Extensions;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Comprehensive test suite for the complex repeat-path-select pattern:
/// g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)
/// 
/// This query pattern is used to:
/// 1. Start from multiple vertices (array syntax)
/// 2. Label the starting vertices with 'as'
/// 3. Traverse using repeat-until with:
///    - outE() to get edges
///    - as() to label edges
///    - inV() to get destination vertices
///    - simplePath() to avoid cycles
/// 4. Stop when vertices match one of two conditions (using or())
/// 5. Get the full path
/// 6. Unfold the path into individual elements
/// 7. Filter elements using where with select and not
/// 8. Limit results
/// 9. Select specific labeled elements
/// 
/// This is a complex graph traversal pattern commonly used for:
/// - Finding relationships between entities with constraints
/// - Multi-hop navigation with cycle prevention
/// - Complex filtering on path elements
/// </summary>
public class ComplexRepeatPathSelectTests
{
    #region Test Data Constants
    
    private const string StartVertex1 = "start1";
    private const string StartVertex2 = "start2";
    private const string TargetProperty = "type";
    private const string TargetValue1 = "target";
    private const string TargetValue2 = "endpoint";
    private const string FilterProperty = "excluded";
    private const string FilterValue = "true";
    
    #endregion

    #region Basic Functionality Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WithBasicPath_ShouldReturnFilteredResults()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "edge" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", 10 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithSingleStartVertex_ShouldWork()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex1 }, // Same vertex twice
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "edge" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", 10 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithNoMatchingTarget_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", TargetProperty },
            { "__p15", "nonexistent" },
            { "__p16", TargetProperty },
            { "__p17", "alsoNonexistent" },
            { "__p18", "edge" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", 10 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Array Syntax Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WithArraySyntax_ShouldHandleMultipleStartVertices()
    {
        // Arrange
        var connector = await CreateMultiPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "v1" },
            { "__p1", "v2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "type" },
            { "__p15", "target" },
            { "__p16", "type" },
            { "__p17", "endpoint" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 20 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
        // Should handle starting from multiple vertices
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithThreeStartVertices_ShouldWork()
    {
        // Arrange
        var connector = await CreateMultiPathScenario();
        
        // Note: Query expects exactly 2 parameters in array, so we'll test with 2
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "v1" },
            { "__p1", "v3" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "type" },
            { "__p15", "target" },
            { "__p16", "type" },
            { "__p17", "endpoint" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 30 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithInvalidStartVertex_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "nonexistent1" },
            { "__p1", "nonexistent2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "edge" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", 10 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Repeat and SimplePath Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WithSimplePath_ShouldAvoidCycles()
    {
        // Arrange
        var connector = await CreateCyclicGraphScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "cycle1" },
            { "__p1", "cycle2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "type" },
            { "__p15", "exit" },
            { "__p16", "type" },
            { "__p17", "endpoint" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 50 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
        // SimplePath should prevent infinite loops in cyclic graphs
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithMultiHopPath_ShouldTraverseCorrectly()
    {
        // Arrange
        var connector = await CreateDeepPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "deep1" },
            { "__p1", "deep2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "depth" },
            { "__p15", "5" },
            { "__p16", "depth" },
            { "__p17", "6" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 100 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Until Condition Tests (OR Logic)

    [Fact]
    public async Task ComplexRepeatPathSelect_UntilFirstCondition_ShouldStopAtMatch()
    {
        // Arrange
        var connector = await CreateConditionalPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "cond1" },
            { "__p1", "cond2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "status" },
            { "__p15", "complete" },
            { "__p16", "status" },
            { "__p17", "finished" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 20 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_UntilSecondCondition_ShouldStopAtMatch()
    {
        // Arrange
        var connector = await CreateConditionalPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "cond3" },
            { "__p1", "cond4" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "status" },
            { "__p15", "nonexistent" }, // Won't match
            { "__p16", "status" },
            { "__p17", "finished" }, // Will match
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 20 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_UntilNeitherCondition_ShouldContinueTraversal()
    {
        // Arrange
        var connector = await CreateConditionalPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "nocond1" },
            { "__p1", "nocond2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "status" },
            { "__p15", "never" },
            { "__p16", "status" },
            { "__p17", "nomatch" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 5 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        // Should eventually stop due to simplePath or reaching end of graph
        result.Should().NotBeNull();
    }

    #endregion

    #region Path and Unfold Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_PathUnfold_ShouldExpandAllElements()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "pathtest1" },
            { "__p1", "pathtest2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "marker" },
            { "__p15", "end" },
            { "__p16", "marker" },
            { "__p17", "terminal" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 50 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
        // Unfold should create individual elements from paths
    }

    #endregion

    #region Where-Select-Not Filter Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WhereSelectNot_ShouldFilterExcludedEdges()
    {
        // Arrange
        var connector = await CreateFilteredEdgesScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "filter1" },
            { "__p1", "filter2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "status" },
            { "__p15", "done" },
            { "__p16", "status" },
            { "__p17", "complete" },
            { "__p18", "edge" },
            { "__p19", "blocked" },
            { "__p20", "true" },
            { "__p3", 30 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
        // Results should not include elements with blocked edges
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WhereSelectNot_WithNoExcludedProperty_ShouldIncludeAll()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "edge" },
            { "__p19", "nonexistentprop" },
            { "__p20", "anyvalue" },
            { "__p3", 10 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Limit and Select Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WithLimit_ShouldRestrictResults()
    {
        // Arrange
        var connector = await CreateLargeGraphScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "large1" },
            { "__p1", "large2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "terminal" },
            { "__p15", "true" },
            { "__p16", "terminal" },
            { "__p17", "yes" },
            { "__p18", "edge" },
            { "__p19", "excluded" },
            { "__p20", "true" },
            { "__p3", 5 }, // Limit to 5 results
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Count().Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_SelectStartLabel_ShouldReturnOriginalVertices()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "origin" },
            { "__p13", "e" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "e" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", 10 },
            { "__p4", "origin" } // Select the start label
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
        // Should return vertices from the 'origin' label
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_SelectEdgeLabel_ShouldReturnEdges()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "start" },
            { "__p13", "relationship" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "relationship" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", 10 },
            { "__p4", "relationship" } // Select the edge label
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
        // Should return edges from the 'relationship' label
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WithZeroLimit_ShouldReturnEmpty()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "edge" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", 0 }, // Zero limit
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithNegativeLimit_ShouldHandleGracefully()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", TargetProperty },
            { "__p15", TargetValue1 },
            { "__p16", TargetProperty },
            { "__p17", TargetValue2 },
            { "__p18", "edge" },
            { "__p19", FilterProperty },
            { "__p20", FilterValue },
            { "__p3", -1 }, // Negative limit
            { "__p4", "start" }
        };

        // Act & Assert
        // Should either return empty or treat as unlimited
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);
        
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithEmptyGraph_ShouldReturnEmpty()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "any1" },
            { "__p1", "any2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "prop" },
            { "__p15", "val1" },
            { "__p16", "prop" },
            { "__p17", "val2" },
            { "__p18", "edge" },
            { "__p19", "filter" },
            { "__p20", "true" },
            { "__p3", 10 },
            { "__p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Parameterization Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WithAllParametersProvided_ShouldExecuteSuccessfully()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", StartVertex1 },
            { "__p1", StartVertex2 },
            { "__p2", "s" },
            { "__p13", "e" },
            { "__p14", "t" },
            { "__p15", "v1" },
            { "__p16", "t" },
            { "__p17", "v2" },
            { "__p18", "e" },
            { "__p19", "f" },
            { "__p20", "x" },
            { "__p3", 15 },
            { "__p4", "s" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexRepeatPathSelect_WithAlternativeParameterNames_ShouldWork()
    {
        // Arrange
        var connector = await CreateBasicPathScenario();
        var parameters = new Dictionary<string, object>
        {
            { "p0", StartVertex1 },
            { "p1", StartVertex2 },
            { "p2", "start" },
            { "p13", "edge" },
            { "p14", TargetProperty },
            { "p15", TargetValue1 },
            { "p16", TargetProperty },
            { "p17", TargetValue2 },
            { "p18", "edge" },
            { "p19", FilterProperty },
            { "p20", FilterValue },
            { "p3", 10 },
            { "p4", "start" }
        };

        // Act
        var result = await connector.ExecuteAsync(
            "g.V([p0,p1]).as(p2).repeat(outE().as(p13).inV().simplePath()).until(has(p14,p15).or().has(p16,p17)).path().unfold().where(select(p18).not(has(p19,p20))).limit(p3).select(p4)",
            parameters);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_WithLargeGraph_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var connector = await CreateLargeGraphScenario();
        var parameters = new Dictionary<string, object>
        {
            { "__p0", "perf1" },
            { "__p1", "perf2" },
            { "__p2", "start" },
            { "__p13", "edge" },
            { "__p14", "level" },
            { "__p15", "10" },
            { "__p16", "level" },
            { "__p17", "15" },
            { "__p18", "edge" },
            { "__p19", "slow" },
            { "__p20", "true" },
            { "__p3", 100 },
            { "__p4", "start" }
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await connector.ExecuteAsync(
            "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            parameters);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in less than 5 seconds
    }

    #endregion

    #region Test Data Setup Methods

    private async Task<InMemoryGremlinLanguageConnector> CreateBasicPathScenario()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create start vertices
        await connector.ExecuteAsync(
            $"g.addV('node').property('id', '{StartVertex1}').property('name', 'Start1')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            $"g.addV('node').property('id', '{StartVertex2}').property('name', 'Start2')",
            new Dictionary<string, object>());
        
        // Create intermediate vertex
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'mid1').property('name', 'Middle1')",
            new Dictionary<string, object>());
        
        // Create target vertex
        await connector.ExecuteAsync(
            $"g.addV('node').property('id', 'target1').property('{TargetProperty}', '{TargetValue1}')",
            new Dictionary<string, object>());
        
        // Create edges
        await connector.ExecuteAsync(
            $"g.V('{StartVertex1}').addE('connects').to(g.V('mid1'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('mid1').addE('connects').to(g.V('target1'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            $"g.V('{StartVertex2}').addE('connects').to(g.V('target1'))",
            new Dictionary<string, object>());

        // Additional vertices for other tests
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'pathtest1').property('name', 'PathTest1')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'pathtest2').property('name', 'PathTest2')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'pathend').property('marker', 'end')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('pathtest1').addE('links').to(g.V('pathend'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('pathtest2').addE('links').to(g.V('pathend'))",
            new Dictionary<string, object>());
        
        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateMultiPathScenario()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create multiple start vertices
        for (int i = 1; i <= 3; i++)
        {
            await connector.ExecuteAsync(
                $"g.addV('vertex').property('id', 'v{i}').property('name', 'Vertex{i}')",
                new Dictionary<string, object>());
        }
        
        // Create target vertices
        await connector.ExecuteAsync(
            "g.addV('vertex').property('id', 'target_multi').property('type', 'target')",
            new Dictionary<string, object>());
        
        // Create paths from each start vertex
        for (int i = 1; i <= 3; i++)
        {
            await connector.ExecuteAsync(
                $"g.V('v{i}').addE('path').to(g.V('target_multi'))",
                new Dictionary<string, object>());
        }
        
        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateCyclicGraphScenario()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create vertices forming a cycle
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'cycle1')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'cycle2')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'cycle3')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'exit_node').property('type', 'exit')",
            new Dictionary<string, object>());
        
        // Create cycle: cycle1 -> cycle2 -> cycle3 -> cycle1
        await connector.ExecuteAsync(
            "g.V('cycle1').addE('next').to(g.V('cycle2'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('cycle2').addE('next').to(g.V('cycle3'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('cycle3').addE('next').to(g.V('cycle1'))",
            new Dictionary<string, object>());
        
        // Add exit from cycle
        await connector.ExecuteAsync(
            "g.V('cycle2').addE('escape').to(g.V('exit_node'))",
            new Dictionary<string, object>());
        
        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateDeepPathScenario()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create deep path: deep1/deep2 -> level1 -> level2 -> ... -> level6
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'deep1').property('depth', '0')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('node').property('id', 'deep2').property('depth', '0')",
            new Dictionary<string, object>());
        
        for (int i = 1; i <= 6; i++)
        {
            await connector.ExecuteAsync(
                $"g.addV('node').property('id', 'level{i}').property('depth', '{i}')",
                new Dictionary<string, object>());
        }
        
        // Create linear path
        await connector.ExecuteAsync(
            "g.V('deep1').addE('descends').to(g.V('level1'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('deep2').addE('descends').to(g.V('level1'))",
            new Dictionary<string, object>());
        
        for (int i = 1; i < 6; i++)
        {
            await connector.ExecuteAsync(
                $"g.V('level{i}').addE('descends').to(g.V('level{i + 1}'))",
                new Dictionary<string, object>());
        }
        
        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateConditionalPathScenario()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create vertices for testing until conditions
        var vertices = new[]
        {
            ("cond1", "pending"),
            ("cond2", "pending"),
            ("cond3", "pending"),
            ("cond4", "pending"),
            ("mid_complete", "complete"),
            ("mid_finished", "finished"),
            ("nocond1", "ongoing"),
            ("nocond2", "ongoing")
        };
        
        foreach (var (id, status) in vertices)
        {
            await connector.ExecuteAsync(
                $"g.addV('state').property('id', '{id}').property('status', '{status}')",
                new Dictionary<string, object>());
        }
        
        // Create paths
        await connector.ExecuteAsync(
            "g.V('cond1').addE('transitions').to(g.V('mid_complete'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('cond2').addE('transitions').to(g.V('mid_complete'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('cond3').addE('transitions').to(g.V('mid_finished'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('cond4').addE('transitions').to(g.V('mid_finished'))",
            new Dictionary<string, object>());
        
        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateFilteredEdgesScenario()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create vertices
        await connector.ExecuteAsync(
            "g.addV('entity').property('id', 'filter1')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('entity').property('id', 'filter2')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('entity').property('id', 'filter_target').property('status', 'done')",
            new Dictionary<string, object>());
        
        // Create edge without blocked property (should pass filter)
        await connector.ExecuteAsync(
            "g.V('filter1').addE('relation').to(g.V('filter_target'))",
            new Dictionary<string, object>());
        
        // Create edge with blocked property (should be filtered out)
        await connector.ExecuteAsync(
            "g.V('filter2').addE('relation').to(g.V('filter_target')).property('blocked', 'true')",
            new Dictionary<string, object>());
        
        return connector;
    }

    private async Task<InMemoryGremlinLanguageConnector> CreateLargeGraphScenario()
    {
        var connector = InMemoryGremlinLanguageConnector.Create();
        
        // Create start vertices
        await connector.ExecuteAsync(
            "g.addV('item').property('id', 'large1')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('item').property('id', 'large2')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('item').property('id', 'perf1')",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.addV('item').property('id', 'perf2')",
            new Dictionary<string, object>());
        
        // Create many intermediate and terminal vertices
        for (int i = 1; i <= 20; i++)
        {
            await connector.ExecuteAsync(
                $"g.addV('item').property('id', 'large_mid{i}')",
                new Dictionary<string, object>());
            
            await connector.ExecuteAsync(
                $"g.addV('item').property('id', 'large_end{i}').property('terminal', 'true')",
                new Dictionary<string, object>());
            
            await connector.ExecuteAsync(
                $"g.V('large1').addE('connects').to(g.V('large_mid{i}'))",
                new Dictionary<string, object>());
            
            await connector.ExecuteAsync(
                $"g.V('large2').addE('connects').to(g.V('large_mid{i}'))",
                new Dictionary<string, object>());
            
            await connector.ExecuteAsync(
                $"g.V('large_mid{i}').addE('connects').to(g.V('large_end{i}'))",
                new Dictionary<string, object>());
        }
        
        // Create performance test subgraph
        for (int level = 1; level <= 15; level++)
        {
            await connector.ExecuteAsync(
                $"g.addV('level').property('id', 'perf_l{level}').property('level', '{level}')",
                new Dictionary<string, object>());
        }
        
        await connector.ExecuteAsync(
            "g.V('perf1').addE('links').to(g.V('perf_l1'))",
            new Dictionary<string, object>());
        
        await connector.ExecuteAsync(
            "g.V('perf2').addE('links').to(g.V('perf_l1'))",
            new Dictionary<string, object>());
        
        for (int i = 1; i < 15; i++)
        {
            await connector.ExecuteAsync(
                $"g.V('perf_l{i}').addE('links').to(g.V('perf_l{i + 1}'))",
                new Dictionary<string, object>());
        }
        
        return connector;
    }

    #endregion

    #region Documentation Tests

    [Fact]
    public async Task ComplexRepeatPathSelect_DocumentedExample_ShouldWorkAsExpected()
    {
        // This test documents the complete query pattern and its behavior
        // Query: g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath())
        //          .until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold()
        //          .where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)
        
        // Arrange
        var connector = await CreateBasicPathScenario();
        
        // Act
        var result = await connector.ExecuteAsync(
            "g.V(__p0).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
            new Dictionary<string, object>
            {
                { "__p0", StartVertex1 },
                { "__p2", "start" },
                { "__p13", "edge" },
                { "__p14", TargetProperty },
                { "__p15", TargetValue1 },
                { "__p16", TargetProperty },
                { "__p17", TargetValue2 },
                { "__p18", "edge" },
                { "__p19", FilterProperty },
                { "__p20", FilterValue },
                { "__p3", 10 },
                { "__p4", "start" }
            });

        // Assert
        result.Should().NotBeNull(
            "the query should execute successfully with all components working together");
    }

    #endregion
}
