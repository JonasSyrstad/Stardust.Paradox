# Complex Repeat-Path-Select Test Suite

## Overview

This document describes the comprehensive test suite for the complex Gremlin query pattern:

```gremlin
g.V([__p0,__p1]).as(__p2).repeat(outE().as(__p13).inV().simplePath()).until(has(__p14,__p15).or().has(__p16,__p17)).path().unfold().where(select(__p18).not(has(__p19,__p20))).limit(__p3).select(__p4)
```

## Query Components Breakdown

### 1. **Array Vertex Selection**: `g.V([__p0,__p1])`
- Starts from multiple vertices using array syntax
- Parameters: `__p0`, `__p1` (vertex IDs)
- Tests cover: single vertex, multiple vertices, invalid vertices

### 2. **Initial Labeling**: `.as(__p2)`
- Labels the starting vertices
- Parameter: `__p2` (label name)
- Used later by `select()` to retrieve these vertices

### 3. **Repeat Traversal**: `.repeat(outE().as(__p13).inV().simplePath())`
- **outE()**: Get outgoing edges
- **as(__p13)**: Label edges for later filtering
- **inV()**: Get destination vertices
- **simplePath()**: Avoid cycles in traversal
- Tests cover: cyclic graphs, deep paths, multi-hop navigation

### 4. **Until Condition**: `.until(has(__p14,__p15).or().has(__p16,__p17))`
- Stops when vertex has property matching either condition
- **First condition**: `has(__p14,__p15)` - property `__p14` equals `__p15`
- **Second condition**: `has(__p16,__p17)` - property `__p16` equals `__p17`
- **OR logic**: Stops if EITHER condition is true
- Tests cover: first condition match, second condition match, no match

### 5. **Path Extraction**: `.path()`
- Returns the complete path from start to end vertices
- Includes all vertices and edges traversed

### 6. **Path Unfolding**: `.unfold()`
- Expands the path into individual elements
- Each vertex and edge becomes a separate result

### 7. **Where-Select-Not Filter**: `.where(select(__p18).not(has(__p19,__p20)))`
- **select(__p18)**: Get elements with label `__p18` (typically edges)
- **not(has(__p19,__p20))**: Filter OUT elements with property `__p19` = `__p20`
- Removes path elements that have the excluded property
- Tests cover: filtering edges, no excluded properties

### 8. **Limit Results**: `.limit(__p3)`
- Restricts the number of results returned
- Parameter: `__p3` (integer limit)
- Tests cover: various limits, zero limit, negative limit

### 9. **Final Selection**: `.select(__p4)`
- Returns only elements with the specified label
- Parameter: `__p4` (label name, typically matches `__p2`)
- Can select vertices or edges from the filtered path

## Test Categories

### Basic Functionality Tests (3 tests)
- ? Basic path traversal with filtering
- ? Single start vertex handling
- ? No matching target scenario

### Array Syntax Tests (4 tests)
- ? Multiple start vertices
- ? Three start vertices (tests extensibility)
- ? Invalid start vertices

### Repeat and SimplePath Tests (3 tests)
- ? Cycle avoidance with simplePath
- ? Multi-hop path traversal
- ? Deep path navigation (6+ levels)

### Until Condition Tests (OR Logic) (3 tests)
- ? First condition matching
- ? Second condition matching
- ? Neither condition matching

### Path and Unfold Tests (1 test)
- ? Path expansion into individual elements

### Where-Select-Not Filter Tests (2 tests)
- ? Filtering excluded edges
- ? No exclusions (all pass through)

### Limit and Select Tests (3 tests)
- ? Result limiting
- ? Selecting start label (vertices)
- ? Selecting edge label (edges)

### Edge Cases and Boundary Tests (4 tests)
- ? Zero limit
- ? Negative limit handling
- ? Empty graph
- ? Parameterization variations

### Parameterization Tests (2 tests)
- ? All parameters provided
- ? Alternative parameter naming (p0 vs __p0)

### Performance Tests (1 test)
- ? Large graph performance (< 5 seconds)

### Documentation Tests (1 test)
- ? Complete documented example

## Total Test Count: 26 Tests

## Test Scenarios

### Scenario 1: Basic Path
```
StartVertex1 -> Mid1 -> Target1 (type=target)
StartVertex2 -> Target1
```

### Scenario 2: Multiple Paths
```
V1 -> TargetMulti (type=target)
V2 -> TargetMulti
V3 -> TargetMulti
```

### Scenario 3: Cyclic Graph
```
Cycle1 -> Cycle2 -> Cycle3 -> Cycle1 (cycle)
Cycle2 -> ExitNode (type=exit)
```

### Scenario 4: Deep Path
```
Deep1/Deep2 -> Level1 -> Level2 -> ... -> Level6 (depth=6)
```

### Scenario 5: Conditional Paths
```
Cond1/Cond2 -> MidComplete (status=complete)
Cond3/Cond4 -> MidFinished (status=finished)
```

