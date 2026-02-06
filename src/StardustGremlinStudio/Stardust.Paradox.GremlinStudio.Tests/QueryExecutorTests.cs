using Microsoft.Extensions.Logging;
using Moq;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.GremlinStudio.Core.Execution;

namespace Stardust.Paradox.GremlinStudioTests;

public class QueryExecutorTests
{
    private readonly Mock<ILogger<GremlinQueryExecutor>> _mockLogger;

    public QueryExecutorTests()
    {
        _mockLogger = new Mock<ILogger<GremlinQueryExecutor>>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidQuery_ReturnsSuccess()
    {
        // Arrange
        var executor = new GremlinQueryExecutor(_mockLogger.Object);
        using var connector = new InMemoryGremlinLanguageConnector();

        // Act
        var result = await executor.ExecuteAsync(connector, "g.V().limit(1)");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResultJson);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidQuery_ReturnsFailure()
    {
        // Arrange
        var executor = new GremlinQueryExecutor(_mockLogger.Object);
        using var connector = new InMemoryGremlinLanguageConnector();

        // Act
        var result = await executor.ExecuteAsync(connector, "g.invalidStep()");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void GetExecutionLog_AfterExecution_ContainsEntry()
    {
        // Arrange
        var executor = new GremlinQueryExecutor(_mockLogger.Object);
        using var connector = new InMemoryGremlinLanguageConnector();

        // Act
        _ = executor.ExecuteAsync(connector, "g.V().count()").GetAwaiter().GetResult();
        var log = executor.GetExecutionLog();

        // Assert
        Assert.Single(log);
        Assert.Equal("g.V().count()", log[0].Query);
    }

    [Fact]
    public void ClearExecutionLog_AfterClear_LogIsEmpty()
    {
        // Arrange
        var executor = new GremlinQueryExecutor(_mockLogger.Object);
        using var connector = new InMemoryGremlinLanguageConnector();
        _ = executor.ExecuteAsync(connector, "g.V().count()").GetAwaiter().GetResult();

        // Act
        executor.ClearExecutionLog();

        // Assert
        Assert.Empty(executor.GetExecutionLog());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ReturnsFailure()
    {
        // Arrange
        var executor = new GremlinQueryExecutor(_mockLogger.Object);
        using var connector = new InMemoryGremlinLanguageConnector();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await executor.ExecuteAsync(
            connector, 
            "g.V().count()", 
            cancellationToken: cts.Token);

        // Assert
        Assert.False(result.IsSuccess);
    }
}
