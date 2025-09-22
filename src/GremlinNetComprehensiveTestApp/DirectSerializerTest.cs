using System;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;
using System.Collections.Generic;
using System.Linq;

namespace GremlinNetComprehensiveTestApp
{
    class DirectSerializerTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("?? Direct Serializer Test for System.Text.Json Compatibility");
            Console.WriteLine("=" + new string('=', 60));

            try
            {
                // Test the fixed serializer directly
                Console.WriteLine("?? Testing GremlinNetCompatibleSerializer directly...");
                
                var serializer = new GremlinNetCompatibleSerializer();
                
                // Create a test response with numbers (the problematic case)
                var testResponse = new TinkerPopResponse
                {
                    RequestId = Guid.NewGuid(),
                    Status = new TinkerPopStatus
                    {
                        Code = 200,
                        Message = "",
                        Attributes = new Dictionary<string, object>()
                    },
                    Result = new TinkerPopResult
                    {
                        Data = new List<object> { 1, 2, 3 }, // Numbers that cause issues
                        Meta = new Dictionary<string, object>()
                    }
                };
                
                Console.WriteLine("?? Serializing test response with numbers...");
                var json = serializer.SerializeResponse(testResponse);
                
                Console.WriteLine("? Serialization successful!");
                Console.WriteLine($"?? Generated JSON: {json}");
                
                // Verify the JSON doesn't contain raw numbers
                if (json.Contains("\"200\"") && json.Contains("[\"1\",\"2\",\"3\"]"))
                {
                    Console.WriteLine("? SUCCESS: All numbers converted to strings!");
                    Console.WriteLine("? This format should be compatible with System.Text.Json");
                }
                else
                {
                    Console.WriteLine("? ISSUE: JSON still contains raw numbers");
                    Console.WriteLine("? This may cause System.Text.Json parsing errors");
                }
                
                // Test MIME-prefixed message parsing
                Console.WriteLine("\n?? Testing MIME-prefixed message parsing...");
                var testMessage = "!application/vnd.gremlin-v3.0+json{\"requestId\":\"12345678-1234-1234-1234-123456789012\",\"op\":\"eval\",\"processor\":\"\",\"args\":{\"gremlin\":\"g.V().count()\"}}";
                
                try
                {
                    var parsed = serializer.DeserializeMessage(testMessage);
                    Console.WriteLine($"? MIME parsing successful!");
                    Console.WriteLine($"   RequestId: {parsed.RequestId}");
                    Console.WriteLine($"   Operation: {parsed.Op}");
                    Console.WriteLine($"   Gremlin: {parsed.Args.GetValueOrDefault("gremlin", "unknown")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? MIME parsing failed: {ex.Message}");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\n?? Direct serializer test completed. Press any key to exit...");
            Console.ReadKey();
        }
    }
}