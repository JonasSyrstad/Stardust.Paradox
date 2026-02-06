using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;

namespace Stardust.Paradox.GremlinStudio.Core.Connections;

/// <summary>
/// Service for discovering databases and graphs in Cosmos DB using the REST API.
/// </summary>
public class CosmosDbDiscoveryService : ICosmosDbDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CosmosDbDiscoveryService> _logger;

    public CosmosDbDiscoveryService(HttpClient httpClient, ILogger<CosmosDbDiscoveryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> DiscoverDatabasesAsync(
        string endpoint, 
        string key, 
        CancellationToken cancellationToken = default)
    {
        var databases = new List<string>();
        
        try
        {
            var uri = new Uri(new Uri(endpoint), "/dbs");
            var dateString = DateTime.UtcNow.ToString("r");
            
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("x-ms-date", dateString);
            request.Headers.Add("x-ms-version", "2018-12-31");
            
            // Build proper authorization header for Cosmos DB
            var authToken = GenerateAuthToken(HttpMethod.Get, "dbs", "", dateString, key);
            request.Headers.TryAddWithoutValidation("Authorization", authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonDocument.Parse(content);

            if (json.RootElement.TryGetProperty("Databases", out var databasesArray))
            {
                foreach (var db in databasesArray.EnumerateArray())
                {
                    if (db.TryGetProperty("id", out var idProp))
                    {
                        databases.Add(idProp.GetString() ?? "");
                    }
                }
            }

            _logger.LogInformation("Discovered {Count} databases in Cosmos DB", databases.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover databases");
            throw;
        }

        return databases;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> DiscoverGraphsAsync(
        string endpoint, 
        string key, 
        string database, 
        CancellationToken cancellationToken = default)
    {
        var graphs = new List<string>();
        
        try
        {
            var uri = new Uri(new Uri(endpoint), $"/dbs/{database}/colls");
            var dateString = DateTime.UtcNow.ToString("r");
            
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("x-ms-date", dateString);
            request.Headers.Add("x-ms-version", "2018-12-31");
            
            // Build proper authorization header for Cosmos DB
            var authToken = GenerateAuthToken(HttpMethod.Get, "colls", $"dbs/{database}", dateString, key);
            request.Headers.TryAddWithoutValidation("Authorization", authToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonDocument.Parse(content);

            if (json.RootElement.TryGetProperty("DocumentCollections", out var collectionsArray))
            {
                foreach (var coll in collectionsArray.EnumerateArray())
                {
                    if (coll.TryGetProperty("id", out var idProp))
                    {
                        graphs.Add(idProp.GetString() ?? "");
                    }
                }
            }

            _logger.LogInformation("Discovered {Count} graphs in database {Database}", graphs.Count, database);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover graphs in database {Database}", database);
            throw;
        }

        return graphs;
    }

    /// <summary>
    /// Generates the complete authorization token for Cosmos DB REST API.
    /// Format: type={typeoftoken}&amp;ver={tokenversion}&amp;sig={hashsignature}
    /// </summary>
    private static string GenerateAuthToken(
        HttpMethod verb, 
        string resourceType, 
        string resourceId, 
        string date, 
        string key)
    {
        var keyBytes = Convert.FromBase64String(key);
        
        // Build the payload to sign
        var payload = $"{verb.Method.ToLowerInvariant()}\n{resourceType.ToLowerInvariant()}\n{resourceId}\n{date.ToLowerInvariant()}\n\n";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        
        // Compute HMAC-SHA256 signature
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var signature = Convert.ToBase64String(hash);
        
        // Build the complete authorization token
        // Format: type=master&ver=1.0&sig={signature}
        return $"type=master&ver=1.0&sig={HttpUtility.UrlEncode(signature)}";
    }
}
