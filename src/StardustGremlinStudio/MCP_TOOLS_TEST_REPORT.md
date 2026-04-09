# GremlinStudio MCP Tools – Test Report

**Date:** 2026-04-09 (re-verified after fixes)  
**Scope:** All MCP tools exposed by `Stardust.Paradox.GremlinStudio.McpServer`  
**Method:** Invoked every tool via the MCP protocol with real data, verified return values, then reviewed source code for correctness.

---

## Final Verification Results (Post-Fix)

| # | Tool | Status | Notes |
|---|------|--------|-------|
| 1 | `ListConnections` | ✅ PASS | Returns all 12 saved connections |
| 2 | `GetConnectionDetails` | ✅ PASS | Works by name and by ID |
| 3 | `ListSnippets` | ✅ PASS | Returns 4 snippets |
| 4 | `GetSnippet` | ✅ PASS | Lookup by name works |
| 5 | `ListQueryHistory` | ✅ PASS | Name-to-ID resolution confirmed working |
| 6 | `ExecuteGremlinQuery` | ✅ PASS | Ran g.V().count(), g.V().limit(3), g.E().count() |
| 7 | `DiscoverGraphSchema` | ✅ PASS | Returns structured SchemaToolResult |
| 8 | `ExportSchemaAsCode` | ✅ PASS | Returns structured SchemaCodeExportResult |
| 9 | `ExportScenario` | ✅ PASS | Returns structured ScenarioExportResult with vertex/edge counts |
| 10 | `ListScenarios` | ✅ PASS | Returns all 7 built-in scenarios |

### Error Handling Verified
- `ExecuteGremlinQuery` with invalid connection → structured error listing available connections ✅

---

## Original Issues Found (Pre-Fix)

| # | Tool | File | Result |
|---|------|------|--------|
| 1 | `ListConnections` | `ConnectionTools.cs` | ✅ Returned data (verified connections.json has 7 KB) |
| 2 | `GetConnectionDetails` | `ConnectionTools.cs` | ✅ Structural review OK |
| 3 | `ListSnippets` | `SnippetTools.cs` | ✅ Returned data (verified snippets.json has entries) |
| 4 | `GetSnippet` | `SnippetTools.cs` | ✅ Structural review OK |
| 5 | `ListQueryHistory` | `SnippetTools.cs` | ⚠️ **Bug:** name-to-ID filter mismatch |
| 6 | `ExecuteGremlinQuery` | `QueryTools.cs` | ⚠️ **Bug:** connector resource leak |
| 7 | `DiscoverGraphSchema` | `SchemaTools.cs` | ⚠️ **Bug:** connector resource leak |
| 8 | `ExportSchemaAsCode` | `SchemaTools.cs` | ⚠️ **Bug:** inconsistent error handling + connector leak |
| 9 | `ExportScenario` | `ScenarioTools.cs` | ⚠️ **Bug:** connector resource leak |
| 10 | `ListScenarios` | `ScenarioTools.cs` | ❌ **Dead tool:** code is commented out |

---

## Issue Details

### Issue 1 – `ListScenarios` tool is dead (Severity: Medium)

**File:** `ScenarioTools.cs` lines 36-48  
**Problem:** The `ListScenarios` method and its `[McpServerTool]` attribute are fully commented out. 
The tool still appears in the MCP tool registry (likely cached/stale metadata) and returns "nothing" when called.
There are 7 built-in scenarios registered in `InMemoryScenarioRegistry` that should be discoverable.

**Fix:** Uncomment the method or remove the `ScenarioSummary` class if the tool is intentionally disabled.

---

### Issue 2 – `ListQueryHistory` filter by name fails (Severity: Medium)

**File:** `SnippetTools.cs` lines 71-94  
**Problem:** The `connectionNameOrId` parameter is passed directly to `_historyService.GetHistory(connectionId)`, 
which does an exact `StringComparison.Ordinal` match against `QueryHistoryItem.ConnectionId` (a GUID).

