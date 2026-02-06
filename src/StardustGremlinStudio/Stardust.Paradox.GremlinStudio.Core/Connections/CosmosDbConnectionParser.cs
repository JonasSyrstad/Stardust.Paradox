namespace Stardust.Paradox.GremlinStudio.Core.Connections;

/// <summary>
/// Parses Azure Cosmos DB connection strings.
/// </summary>
public static class CosmosDbConnectionParser
{
    /// <summary>
    /// Parses a Cosmos DB connection string into its components.
    /// </summary>
    /// <param name="connectionString">Connection string in format: AccountEndpoint=...;AccountKey=...;</param>
    /// <returns>Parsed endpoint and key, or null if invalid.</returns>
    public static (string Endpoint, string Key)? Parse(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        string? endpoint = null;
        string? key = null;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            
            if (trimmed.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = trimmed["AccountEndpoint=".Length..];
            }
            else if (trimmed.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
            {
                key = trimmed["AccountKey=".Length..];
            }
        }

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            return null;
        }

        return (endpoint, key);
    }

    /// <summary>
    /// Converts a Cosmos DB document endpoint to a Gremlin endpoint.
    /// </summary>
    public static string ToGremlinEndpoint(string documentEndpoint)
    {
        // https://account.documents.azure.com:443/ -> account.gremlin.cosmos.azure.com
        var uri = new Uri(documentEndpoint);
        return uri.Host.Replace(".documents.azure.com", ".gremlin.cosmos.azure.com");
    }
}
