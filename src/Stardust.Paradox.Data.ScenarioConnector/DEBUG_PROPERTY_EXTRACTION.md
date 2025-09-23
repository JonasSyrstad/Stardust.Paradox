# Property Export Debugging Script

## Step-by-Step Debugging Process

### 1. Build and Run the ScenarioConnector Tool
```bash
cd Stardust.Paradox.Data.ScenarioConnector
dotnet build
dotnet run
```

### 2. Enable Debug Mode
When you run the tool:
1. Select option "1" to connect to a database
2. Choose your existing connection
3. **IMPORTANT:** When prompted "Enable debug logging for property extraction? (y/N):" 
   - Type **"y"** and press Enter
4. You should see: "Debug logging enabled."

### 3. Test Single Vertex Property Fetching
1. In the Export Menu, select option **"3"** - "Test property fetching for single vertex"
2. Enter a vertex ID that you know has properties
3. Look for the detailed JSON output

### 4. What to Look For in Debug Output

#### If Properties Are Found:
```
Debug: Simple vertex data structure: {
  "id": "your-vertex-id",
  "label": "your-label",
  "properties": {
    "name": [{"value": "Some Name"}],
    "email": [{"value": "email@example.com"}]
  }
}
Debug: Added property name = Some Name (type: String)
Debug: Vertex your-vertex-id final property count: 2
```

#### If Properties Are Missing:
```
Debug: Simple vertex data structure: {
  "id": "your-vertex-id", 
  "label": "your-label"
}
Debug: Vertex your-vertex-id has no properties object
Debug: Vertex your-vertex-id final property count: 0
```

### 5. Common Issues and Solutions

#### Issue 1: No Properties in Basic Query
If you see no properties in the basic query, this is expected. Look for:
```
No properties found in query results, fetching properties separately...
Debug: Parsing vertex your-vertex-id with X data entries
```

#### Issue 2: ValueMap Query Fails
If you see errors like:
```
Warning: Failed to fetch properties for vertex 'your-id': [Error Message]
```
This indicates a query compatibility issue.

#### Issue 3: Properties Exist But Not Extracted
If the JSON shows properties but the final count is 0:
```
Debug: Simple vertex data structure: { "properties": {...} }
Debug: Vertex your-vertex-id final property count: 0
```
This indicates a parsing issue in `ExtractPropertyValue()`.

### 6. Test with Export by Query
After testing single vertex:
1. Go back to Export Menu
2. Select "1. Export by (Q)uery"
3. Try a simple query: `g.V().limit(1)`
4. Watch the debug output during export

### 7. Check Generated Files
After export, check the generated files:
- **JSON File**: Look for empty `"Properties": {}` objects
- **C# File**: Look for vertices without `Props()` calls

## Expected Debug Output Pattern

When working correctly, you should see:
```
Executing query to find vertices: g.V().limit(1)
Debug: Parsing vertex abc123 from Gremlin result
Debug: Vertex abc123 has no properties object
Debug: Vertex abc123 final property count: 0
No properties found in query results, fetching properties separately...
Debug: Parsing vertex abc123 with 5 data entries  
Debug: Added property name = John Doe (type: String)
Debug: Added property email = john@example.com (type: String)
Debug: Vertex abc123 has 2 properties
Found 1 vertices from query
Finding edges between 1 vertices...
Found 0 edges between vertices
```

## Troubleshooting Commands

If the tool isn't working:

### Check Build
```bash
dotnet build --verbosity normal
```

### Run with Detailed Output
```bash
dotnet run --verbosity normal
```

### Test Single Assembly
```bash
dotnet Stardust.Paradox.Data.ScenarioConnector.dll
```

## What to Report Back

Please share:
1. **The complete debug output** from testing a single vertex
2. **The JSON structure** shown in the debug output
3. **Any error messages** during property fetching
4. **The content of a generated JSON file** (first few vertices)
5. **Your CosmosDB setup** (partition key usage, etc.)

This will help identify exactly where properties are being lost in the extraction process.