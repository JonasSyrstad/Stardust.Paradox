# Complex Repeat-Path-Select Query - Quick Reference

## Query Pattern
```gremlin
g.V([__p0,__p1])
 .as(__p2)
 .repeat(outE().as(__p13).inV().simplePath())
 .until(has(__p14,__p15).or().has(__p16,__p17))
 .path()
 .unfold()
 .where(select(__p18).not(has(__p19,__p20)))
 .limit(__p3)
 .select(__p4)
```

## Parameters Quick Reference

| Param | Purpose | Type | Example |
|-------|---------|------|---------|
| `__p0` | Start vertex 1 | string | "vertex1" |
| `__p1` | Start vertex 2 | string | "vertex2" |
| `__p2` | Start label | string | "start" |
| `__p13` | Edge label | string | "edge" |
| `__p14` | Until prop 1 | string | "type" |
| `__p15` | Until value 1 | string | "target" |
| `__p16` | Until prop 2 | string | "type" |
| `__p17` | Until value 2 | string | "endpoint" |
| `__p18` | Filter label | string | "edge" |
| `__p19` | Exclude prop | string | "excluded" |
| `__p20` | Exclude value | string | "true" |
| `__p3` | Limit | int | 10 |
| `__p4` | Select label | string | "start" |

## Step-by-Step Execution

1. **V([id1,id2])** ? Start from multiple vertices
2. **as(label)** ? Label starting vertices
3. **repeat()** ? Begin loop:
   - **outE()** ? Get outgoing edges
   - **as(edge_label)** ? Label edges
   - **inV()** ? Get destination vertices
   - **simplePath()** ? Prevent cycles
4. **until(condition)** ? Stop when:
   - Vertex has property `__p14` = `__p15` **OR**
   - Vertex has property `__p16` = `__p17`
5. **path()** ? Get complete path
6. **unfold()** ? Expand path to elements
7. **where()** ? Filter:
   - **select(edge_label)** ? Get labeled edges
   - **not(has(prop,val))** ? Exclude matching edges
8. **limit(n)** ? Take first n results
9. **select(label)** ? Return labeled elements

## Test Categories (26 Tests Total)

### ? Basic Functionality (3)
- Basic path with filtering
- Single start vertex
- No matching targets

### ? Array Syntax (4)
- Multiple vertices
- Invalid vertices
- Array handling

### ? Repeat & SimplePath (3)
- Cycle avoidance
- Multi-hop traversal
- Deep paths

### ? Until OR Logic (3)
- First condition match
- Second condition match
- No condition match

### ? Path & Unfold (1)
- Path expansion

### ? Where-Select-Not (2)
- Edge filtering
- No exclusions

### ? Limit & Select (3)
- Result limiting
- Vertex selection
- Edge selection

### ? Edge Cases (4)
- Zero limit
- Negative limit
- Empty graph
- Parameter variations

### ? Parameterization (2)
- Standard params
- Alternative naming

### ? Performance (1)
- Large graph test

### ? Documentation (1)
- Complete example

## Common Issues & Solutions

### Issue: Infinite Loop
**Cause**: Missing simplePath() or cyclic graph
**Solution**: simplePath() prevents revisiting vertices
```gremlin
.repeat(outE().as("e").inV().simplePath())  // ? Correct
.repeat(outE().as("e").inV())                // ? May loop forever
```

### Issue: OR Not Working
**Cause**: Incorrect until() syntax
**Solution**: Use `.or()` between conditions
```gremlin
.until(has("type","target").or().has("type","endpoint"))  // ? Correct
.until(has("type","target"),has("type","endpoint"))        // ? Wrong
```

### Issue: Filter Not Working
**Cause**: Label mismatch in select()
**Solution**: Match edge label in where-select
```gremlin
.repeat(outE().as("edge")...)                              // Label here
.where(select("edge").not(has("excluded","true")))        // ? Same label
.where(select("wronglabel").not(has("excluded","true")))  // ? Different label
```

### Issue: Empty Results
**Cause**: Start vertices don't exist
**Solution**: Verify vertex IDs
```csharp
// ? Check vertices exist first
var exists = await connector.ExecuteAsync("g.V('id1').count()", params);
```

## Example Usage

