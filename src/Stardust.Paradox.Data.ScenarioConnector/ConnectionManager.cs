using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Represents a CosmosDB connection configuration
/// </summary>
public class CosmosDbConnection
{
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string GraphName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    public string GetDisplayName() => $"{Name} ({Hostname}/{DatabaseName}/{GraphName})";
}

/// <summary>
/// Manages secure storage and retrieval of CosmosDB connection strings on Windows
/// </summary>
public class ConnectionManager
{
    private const string StorageFileName = "cosmosdb_connections.json";
    private static readonly string StorageFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StardustParadox",
        "ScenarioConnector",
        StorageFileName);

    private readonly List<CosmosDbConnection> _connections;

    public ConnectionManager()
    {
        _connections = LoadConnections();
    }

    /// <summary>
    /// Gets all stored connections (read-only)
    /// </summary>
    public IReadOnlyList<CosmosDbConnection> GetConnections()
    {
        return _connections.AsReadOnly();
    }

    /// <summary>
    /// Adds a new connection to secure storage
    /// </summary>
    public void AddConnection(CosmosDbConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.Name))
            throw new ArgumentException("Connection name cannot be empty");
        
        if (string.IsNullOrWhiteSpace(connection.Hostname))
            throw new ArgumentException("Hostname cannot be empty");
        
        if (string.IsNullOrWhiteSpace(connection.AccessKey))
            throw new ArgumentException("Access key cannot be empty");

        // Check for duplicate names
        if (_connections.Any(c => c.Name.Equals(connection.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Connection with name '{connection.Name}' already exists");

        connection.CreatedAt = DateTime.UtcNow;
        connection.LastUsed = DateTime.UtcNow;
        
        _connections.Add(connection);
        SaveConnections();
    }

    /// <summary>
    /// Removes a connection by name
    /// </summary>
    public bool RemoveConnection(string name)
    {
        var connection = _connections.FirstOrDefault(c => 
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        
        if (connection == null)
            return false;

        _connections.Remove(connection);
        SaveConnections();
        return true;
    }

    /// <summary>
    /// Gets a connection by name
    /// </summary>
    public CosmosDbConnection? GetConnection(string name)
    {
        return _connections.FirstOrDefault(c => 
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Updates the last used timestamp for a connection
    /// </summary>
    public void UpdateLastUsed(string name)
    {
        var connection = GetConnection(name);
        if (connection != null)
        {
            connection.LastUsed = DateTime.UtcNow;
            SaveConnections();
        }
    }

    /// <summary>
    /// Load connections from secure storage
    /// </summary>
    private List<CosmosDbConnection> LoadConnections()
    {
        try
        {
            if (!File.Exists(StorageFilePath))
                return new List<CosmosDbConnection>();

            var encryptedData = File.ReadAllBytes(StorageFilePath);
            var decryptedData = ProtectedData.Unprotect(
                encryptedData, 
                null, 
                DataProtectionScope.CurrentUser);
            
            var json = Encoding.UTF8.GetString(decryptedData);
            return JsonConvert.DeserializeObject<List<CosmosDbConnection>>(json) 
                ?? new List<CosmosDbConnection>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load connections: {ex.Message}");
            return new List<CosmosDbConnection>();
        }
    }

    /// <summary>
    /// Save connections to secure storage
    /// </summary>
    private void SaveConnections()
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(StorageFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonConvert.SerializeObject(_connections, Formatting.Indented);
            var dataToEncrypt = Encoding.UTF8.GetBytes(json);
            var encryptedData = ProtectedData.Protect(
                dataToEncrypt, 
                null, 
                DataProtectionScope.CurrentUser);
            
            File.WriteAllBytes(StorageFilePath, encryptedData);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save connections: {ex.Message}", ex);
        }
    }
}