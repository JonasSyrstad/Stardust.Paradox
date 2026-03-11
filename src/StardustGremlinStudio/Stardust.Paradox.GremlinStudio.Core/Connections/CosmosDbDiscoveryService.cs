using System.Collections.Concurrent;
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

    /// <summary>
    /// Cached partition key ranges per endpoint/db/container.
    /// The Cosmos DB REST API gateway cannot serve cross-partition queries directly;
    /// instead we must fan out to each physical partition. Ranges are stable and
    /// rarely change, so caching avoids repeated lookups within a session.
    /// </summary>
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _partitionRangeCache = new();

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

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, long>> DiscoverLabelCountsAsync(
        string endpoint,
        string key,
        string database,
        string container,
        bool isEdge,
        CancellationToken cancellationToken = default)
    {
        var ranges = await GetPartitionKeyRangesAsync(endpoint, key, database, container, cancellationToken)
            .ConfigureAwait(false);

        var filter = isEdge
            ? "c._isEdge = true"
            : "NOT IS_DEFINED(c._isEdge)";

        // GROUP BY works within a single partition — the cross-partition restriction
        // only applies when the gateway must merge results from multiple partitions.
        // We target each partition individually and merge the label?count maps client-side.
        var sqlQuery = new
        {
            query = $"SELECT c.label, COUNT(1) AS cnt FROM c WHERE {filter} GROUP BY c.label",
            parameters = Array.Empty<object>()
        };

        var tasks = ranges.Select(rangeId =>
            ExecuteGroupByQueryForPartitionAsync(endpoint, key, database, container, sqlQuery, rangeId, cancellationToken));

        var partitionResults = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Merge per-partition dictionaries
        var merged = new Dictionary<string, long>();
        foreach (var partition in partitionResults)
        {
            foreach (var (label, count) in partition)
            {
                if (merged.TryGetValue(label, out var existing))
                    merged[label] = existing + count;
                else
                    merged[label] = count;
            }
        }

        _logger.LogInformation(
            "Discovered {LabelCount} {Kind} labels across {PartitionCount} partitions for {Database}/{Container}",
            merged.Count, isEdge ? "edge" : "vertex", ranges.Count, database, container);

        return merged;
    }

    /// <summary>
    /// Retrieves partition key range IDs for the collection.
    /// Results are cached per endpoint/database/container for the lifetime of the service.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetPartitionKeyRangesAsync(
        string endpoint,
        string key,
        string database,
        string container,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{endpoint}|{database}|{container}";
        if (_partitionRangeCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var resourceId = $"dbs/{database}/colls/{container}";
        var uri = new Uri(new Uri(endpoint), $"/{resourceId}/pkranges");
        var dateString = DateTime.UtcNow.ToString("r");

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("x-ms-date", dateString);
        request.Headers.Add("x-ms-version", "2018-12-31");

        var authToken = GenerateAuthToken(HttpMethod.Get, "pkranges", resourceId, dateString, key);
        request.Headers.TryAddWithoutValidation("Authorization", authToken);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Failed to get partition key ranges ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
            throw new HttpRequestException(
                $"Failed to get partition key ranges: {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var json = JsonDocument.Parse(content);

        var ranges = new List<string>();
        if (json.RootElement.TryGetProperty("PartitionKeyRanges", out var rangesArray))
        {
            foreach (var range in rangesArray.EnumerateArray())
            {
                if (range.TryGetProperty("id", out var idProp))
                {
                    ranges.Add(idProp.GetString()!);
                }
            }
        }

        _logger.LogInformation("Discovered {Count} partition key ranges for {Database}/{Container}",
            ranges.Count, database, container);

        _partitionRangeCache[cacheKey] = ranges;
        return ranges;
    }

    /// <summary>
    /// Executes a GROUP BY query against a single partition key range
    /// and returns a dictionary of label ? count for that partition.
    /// </summary>
    private async Task<Dictionary<string, long>> ExecuteGroupByQueryForPartitionAsync(
        string endpoint,
        string key,
        string database,
        string container,
        object sqlQuery,
        string partitionKeyRangeId,
        CancellationToken cancellationToken)
    {
        var resourceId = $"dbs/{database}/colls/{container}";
        var uri = new Uri(new Uri(endpoint), $"/{resourceId}/docs");
        var dateString = DateTime.UtcNow.ToString("r");

        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("x-ms-date", dateString);
        request.Headers.Add("x-ms-version", "2018-12-31");
        request.Headers.Add("x-ms-documentdb-isquery", "True");
        request.Headers.Add("x-ms-documentdb-partitionkeyrangeid", partitionKeyRangeId);

        var authToken = GenerateAuthToken(HttpMethod.Post, "docs", resourceId, dateString, key);
        request.Headers.TryAddWithoutValidation("Authorization", authToken);

        var body = new StringContent(JsonSerializer.Serialize(sqlQuery), Encoding.UTF8);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/query+json");
        request.Content = body;

        var result = new Dictionary<string, long>();

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Partition {PartitionId} GROUP BY query failed ({StatusCode}): {ErrorBody}",
                partitionKeyRangeId, response.StatusCode, errorBody);
            return result;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var json = JsonDocument.Parse(content);

        if (json.RootElement.TryGetProperty("Documents", out var documents))
        {
            foreach (var doc in documents.EnumerateArray())
            {
                var label = doc.TryGetProperty("label", out var labelProp)
                    ? labelProp.GetString()
                    : null;
                var count = doc.TryGetProperty("cnt", out var cntProp) && cntProp.ValueKind == JsonValueKind.Number
                    ? cntProp.GetInt64()
                    : 0;

                if (!string.IsNullOrEmpty(label))
                {
                    result[label] = count;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Sends a SQL query to the Cosmos DB REST API and returns the parsed response.
    /// </summary>
    private async Task<JsonDocument> ExecuteSqlQueryAsync(
        string endpoint,
        string key,
        string database,
        string container,
        object sqlQuery,
        CancellationToken cancellationToken)
    {
        var resourceId = $"dbs/{database}/colls/{container}";
        var uri = new Uri(new Uri(endpoint), $"/{resourceId}/docs");
        var dateString = DateTime.UtcNow.ToString("r");

        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("x-ms-date", dateString);
        request.Headers.Add("x-ms-version", "2018-12-31");
        request.Headers.Add("x-ms-documentdb-isquery", "True");
        request.Headers.Add("x-ms-documentdb-query-enablecrosspartition", "True");

        var authToken = GenerateAuthToken(HttpMethod.Post, "docs", resourceId, dateString, key);
        request.Headers.TryAddWithoutValidation("Authorization", authToken);

        // Content-Type must be exactly "application/query+json" without charset suffix.
        // Using the StringContent(string, Encoding, string) overload appends "; charset=utf-8"
        // which Cosmos DB rejects with 400 Bad Request.
        var body = new StringContent(JsonSerializer.Serialize(sqlQuery), Encoding.UTF8);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/query+json");
        request.Content = body;

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Cosmos DB SQL query failed ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
            throw new HttpRequestException(
                $"Cosmos DB SQL query failed with {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(content);
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
