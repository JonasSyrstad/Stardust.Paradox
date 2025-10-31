# Cosmos DB Test Configuration

This project uses configuration files to store Cosmos DB connection details.

## Setup Instructions

### 1. Create Local Configuration File

Copy `appsettings.json` to `appsettings.local.json` in the `Stardust.Paradox.Data.CosmosDbTests` directory:

```bash
cd Stardust.Paradox.Data.CosmosDbTests
copy appsettings.json appsettings.local.json
```

### 2. Update Credentials

Edit `appsettings.local.json` and replace the placeholder values with your actual Cosmos DB credentials:

```json
{
  "CosmosDb": {
    "Account": "your-account.gremlin.cosmos.azure.com",
    "Key": "YOUR_ACTUAL_COSMOS_DB_KEY_HERE",
    "Database": "graphTest",
    "Collection": "graphTest"
  }
}
```

### 3. Configuration Files

- **appsettings.json** - Template file (committed to git) with placeholder values
- **appsettings.local.json** - Local file (excluded from git) with actual credentials

The application will load `appsettings.json` first, then override with values from `appsettings.local.json` if it exists.

## Security Notes

- **NEVER** commit `appsettings.local.json` to version control
- The `.gitignore` file is configured to exclude `appsettings.local.json` automatically
- Store production credentials in Azure Key Vault or environment variables
- The commented Key Vault code in `TestBp.cs` shows an alternative secure approach

## Alternative: Azure Key Vault

For production scenarios, uncomment the Key Vault code in `TestBp.cs` and configure Azure Key Vault:

```csharp
var azureServiceTokenProvider = new AzureServiceTokenProvider();
var keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
  await azureServiceTokenProvider.GetAccessTokenAsync(resource));
var cosmosDbAccount = keyVaultClient.GetSecretAsync("https://your-vault.vault.azure.net/", "cosmosAccountName").Result;
var cosmosDbKey = keyVaultClient.GetSecretAsync("https://your-vault.vault.azure.net/", "cosmosAccountKey").Result;
```
