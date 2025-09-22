using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Basic functionality tests for InMemoryGremlinLanguageConnector
/// </summary>
public class InMemoryGremlinConnectorBasicTests
{
    [Fact]
    public void Create_WithDefaultOptions_ShouldReturnValidConnector()
    {
        // Act
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Assert
        connector.Should().NotBeNull();
        connector.CanParameterizeQueries.Should().BeTrue();
        connector.ConsumedRU.Should().Be(0.0);
    }

    [Fact]
    public void Create_WithCustomOptions_ShouldApplyOptions()
    {
        // Arrange
        var options = new InMemoryDatabaseOptions
        {
            LogQueries = true,
            SimulatedRUPerQuery = 2.5,
            EnableQueryLogging = true
        };

        // Act
        var connector = InMemoryGremlinLanguageConnector.Create(options);

        // Assert
        connector.Should().NotBeNull();
        connector.CanParameterizeQueries.Should().BeTrue();
    }

    [Fact]
    public void Create_WithConfigurationAction_ShouldApplyConfiguration()
    {
        // Act
        var connector = InMemoryGremlinLanguageConnector.Create(opts =>
        {
            opts.LogQueries = true;
            opts.SimulatedRUPerQuery = 3.0;
        });

        // Assert
        connector.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullQuery_ShouldThrowArgumentException()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            connector.ExecuteAsync(null!, new Dictionary<string, object>()));
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyQuery_ShouldThrowArgumentException()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            connector.ExecuteAsync("", new Dictionary<string, object>()));
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceQuery_ShouldThrowArgumentException()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            connector.ExecuteAsync("   ", new Dictionary<string, object>()));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullParameters_ShouldNotThrow()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        var result = await connector.ExecuteAsync("g.V().count()", null);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsumedRU_ShouldTrackCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts =>
        {
            opts.SimulatedRUPerQuery = 2.5;
        });

        // Act
        await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.E().count()", new Dictionary<string, object>());

        // Assert
        connector.ConsumedRU.Should().Be(5.0);
    }

    [Fact]
    public void Clear_ShouldResetRUAndData()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(opts =>
        {
            opts.SimulatedRUPerQuery = 1.0;
        });

        // Act
        connector.AddVertex("test", "test1");
        connector.Clear();

        // Assert
        connector.ConsumedRU.Should().Be(0.0);
        connector.GetAllVertices().Should().BeEmpty();
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act
        connector.AddVertex("person", "p1");
        connector.AddVertex("person", "p2");
        connector.AddEdge("knows", "p1", "p2");

        var (vertexCount, edgeCount) = connector.GetStatistics();

        // Assert
        vertexCount.Should().Be(2);
        edgeCount.Should().Be(1);
    }

    [Fact]
    public void ImportExportData_ShouldPreserveData()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();
        connector.AddVertex("person", "p1");
        connector.AddVertex("person", "p2");
        connector.AddEdge("knows", "p1", "p2");

        // Act
        var (vertices, edges) = connector.ExportData();
        connector.Clear();
        // Use the extension method ImportData from Extensions namespace which handles IEnumerable<InMemoryVertex>
        connector.Database.ImportData(vertices, edges);

        var (vertexCount, edgeCount) = connector.GetStatistics();

        // Assert
        vertexCount.Should().Be(2);
        edgeCount.Should().Be(1);
    }
}