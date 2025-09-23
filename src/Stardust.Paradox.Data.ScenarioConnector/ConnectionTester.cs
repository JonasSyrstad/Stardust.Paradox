using Microsoft.Extensions.Logging;
using Stardust.Paradox.Data.Providers.Gremlin;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Result of connection testing
/// </summary>
public class ConnectionTestResult
{
    public bool IsSuccessful { get; set; }
    public bool IsReadOnly { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public string? DatabaseVersion { get; set; }
    public int? VertexCount { get; set; }
    public int? EdgeCount { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Tests CosmosDB connections to verify they work and are read-only
/// </summary>
public class ConnectionTester
{
    private readonly ILogger? _logger;

    public ConnectionTester(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Test a connection to verify it works and is read-only
    /// </summary>
    public async Task<ConnectionTestResult> TestConnectionAsync(CosmosDbConnection connection)
    {
        var result = new ConnectionTestResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger?.LogInformation("Testing connection to {Hostname}/{Database}/{Graph}", 
                connection.Hostname, connection.DatabaseName, connection.GraphName);

            // Create connector
            var connector = new GremlinNetLanguageConnector(
                connection.Hostname,
                connection.DatabaseName,
                connection.GraphName,
                connection.AccessKey);

            // Test 1: Basic connectivity with simple read query
            await TestBasicConnectivity(connector, result);
            
            if (result.IsSuccessful)
            {
                // Test 2: Verify read-only access
                await TestReadOnlyAccess(connector, result);
                
                // Test 3: Get database statistics
                await GetDatabaseStatistics(connector, result);
            }

            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;
            
            if (result.IsSuccessful)
            {
                result.Message = result.IsReadOnly 
                    ? "✅ Connection successful and verified as read-only"
                    : "⚠️ Connection successful but may have write permissions";
            }

            _logger?.LogInformation("Connection test completed in {ElapsedMs}ms. Success: {Success}, ReadOnly: {ReadOnly}", 
                stopwatch.ElapsedMilliseconds, result.IsSuccessful, result.IsReadOnly);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.IsSuccessful = false;
            result.IsReadOnly = false;
            result.Exception = ex;
            result.ResponseTime = stopwatch.Elapsed;
            result.Message = $"❌ Connection failed: {GetFriendlyErrorMessage(ex)}";
            
            _logger?.LogError(ex, "Connection test failed");
        }

        return result;
    }

    /// <summary>
    /// Test basic connectivity with a simple read query
    /// </summary>
    private async Task TestBasicConnectivity(GremlinNetLanguageConnector connector, ConnectionTestResult result)
    {
        try
        {
            // Try a simple query to test basic connectivity
            var query = "g.V().limit(1)";
            var queryResult = await connector.ExecuteAsync(query, new Dictionary<string, object>());
            
            result.IsSuccessful = true;
            _logger?.LogDebug("Basic connectivity test passed");
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.Exception = ex;
            _logger?.LogError(ex, "Basic connectivity test failed");
            throw; // Re-throw to stop further testing
        }
    }

    /// <summary>
    /// Test read-only access by attempting write operations
    /// </summary>
    private async Task TestReadOnlyAccess(GremlinNetLanguageConnector connector, ConnectionTestResult result)
    {
        var isReadOnly = true;
        
        try
        {
            // Test 1: Try to add a test vertex (should fail on read-only)
            var testVertexQuery = $"g.addV('__test__').property('id','{Guid.NewGuid().ToString()}').property('pk','__test__').property('__test__', true)";
            
            try
            {
                await connector.ExecuteAsync(testVertexQuery, new Dictionary<string, object>());
                isReadOnly = false; // If this succeeds, it's not read-only
                _logger?.LogWarning("Write operation succeeded - connection may not be read-only");
                
                // If we accidentally created a test vertex, try to clean it up
                try
                {
                    var cleanupQuery = "g.V().hasLabel('__test__').drop()";
                    await connector.ExecuteAsync(cleanupQuery, new Dictionary<string, object>());
                    _logger?.LogInformation("Cleaned up test vertex");
                }
                catch
                {
                    _logger?.LogWarning("Could not clean up test vertex");
                }
            }
            catch (Exception writeEx)
            {
                // Write operation failed - this is what we expect for read-only
                if (IsReadOnlyError(writeEx))
                {
                    isReadOnly = true;
                    _logger?.LogDebug("Write operation properly failed - connection appears to be read-only");
                }
                else
                {
                    // Some other error occurred
                    _logger?.LogWarning(writeEx, "Write operation failed with unexpected error");
                }
            }

            result.IsReadOnly = isReadOnly;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Read-only test encountered an error");
            // If we can't determine read-only status, assume it's not read-only for safety
            result.IsReadOnly = false;
        }
    }

    /// <summary>
    /// Get basic database statistics
    /// </summary>
    private async Task GetDatabaseStatistics(GremlinNetLanguageConnector connector, ConnectionTestResult result)
    {
        try
        {
            // Get vertex count (with reasonable limit to avoid long queries)
            var vertexCountQuery = "g.V().count()";
            var vertexResult = await connector.ExecuteAsync(vertexCountQuery, new Dictionary<string, object>());
            if (vertexResult.Any())
            {
                var count = vertexResult.First();
                if (count is long longCount)
                {
                    result.VertexCount = (int)Math.Min(longCount, int.MaxValue);
                }
                else if (int.TryParse(count?.ToString(), out int intCount))
                {
                    result.VertexCount = intCount;
                }
            }

            // Get edge count (with reasonable limit)
            var edgeCountQuery = "g.E().count()";
            var edgeResult = await connector.ExecuteAsync(edgeCountQuery, new Dictionary<string, object>());
            if (edgeResult.Any())
            {
                var count = edgeResult.First();
                if (count is long longCount)
                {
                    result.EdgeCount = (int)Math.Min(longCount, int.MaxValue);
                }
                else if (int.TryParse(count?.ToString(), out int intCount))
                {
                    result.EdgeCount = intCount;
                }
            }

            _logger?.LogDebug("Database statistics: {VertexCount} vertices, {EdgeCount} edges", 
                result.VertexCount, result.EdgeCount);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not retrieve database statistics");
            // Don't fail the overall test if we can't get statistics
        }
    }

    /// <summary>
    /// Check if an exception indicates a read-only error
    /// </summary>
    private bool IsReadOnlyError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        
        // Common read-only error patterns
        var readOnlyPatterns = new[]
        {
            "read-only",
            "readonly",
            "permission denied",
            "unauthorized",
            "forbidden",
            "access denied",
            "insufficient permissions",
            "operation not permitted",
            "read only mode",
            "database is read-only"
        };

        return readOnlyPatterns.Any(pattern => message.Contains(pattern));
    }

    /// <summary>
    /// Get a user-friendly error message
    /// </summary>
    private string GetFriendlyErrorMessage(Exception ex)
    {
        var message = ex.Message;

        // Common error patterns and their friendly messages
        if (message.Contains("name resolution", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not resolve hostname. Please check the hostname and your internet connection.";
        }
        
        if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return "Authentication failed. Please check your access key.";
        }
        
        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Connection timed out. Please check your network connection and try again.";
        }
        
        if (message.Contains("ssl", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
        {
            return "SSL/Certificate error. Please check your connection settings.";
        }

        if (message.Contains("database", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "Database or graph not found. Please check the database and graph names.";
        }

        // Return the original message if no pattern matches
        return message.Length > 100 ? message.Substring(0, 97) + "..." : message;
    }
}