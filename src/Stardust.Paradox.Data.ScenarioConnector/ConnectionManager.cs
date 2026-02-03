using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Represents a CosmosDB connection configuration
/// </summary>
public class CosmosDbConnection
{
    public string Name { get; set; } = string.Empty;
    private string _hostname = string.Empty;
    public string Hostname 
    { 
        get => _hostname; 
        set => _hostname = NormalizeHostname(value); 
    }
    public string DatabaseName { get; set; } = string.Empty;
    public string GraphName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    public string GetDisplayName() => $"{Name} ({Hostname}/{DatabaseName}/{GraphName})";

    /// <summary>
    /// Normalizes a hostname by removing protocol prefixes, port numbers, trailing slashes, and whitespace
    /// </summary>
  private static string NormalizeHostname(string hostname)
    {
     if (string.IsNullOrWhiteSpace(hostname))
    return string.Empty;

        // Trim whitespace
        hostname = hostname.Trim();

        // Remove protocol prefix if present (https://, http://, wss://, ws://)
      var protocolPrefixes = new[] { "https://", "http://", "wss://", "ws://" };
    foreach (var prefix in protocolPrefixes)
        {
    if (hostname.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
 {
             hostname = hostname.Substring(prefix.Length);
break;
    }
        }

        // Remove trailing slashes
        hostname = hostname.TrimEnd('/');

        // Remove port number if present (e.g., :443, :8182)
        var colonIndex = hostname.IndexOf(':');
        if (colonIndex > 0)
        {
            // Make sure it's a port and not part of the hostname
    var portPart = hostname.Substring(colonIndex + 1);
         if (int.TryParse(portPart, out _))
   {
   hostname = hostname.Substring(0, colonIndex);
      }
   }

        return hostname;
    }
}

/// <summary>
/// Manages secure storage and retrieval of CosmosDB connection strings across Windows, macOS, and Linux
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
    private readonly ICrossPlatformEncryption _encryption;

    public ConnectionManager()
    {
        _encryption = CreateEncryptionProvider();
        _connections = LoadConnections();
    }

    /// <summary>
    /// Creates the appropriate encryption provider based on the current operating system
    /// </summary>
    private static ICrossPlatformEncryption CreateEncryptionProvider()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsEncryption();
        }
        else
        {
            return new UnixEncryption();
        }
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
        // Validate and sanitize connection details
ValidateConnection(connection);

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
    /// Validates connection details and provides helpful error messages
    /// </summary>
    private void ValidateConnection(CosmosDbConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        // Validate name
    if (string.IsNullOrWhiteSpace(connection.Name))
        throw new ArgumentException("Connection name is required", nameof(connection));

        // Validate hostname format
   if (!string.IsNullOrWhiteSpace(connection.Hostname))
   {
            // Check if hostname looks like a valid CosmosDB hostname
  if (!connection.Hostname.Contains("."))
   {
    throw new ArgumentException(
    "Hostname appears invalid. Expected format: accountname.gremlin.cosmosdb.azure.com",
 nameof(connection));
    }

            // Warn if hostname still has protocol (shouldn't happen with normalization)
   if (connection.Hostname.Contains("://"))
 {
   throw new ArgumentException(
 "Hostname should not include protocol (http:// or https://)",
          nameof(connection));
    }
        }

        // Validate database name
      if (string.IsNullOrWhiteSpace(connection.DatabaseName))
      throw new ArgumentException("Database name is required", nameof(connection));

        // Validate graph name
        if (string.IsNullOrWhiteSpace(connection.GraphName))
  throw new ArgumentException("Graph name is required", nameof(connection));

        // Validate access key
   if (string.IsNullOrWhiteSpace(connection.AccessKey))
   throw new ArgumentException("Access key is required", nameof(connection));

        // Check access key length (CosmosDB keys are typically base64 encoded, ~88 chars)
        if (connection.AccessKey.Length < 20)
        {
throw new ArgumentException(
          "Access key appears too short. Please verify your access key.", 
     nameof(connection));
        }
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
            var decryptedData = _encryption.Decrypt(encryptedData);
            
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
            var encryptedData = _encryption.Encrypt(dataToEncrypt);
            
            File.WriteAllBytes(StorageFilePath, encryptedData);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save connections: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Interface for cross-platform encryption
/// </summary>
internal interface ICrossPlatformEncryption
{
    byte[] Encrypt(byte[] data);
    byte[] Decrypt(byte[] encryptedData);
}

/// <summary>
/// Windows-specific encryption using ProtectedData
/// </summary>
internal class WindowsEncryption : ICrossPlatformEncryption
{
    public byte[] Encrypt(byte[] data)
    {
        return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
    }

    public byte[] Decrypt(byte[] encryptedData)
    {
        return ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
    }
}

/// <summary>
/// Unix-based encryption (macOS/Linux) using AES with machine/user-specific key derivation
/// </summary>
internal class UnixEncryption : ICrossPlatformEncryption
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public UnixEncryption()
    {
        (_key, _iv) = DeriveKeyAndIV();
    }

    public byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }

    public byte[] Decrypt(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }

    /// <summary>
    /// Derives a consistent key and IV based on machine and user characteristics
    /// </summary>
    private static (byte[] key, byte[] iv) DeriveKeyAndIV()
    {
        // Create a deterministic seed from machine and user info
        var seedComponents = new List<string>
        {
            Environment.MachineName,
            Environment.UserName,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StardustParadox.ScenarioConnector.v1" // Version salt to invalidate old keys if needed
        };

        // Get additional machine-specific info if available
        try
        {
            if (File.Exists("/etc/machine-id"))
            {
                seedComponents.Add(File.ReadAllText("/etc/machine-id").Trim());
            }
            else if (File.Exists("/var/lib/dbus/machine-id"))
            {
                seedComponents.Add(File.ReadAllText("/var/lib/dbus/machine-id").Trim());
            }
        }
        catch
        {
            // Ignore errors reading machine-id files
        }

        var seedString = string.Join("|", seedComponents);
        var seedBytes = Encoding.UTF8.GetBytes(seedString);

        // Use PBKDF2 to derive key and IV
        using var pbkdf2 = new Rfc2898DeriveBytes(seedBytes, 
            Encoding.UTF8.GetBytes("StardustParadoxSalt"), 
            10000, 
            HashAlgorithmName.SHA256);

        var key = pbkdf2.GetBytes(32); // 256-bit key
        var iv = pbkdf2.GetBytes(16);  // 128-bit IV

        return (key, iv);
    }
}