### Basic Example
```csharp
var parameters = new Dictionary<string, object>
{
    { "__p0", "start1" },          // Start vertex 1
    { "__p1", "start2" },          // Start vertex 2
    { "__p2", "origin" },          // Label for start
    { "__p13", "relationship" },   // Label for edges
    { "__p14", "type" },           // Until property 1
    { "__p15", "target" },         // Until value 1
    { "__p16", "type" },           // Until property 2
    { "__p17", "endpoint" },       // Until value 2
    { "__p18", "relationship" },   // Filter by edge label
    { "__p19", "excluded" },       // Exclude property
    { "__p20", "true" },           // Exclude value
    { "__p3", 10 },                // Limit to 10
    { "__p4", "origin" }           // Select start vertices
};

var result = await connector.ExecuteAsync(
    "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)",
    parameters);
```

### Real-World Scenario: Finding Accessible Resources
```csharp
// Find resources accessible from multiple users, excluding blocked paths
var params = new Dictionary<string, object>
{
    { "__p0", "user1" },
    { "__p1", "user2" },
    { "__p2", "users" },
    { "__p13", "permission" },
    { "__p14", "resourceType" },
    { "__p15", "document" },
    { "__p16", "resourceType" },
    { "__p17", "folder" },
    { "__p18", "permission" },
    { "__p19", "revoked" },
    { "__p20", "true" },
    { "__p3", 50 },
    { "__p4", "users" }
};
```

## Test Data Setup Pattern

```csharp
private async Task<InMemoryGremlinLanguageConnector> CreateTestScenario()
{
    var connector = InMemoryGremlinLanguageConnector.Create();
    
    // 1. Create start vertices
    await connector.ExecuteAsync(
        "g.addV('node').property('id', 'start1')", 
        new Dictionary<string, object>());
    
    // 2. Create intermediate vertices
    await connector.ExecuteAsync(
        "g.addV('node').property('id', 'mid1')", 
        new Dictionary<string, object>());
    
    // 3. Create target vertices with until conditions
    await connector.ExecuteAsync(
        "g.addV('node').property('id', 'target1').property('type', 'target')", 
        new Dictionary<string, object>());
    
    // 4. Create edges (some with exclusion properties)
    await connector.ExecuteAsync(
        "g.V('start1').addE('connects').to(g.V('mid1'))", 
        new Dictionary<string, object>());
    
    await connector.ExecuteAsync(
        "g.V('mid1').addE('connects').to(g.V('target1')).property('excluded', 'true')", 
        new Dictionary<string, object>());
    
    return connector;
}
```

## Performance Tips

1. **Use Limit**: Always limit results for large graphs
2. **SimplePath**: Required for cyclic graphs, adds overhead
3. **Index Properties**: Until conditions use property lookups
4. **Early Filtering**: Filter in until() when possible
5. **Batch Creation**: Create test data in batches for large scenarios

## Debugging Tips

### Enable Query Logging
```csharp
var connector = new InMemoryGremlinLanguageConnector(
    new InMemoryDatabaseOptions 
    { 
        EnableQueryLogging = true,
        EnableDebugLogging = true 
    });
```

### Check Intermediate Results
```csharp
// Remove unfold/where/limit/select to see full paths
var debugQuery = "g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path()";
```

### Verify Until Conditions
```csharp
// Test until conditions separately
var conditionTest = "g.V().has(__p14,__p15)";
var result = await connector.ExecuteAsync(conditionTest, params);
```

## Related Queries

### Simpler Version (No Filtering)
```gremlin
g.V([id1,id2])
 .repeat(out().simplePath())
 .until(has("type","target"))
 .path()
```

### With Emit (Collect All Intermediate)
```gremlin
g.V([id1,id2])
 .repeat(out().simplePath())
 .emit()
 .until(has("type","target"))
 .path()
```

### Without Array Syntax
```gremlin
g.V(id1).as("start")
 .repeat(outE().as("e").inV().simplePath())
 .until(has("type","target"))
 .path()
```

## Test File Location
`Stardust.Paradox.Data.InMemory.Tests/ComplexRepeatPathSelectTests.cs`

## Run Tests
```bash
# All tests
dotnet test --filter "ComplexRepeatPathSelectTests"

# Specific test
dotnet test --filter "ComplexRepeatPathSelectTests.ComplexRepeatPathSelect_WithBasicPath_ShouldReturnFilteredResults"
```
