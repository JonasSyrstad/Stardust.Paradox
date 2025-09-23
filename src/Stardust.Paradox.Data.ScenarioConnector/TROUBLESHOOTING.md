# Troubleshooting Guide

## Common Issues and Solutions

### 1. Composite Key / Partition Key Errors

**Error Message:**
```
Gremlin query syntax error: Invalid composite key argument for g.V(...). 
Composite key requires two values [partition_key, id], per composite key expression.
```

**Cause:** Your CosmosDB database uses partition keys, and the tool attempted to use array syntax that's not supported.

**Solutions:**

1. **For Export by Query:** Use label-based or property-based queries instead of ID arrays
   ```gremlin
   ? Good: g.V().hasLabel('person').limit(100)
   ? Good: g.V().has('category', 'product')
   ? Avoid: g.V(['id1', 'id2', 'id3'])
   ```

2. **For Export by IDs:** Use the partition key format
   ```
   ? Good: partitionKey|vertexId (e.g., user|123)
   ? Good: pk1|vertex1, pk2|vertex2
   ? Avoid: Just vertex IDs without partition keys
   ```

### 2. Connection Issues

**Error Message:**
```
Failed to connect: [Connection/Authentication error]
```

**Solutions:**

1. **Verify Connection Details:**
   - Hostname format: `account.gremlin.cosmosdb.azure.com`
   - Ensure database and graph names are correct
   - Check access key is valid and has read permissions

2. **Network Issues:**
   - Check firewall settings
   - Verify CosmosDB allows connections from your IP
   - Test connection from Azure portal first

3. **Authentication:**
   - Regenerate access keys if needed
   - Ensure you're using the primary or secondary key, not connection string

### 3. No Edges Found

**Error Message:**
```
Found 0 edges between vertices
```

**Possible Causes:**

1. **Isolated Vertices:** The exported vertices have no connections
2. **Partition Key Issues:** Edges exist but aren't found due to partition constraints
3. **Large Dataset:** Edge discovery timed out

**Solutions:**

1. **Verify Edges Exist:**
   ```gremlin
   g.V('yourVertexId').bothE().count()
   ```

2. **Check Vertex IDs:** Ensure the vertex IDs are correct and include partition keys if needed

3. **Try Smaller Dataset:** Export fewer vertices to test edge discovery

### 4. Export Performance Issues

**Symptoms:**
- Long export times
- Timeouts
- Memory issues

**Solutions:**

1. **Limit Query Results:**
   ```gremlin
   g.V().hasLabel('person').limit(50)  // Start small
   ```

2. **Export in Batches:** Split large exports into smaller chunks

3. **Optimize Queries:** Use indexed properties for filtering

### 5. File Permission Errors

**Error Message:**
```
Failed to save connections: Access denied
```

**Solutions:**

1. **Check Permissions:** Ensure write access to Documents folder
2. **Run as Administrator:** If needed for first-time setup
3. **Antivirus:** Check if antivirus is blocking file operations

### 6. Generated C# Class Issues

**Problem:** Generated class doesn't compile

**Solutions:**

1. **Check Namespace:** Ensure the namespace matches your project
2. **Add References:** Include required NuGet packages:
   ```xml
   <PackageReference Include="Stardust.Paradox.Data.InMemory" Version="..." />
   ```
3. **Property Names:** Verify property names are valid C# identifiers

### 7. Missing Properties in Exported Scenarios

**Problem:** Exported scenarios have empty Properties dictionaries

**Causes:**
1. Query doesn't include property data
2. Database uses unsupported property formats
3. Permission issues reading properties

**Solutions:**

1. **Check Export Output:** Look for console messages about property fetching
   ```
   "No properties found in query results, fetching properties separately..."
   "Warning: Failed to fetch properties for vertex 'xyz'"
   ```

2. **Verify Source Data:** Test properties exist in source database:
   ```gremlin
   g.V('yourVertexId').valueMap()
   g.E('yourEdgeId').valueMap()
   ```

3. **Check Permissions:** Ensure read access to all vertex/edge properties

4. **Review Query:** Use property-inclusive queries:
   ```gremlin
   ? Good: g.V().hasLabel('user').limit(10)  // Tool will fetch properties
   ? Good: g.V().valueMap()                  // Includes properties
   ? Avoid: Complex projections that strip properties
   ```

5. **Test Manual Property Fetch:** Try in Azure portal:
   ```gremlin
   g.V('testId').elementMap()
   g.V('testId').valueMap(true)
   ```

### 7. ElementMap Method Not Supported

**Error Message:**
```
ScriptEvaluationError: Gremlin Query Compilation Error: Unable to find any method 'elementMap'
```

**Cause:** CosmosDB doesn't support the `elementMap()` Gremlin method

**Solutions:**

1. **This is Fixed Automatically:** The tool now uses compatible methods
2. **If Still Occurring:** Check you're using the latest version of the tool
3. **Manual Verification:** Test basic queries in Azure portal:
   ```gremlin
   ? Works: g.V().limit(1).valueMap(true)
   ? Fails: g.V().limit(1).elementMap()
   ```

### 8. Edge Properties Not Captured

**Problem:** Some edges have empty Properties in exported scenarios

**Causes:**
1. Edges genuinely have no properties
2. Permission issues accessing edge properties
3. Complex edge property structures

**Solutions:**

1. **Verify Edge Has Properties:**
   ```gremlin
   g.E('yourEdgeId').valueMap()
   g.E().hasLabel('yourEdgeLabel').valueMap()
   ```

2. **Check Console Output:** Look for property fetching messages:
   ```
   "Warning: Failed to fetch properties for edge 'xyz'"
   "Found 5 edges between vertices"
   ```

3. **Test Manual Property Access:**
   ```gremlin
   g.E().limit(1).properties()
   g.E().limit(1).values()
   ```

## Best Practices

### 1. Starting with a New Database

1. **Test Connection:** Use "List connections" to verify setup
2. **Run Validation:** Use the validation option to test components
3. **Start Small:** Export 5-10 vertices first
4. **Verify Output:** Check both JSON and C# files

### 2. Working with Large Datasets

1. **Use Specific Queries:** Target specific vertex types or properties
2. **Batch Processing:** Export in chunks of 50-100 vertices
3. **Monitor RU Consumption:** Watch for throttling in CosmosDB

### 3. Debugging Export Issues

1. **Enable Verbose Output:** The tool provides detailed console output
2. **Check Vertex Structure:** Use CosmosDB Data Explorer to verify data format
3. **Test Queries:** Try your Gremlin queries in Azure portal first

## Getting Help

If you continue to have issues:

1. **Check Error Messages:** Look for specific CosmosDB error codes
2. **Verify Data:** Use Azure portal to check your graph structure
3. **Test Connectivity:** Ensure basic Gremlin queries work
4. **Review Logs:** Check the console output for detailed error information

## Known Limitations

1. **Large Graphs:** Performance may degrade with very large vertex sets
2. **Complex Queries:** Some advanced Gremlin features may not be supported
3. **Partition Keys:** Automatic detection is limited; manual format may be needed
4. **Edge Properties:** Complex edge property types may not serialize perfectly