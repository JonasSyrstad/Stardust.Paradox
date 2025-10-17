using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Scenarios;
using Stardust.Paradox.Data.InMemory.Tests;

class Program
{
    static async Task Main()
    {
        try
        {
            InMemoryScenarioRegistry.Register(new ServiceElementScenario());
        }
        catch (Exception e)
        {
            Console.WriteLine($"Scenario registration: {e.Message}");
        }

        string tenantId = "31729bb9-5b5a-423b-b8ff-1cb81dfbb5b3";
        string mainAssetId = "6000ba9f-2ade-4412-8c82-e33d32a725cf";
        string testProfileId = "74f0fc83-51e3-48af-ac58-25176301cd6f";

        var connector = InMemoryGremlinLanguageConnector.Create(options =>
        {
            options.LogQueries = true;
            options.EnableDebugLogging = true;
            options.EnableQueryLogging = true;
        });

        InMemoryScenarioRegistry.ApplyScenario(connector.Database, "");

        Console.WriteLine("\n=== Testing different query variations ===\n");

        // Test 1: Check if vertices exist
        Console.WriteLine("Test 1: Check vertices exist");
        var allVertices = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
        Console.WriteLine($"Total vertices: {allVertices.Count()}");

        // Test 2: Check specific vertices by ID
        Console.WriteLine("\nTest 2: Get main asset by ID");
        var mainAsset = await connector.ExecuteAsync(
            $"g.V('{mainAssetId}')",
            new Dictionary<string, object>());
        Console.WriteLine($"Main asset found: {mainAsset.Count()}");
        foreach (var v in mainAsset)
        {
            Console.WriteLine($"  ID: {v.id}, Label: {v.label}, EntityType: {v.properties?.entityType}");
        }

        // Test 3: Test has with within - QUOTED
        Console.WriteLine("\nTest 3: has('id', within(...)) with QUOTED strings");
        var withinTestQuoted = await connector.ExecuteAsync(
            $"g.V().has('id',within('{mainAssetId}', '{testProfileId}'))",
            new Dictionary<string, object>());
        Console.WriteLine($"Results with quoted: {withinTestQuoted.Count()}");

        // Test 4: Test has with within - UNQUOTED (as in failing test)
        Console.WriteLine("\nTest 4: has('id', within(...)) with UNQUOTED strings");
        var withinTestUnquoted = await connector.ExecuteAsync(
            $"g.V().has('id',within({mainAssetId}, {testProfileId}))",
            new Dictionary<string, object>());
        Console.WriteLine($"Results with unquoted: {withinTestUnquoted.Count()}");

        // Test 5: Simple has filter
        Console.WriteLine("\nTest 5: Simple has filter");
        var hasTest = await connector.ExecuteAsync(
            $"g.V().has('id','{mainAssetId}')",
            new Dictionary<string, object>());
        Console.WriteLine($"Has test results: {hasTest.Count()}");

        // Test 6: Test outE from main asset
        Console.WriteLine("\nTest 6: outE from main asset");
        var edgesTest = await connector.ExecuteAsync(
            $"g.V('{mainAssetId}').outE('members')",
            new Dictionary<string, object>());
        Console.WriteLine($"Outgoing 'members' edges: {edgesTest.Count()}");
        foreach (var e in edgesTest)
        {
            Console.WriteLine($"  Edge ID: {e.id}, Label: {e.label}, memberType: {e.properties?.memberType}");
        }

        // Test 7: Full complex query with QUOTED IDs
        Console.WriteLine("\nTest 7: Full complex query with QUOTED IDs");
        var fullQuery = $@"g.V().has('id',within('{mainAssetId}', '{testProfileId}'))
  .has('pk','{tenantId}')
  .as('a')
  .repeat(outE('members').as('e').otherV().simplePath())
  .until(or(has('entityType','profile'),has('entityType','userGroup')))
  .path().unfold()
  .where(select('e').not(has('memberType','assetStructure')))
  .limit(1)
  .select('e')";
        Console.WriteLine($"Query: {fullQuery}");
        var fullResult = await connector.ExecuteAsync(fullQuery, new Dictionary<string, object>());
        Console.WriteLine($"Full query results: {fullResult.Count()}");

        // Test 8: Simpler version - just repeat/until
        Console.WriteLine("\nTest 8: Simpler repeat/until");
        var simpleRepeat = await connector.ExecuteAsync(
            $@"g.V('{mainAssetId}')
  .repeat(outE('members').as('e').otherV().simplePath())
  .until(or(has('entityType','profile'),has('entityType','userGroup')))
  .path()",
            new Dictionary<string, object>());
        Console.WriteLine($"Repeat/until path results: {simpleRepeat.Count()}");

        Console.WriteLine("\nDone!");
    }
}
