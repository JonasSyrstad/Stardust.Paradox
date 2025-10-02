# Stardust.Paradox.Data.Annotations

Core annotations and interfaces for the Stardust.Paradox.Data graph database toolkit.

## Overview

This package provides the essential attributes, interfaces, and data types needed to define graph entities and relationships in Stardust.Paradox.Data. It contains the foundational components for modeling vertices, edges, and their properties in a type-safe manner.

## Key Components

### Core Interfaces

- **`IVertex`** - Base interface for all vertex entities
- **`IEdge`** - Base interface for all edge entities  
- **`IGraphEntity`** - Common interface for graph elements
- **`IGraphSet<T>`** - Collection interface for graph entity sets

### Vertex Annotations

- **`[VertexLabel]`** - Specifies the label for a vertex type
- **`[VertexProperty]`** - Marks properties as vertex properties
- **`[PropertyKey]`** - Defines property keys with metadata

### Edge Annotations

- **`[EdgeLabel]`** - Specifies the label for an edge type (⚠️ Obsolete - use `InLabelAttribute`)
- **`[ReverseEdgeLabel]`** - Specifies reverse edge labels (⚠️ Obsolete - use `OutLabelAttribute`)
- **`[InLabelAttribute]`** - Defines incoming edge relationships
- **`[OutLabelAttribute]`** - Defines outgoing edge relationships

### Special Data Types

- **`EpochDateTime`** - DateTime type that serializes as Unix epoch timestamps
- **`Property<T>`** - Wrapper for graph properties with metadata

## Installation

```bash
dotnet add package Stardust.Paradox.Data.Annotations
```

## Usage Examples

### Basic Vertex Definition

```csharp
using Stardust.Paradox.Data.Annotations;

[VertexLabel("person")]
public class Person : IVertex
{
    public string Id { get; set; }
    
    [PropertyName("full_name")]
    public string Name { get; set; }
    
    public int Age { get; set; }
    
    [JsonProperty("birth_date")]
    public EpochDateTime BirthDate { get; set; }
    
    // Navigation properties
    [OutLabel("knows")]
    public ICollection<Person> Friends { get; set; }
    
    [InLabel("worksFor")]
    public Company Employer { get; set; }
    
    [Ignore]
    public string ComputedProperty => $"{Name} ({Age})";
}
```

### Edge Definition

```csharp
[EdgeLabel("friendship")]
public class Friendship : IEdge
{
    public string Id { get; set; }
    public string InVertexId { get; set; }
    public string OutVertexId { get; set; }
    
    public EpochDateTime Since { get; set; }
    public string Strength { get; set; } // "weak", "strong"
}
```

### Property Wrappers

```csharp
public class PersonWithMetadata : IVertex
{
    public string Id { get; set; }
    
    // Simple property
    public string Name { get; set; }
    
    // Property with metadata
    public Property<int> Age { get; set; }
    
    // Access property value and metadata
    public void Example()
    {
        Age.Value = 30;
        Age.Key = "age";
        var ageValue = Age.Value; // 30
    }
}
```

### EpochDateTime Usage

```csharp
public class Event : IVertex
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    // Automatically converts to/from Unix timestamp
    public EpochDateTime Timestamp { get; set; }
    
    public void SetToNow()
    {
        Timestamp = EpochDateTime.Now;
    }
    
    public DateTime AsDateTime()
    {
        return Timestamp.ToDateTime();
    }
}
```

### Migration from Obsolete Attributes

```csharp
// OLD (Obsolete)
[EdgeLabel("knows")]
public ICollection<Person> Friends { get; set; }

[ReverseEdgeLabel("worksFor")]  
public ICollection<Person> Employees { get; set; }

// NEW (Recommended)
[InLabel("knows")]
public ICollection<Person> Friends { get; set; }

[OutLabel("worksFor")]
public ICollection<Person> Employees { get; set; }
```

## Compatibility

- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+
- **Multi-targeting** - Supports modern .NET versions when used with the main Stardust.Paradox.Data package

## Dependencies

- **Microsoft.Extensions.DependencyInjection.Abstractions** (2.1.1)
- **Newtonsoft.Json** (13.0.1)

## Related Packages

- **[Stardust.Paradox.Data](https://www.nuget.org/packages/Stardust.Paradox.Data/)** - Main graph database toolkit
- **[Stardust.Paradox.Data.Providers.CosmosDb](https://www.nuget.org/packages/Stardust.Paradox.Data.Providers.CosmosDb/)** - Azure Cosmos DB provider
- **[Stardust.Paradox.Data.Providers.Gremlin](https://www.nuget.org/packages/Stardust.Paradox.Data.Providers.Gremlin/)** - TinkerPop Gremlin provider

## Documentation

For complete documentation and examples:
- [GitHub Repository](https://github.com/JonasSyrstad/Stardust.Paradox)
- [Main Package Documentation](https://github.com/JonasSyrstad/Stardust.Paradox/wiki)

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/JonasSyrstad/Stardust.Paradox/blob/main/LICENSE) file for details.

---

**Tags:** CosmosDB, Gremlin, TinkerPop, EntityFramework, Annotations, Graph Database