If the user passes a **connection name** (e.g., `"DevTest services/tenant"`), it will never match 
because history items store the connection **ID** (e.g., `"47e19ce2-fa52-4529-9b69-5c508357d47d"`).

The comment on line 79 acknowledges this but does not fix it.

**Fix:** Resolve the name to an ID by looking it up in `IGremlinConnectionStore` before passing to `GetHistory`.

---

### Issue 3 – Connector resource leak in query/schema/scenario tools (Severity: High)

**Files:** `QueryTools.cs:93-96`, `SchemaTools.cs:83-87`, `ScenarioTools.cs:123-127`  
**Problem:** All three tool classes create connectors via `_connectorFactory.CreateConnector(settings)` and 
attempt disposal in a `finally` block with:

```csharp
if (connector is IDisposable disposable)
    disposable.Dispose();
```

However, `GremlinNetLanguageConnector` does **not** implement `IDisposable` (the implementation is 
commented out at line 16 and lines 201-219 of `GremlinNetLanguageConnector.cs`).

This means:
- **CosmosDb/GremlinServer connectors** are never disposed → `GremlinClient` instances and WebSocket connections leak.
- **InMemory connectors** are disposed correctly (they do implement `IDisposable`).

Additionally, none of the tools check for `IAsyncDisposable`.

**Fix:** Either:
- (Preferred) Make `GremlinNetLanguageConnector` implement `IDisposable` (un-comment the existing code), or
- Add a `CloseConnection` method to `IGremlinLanguageConnector` and call it from tools, or
- At minimum, call `GremlinClient.Dispose()` via the connector factory when done.

---

### Issue 4 – `ExportSchemaAsCode` uses inconsistent error pattern (Severity: Low)

**File:** `SchemaTools.cs` lines 94-146  
**Problem:** `ExportSchemaAsCode` returns a plain `string`. Errors are returned as `"Error: ..."` strings 
that an AI agent must parse via string matching. This is inconsistent with `DiscoverGraphSchema` which 
returns a structured `SchemaToolResult` with `IsSuccess` and `ErrorMessage` fields.

**Fix:** Return a structured result type (similar to `SchemaToolResult`) instead of raw strings.

---

### Issue 5 – `ExportScenario` uses inconsistent error pattern (Severity: Low)

**File:** `ScenarioTools.cs` lines 55-129  
**Problem:** Same as Issue 4. `ExportScenario` returns plain `string` with `"Error: ..."` prefix for failures.
AI agents cannot reliably distinguish errors from valid export content.

**Fix:** Return a structured result type with `IsSuccess`, `Content`, and `ErrorMessage` fields.

---

### Issue 6 – `ListQueryHistory` does not auto-load history (Severity: Low)

**File:** `SnippetTools.cs` line 74  
**Problem:** `ListQueryHistory` calls `_historyService.LoadAsync()` every time it's invoked. 
Since `FileQueryHistoryService` is registered as a singleton, this reloads from disk on every call.
While not a bug per se, it's inefficient for repeated calls and the load operation replaces the 
in-memory state (see `FileQueryHistoryService.LoadAsync` line 186+), which could discard any 
in-session additions if another process modified the file.

**Fix:** Consider loading once at startup (or using a "loaded" flag), or document this as intentional 
for freshness.

---

## Summary of Recommended Fixes

| Priority | Issue | Effort |
|----------|-------|--------|
| 🔴 High | Issue 3: Connector resource leak | Medium – requires decision on disposal strategy |
| 🟡 Medium | Issue 1: ListScenarios dead tool | Small – uncomment code |
| 🟡 Medium | Issue 2: ListQueryHistory name filter | Small – add name-to-ID resolution |
| 🟢 Low | Issue 4: ExportSchemaAsCode error pattern | Small – add result type |
| 🟢 Low | Issue 5: ExportScenario error pattern | Small – add result type |
| 🟢 Low | Issue 6: ListQueryHistory reloads every call | Minimal – add loaded flag or document |
