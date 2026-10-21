using Microsoft.Extensions.Logging;
using Moq;
using Stardust.Paradox.GremlinStudio.Core.Connections;

namespace Stardust.Paradox.GremlinStudioTests;

/// <summary>
/// Tests that verify <see cref="FileGremlinConnectionStore"/> correctly deserializes
/// connections.json regardless of property casing and enum format.
/// These directly validate the fix for the list_connections MCP tool returning empty.
/// </summary>
public class ConnectionStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _connectionsFilePath;
    private readonly Mock<ISecureSecretStore> _mockSecretStore;
    private readonly Mock<ILogger<FileGremlinConnectionStore>> _mockLogger;

    public ConnectionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GremlinStudioTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _connectionsFilePath = Path.Combine(_tempDir, "connections.json");
        _mockSecretStore = new Mock<ISecureSecretStore>();
        _mockLogger = new Mock<ILogger<FileGremlinConnectionStore>>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private FileGremlinConnectionStore CreateStore()
    {
        // Override the metadata file path via reflection since it's set from AppDataPaths
        var store = new FileGremlinConnectionStore(_mockSecretStore.Object, _mockLogger.Object);
        var field = typeof(FileGremlinConnectionStore).GetField("_metadataFilePath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(store, _connectionsFilePath);
        return store;
    }

    [Fact]
    public async Task GetAllConnections_WithPascalCaseJson_ReturnsConnections()
    {
        // Arrange - PascalCase property names (standard .NET serialization)
        var json = """
        [
          {
            "Id": "1c26f66c-ddf1-40f6-af51-aed9a007d32a",
            "Name": "DevTest notifications",
            "Kind": "CosmosDb",
            "Host": "myhost.gremlin.cosmos.azure.com",
            "Port": 443,
            "Username": "/dbs/notifications/colls/graph",
            "EnableSsl": true,
            "DatabaseName": "notifications",
            "GraphName": "graph",
            "PoolSize": 4,
            "MaxConnectionPoolSize": 8,
            "ConnectionTimeoutSeconds": 30,
            "CreatedAt": "2024-06-15T10:30:00+00:00"
          }
        ]
        """;
        await File.WriteAllTextAsync(_connectionsFilePath, json);
        var store = CreateStore();

        // Act
        var result = await store.GetAllConnectionsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("1c26f66c-ddf1-40f6-af51-aed9a007d32a", result[0].Id);
        Assert.Equal("DevTest notifications", result[0].Name);
        Assert.Equal(GremlinConnectionKind.CosmosDb, result[0].Kind);
        Assert.Equal("myhost.gremlin.cosmos.azure.com", result[0].Host);
        Assert.Equal(443, result[0].Port);
    }

    [Fact]
    public async Task GetAllConnections_WithCamelCaseJson_ReturnsConnections()
    {
        // Arrange - camelCase property names (as produced by some serializers or legacy versions)
        var json = """
        [
          {
            "id": "e8766022-aaaa-bbbb-cccc-123456789abc",
            "name": "Production Graph",
            "kind": "CosmosDb",
            "host": "prod.gremlin.cosmos.azure.com",
            "port": 443,
            "username": "/dbs/mydb/colls/mygraph",
            "enableSsl": true,
            "databaseName": "mydb",
            "graphName": "mygraph",
            "poolSize": 4,
            "maxConnectionPoolSize": 8,
            "connectionTimeoutSeconds": 30,
            "createdAt": "2024-01-10T08:00:00+00:00"
          }
        ]
        """;
        await File.WriteAllTextAsync(_connectionsFilePath, json);
        var store = CreateStore();

        // Act
        var result = await store.GetAllConnectionsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("e8766022-aaaa-bbbb-cccc-123456789abc", result[0].Id);
        Assert.Equal("Production Graph", result[0].Name);
        Assert.Equal(GremlinConnectionKind.CosmosDb, result[0].Kind);
        Assert.Equal("prod.gremlin.cosmos.azure.com", result[0].Host);
        Assert.Equal(443, result[0].Port);
        Assert.Equal("mydb", result[0].DatabaseName);
        Assert.Equal("mygraph", result[0].GraphName);
    }

    [Fact]
    public async Task GetAllConnections_WithNumericEnumValue_ReturnsConnections()
    {
        // Arrange - numeric enum values (possible from System.Text.Json default without converter)
        var json = """
        [
          {
            "Id": "46420ee5-1111-2222-3333-444455556666",
            "Name": "TinkerPop Server",
            "Kind": 1,
            "Host": "localhost",
            "Port": 8182,
            "EnableSsl": false,
            "CreatedAt": "2024-03-20T12:00:00+00:00"
          }
        ]
        """;
        await File.WriteAllTextAsync(_connectionsFilePath, json);
        var store = CreateStore();

        // Act
        var result = await store.GetAllConnectionsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("TinkerPop Server", result[0].Name);
        Assert.Equal(GremlinConnectionKind.GremlinServer, result[0].Kind);
        Assert.Equal("localhost", result[0].Host);
        Assert.Equal(8182, result[0].Port);
        Assert.False(result[0].EnableSsl);
    }

    [Fact]
    public async Task GetAllConnections_WithStringEnumValue_ReturnsConnections()
    {
        // Arrange - string enum values (the format that previously caused deserialization failure)
        var json = """
        [
          {
            "Id": "899f65c0-dead-beef-cafe-abcdef012345",
            "Name": "InMemory Playground",
            "Kind": "InMemory",
            "Host": "",
            "Port": 0,
            "EnableSsl": false,
            "ScenarioName": "social-network",
            "CreatedAt": "2024-05-01T00:00:00+00:00"
          }
        ]
        """;
        await File.WriteAllTextAsync(_connectionsFilePath, json);
        var store = CreateStore();

        // Act
        var result = await store.GetAllConnectionsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("InMemory Playground", result[0].Name);
        Assert.Equal(GremlinConnectionKind.InMemory, result[0].Kind);
        Assert.Equal("social-network", result[0].ScenarioName);
    }

    [Fact]
    public async Task GetAllConnections_WithMultipleConnections_ReturnsAll()
    {
        // Arrange - mixed casing and formats, mimicking a real-world file
        var json = """
        [
          {
            "id": "1c26f66c-ddf1-40f6-af51-aed9a007d32a",
            "name": "DevTest notifications",
            "kind": "CosmosDb",
            "host": "dev.gremlin.cosmos.azure.com",
            "port": 443,
            "enableSsl": true,
            "databaseName": "notifications",
            "graphName": "graph",
            "createdAt": "2024-06-15T10:30:00+00:00"
          },
          {
            "Id": "e8766022-2222-3333-4444-555566667777",
            "Name": "Local TinkerPop",
            "Kind": "GremlinServer",
            "Host": "localhost",
            "Port": 8182,
            "EnableSsl": false,
            "CreatedAt": "2024-07-01T00:00:00+00:00"
          },
          {
            "Id": "46420ee5-aaaa-bbbb-cccc-ddddeeee0000",
            "Name": "Test InMemory",
            "Kind": "InMemory",
            "Host": "",
            "Port": 0,
            "EnableSsl": false,
            "ScenarioName": "modern-graph",
            "CreatedAt": "2024-08-01T00:00:00+00:00"
          }
        ]
        """;
        await File.WriteAllTextAsync(_connectionsFilePath, json);
        var store = CreateStore();

        // Act
        var result = await store.GetAllConnectionsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("DevTest notifications", result[0].Name);
        Assert.Equal("Local TinkerPop", result[1].Name);
        Assert.Equal("Test InMemory", result[2].Name);
    }

    [Fact]
    public async Task GetAllConnections_WhenFileDoesNotExist_ReturnsEmpty()
    {
        // Arrange - no file exists
        var store = CreateStore();

        // Act
        var result = await store.GetAllConnectionsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllConnections_WithMalformedJson_ReturnsEmptyAndLogs()
    {
        // Arrange
        await File.WriteAllTextAsync(_connectionsFilePath, "not valid json {{{");
        var store = CreateStore();

        // Act
        var result = await store.GetAllConnectionsAsync();

        // Assert
        Assert.Empty(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
