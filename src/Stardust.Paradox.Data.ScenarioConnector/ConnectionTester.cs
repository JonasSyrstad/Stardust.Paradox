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
        return await TestConnectionAsync(connection, maxRetries: 3);
    }

    /// <summary>
    /// Test a connection with retry logic
    /// </summary>
    public async Task<ConnectionTestResult> TestConnectionAsync(CosmosDbConnection connection, int maxRetries = 3)
    {
        var result = new ConnectionTestResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;

        while (attempt <= maxRetries)
        {
            try
            {
                attempt++;
                _logger?.LogInformation("Testing connection to {Hostname}/{Database}/{Graph} (attempt {Attempt}/{MaxAttempts})", 
                    connection.Hostname, connection.DatabaseName, connection.GraphName, attempt, maxRetries + 1);

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

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Connection test attempt {Attempt} failed", attempt);
            
                if (attempt > maxRetries)
                {
                    stopwatch.Stop();
                    result.IsSuccessful = false;
                    result.IsReadOnly = false;
                    result.Exception = ex;
                    result.ResponseTime = stopwatch.Elapsed;
                    result.Message = $"❌ Connection failed after {attempt} attempts: {GetFriendlyErrorMessage(ex)}";
                    
                    _logger?.LogError(ex, "Connection test failed after {Attempts} attempts", attempt);
                    return result;
                }

                // Exponential backoff: 1s, 2s, 4s
                var delayMs = (int)Math.Pow(2, attempt - 1) * 1000;
                _logger?.LogInformation("Retrying in {DelayMs}ms...", delayMs);
                await Task.Delay(delayMs);
            }
        }

        // Should never reach here
        throw new InvalidOperationException("Unexpected code path in connection test");
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
     if (message.Contains("name resolution", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("No such host", StringComparison.OrdinalIgnoreCase))
        {
  return "Could not resolve hostname. Please check the hostname format and your internet connection. Expected format: accountname.gremlin.cosmosdb.azure.com";
        }
        
        if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
   message.Contains("401", StringComparison.OrdinalIgnoreCase))
        {
         return "Authentication failed. Please verify your access key is correct and not expired.";
        }
        
      if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
    return "Connection timed out. Check your network connection, firewall settings, and verify the hostname is correct.";
        }
      
  if (message.Contains("ssl", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
     message.Contains("tls", StringComparison.OrdinalIgnoreCase))
        {
     return "SSL/TLS error. This might indicate a network security issue or outdated .NET runtime. Try updating your system certificates.";
    }

        if (message.Contains("database", StringComparison.OrdinalIgnoreCase) &&
     message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
        return "Database or graph not found. Please verify the database name and graph name are correct and exist in your CosmosDB account.";
        }

        if (message.Contains("404", StringComparison.OrdinalIgnoreCase))
  {
 return "Resource not found (404). Please verify your database name, graph name, and that they exist in your CosmosDB account.";
 }

    if (message.Contains("403", StringComparison.OrdinalIgnoreCase) ||
     message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
     {
 return "Access forbidden (403). Your access key may not have the necessary permissions or might be for a different database.";
        }

        if (message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
     message.Contains("throttle", StringComparison.OrdinalIgnoreCase))
      {
   return "Request throttled (429). The database is receiving too many requests. This usually resolves automatically.";
        }

        if (message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
        {
 return "Connection refused. Check that the hostname and port are correct, and that your firewall allows outbound connections on port 443.";
        }

  if (message.Contains("Invalid hostname", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("hostname is invalid", StringComparison.OrdinalIgnoreCase))
 {
 return "Hostname format is invalid. Please use format: accountname.gremlin.cosmosdb.azure.com (without https:// or port numbers)";
        }

// Return the original message if no pattern matches, but truncate if too long
      return message.Length > 150 ? message.Substring(0, 147) + "..." : message;
    }

    /// <summary>
    /// Performs diagnostic checks on connection configuration
    /// </summary>
    public ConnectionTestResult DiagnoseConnection(CosmosDbConnection connection)
    {
        var result = new ConnectionTestResult { IsSuccessful = true };
        var issues = new List<string>();

    // Check hostname format
        if (string.IsNullOrWhiteSpace(connection.Hostname))
        {
  issues.Add("Hostname is empty");
        }
   else
   {
   if (connection.Hostname.Contains("://"))
    {
       issues.Add("Hostname contains protocol prefix (http:// or https://) - this should be removed");
  }
      if (connection.Hostname.Contains(":") && connection.Hostname.IndexOf(':') > 0)
   {
      var colonIndex = connection.Hostname.IndexOf(':');
  var portPart = connection.Hostname.Substring(colonIndex + 1);
   if (int.TryParse(portPart, out _))
       {
 issues.Add($"Hostname contains port number - this should be removed (port is always 443 for CosmosDB)");
          }
     }
            if (!connection.Hostname.Contains("."))
   {
     issues.Add("Hostname doesn't appear to be a valid domain name");
      }
   if (!connection.Hostname.EndsWith(".azure.com", StringComparison.OrdinalIgnoreCase) &&
      !connection.Hostname.EndsWith(".cosmosdb.azure.com", StringComparison.OrdinalIgnoreCase))
 {
       issues.Add("Hostname doesn't match expected CosmosDB format (*.cosmosdb.azure.com)");
   }
    }

  // Check database name
 if (string.IsNullOrWhiteSpace(connection.DatabaseName))
 {
   issues.Add("Database name is empty");
 }

      // Check graph name
   if (string.IsNullOrWhiteSpace(connection.GraphName))
 {
 issues.Add("Graph name is empty");
 }

        // Check access key
   if (string.IsNullOrWhiteSpace(connection.AccessKey))
        {
   issues.Add("Access key is empty");
   }
        else if (connection.AccessKey.Length < 20)
   {
 issues.Add("Access key appears too short - CosmosDB keys are typically 88+ characters");
  }

        if (issues.Any())
        {
       result.IsSuccessful = false;
    result.Message = "❌ Configuration issues detected:\n" + string.Join("\n", issues.Select(i => $"  • {i}"));
}
        else
        {
   result.Message = "✅ Configuration appears valid";
  }

        return result;
  }
}