### Scenario 6: Filtered Edges
```
Filter1 -> FilterTarget (edge: no properties)
Filter2 -> FilterTarget (edge: blocked=true)
```

### Scenario 7: Large Graph
```
Large1/Large2 -> 20 intermediate nodes -> 20 terminal nodes
Perf1/Perf2 -> 15 level chain
```

## Parameter Mapping

| Parameter | Purpose | Example Value |
|-----------|---------|---------------|
| `__p0` | First start vertex ID | "start1" |
| `__p1` | Second start vertex ID | "start2" |
| `__p2` | Start vertex label | "start" |
| `__p13` | Edge label | "edge" |
| `__p14` | First until property name | "type" |
| `__p15` | First until property value | "target" |
| `__p16` | Second until property name | "type" |
| `__p17` | Second until property value | "endpoint" |
| `__p18` | Filter select label | "edge" |
| `__p19` | Filter property name | "excluded" |
| `__p20` | Filter property value | "true" |
| `__p3` | Limit count | 10 |
| `__p4` | Final select label | "start" |

## Key Testing Patterns

### 1. **Cycle Prevention Testing**
```csharp
// Creates cyclic graph and verifies simplePath prevents infinite loops
var connector = await CreateCyclicGraphScenario();
```

### 2. **OR Condition Testing**
```csharp
// Tests both branches of or() in until clause
until(has(__p14,__p15).or().has(__p16,__p17))
```

### 3. **Edge Filtering Testing**
```csharp
// Tests where-select-not pattern for excluding specific edges
where(select(__p18).not(has(__p19,__p20)))
```

### 4. **Array Syntax Testing**
```csharp
// Tests starting from multiple vertices
g.V([__p0,__p1])
```

## Common Use Cases

1. **Multi-source Path Finding**: Finding paths from multiple starting points
2. **Cycle-free Traversal**: Navigating without revisiting vertices
3. **Conditional Termination**: Stopping at vertices matching specific criteria
4. **Path Filtering**: Removing unwanted edges or vertices from results
5. **Relationship Discovery**: Finding connections between entities with constraints

## Implementation Requirements

### Must Support:
- ? V() with array syntax `[id1, id2]`
- ? as() for labeling vertices and edges
- ? repeat()-until() loops
- ? outE(), inV() edge traversal
- ? simplePath() cycle prevention
- ? has() with property matching
- ? or() logical operator in until()
- ? path() path extraction
- ? unfold() path expansion
- ? where() filtering
- ? select() with labels
- ? not() logical negation
- ? limit() result restriction

### Query Execution Flow:
1. Parse V([...]) array syntax ? multiple start vertices
2. Apply as() label to start vertices
3. Begin repeat loop:
   - Get outgoing edges (outE)
   - Label edges (as)
   - Get destination vertices (inV)
   - Check simplePath constraint
   - Check until conditions (with or())
   - Continue or stop
4. Extract complete paths
5. Unfold paths into individual elements
6. Apply where-select-not filter
7. Limit results
8. Select final elements by label

## Test Execution

```bash
# Run all tests in the suite
dotnet test --filter "FullyQualifiedName~ComplexRepeatPathSelectTests"

# Run specific category
dotnet test --filter "FullyQualifiedName~ComplexRepeatPathSelectTests.ArraySyntax"

# Run performance test
dotnet test --filter "FullyQualifiedName~ComplexRepeatPathSelectTests.Performance"
```

## Expected Outcomes

### All Tests Should:
- ? Compile without errors
- ? Execute without exceptions
- ? Return expected result counts
- ? Complete within performance bounds
- ? Handle edge cases gracefully

### Query Engine Should:
- ? Parse complex nested structure
- ? Execute repeat-until correctly
- ? Apply simplePath constraint
- ? Evaluate OR conditions properly
- ? Filter using where-select-not
- ? Return correctly labeled elements

## Maintenance Notes

### When Adding New Tests:
1. Follow naming convention: `ComplexRepeatPathSelect_<Scenario>_<ExpectedBehavior>`
2. Add to appropriate category section
3. Include detailed arrange-act-assert comments
4. Create necessary test data via helper methods
5. Document any new parameter combinations

### When Modifying Query:
1. Update parameter mapping table
2. Adjust all affected tests
3. Update documentation examples
4. Re-run full test suite

## Related Test Suites

- **TenantAdminsQueryTests**: Simpler has-as-out-has-select pattern
- **EmitRepeatOtherVSelectUnfoldTests**: emit-repeat variations
- **TreeStepTests**: tree() step functionality
- **PathOperationTests**: path manipulation operations

## References

- [Apache TinkerPop Documentation](http://tinkerpop.apache.org/)
- [Gremlin Repeat-Until Pattern](http://tinkerpop.apache.org/docs/current/reference/#repeat-step)
- [SimplePath Documentation](http://tinkerpop.apache.org/docs/current/reference/#simplepath-step)
