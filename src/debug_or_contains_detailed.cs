using System;
using System.Linq;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Factory;
using Stardust.Paradox.Data.InMemory.Scenarios;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Stardust.Paradox.Data.Linq.Tests.Scenarios;
using Stardust.Paradox.Data.Traversals;

// Debug test for OR with Contains
var database = new InMemoryGraphDatabase();
var scenario = new LinqTestScenario();
scenario.ConfigureScenario(database);

var connector = InMemoryConnectorFactory.Create(database);
GremlinFactory.SetActivatorFactory(() => connector);

// Execute the query directly  
var query = "g.V().hasLabel('person').where(or(has('name', containing(__p0)), has('email', containing(__p1))))";
var parameters = new Dictionary<string, object>
{
    ["__p0"] = "Alice",
    ["__p1"] = "Diana"
};

Console.WriteLine($"Query: {query}");
Console.WriteLine($"Parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))}");

var results = await connector.ExecuteAsync(query, parameters);
Console.WriteLine($"\nResults: {results.Count()} items");

foreach (var result in results)
{
    Console.WriteLine($"  - {result}");
}

// Also dump all people
Console.WriteLine("\nAll people in database:");
var allPeople = await connector.ExecuteAsync("g.V().hasLabel('person').valueMap()", new Dictionary<string, object>());
foreach (var person in allPeople)
{
    Console.WriteLine($"  - {person}");
}
