using Microsoft.Extensions.Logging;
using Moq;
using Stardust.Paradox.GremlinStudio.Core.Playground;

namespace Stardust.Paradox.GremlinStudioTests;

public class PlaygroundServiceTests
{
    private readonly Mock<ILogger<PlaygroundService>> _mockLogger;

    public PlaygroundServiceTests()
    {
        _mockLogger = new Mock<ILogger<PlaygroundService>>();
    }

    [Fact]
    public void Start_WhenNotRunning_StartsPlayground()
    {
        // Arrange
        using var service = new PlaygroundService(_mockLogger.Object);

        // Act
        service.Start();

        // Assert
        Assert.True(service.State.IsRunning);
        Assert.NotNull(service.Connector);
    }

    [Fact]
    public void Stop_WhenRunning_StopsPlayground()
    {
        // Arrange
        using var service = new PlaygroundService(_mockLogger.Object);
        service.Start();

        // Act
        service.Stop();

        // Assert
        Assert.False(service.State.IsRunning);
        Assert.Null(service.Connector);
    }

    [Fact]
    public void GetAvailableScenarios_ReturnsScenarios()
    {
        // Arrange
        using var service = new PlaygroundService(_mockLogger.Object);

        // Act
        var scenarios = service.GetAvailableScenarios();

        // Assert
        Assert.NotNull(scenarios);
        Assert.True(scenarios.Count > 0);
    }

    [Fact]
    public async Task RefreshState_WhenRunning_ReturnsStats()
    {
        // Arrange
        using var service = new PlaygroundService(_mockLogger.Object);
        service.Start();

        // Act
        var state = await service.RefreshStateAsync();

        // Assert
        Assert.True(state.IsRunning);
        Assert.NotNull(state.VertexLabels);
        Assert.NotNull(state.EdgeLabels);
    }

    [Fact]
    public void StateChanged_WhenStarted_FiresEvent()
    {
        // Arrange
        using var service = new PlaygroundService(_mockLogger.Object);
        PlaygroundState? receivedState = null;
        service.StateChanged += (_, state) => receivedState = state;

        // Act
        service.Start();

        // Assert
        Assert.NotNull(receivedState);
        Assert.True(receivedState.IsRunning);
    }
}
