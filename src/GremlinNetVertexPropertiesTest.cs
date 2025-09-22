using System;
using System.Threading.Tasks;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json;

namespace GremlinNetVertexPropertiesTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Testing Gremlin.Net vertex properties...");

            var server = new GremlinServer("localhost", 8182);
            var client = new GremlinClient(server, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);

            try
            {
                Console.WriteLine("\n1. Creating test vertex with properties...");
                await client.SubmitAsync("g.V().drop()"); // Clear any existing data
                
                var createResult = await client.SubmitAsync("g.addV('person').property('id', 'test1').property('name', 'John').property('age', 30)");
                Console.WriteLine($"   Create result: {JsonConvert.SerializeObject(createResult.ToList(), Formatting.Indented)}");

                Console.WriteLine("\n2. Querying single vertex g.V('test1')...");
                var singleVertex = await client.SubmitAsync("g.V('test1')");
                Console.WriteLine($"   Single vertex result: {JsonConvert.SerializeObject(singleVertex.ToList(), Formatting.Indented)}");

                Console.WriteLine("\n3. Querying all vertices g.V()...");
                var allVertices = await client.SubmitAsync("g.V()");
                Console.WriteLine($"   All vertices result: {JsonConvert.SerializeObject(allVertices.ToList(), Formatting.Indented)}");

                Console.WriteLine("\n4. Using valueMap() to get properties...");
                var valueMap = await client.SubmitAsync("g.V('test1').valueMap()");
                Console.WriteLine($"   ValueMap result: {JsonConvert.SerializeObject(valueMap.ToList(), Formatting.Indented)}");

                Console.WriteLine("\n5. Using elementMap() to get full element...");
                var elementMap = await client.SubmitAsync("g.V('test1').elementMap()");
                Console.WriteLine($"   ElementMap result: {JsonConvert.SerializeObject(elementMap.ToList(), Formatting.Indented)}");

                Console.WriteLine("\n? All tests completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
                Console.WriteLine($"   Stack: {ex.StackTrace}");
            }
            finally
            {
                client?.Dispose();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}