# Property Debugging Diagnostic Guide

## Problem: Properties Still Missing

Despite previous fixes, properties are still not appearing in exported scenario files. This enhanced diagnostic version will help identify the root cause.

## New Debugging Features

### 1. Enhanced JSON Structure Debugging

The tool now outputs the exact JSON structure returned by CosmosDB for both vertices and edges:

```
Debug: Simple vertex data structure: {
  "id": "user123",
  "label": "user",
  "properties": {
    "name": [{"id": "xyz", "value": "John Doe"}],
    "email": [{"id": "abc", "value": "john@example.com"}]
  }
}
```

### 2. Test Property Fetching Menu Option

**New Menu Item:** "3. (T)est property fetching for single vertex"

This allows you to:
- Test property fetching on a single vertex
- See the raw JSON structure returned by CosmosDB
- Compare different query approaches
- Get specific diagnostics for property issues

### 3. Multiple Query Testing

For each vertex, the tool now tests:
1. **Basic Query:** `g.V('id')` - Shows raw vertex structure
2. **ValueMap Query:** `g.V('id').valueMap(true)` - Shows properties in valueMap format
3. **Properties Query:** `g.V('id').properties()` - Shows just the properties

## How to Use the Diagnostics

### Step 1: Enable Debug Mode
1. Connect to your database
2. When prompted: "Enable debug logging for property extraction? (y/N):"
3. Enter 'y' to enable detailed logging

### Step 2: Test Single Vertex
1. Select "3. Test property fetching for single vertex"
2. Enter a vertex ID you know exists and has properties
3. Review the detailed output

### Step 3: Analyze the Results

**Look for these patterns in the output:**

#### Pattern 1: Properties in Initial Query
```json
{
  "id": "user123",
  "label": "user",
  "properties": {
    "name": [{"value": "John Doe"}],
    "email": [{"value": "john@example.com"}]
  }
}
```
**Diagnosis:** Properties are available, extraction should work.

#### Pattern 2: No Properties Object
```json
{
  "id": "user123",
  "label": "user"
}
```
**Diagnosis:** Initial query doesn't include properties, valueMap query needed.

#### Pattern 3: Empty Properties
```json
{
  "id": "user123",
  "label": "user",
  "properties": {}
}
```
**Diagnosis:** Vertex genuinely has no properties, or permission issue.

#### Pattern 4: Nested Property Format
```json
{
  "name": [{"id": "prop123", "value": "John Doe"}],
  "email": [{"id": "prop456", "value": "john@example.com"}]
}
```
**Diagnosis:** CosmosDB property array format, extraction logic should handle this.

### Step 4: Compare Query Results

The test will show output from all three query types:
- If basic query has properties ? Extraction issue
- If valueMap has properties ? Basic query issue
- If properties() has results ? ValueMap parsing issue

## Expected Output Examples

### Successful Property Fetch
```
=== Testing property fetch for vertex: user123 ===

--- Test 1: Basic vertex query ---
Query: g.V('user123')
Results count: 1
Basic result structure: {
  "id": "user123",
  "label": "user",
  "properties": {
    "name": [{"value": "John Doe"}]
  }
}
Parsed vertex: ID=user123, Label=user, Properties=1

--- Test 2: ValueMap query ---
Query: g.V('user123').valueMap(true)
Results count: 1
ValueMap result structure: {
  "id": ["user123"],
  "label": ["user"],
  "name": ["John Doe"]
}
Parsed vertex: ID=user123, Label=user, Properties=1
  Property: name = John Doe (String)
```

### Failed Property Fetch
```
=== Testing property fetch for vertex: user123 ===

--- Test 1: Basic vertex query ---
Query: g.V('user123')
Results count: 1
Basic result structure: {
  "id": "user123",
  "label": "user"
}
Parsed vertex: ID=user123, Label=user, Properties=0

--- Test 2: ValueMap query ---
Query: g.V('user123').valueMap(true)
Results count: 1
ValueMap result structure: {
  "id": ["user123"],
  "label": ["user"]
}
Parsed vertex: ID=user123, Label=user, Properties=0
```

## Troubleshooting Based on Results

### If Basic Query Has Properties But Export Doesn't:
- Issue is in `ParseVertex()` method
- Check `ExtractPropertyValue()` logic
- Verify property parsing loop

### If ValueMap Query Has Properties But Basic Doesn't:
- Initial queries don't request properties
- This is expected behavior
- Check `ParseVertexFromValueMap()` method

### If No Query Returns Properties:
- Vertex genuinely has no properties, OR
- Permission/access issue, OR
- CosmosDB configuration issue, OR
- Vertex ID format issue (try partition key format)

### If Properties() Query Works But Others Don't:
- CosmosDB configuration specific to your instance
- May need custom property fetching approach

## Next Steps

After running the diagnostics:

1. **Share the detailed output** from the test for analysis
2. **Note which queries succeed/fail** 
3. **Check the JSON structure format** - this reveals how CosmosDB returns data
4. **Test with multiple vertex IDs** to see if issue is universal or specific

This enhanced debugging should pinpoint exactly where properties are being lost in the extraction process.