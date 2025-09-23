# Enhanced Property Debugging Fix

## Problem
Properties were still missing from exported scenarios despite the previous fixes. This indicated that there might be issues with property extraction logic or that properties weren't being captured properly from the CosmosDB responses.

## Solution Implemented

### 1. Enhanced Property Extraction Logic

**Updated `ExtractPropertyValue()` method** to handle more property formats:

```csharp
private object ExtractPropertyValue(object value)
{
    if (value == null) return null;
    
    // Handle primitive types directly
    if (value is string || value is bool || value is int || value is long || 
        value is float || value is double || value is DateTime)
        return value;
    
    // Handle CosmosDB property arrays: [{"value": "actualValue"}]
    if (value is IEnumerable<object> valueArray)
    {
        var firstValue = valueArray.FirstOrDefault();
        if (firstValue is IDictionary<string, object> propObj)
        {
            if (propObj.ContainsKey("value"))
                return propObj["value"];
        }
        return firstValue;
    }
    
    // Handle dictionary objects directly
    if (value is IDictionary<string, object> dictValue)
    {
        if (dictValue.ContainsKey("value"))
            return dictValue["value"];
        return dictValue.Count == 1 ? dictValue.Values.FirstOrDefault() : dictValue;
    }
    
    return value;
}
```

### 2. Comprehensive Debug Logging

**Added configurable debug logging** to track property extraction:

```csharp
public class ScenarioExporter
{
    public bool EnableDebugLogging { get; set; } = true;
    
    // Debug output examples:
    // "Debug: Parsing vertex user123 with 5 data entries"
    // "Debug: Added property name = John Doe (type: String)"
    // "Debug: Vertex user123 final property count: 4"
    // "Debug: Edge edge456 has 3 property entries"
}
```

### 3. User-Controlled Debug Mode

**Interactive debug control** in the CLI:

```
Enable debug logging for property extraction? (y/N): y
Debug logging enabled.
```

### 4. Enhanced Property Parsing

**Improved parsing methods** with detailed logging:

- `ParseVertex()` - Tracks properties from initial Gremlin results
- `ParseVertexFromValueMap()` - Tracks properties from valueMap() calls
- `TryFetchEdgeProperties()` - Tracks edge property fetching

## Debug Output Examples

When debug logging is enabled, users will see detailed output like:

```
Debug: Parsing vertex user123 from Gremlin result
Debug: Vertex user123 has 0 property entries from Gremlin result
Debug: Vertex user123 has no properties object
Debug: Vertex user123 final property count: 0
No properties found in query results, fetching properties separately...
Debug: Parsing vertex user123 with 5 data entries
Debug: Added property name = John Doe (type: String)
Debug: Added property email = john@example.com (type: String)
Debug: Added property verified = True (type: Boolean)
Debug: Added property age = 30 (type: Int32)
Debug: Vertex user123 has 4 properties

Debug: Fetching properties for edge follows123
Debug: Edge property query returned 1 results
Debug: Edge follows123 has 2 property entries
Debug: Added edge property since = 2023-01-15 (type: String)
Debug: Added edge property strength = 0.8 (type: Double)
Debug: Edge follows123 final property count: 2
```

## Benefits

### 1. Problem Identification
- **Property Source Detection**: See whether properties come from initial query or separate fetch
- **Data Format Analysis**: Understand how CosmosDB returns property data
- **Extraction Validation**: Verify that property extraction logic works correctly

### 2. Troubleshooting
- **Step-by-step tracking**: Follow properties through the entire extraction process
- **Type information**: See the actual .NET types of extracted values
- **Count verification**: Confirm expected vs. actual property counts

### 3. User Experience
- **Optional debugging**: Users can enable detailed logging when needed
- **Clean output**: Debugging can be disabled for production use
- **Real-time feedback**: See property extraction happening in real-time

## Usage Instructions

### Enable Debug Logging
1. Select a connection in the tool
2. When prompted: "Enable debug logging for property extraction? (y/N):"
3. Enter 'y' or 'yes' to enable detailed logging
4. Proceed with export - you'll see detailed property extraction information

### Disable Debug Logging
- Enter 'n' or any other response to disable debug logging
- Export will proceed with minimal console output

### Interpreting Debug Output
- **"has 0 property entries"** - Properties not included in initial query result
- **"fetching properties separately"** - Tool is making additional queries for properties
- **"Added property X = Y"** - Property successfully extracted and added
- **"final property count: N"** - Summary of total properties found

## Expected Outcomes

With this enhanced debugging:

1. **Identify Missing Properties**: See exactly where properties are being lost
2. **Validate Extraction Logic**: Confirm that property values are being extracted correctly
3. **Debug CosmosDB Responses**: Understand the actual format of data returned by CosmosDB
4. **Optimize Queries**: Determine if initial queries can be improved to include properties

This should help identify and resolve any remaining issues with property extraction in exported scenarios.