# ScenarioConnector Connection Issues - Quick Fix Reference

## Most Common Issues (90% of problems)

### 1. Hostname Format Problem
**Symptom:** "Could not resolve hostname" or "Invalid hostname"

**Quick Fix:**
```
? Wrong: https://myaccount.gremlin.cosmosdb.azure.com:443
? Right: myaccount.gremlin.cosmosdb.azure.com
```

**Automatic Fix:** The tool NOW automatically cleans hostnames!
- Removes `https://`, `http://`, `wss://`, `ws://`
- Removes port numbers like `:443` or `:8182`
- Trims whitespace and trailing slashes

### 2. Wrong Access Key
**Symptom:** "Authentication failed" or "401 Unauthorized"

**Quick Fix:**
1. Go to Azure Portal ? Your CosmosDB Account ? Keys
2. Copy PRIMARY KEY or SECONDARY KEY (the long one, ~88 chars)
3. Paste carefully (no extra spaces)

**Common Mistakes:**
- Using URI instead of key
- Using expired/rotated key
- Extra spaces or newlines in key

### 3. Database/Graph Not Found
**Symptom:** "404 Not Found" or "Resource not found"

**Quick Fix:**
1. Azure Portal ? Your CosmosDB Account ? Data Explorer
2. Note exact spelling (case-sensitive):
   - Database name (e.g., "graphTest")
   - Graph/Collection name (e.g., "graphTest" or "tenant")
3. Re-enter exactly as shown

### 4. Network/Firewall Issue
**Symptom:** "Connection timeout" or "Connection refused"

**Quick Fix:**
- Check internet connection
- Disable VPN temporarily to test
- Allow outbound HTTPS (port 443) in firewall
- Try from different network to isolate issue

## Less Common Issues

### 5. Stored Connection Has Wrong Format
**Symptom:** Old connections still fail after update

**Solution:**
```bash
# Remove and re-add the connection
1. Run tool ? Option (3) "Remove connection"
2. Select problematic connection
3. Run tool ? Option (2) "Add new connection"
4. Enter details (tool will clean them automatically)
```

### 6. Read-Only vs Write Access
**Symptom:** Warning about write permissions

**Not Actually a Problem:** Tool warns you for safety
- ??  Means connection *might* allow writes
- Tool only performs read operations
- Use read-only keys when possible

## Pre-Connection Diagnostic

The tool now includes automatic diagnostic checks:

### What Gets Checked
- ? Hostname format validation
- ? Protocol/port detection
- ? Domain name structure
- ? Database name presence
- ? Graph name presence  
- ? Access key length

### When Diagnostics Run
1. When adding new connection
2. Before saving connection
3. Shows warnings if issues found
4. Allows you to continue or cancel

## Connection Testing Features

### Automatic Retries
- Tests connection up to 3 times
- Exponential backoff: 1s ? 2s ? 4s
- Handles transient network issues
- Shows attempt number in output

### What Gets Tested
1. **Basic connectivity** (can reach database)
2. **Authentication** (access key valid)
3. **Read access** (can query data)
4. **Write check** (verifies read-only)
5. **Statistics** (vertex/edge counts)

## Step-by-Step Connection

```
1. Run: dotnet run
2. Choose: 2 (Add new connection)
3. Name: MyProject (Dev)
4. Hostname: myaccount.gremlin.cosmosdb.azure.com
   ? Tool automatically cleans if you enter https:// or :443
5. Database: graphTest
6. Graph: graphTest
7. Key: [paste from Azure Portal]
   ? Will show as ******* for security
8. Wait for diagnostic checks
9. Wait for connection test
10. Confirm if warnings shown
```

## Error Message Quick Reference

| Error Message | What It Means | Quick Fix |
|--------------|---------------|-----------|
| "Could not resolve hostname" | Can't find the server | Check hostname format |
| "Authentication failed" | Wrong access key | Copy key from Azure Portal |
| "404 Not Found" | Database/graph doesn't exist | Verify names in Data Explorer |
| "403 Forbidden" | No permission | Check key permissions |
| "Connection timeout" | Network issue | Check firewall/internet |
| "429 Throttled" | Too many requests | Wait and retry |
| "Invalid hostname format" | Protocol/port included | Remove https:// and :443 |

## Manual Connection Test (Without Tool)

If tool fails, test manually to isolate issue:

```bash
# Test 1: DNS Resolution
ping myaccount.gremlin.cosmosdb.azure.com

# Test 2: Port connectivity (Windows)
Test-NetConnection -ComputerName myaccount.gremlin.cosmosdb.azure.com -Port 443

# Test 3: Azure Portal Data Explorer
- Go to your CosmosDB in Azure Portal
- Click "Data Explorer"
- Try running: g.V().limit(1)
- If this works, connection issue is in tool config
```

## Files to Check

### Connection Storage Location
```
Windows: %LOCALAPPDATA%\StardustParadox\ScenarioConnector\
macOS/Linux: ~/.local/share/StardustParadox/ScenarioConnector/
```

### Delete Corrupt Connections
```bash
# If connections are corrupted, delete file:
# Windows
del %LOCALAPPDATA%\StardustParadox\ScenarioConnector\cosmosdb_connections.json

# macOS/Linux  
rm ~/.local/share/StardustParadox/ScenarioConnector/cosmosdb_connections.json
```

## Getting Help

### Information to Provide
If asking for help, include:
1. Error message (exact text)
2. Hostname format you're using
3. Whether you can access via Azure Portal Data Explorer
4. Network environment (corporate VPN, firewall, etc.)
5. Tool version

### What NOT to Share
- ? Access keys
- ? Full connection strings with keys
- ? Account names (if sensitive)

## Recent Improvements (This Update)

### New Features
1. **Automatic hostname cleaning** - removes protocols and ports
2. **Retry logic** - 3 attempts with exponential backoff
3. **Pre-connection diagnostics** - catches issues before testing
4. **Better error messages** - tells you exactly what's wrong
5. **Validation** - checks format before saving

### How This Helps
- **Before:** Had to manually format hostname correctly
- **After:** Tool fixes common mistakes automatically

- **Before:** One connection attempt, then fail
- **After:** Retries 3 times with smart delays

- **Before:** Generic error "connection failed"
- **After:** Specific guidance on what to fix

## Still Having Issues?

1. **Update the tool** - make sure you have latest version
2. **Check logs** - detailed error info in console output
3. **Try different connection** - to isolate account-specific issues
4. **Test from Azure Portal** - verifies CosmosDB is accessible
5. **Review CONNECTION_GUIDE.md** - for detailed documentation

## Success Checklist

Before reporting an issue, verify:
- [ ] Hostname has NO `https://` or `:443`
- [ ] Access key is PRIMARY or SECONDARY key (long, ~88 chars)
- [ ] Database name matches exactly (case-sensitive)
- [ ] Graph name matches exactly (case-sensitive)
- [ ] Can connect via Azure Portal Data Explorer
- [ ] Firewall allows outbound port 443
- [ ] Not behind corporate VPN/proxy blocking Azure
- [ ] Tried removing and re-adding connection
- [ ] Read error message carefully for specific guidance
