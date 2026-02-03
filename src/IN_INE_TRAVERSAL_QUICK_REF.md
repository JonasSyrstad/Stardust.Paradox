# In/InE Traversal Quick Reference

## Summary
Added 6 new tests for `In` and `InE` traversals + implemented `InV<TVertex>` shorthand method.

## New Test Methods
```csharp
// 1. Skills ? In ? filter ? Select
ListAllSkillsWithSelectAsUsingIn()        // g.V().hasLabel('skill').as('a').in('hasSkill').has(...).select('a')

// 2. Skills ? InE ? get edges
ListAllSkillEdgesWithInE()  // g.V().hasLabel('skill').inE('hasSkill')

// 3. Skills ? InE ? InV ? get people
ListAllPeopleWithInEAndInV()    // g.V().hasLabel('skill').has(...).inE().inV()

// 4. Skills ? InE.As ? InV ? Select  
ListAllSkillEdgesWithInESelectAs()        // g.V().hasLabel('skill').inE().as('a').inV().select('a')

// 5. Skills ? InE<TEdge> shorthand
ListAllSkillEdgesUsingInEShorthand()      // g.V().hasLabel('skill').inE()
```

## Key Implementation
### InV Shorthand
```csharp
// Before: Had to specify both edge and vertex type
.InV<IUserSkill, IPerson>()

// After: Can use shorthand with just vertex type
.InV<IPerson>()
```

### Method Resolution Fix
Fixed ambiguous match error by using `GetMethods().Where()` instead of `GetMethod()`.

## Usage Patterns
```csharp
// Pattern 1: In traversal with lambda
.In(t => t.PropertyName)

// Pattern 2: InE traversal with lambda
.InE(skill => skill.Practitioners).Cast<IUserSkill>()

// Pattern 3: InE shorthand
.InE<IUserSkill>()

// Pattern 4: InV shorthand
.InE<IUserSkill>().InV<IPerson>()

// Pattern 5: As/Select with InE
.InE(...).As("a").InV<TVertex>().Select<TEdge>("a")
```

## Test Status
? All 14 RealLifeTests passing  
? In/InE symmetry with Out/OutE complete  
? All query patterns validated
