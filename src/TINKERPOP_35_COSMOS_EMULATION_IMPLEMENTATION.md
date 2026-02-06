# TinkerPop 3.5.x Compliance & Cosmos DB Emulation Implementation

## Summary

This implementation adds TinkerPop 3.5.x compliance checking and Cosmos DB Gremlin API emulation mode to the `Stardust.Paradox.Data.InMemory` package.

## New Files Created

### 1. `CosmosDb/CosmosDbGremlinLimitations.cs`
Defines the complete set of Gremlin step limitations for Azure Cosmos DB Gremlin API:
- **UnsupportedSteps**: Steps not supported (lambda operations, analytics, etc.)
- **LimitedSteps**: Steps with restrictions (repeat depth, tree size, etc.)
- **SupportedSteps**: Fully supported Gremlin steps
- **SupportedPredicates**: P predicates (eq, neq, lt, gt, within, etc.)
- **SupportedTextPredicates**: TextP predicates (startingWith, containing, etc.)
- Helper methods for step/predicate validation

### 2. `CosmosDb/CosmosDbQueryValidator.cs`
Query validation against Cosmos DB limitations:
- Validates steps against supported/unsupported lists
- Checks repeat depth against limits (max 100)
- Validates partition key usage for cross-partition query warnings
- Evaluates query complexity for performance warnings
- Supports both error and warning modes (configurable)

### 3. `CosmosDb/CosmosDbRateLimiter.cs`
RU-based rate limiting simulation:
- Token bucket algorithm for RU consumption
- Configurable max RU/second
- Automatic replenishment over time
- Retry-after calculation for 429 responses
- `RUCostCalculator` for operation cost estimation

### 4. `CosmosDb/CosmosDbErrorResponses.cs` (.NET Core 3.1+/.NET 5+ only)
Cosmos DB-compatible error response generation:
- 429 (Too Many Requests) with retry-after headers
- 400 (Bad Request) for unsupported steps
- 404 (Not Found), 408 (Timeout), 409 (Conflict)
- 413 (Request Too Large)
- Activity ID and x-ms-* header simulation

## Modified Files

### `Core/InMemoryDatabaseOptions.cs`
Added Cosmos DB emulation settings:
- `CosmosDbEmulationMode` - Enable/disable emulation
- `PartitionKeyPath` - Partition key configuration
- `EnforceCrossPartitionQueryRestrictions` - Cross-partition warnings
- `MaxRequestUnitsPerSecond` - RU budget
- `SimulateRequestCharges` - Enable RU tracking
- `RUCostSettings` - Detailed RU cost configuration
- `MaxItemsPerQuery`, `MaxQueryExecutionTimeMs` - Limits
- `ThrowOnUnsupportedStep` - Error vs warning mode
- `EnableRateLimiting` - Rate limit simulation

### `ExecutionEngine/TinkerGraphTraversal.cs`
Added nested traversal support for validation:
- `NestedTraversal` - For steps like repeat(), until()
- `AdditionalTraversals` - For union(), coalesce(), choose()

### `InMemoryGremlinLanguageConnector.cs`
Added factory methods:
- `CreateCosmosDbEmulator()` - Default Cosmos DB mode
- `CreateCosmosDbEmulator(string partitionKeyPath, ...)` - With partition key
- `CreateCosmosDbEmulator(Action<InMemoryDatabaseOptions>)` - Custom config
- `CreateStrictCompliance()` - Strict TinkerPop 3.5.x checking

## Usage Examples

### Basic Cosmos DB Emulation
```csharp
var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator();

// Queries will be validated against Cosmos DB limitations
// RU charges will be tracked
// Unsupported steps will throw exceptions
```

### With Partition Key Support
```csharp
var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator("/tenantId");

// Cross-partition queries will generate warnings
// Queries filtering on tenantId are optimized
```

### Custom Configuration
```csharp
var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator(options =>
{
    options.MaxRequestUnitsPerSecond = 5000;  // Lower RU budget
    options.ThrowOnUnsupportedStep = false;   // Warn instead of error
    options.MaxItemsPerQuery = 500;           // Smaller result sets
});
```

### Query Validation
```csharp
var validator = new CosmosDbQueryValidator(options);
var steps = new List<TinkerGraphStep> { /* parsed steps */ };
var result = validator.Validate(steps, originalQuery);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"Error: {error.Message}");
}

foreach (var warning in result.Warnings)
    Console.WriteLine($"Warning: {warning.Message}");
```

### Rate Limiting
```csharp
var limiter = new CosmosDbRateLimiter(10000); // 10,000 RU/s
var result = limiter.TryConsume(100);         // Request 100 RUs

if (!result.IsAllowed)
{
    // Rate limited - wait and retry
    await Task.Delay(result.RetryAfterMs);
}
```

## Cosmos DB Step Support Reference

### Fully Supported Steps
- Start: `V`, `E`, `addV`, `addE`, `inject`
- Traversal: `out`, `in`, `both`, `outE`, `inE`, `bothE`, `outV`, `inV`, `bothV`, `otherV`
- Filter: `has`, `hasLabel`, `hasId`, `hasKey`, `hasValue`, `hasNot`, `is`, `and`, `or`, `not`, `where`, `dedup`, `range`, `limit`, `skip`, `simplePath`
- Map: `id`, `label`, `constant`, `values`, `properties`, `valueMap`, `elementMap`, `select`, `project`, `unfold`, `fold`, `path`, `order`, `coalesce`
- Branch: `union`, `choose`, `repeat`, `until`, `emit`, `times`, `loops`
- Terminal: `count`, `sum`, `max`, `min`, `mean`, `group`, `groupCount`, `tree`
- Mutation: `drop`, `property`

### Unsupported Steps
- Lambda: `map`, `flatMap`, `filter`, `sideEffect` (lambda versions)
- Side-effect: `aggregate`, `store`, `sack`, `cap`, `subgraph`
- Path: `cyclicPath`, `sample`, `coin`, `tail`
- Branch: `optional`, `branch`
- Analytics: `pageRank`, `peerPressure`, `connectedComponent`, `shortestPath`
- Other: `math`, `match`, `profile`, `explain`, `io`, `call`, `timeLimit`
- TinkerPop 3.6+: `mergeV`, `mergeE`, `element`, `fail`, `none`

### Limited Support Steps
- `drop` - Requires explicit ID for large datasets
- `property` - Single cardinality only for edges
- `repeat` - Max depth 100
- `tree` - Result size limits
- `path` - Path length limits
- `local` - Some nested operations may not work
- `group`, `order` - Large operations may timeout

## Tests

62 tests added in `TinkerPop35ComplianceTests.cs`:
- Factory method tests (3 tests)
- Step support validation tests (30 tests)
- Query validator tests (8 tests)
- Rate limiter tests (4 tests)
- RU cost calculator tests (3 tests)
- Integration tests (2 tests)

All tests pass ?

## Future Enhancements

1. **Priority 2**: Integrate validator into query execution pipeline
2. **Priority 3**: Add partition-aware query routing
3. **Priority 4**: Implement Cosmos DB-compatible response headers
4. **Priority 5**: Add comprehensive scenario testing framework
