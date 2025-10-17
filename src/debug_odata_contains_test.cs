using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.OData;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Extensions;
using Stardust.Paradox.Data.OData;
using Stardust.Paradox.Data.Traversals;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    [VertexLabel("person")]
    public interface ITestPerson : IVertex
    {
        string Name { get; set; }
        int Age { get; set; }
        bool Active { get; set; }
        string Email { get; set; }
        string Description { get; set; }
    }

    private class TestGraphContext : GraphContextBase
    {
        public TestGraphContext(IGremlinLanguageConnector connector) : base(connector, CreateServiceProvider())
        {
        }

        public IGraphSet<ITestPerson> People { get; }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddEntityBinding((entity, implementation) =>
            {
                services.AddTransient(entity, implementation);
            });
            return services.BuildServiceProvider();
        }

        protected override bool InitializeModel(IGraphConfiguration configuration)
        {
            configuration.ConfigureCollection<ITestPerson>();
            return true;
        }
    }

    static async Task Main(string[] args)
    {
        var connector = new InMemoryGremlinLanguageConnector(new InMemoryDatabaseOptions 
        { 
            EnableDebugLogging = true, 
            EnableQueryLogging = true 
        });

        // Apply the OData test scenario
        Scenarios.InMemoryScenarioRegistry.ApplyScenario(connector.Database, "OdataTestScenario");

        var context = new TestGraphContext(connector);

        var options = new ODataSearchOptions
        {
            Filter = "email contains 'example'"
        };

        Console.WriteLine($"Filter: {options.Filter}");
        Console.WriteLine();

        try
        {
            var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
            Console.WriteLine($"Results count: {list.Count}");
            foreach (var person in list)
            {
                Console.WriteLine($"  Name: {person.Name}, Email: {person.Email}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
}
