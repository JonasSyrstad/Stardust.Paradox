using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Stardust.Paradox.GremlinStudio.Core.Connections;

/// <summary>
/// Secure secret storage using Windows Data Protection API (DPAPI).
/// Secrets are encrypted using the current user's credentials.
/// </summary>
public sealed class DpapiSecureSecretStore : ISecureSecretStore
{
    private readonly string _storageDirectory;
    private readonly ILogger<DpapiSecureSecretStore> _logger;
    private const string FileExtension = ".secret";
    private static readonly object _fileLock = new();

    public DpapiSecureSecretStore(ILogger<DpapiSecureSecretStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _storageDirectory = Path.Combine(appDataPath, "GremlinStudio", "Secrets");
        
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
            _logger.LogDebug("Created secrets directory at {Path}", _storageDirectory);
        }
    }

    public Task StoreSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(secret);
        
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var filePath = GetFilePath(key);
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            
            // Encrypt using DPAPI with CurrentUser scope
            var encryptedBytes = ProtectedData.Protect(
                secretBytes, 
                null, 
                DataProtectionScope.CurrentUser);

            lock (_fileLock)
            {
                File.WriteAllBytes(filePath, encryptedBytes);
            }

            _logger.LogDebug("Stored secret for key {Key}", SanitizeKeyForLogging(key));
        }, cancellationToken);
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var filePath = GetFilePath(key);
            
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Secret not found for key {Key}", SanitizeKeyForLogging(key));
                return null;
            }

            byte[] encryptedBytes;
            lock (_fileLock)
            {
                encryptedBytes = File.ReadAllBytes(filePath);
            }

            // Decrypt using DPAPI
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes, 
                null, 
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decryptedBytes);
        }, cancellationToken);
    }

    public Task<bool> DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var filePath = GetFilePath(key);
            
            if (!File.Exists(filePath))
            {
                return false;
            }

            lock (_fileLock)
            {
                File.Delete(filePath);
            }

            _logger.LogDebug("Deleted secret for key {Key}", SanitizeKeyForLogging(key));
            return true;
        }, cancellationToken);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath(key);
            return File.Exists(filePath);
        }, cancellationToken);
    }

    private string GetFilePath(string key)
    {
        // Hash the key to create a safe filename
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var hashBytes = SHA256.HashData(keyBytes);
        var fileName = Convert.ToHexString(hashBytes) + FileExtension;
        return Path.Combine(_storageDirectory, fileName);
    }

    private static string SanitizeKeyForLogging(string key)
    {
        // Truncate and mask the key for safe logging
        if (key.Length <= 8)
        {
            return "****";
        }
        return key[..4] + "****" + key[^4..];
    }
}
