# Drop Edge Test Fix Summary

## Problem
The `DropEdgeTest` was failing because edges created with an explicit ID via `.property('id', '<value>')` were not using that ID as the edge's actual ID. Instead, the database was generating its own ID and storing the provided ID as a regular property.

## Root Cause
In the `FromStepExecutor` and `ToStepExecutor`, when completing an `addE()` operation, the code was:
1. Creating the edge with `Database.AddEdge(label, fromVertexId, toVertexId)` - which uses auto-generated ID
2. Then applying all properties including 'id' as regular properties

This meant the edge's actual ID was different from the ID specified in the query.

## Solution
Modified both `FromStepExecutor.cs` and `ToStepExecutor.cs` to:
1. Extract the 'id' property from the properties dictionary before edge creation
2. Pass it as the `id` parameter to `Database.AddEdge(label, fromVertexId, toVertexId, edgeId)`
3. Remove 'id' from the properties dictionary so it's not set as a regular property
4. Apply remaining properties after edge creation

## Changes Made

### FromStepExecutor.cs
```csharp
private void TryExecutePendingAddE(TinkerTraversalContext context)
{
    // ... existing code to get metadata ...
    
    // Extract the 'id' property if present - it should be used as the edge ID
    string edgeId = null;
    if (properties.ContainsKey("id"))
    {
        edgeId = properties["id"]?.ToString();
        // Remove from properties dictionary since it's used as the ID, not a property
        properties.Remove("id");
    }

    // ... existing code to resolve vertices ...
    
    // Use the provided edge ID if available, otherwise let database generate one
    var edge = Database.AddEdge(label, fromVertexId, toVertexId, edgeId);
    
    // Apply remaining properties (excluding 'id' which was already handled)
    foreach (var prop in properties)
    {
        edge.SetProperty(prop.Key, prop.Value);
    }
    
    // ... rest of code ...
}
```

### ToStepExecutor.cs
Same changes as `FromStepExecutor.cs` since both executors can complete the `addE()` operation.

## Test Flow Fixed
1. **Create**: `tc.Employments.Create(alex, ZephyrCorp)` creates edge with GUID ID = "abc123"
2. **Save**: Executes `g.V('Alexis').as('a').V('ZephyrCorp').as('b').addE('employer').from('b').to('a').property('id','abc123')`
3. **Edge Created**: Edge is now created with ID = "abc123" (not auto-generated)
4. **Query**: `g.E().Has("label", "employer")` returns edge with ID = "abc123"
5. **Delete**: `g.E('abc123').drop()` finds and deletes the correct edge
6. **Verify**: Count decreases by 1 as expected

## Impact
This fix ensures that edges created through the Paradox framework with explicit IDs work correctly with:
- Edge retrieval by ID
- Edge deletion by ID
- Edge queries that depend on the ID being set correctly
- Any other operations that reference edges by ID

## Testing
The `DropEdgeTest` should now pass because:
- Edges are created with the specified ID
- Querying edges returns edges with the correct IDs
- Deleting edges by ID works as expected
- The count verification succeeds
