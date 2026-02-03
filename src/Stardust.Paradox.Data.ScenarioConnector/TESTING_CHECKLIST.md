# Connection Fix - Testing Checklist

## Quick Verification Steps

### 1. Test Automatic Hostname Cleaning
```bash
cd Stardust.Paradox.Data.ScenarioConnector
dotnet run
# Choose option: 4 (List all connections)
# Verify hostnames are clean (no https://, no :443)
```

**Expected:** All stored hostnames should show clean format:
- ? `jonas-playground.gremlin.cosmos.azure.com`
- ? NOT `https://jonas-playground.gremlin.cosmos.azure.com:443`

### 2. Test Connection with Stored Connection
```bash
# Choose option: 1 (Connect to database)
# Select an existing connection
# Watch for retry attempts if network issues
```

**Expected:** 
- Connection should succeed (existing connections auto-cleaned)
- If it fails, should see helpful error message with guidance
- Should retry up to 3 times with delays

### 3. Test Adding New Connection with Bad Format
```bash
# Choose option: 2 (Add new connection)
# Name: TestConnection
# Hostname: https://jonas-playground.gremlin.cosmos.azure.com:443
# Database: graphTest
# Graph: graphTest
# AccessKey: [your key]
```

**Expected:**
- Tool should show diagnostic checks
- Should auto-clean hostname
- May show warning about protocol/port (then auto-fix)
- Should test connection successfully

### 4. Test Connection Test Feature
```bash
# Choose option: 5 (Test existing connection)
# Select a connection to test
```

**Expected:**
- Should show detailed test results
- Shows response time
- Shows vertex/edge counts if successful
- Shows read-only status
- Retries if temporary failure

### 5. Test Error Messages
Try these scenarios to verify improved error messages:

#### Wrong Access Key
```bash
# Add connection with wrong key
# Expected: "Authentication failed. Please verify your access key..."
```

#### Wrong Database Name
```bash
# Add connection with wrong database
# Expected: "Database or graph not found. Please verify..."
```

#### Bad Hostname
```bash
# Try: myaccount.example.com
# Expected: "Hostname doesn't match expected CosmosDB format..."
```

## Files Changed - Quick Reference

### Core Changes
1. **ConnectionManager.cs**
   - Line ~15-50: `CosmosDbConnection` class with hostname normalization
   - Line ~120-165: `ValidateConnection()` method

2. **ConnectionTester.cs**
   - Line ~35-85: `TestConnectionAsync()` with retry logic
   - Line ~195-235: `DiagnoseConnection()` method  
   - Line ~260-330: Enhanced `GetFriendlyErrorMessage()`

### New Files
3. **HostnameNormalizer.cs** - Utility class (created, not yet integrated)
4. **CONNECTION_GUIDE.md** - User documentation
5. **TROUBLESHOOTING_QUICK_REF.md** - Quick fixes
6. **CONNECTION_FIX_SUMMARY.md** - Technical summary

## What to Look For

### Success Indicators ?
- Hostnames automatically cleaned
- Error messages are specific and helpful
- Connections retry on transient failures
- Diagnostic checks catch config issues early
- Existing connections still work

### Potential Issues ??
- Build warnings (acceptable - just nullability)
- File locked errors (close running instances)
- Need to update stored connections manually (shouldn't be needed)

## Manual Test Script

```bash
# 1. Start tool
cd Stardust.Paradox.Data.ScenarioConnector
dotnet run

# 2. List connections (should show clean hostnames)
> 4

# 3. Test a connection
> 5
> [select connection]
# Watch for: retry attempts, detailed error messages

# 4. Try connecting
> 1
> [select connection]
# Should work with auto-cleaned hostnames

# 5. Add new connection with messy format
> 2
Name: Test
Hostname: https://account.gremlin.cosmosdb.azure.com:443/
Database: test
Graph: test
Key: [test key]
# Watch for: diagnostic warnings, auto-cleaning message

# 6. Quit
> 13 (or 6 in release mode)
```

## Expected Behavior Changes

### Before Fix
```
User: *enters https://account.gremlin.cosmosdb.azure.com:443*
Tool: ? Connection failed: Could not resolve hostname
User: *confused, no idea what's wrong*
```

### After Fix
```
User: *enters https://account.gremlin.cosmosdb.azure.com:443*
Tool: ?? Note: Hostname will be automatically cleaned
Tool: ?? Running diagnostic checks...
Tool: ? Configuration appears valid
Tool: ?? Testing connection (attempt 1/3)...
Tool: ? Connection successful and verified as read-only
Tool: Connection 'Test' added successfully!
```

## Verify Documentation

### Check these files exist:
```bash
ls Stardust.Paradox.Data.ScenarioConnector/
# Should see:
# - CONNECTION_GUIDE.md
# - TROUBLESHOOTING_QUICK_REF.md
# - CONNECTION_FIX_SUMMARY.md
# - HostnameNormalizer.cs (in Providers.Gremlin)
```

### Read documentation:
```bash
# Quick help
cat Stardust.Paradox.Data.ScenarioConnector/TROUBLESHOOTING_QUICK_REF.md

# Detailed guide
cat Stardust.Paradox.Data.ScenarioConnector/CONNECTION_GUIDE.md

# Technical summary
cat Stardust.Paradox.Data.ScenarioConnector/CONNECTION_FIX_SUMMARY.md
```

## Common Issues During Testing

### Issue: Can't build due to file lock
**Fix:** Close any running ScenarioConnector instances

### Issue: Old connection still fails
**Fix:** Remove and re-add the connection (hostname will be cleaned)

### Issue: Warnings during build
**Expected:** Nullability warnings are OK, not errors

## Success Criteria

Your test is successful if:
- [x] Build completes (warnings OK)
- [ ] Stored connections show clean hostnames
- [ ] Can connect with existing connections
- [ ] New connections with bad format auto-clean
- [ ] Error messages are helpful and specific
- [ ] Connection test retries on failure
- [ ] Documentation is clear and useful

## Next Steps After Testing

1. **If all tests pass:**
   - Update existing connections if any still have issues
   - Share documentation with users
   - Monitor for new error patterns

2. **If tests reveal issues:**
   - Check error messages for improvements
   - Review documentation for clarity
   - Add missing error patterns to `GetFriendlyErrorMessage()`

3. **Long-term:**
   - Consider integrating `HostnameNormalizer` into `GremlinNetLanguageConnector`
   - Add more diagnostic checks as patterns emerge
   - Gather user feedback on error message clarity
