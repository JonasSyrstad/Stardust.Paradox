using System;
using System.Collections.Generic;
using System.Linq;
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
        catch { }

        string tenantId = "31729bb9-5b5a-423b-b8ff-1cb81dfbb5b3";
        string mainAssetId = "6000ba9f-2ade-4412-8c82-e33d32a725cf";
        string testProfileId = "74f0fc83-51e3-48af-ac58-25176301cd6f";

        var connector = InMemoryGremlinLanguageConnector.Create(options =>
        {
            options.LogQueries = true;
            options.EnableDebugLogging = true;
            options.EnableQueryLogging = true;
        });

        Scenarios.InMemoryScenarioRegistry.ApplyScenario(connector.Database, "ServiceElementScenario");

        Console.WriteLine("=== Testing Step by Step ===\n");

        // Step 1: Check vertex exists
        var step1 = await connector.ExecuteAsync($"g.V('{mainAssetId}')", new Dictionary<string, object>());
        Console.WriteLine($"Step 1 (vertex exists): {step1.Count()} results");
        if (step1.Any())
        {
            var v = step1.First();
            Console.WriteLine($"  Vertex: id={v.id}, label={v.label}");
        }

        // Step 2: Check outgoing edges
        var step2 = await connector.ExecuteAsync($"g.V('{mainAssetId}').outE('members')", new Dictionary<string, object>());
        Console.WriteLine($"\nStep 2 (outE members): {step2.Count()} results");
        foreach (var e in step2)
        {
            Console.WriteLine($"  Edge: id={e.id}, label={e.label}, inV={e.inV}");
        }

        // Step 3: Repeat without path
        var step3Query = $"g.V('{mainAssetId}').repeat(outE('members').as('e').otherV().simplePath()).until(or(has('entityType','profile'),has('entityType','userGroup')))";
        var step3 = await connector.ExecuteAsync(step3Query, new Dictionary<string, object>());
        Console.WriteLine($"\nStep 3 (repeat/until): {step3.Count()} results");
        foreach (var v in step3.Take(3))
        {
            Console.WriteLine($"  Result: id={v.id}, entityType={v.properties?.entityType}");
        }

        // Step 4: With path
        var step4Query = $"g.V('{mainAssetId}').repeat(outE('members').as('e').otherV().simplePath()).until(or(has('entityType','profile'),has('entityType','userGroup'))).path()";
        var step4 = await connector.ExecuteAsync(step4Query, new Dictionary<string, object>());
        Console.WriteLine($"\nStep 4 (with path): {step4.Count()} results");
        if (step4.Any())
        {
            var path = step4.First();
            Console.WriteLine($"  Path type: {path.GetType().Name}");
            Console.WriteLine($"  Path: {path}");
        }

        // Step 5: With path().unfold()
        var step5Query = $"g.V('{mainAssetId}').repeat(outE('members').as('e').otherV().simplePath()).until(or(has('entityType','profile'),has('entityType','userGroup'))).path().unfold()";
        var step5 = await connector.ExecuteAsync(step5Query, new Dictionary<string, object>());
        Console.WriteLine($"\nStep 5 (path unfold): {step5.Count()} results");
        foreach (var item in step5.Take(5))
        {
            Console.WriteLine($"  Unfolded: {item}");
        }

        // Full query
        var finalQuery = $@"g.V().has('id',within('{mainAssetId}', '{testProfileId}'))
  .has('pk','{tenantId}')
  .as('a')
  .repeat(outE('members').as('e').otherV().simplePath())
  .until(or(has('entityType','profile'),has('entityType','userGroup')))
  .path().unfold()
  .where(select('e').not(has('memberType','assetStructure')))
  .limit(1)
  .select('e')";
        
        Console.WriteLine($"\nFinal Query:\n{finalQuery}\n");
        var result = await connector.ExecuteAsync(finalQuery, new Dictionary<string, object>());
        Console.WriteLine($"Final result: {result.Count()} results");
        foreach (var item in result)
        {
            Console.WriteLine($"  Result: {item}");
        }
    }
}
