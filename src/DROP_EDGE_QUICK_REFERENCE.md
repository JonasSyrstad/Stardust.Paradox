# Edge Deletion Quick Reference Guide

## Quick Start

The InMemory database **fully supports** all edge deletion operations. Here's how to use them:

## Basic Edge Deletion

### 1. Delete Edge by ID (Recommended)

```csharp
// Create edge
var employment = context.Employments.Create(person, company);
await context.SaveChangesAsync();

// Delete edge
await context.Employments.DeleteAsync(employment.Id);
await context.SaveChangesAsync();
```

### 2. Delete Edge Using Gremlin Query

```csharp
// Drop edge by ID
await context.ExecuteAsync<object>(g => g.E("edge-id").Drop());

// Drop all edges with specific label
await context.ExecuteAsync<object>(g => 
    g.E().HasLabel("employer").Drop());
```

### 3. Delete Edge from Collection

```csharp
var person = await context.Profiles.GetAsync("person-id");
var company = await context.Companies.GetAsync("company-id");

// Remove via collection
person.Employers.Remove(company);
await context.SaveChangesAsync();
```

## Advanced Patterns

### Filter and Delete

```csharp
// Delete edges with specific property value
await context.ExecuteAsync<object>(g => 
    g.E()
     .HasLabel("employer")
     .Has("manager", "OldManager")
     .Drop());

// Delete edges from specific vertex
await context.ExecuteAsync<object>(g => 
    g.V("person-id")
     .OutE("employer")
     .Drop());
```

### Batch Deletion

```csharp
// Delete first N edges
await context.ExecuteAsync<object>(g => 
    g.E()
     .HasLabel("employer")
     .Limit(10)
     .Drop());

// Delete all edges of a type
await context.ExecuteAsync<object>(g => 
    g.E().HasLabel("parent").Drop());
```

### Conditional Deletion

```csharp
// Delete edges where target vertex matches condition
await context.ExecuteAsync<object>(g => 
    g.V("source-id")
     .OutE("employer")
     .Where(p => p.InV().Has("name", "CompanyA"))
     .Drop());
```

## Testing Edge Deletion

### Basic Test Pattern

```csharp
[Fact]
public async Task TestEdgeDeletion()
{
    using (var context = CreateContext())
    {
        // Arrange - Create test data
        var person = context.Profiles.Create("test-person");
        var company = context.Companies.Create("test-company");
        await context.SaveChangesAsync();
        
        var employment = context.Employments.Create(person, company);
        await context.SaveChangesAsync();
        
        // Act - Delete edge
        await context.Employments.DeleteAsync(employment.Id);
        await context.SaveChangesAsync();
        
        // Assert - Verify deletion
        var check = await context.ExecuteAsync<object>(g => 
            g.E(employment.Id));
        Assert.Empty(check);
    }
}
```

### Verify Vertices Remain After Edge Deletion

```csharp
[Fact]
public async Task EdgDeletion_PreservesVertices()
{
    // Create and delete edge
    var employment = context.Employments.Create(person, company);
    await context.SaveChangesAsync();
    
    await context.Employments.DeleteAsync(employment.Id);
    await context.SaveChangesAsync();
    
    // Verify vertices still exist
    var personCheck = await context.Profiles.GetAsync(person.Id);
    var companyCheck = await context.Companies.GetAsync(company.Id);
    
    Assert.NotNull(personCheck);
    Assert.NotNull(companyCheck);
}
```

### Verify Edge Count Changes

```csharp
[Fact]
public async Task EdgeDeletion_UpdatesCount()
{
    // Get initial count
    var initialCount = (await context.Employments
        .GetAsync(g => g.E().HasLabel("employer")))
        .Count();
    
    // Delete edge
    await context.Employments.DeleteAsync(edgeId);
    await context.SaveChangesAsync();
    
    // Verify count decreased
    var finalCount = (await context.Employments
        .GetAsync(g => g.E().HasLabel("employer")))
        .Count();
    
    Assert.Equal(initialCount - 1, finalCount);
}
```

## Common Scenarios

### Scenario 1: Delete All Edges Between Two Vertices

```csharp
await context.ExecuteAsync<object>(g => 
    g.V("person-id")
     .BothE()
     .Where(p => p.OtherV().HasId("company-id"))
     .Drop());
```

### Scenario 2: Delete Edges Older Than Date

```csharp
var cutoffDate = DateTime.Now.AddYears(-1).ToEpoch();

await context.ExecuteAsync<object>(g => 
    g.E()
     .HasLabel("employer")
     .Has("hiredDate", p => p.Lt(cutoffDate))
     .Drop());
```

### Scenario 3: Delete Orphaned Edges

```csharp
// Note: InMemory database prevents orphaned edges
// This pattern shows how to query for them if needed
var allEdges = await context.ExecuteAsync<object>(g => g.E());
foreach (dynamic edge in allEdges)
{
    var outV = await context.ExecuteAsync<object>(g => 
        g.V(edge.outV.ToString()));
    var inV = await context.ExecuteAsync<object>(g => 
        g.V(edge.inV.ToString()));
    
    if (!outV.Any() || !inV.Any())
    {
        await context.ExecuteAsync<object>(g => 
            g.E(edge.id.ToString()).Drop());
    }
}
```

### Scenario 4: Delete and Recreate Edge

```csharp
// Delete old edge
await context.Employments.DeleteAsync(oldEmployment.Id);
await context.SaveChangesAsync();

// Create new edge with updated properties
var newEmployment = context.Employments.Create(person, company);
newEmployment.Manager = "NewManager";
newEmployment.HiredDate = DateTime.Now;
await context.SaveChangesAsync();
```

## Error Handling

### Handle Non-Existent Edge

```csharp
try
{
    await context.Employments.DeleteAsync("non-existent-id");
    await context.SaveChangesAsync();
}
catch (InvalidOperationException ex)
{
    // Edge doesn't exist - handle gracefully
    _logger.LogWarning($"Edge not found: {ex.Message}");
}
```

### Idempotent Deletion

```csharp
// Safe deletion - check existence first
var edge = await context.Employments.GetAsync(edgeId);
if (edge != null)
{
    await context.Employments.DeleteAsync(edgeId);
    await context.SaveChangesAsync();
}
```

## Performance Tips

1. **Use Direct ID deletion for single edges**
   ```csharp
   await context.Employments.DeleteAsync(edgeId); // O(1)
   ```

2. **Use batch operations for multiple edges**
   ```csharp
   await context.ExecuteAsync<object>(g => 
       g.E().HasLabel("employer").Drop()); // Single query
   ```

3. **Avoid iterating collections for deletion**
   ```csharp
   // ? Slow - multiple queries
   foreach (var edge in edges)
       await context.Employments.DeleteAsync(edge.Id);
   
   // ? Fast - single query
   await context.ExecuteAsync<object>(g => 
       g.E().HasLabel("employer").Drop());
   ```

## Validation Checklist

After edge deletion, verify:

- ? Edge removed: `g.E(edgeId)` returns empty
- ? Not in label index: `g.E().hasLabel(label)` doesn't include it
- ? Not via vertex navigation: `g.V(id).outE(label)` doesn't show it
- ? Vertices still exist: `g.V(id)` returns vertices
- ? Other edges intact: Other edges between vertices remain

## Summary

The InMemory database provides complete edge deletion support with:

- ? Multiple deletion APIs
- ? Full index cleanup
- ? Vertex preservation
- ? Batch operations
- ? Property-based filtering
- ? TinkerPop compatibility

For more details, see `DROP_EDGE_SUPPORT_COMPLETE_SUMMARY.md`.
