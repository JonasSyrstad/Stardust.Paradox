using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Demo;

namespace VertexPropertiesDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Testing vertex properties display in demo app...");

            var server = await GremlinDatabase.StartAsync(options =>
            {
                options.Host = "localhost";
                options.Port = 8182;
                options.HttpPort = 8183;
                options.EnableTcp = false;
                options.EnableWebSocket = true;
                options.EnableHttp = false;
                options.EnableLogging = false;
            });

            try
            {
                // Load scenario
                var scenario = new SocialCommerceScenario(server.Connector);
                await scenario.LoadScenarioAsync();

                // Create client
                var client = await GremlinClientFactory.CreateClientAsync(WireProtocol.Direct, server.Connector);

                // Test the queries that should show properties
                Console.WriteLine("\n?? Testing option 1: List all users (with properties)");
                var users = await client.ExecuteAsync("g.V().hasLabel('user')");
                
                Console.WriteLine($"Result count: {users.Count()}");
                foreach (var user in users.Take(3))
                {
                    Console.WriteLine($"User: {user}");
                }

                Console.WriteLine("\n?? Testing option 5: All products (with properties)");
                var products = await client.ExecuteAsync("g.V().hasLabel('product')");
                
                Console.WriteLine($"Result count: {products.Count()}");
                foreach (var product in products.Take(3))
                {
                    Console.WriteLine($"Product: {product}");
                }

                client?.Dispose();
            }
            finally
            {
                await GremlinDatabase.StopAsync(server);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}