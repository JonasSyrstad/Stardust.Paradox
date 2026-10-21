---
title: How to visualize Gremlin queries with Stardust Gremlin Studio
tags: gremlin, tinkerpop, cosmosdb, graphdatabase
canonical_url: https://jonassyrstad.github.io/Stardust.Paradox/
---

# How to visualize Gremlin queries with Stardust Gremlin Studio

Gremlin queries return JSON, which gets hard to read once traversals span more than a couple of hops. [Stardust Gremlin Studio](https://jonassyrstad.github.io/Stardust.Paradox/) is a free, open source Gremlin IDE that renders results as an interactive graph.

## 1. Install

Download the Windows installer from the [latest release](https://github.com/JonasSyrstad/Stardust.Paradox/releases/latest).

## 2. Load sample data without a database

Open **Local Playground**, choose the **Modern** scenario (the classic TinkerPop graph) and click **Load**.

## 3. Run a traversal

```groovy
g.V().hasLabel('person').outE().inV().path()
```

Press **Ctrl+Enter**.

## 4. Explore the graph

Switch to the **Graph** tab. Drag nodes, zoom, and click edges to inspect properties. Use the **Table** and **Tree** tabs for tabular or hierarchical views.

## 5. Connect to your real graph

- **Azure Cosmos DB**: New connection -> Azure Cosmos DB (Gremlin API) -> paste connection string -> Discover Graphs.
- **TinkerPop / JanusGraph**: New connection -> Generic Gremlin Server -> host, port 8182.

<!-- TODO: add screenshots of each step -->

## Summary

Stardust Gremlin Studio gives you an editor with auto-completion, graph visualization and an offline playground in one free tool. Source: [GitHub](https://github.com/JonasSyrstad/Stardust.Paradox/tree/HEAD/src/StardustGremlinStudio).
