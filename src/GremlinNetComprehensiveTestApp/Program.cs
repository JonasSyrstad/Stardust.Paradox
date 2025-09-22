using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Server;

namespace GremlinNetComprehensiveTestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 Running Gremlin.Net Comprehensive Test App");
            Console.WriteLine("=" + new string('=', 50));

            try
            {
                var result = await SimpleGremlinNetConnectionTest.RunTestAsync();
                Console.WriteLine(result.ToString());
                
                if (result.Success)
                {
                    Console.WriteLine("✅ Test completed successfully!");
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("❌ Test failed!");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Unhandled exception: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
