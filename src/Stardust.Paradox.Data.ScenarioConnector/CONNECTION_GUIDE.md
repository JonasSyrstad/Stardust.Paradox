# ScenarioConnector Connection Guide

## Overview
The ScenarioConnector tool connects to Azure CosmosDB Gremlin API databases to export graph scenarios for testing.

## Connection Requirements

### Hostname Format
**IMPORTANT:** Always enter hostnames WITHOUT protocol or port!

? **Correct formats:**
- `myaccount.gremlin.cosmosdb.azure.com`
- `myaccount.gremlin.cosmos.azure.com`

? **Incorrect formats:**
- `https://myaccount.gremlin.cosmosdb.azure.com`
- `myaccount.gremlin.cosmosdb.azure.com:443`
- `wss://myaccount.gremlin.cosmosdb.azure.com/`

**Note:** The tool now automatically cleans hostnames by removing:
- Protocol prefixes (https://, http://, wss://, ws://)
- Port numbers (:443, :8182, etc.)
- Trailing slashes
- Extra whitespace

### Database and Graph Names
- Enter the exact database name as it appears in your CosmosDB account
- Enter the exact graph name (collection name) within that database
- Names are case-sensitive

### Access Key
- Use either the PRIMARY KEY or SECONDARY KEY from your CosmosDB account
- Keys are typically 88+ characters long
- For security, use a READ-ONLY key when possible

## Connection Troubleshooting

### Common Issues and Solutions

#### "Could not resolve hostname"
**Problem:** The hostname cannot be found
**Solutions:**
- Verify the account name is correct (part before `.gremlin.cosmosdb.azure.com`)
- Check your internet connection
- Ensure you're not behind a firewall blocking Azure connections
- Try the fully qualified hostname if using a custom domain

#### "Authentication failed"
**Problem:** The access key is incorrect or expired
**Solutions:**
- Regenerate keys in Azure Portal if they've been rotated
- Copy the key carefully (they're long and easy to mistype)
- Check you're using the key for the correct CosmosDB account
- Verify the key hasn't been disabled

#### "Database or graph not found (404)"
**Problem:** The specified database or graph doesn't exist
**Solutions:**
- Check spelling of database and graph names (case-sensitive)
- Verify the database exists in your CosmosDB account
- Confirm the graph (collection) exists within that database
- Make sure you're connecting to the right CosmosDB account

#### "Connection refused" or "Timeout"
**Problem:** Cannot establish network connection
**Solutions:**
- Check firewall settings (need outbound port 443)
- Verify VPN isn't blocking Azure connections
- Test connection from a different network
- Ensure CosmosDB account isn't in a VNet without public access

#### "Access forbidden (403)"
**Problem:** Insufficient permissions
**Solutions:**
- Verify the access key matches the database
- Check the key has read permissions
- Ensure the CosmosDB account is active (not suspended)

#### "Hostname contains protocol prefix"
**Problem:** You entered `https://` in the hostname
**Solution:** Remove `https://` - enter only the hostname part

#### "Request throttled (429)"
**Problem:** Too many requests to the database
**Solutions:**
- Wait a few seconds and try again
- This usually resolves automatically
- Check if other applications are heavily using the database

## Best Practices

### Security
1. **Use Read-Only Keys:** Always prefer read-only access keys when exporting scenarios
2. **Verify Permissions:** The tool checks if connections are read-only before allowing use
3. **Secure Storage:** Connection details are encrypted using OS-specific secure storage

### Connection Testing
1. **Test First:** Always test a connection before adding it
2. **Diagnostic Checks:** The tool runs automatic diagnostics on connection configuration
3. **Retry Logic:** Connection tests automatically retry with exponential backoff (1s, 2s, 4s delays)

### Network
1. **Stable Connection:** Ensure stable internet when connecting
2. **Firewall:** Allow outbound HTTPS (port 443) to `*.cosmosdb.azure.com`
3. **Proxy:** Configure system proxy if required by your network

## Connection Storage

### Where Connections are Stored
Connections are encrypted and stored locally at:
- **Windows:** `%LOCALAPPDATA%\StardustParadox\ScenarioConnector\cosmosdb_connections.json`
- **macOS/Linux:** `~/.local/share/StardustParadox/ScenarioConnector/cosmosdb_connections.json`

### Encryption
- **Windows:** Uses DPAPI (Data Protection API) for CurrentUser scope
- **macOS/Linux:** Uses AES-256 encryption with machine and user-specific key derivation

### Managing Connections
- Use option (4) "List all connections" to view stored connections
- Use option (3) "Remove connection" to delete stored connections
- Connection files are portable within the same machine and user account

## Getting Connection Details from Azure Portal

1. Navigate to your CosmosDB account in Azure Portal
2. Go to "Keys" section in the left menu
3. Copy the following information:
   - **Gremlin Endpoint:** Extract hostname from URI (remove `https://` and everything after `.com`)
   - **Primary Key** or **Secondary Key:** Use for Access Key
4. Database and Graph names are found in "Data Explorer"

### Example from Azure Portal
If your Gremlin Endpoint is:
```
https://myaccount.gremlin.cosmosdb.azure.com:443/
```

Enter as hostname:
```
myaccount.gremlin.cosmosdb.azure.com
```

## Testing Connections

### What Gets Tested
1. **Basic Connectivity:** Can connect and execute a simple query
2. **Read-Only Check:** Attempts write operation to verify permissions
3. **Database Statistics:** Counts vertices and edges (if successful)

### Test Results
- ? Green checkmark: Successful connection
- ??  Yellow warning: Connected but may have write permissions
- ? Red X: Connection failed

### Response Time
- < 1000ms: Excellent
- 1000-3000ms: Good
- > 3000ms: Slow (but may still work)
- Timeout: Connection issue

## Advanced Usage

### Multiple Environments
You can store different connections for:
- Development databases
- Test databases
- Production (read-only) databases

Give each connection a descriptive name like:
- "MyProject (Dev)"
- "MyProject (Test)"
- "MyProject (Prod - READ ONLY)"

### Connection Reuse
- Connections are reused within the tool
- Last used timestamp is tracked
- Connection details are cached for the session

## Support

If you continue to experience connection issues after following this guide:

1. Check the tool's console output for detailed error messages
2. Review Azure CosmosDB firewall and network settings
3. Verify the Gremlin API is enabled on your CosmosDB account
4. Test connectivity using Azure Portal's Data Explorer first
5. Check application logs for detailed error information

## Automatic Fixes

The tool now includes these automatic fixes:
- ? Hostname normalization (removes protocols, ports, whitespace)
- ? Connection retry with exponential backoff
- ? Comprehensive error diagnostics
- ? Pre-connection validation checks
- ? Friendly error messages with troubleshooting hints
