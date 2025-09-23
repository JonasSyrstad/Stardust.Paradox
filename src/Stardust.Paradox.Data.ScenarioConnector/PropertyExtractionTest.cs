using Newtonsoft.Json;
using System.Dynamic;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.ScenarioConnector;

/// <summary>
/// Test utility to validate property extraction logic with real CosmosDB response formats
/// </summary>
public static class PropertyExtractionTest
{
    public static async Task RunPropertyExtractionTestsAsync()
    {
        Console.WriteLine("=== Property Extraction Test ===");
        Console.WriteLine("Testing various CosmosDB response formats for property extraction");
        Console.WriteLine();

        // Test 1: Empty valueMap response (what we're seeing)
        Console.WriteLine("--- Test 1: Empty valueMap Response ---");
        TestEmptyValueMapResponse();

        // Test 2: Standard Gremlin vertex response with properties
        Console.WriteLine("\n--- Test 2: Standard Vertex Response ---");
        TestStandardVertexResponse();

        // Test 3: CosmosDB vertex response with properties
        Console.WriteLine("\n--- Test 3: CosmosDB Vertex Response ---");
        TestCosmosDbVertexResponse();

        // Test 4: Edge property extraction
        Console.WriteLine("\n--- Test 4: Edge Property Extraction ---");
        TestEdgePropertyExtraction();

        Console.WriteLine("\n=== Property Extraction Test Complete ===");
    }

    private static void TestEmptyValueMapResponse()
    {
        // Simulate the empty response we're getting
        var emptyResponse = "{}";
        var emptyObj = JsonConvert.DeserializeObject(emptyResponse);
        
        Console.WriteLine($"Empty response: {emptyResponse}");
        Console.WriteLine($"Parsed as: {emptyObj?.GetType().Name}");
        Console.WriteLine($"Is dictionary: {emptyObj is IDictionary<string, object>}");
        
        if (emptyObj is IDictionary<string, object> dict)
        {
            Console.WriteLine($"Dictionary count: {dict.Count}");
        }
    }

    private static void TestStandardVertexResponse()
    {
        // Simulate a standard Gremlin vertex response
        var vertexJson = @"{
            ""id"": ""Emma"",
            ""label"": ""person"",
            ""type"": ""vertex"",
            ""properties"": {
                ""name"": [{""id"": ""prop1"", ""value"": ""Emma Smith""}],
                ""age"": [{""id"": ""prop2"", ""value"": 28}],
                ""active"": [{""id"": ""prop3"", ""value"": true}]
            }
        }";
        
        var vertex = JsonConvert.DeserializeObject(vertexJson);
        Console.WriteLine($"Vertex JSON: {vertexJson}");
        Console.WriteLine($"Parsed vertex: {JsonConvert.SerializeObject(vertex, Formatting.Indented)}");
        
        // Test property extraction
        dynamic dynVertex = vertex!;
        if (dynVertex.properties != null)
        {
            var props = dynVertex.properties as IDictionary<string, object>;
            if (props != null)
            {
                Console.WriteLine($"Properties found: {props.Count}");
                foreach (var prop in props)
                {
                    Console.WriteLine($"Property {prop.Key}: {JsonConvert.SerializeObject(prop.Value)}");
                    
                    // Extract value using our logic
                    var extractedValue = ExtractPropertyValue(prop.Value);
                    Console.WriteLine($"Extracted value: {extractedValue} (type: {extractedValue?.GetType().Name})");
                }
            }
        }
    }

    private static void TestCosmosDbVertexResponse()
    {
        // Simulate CosmosDB valueMap(true) response format
        var cosmosDbJson = @"{
            ""id"": [""Emma""],
            ""label"": [""person""],
            ""name"": [""Emma Smith""],
            ""age"": [28],
            ""active"": [true]
        }";
        
        var cosmosVertex = JsonConvert.DeserializeObject(cosmosDbJson);
        Console.WriteLine($"CosmosDB JSON: {cosmosDbJson}");
        Console.WriteLine($"Parsed CosmosDB vertex: {JsonConvert.SerializeObject(cosmosVertex, Formatting.Indented)}");
        
        // Test our parsing logic
        dynamic dynVertex = cosmosVertex!;
        var data = dynVertex as IDictionary<string, object>;
        if (data != null)
        {
            Console.WriteLine($"Data entries: {data.Count}");
            foreach (var kvp in data)
            {
                if (kvp.Key != "id" && kvp.Key != "label")
                {
                    var extractedValue = ExtractPropertyValue(kvp.Value);
                    Console.WriteLine($"Property {kvp.Key}: {extractedValue} (type: {extractedValue?.GetType().Name})");
                }
            }
        }
    }

    private static void TestEdgePropertyExtraction()
    {
        // Simulate edge with properties
        var edgeJson = @"{
            ""id"": ""edge123"",
            ""label"": ""knows"",
            ""type"": ""edge"",
            ""inV"": ""Emma"",
            ""outV"": ""John"",
            ""properties"": {
                ""since"": ""2020-01-01"",
                ""strength"": 0.8,
                ""verified"": true
            }
        }";
        
        var edge = JsonConvert.DeserializeObject(edgeJson);
        Console.WriteLine($"Edge JSON: {edgeJson}");
        Console.WriteLine($"Parsed edge: {JsonConvert.SerializeObject(edge, Formatting.Indented)}");
        
        // Test property extraction
        dynamic dynEdge = edge!;
        if (dynEdge.properties != null)
        {
            var props = dynEdge.properties as IDictionary<string, object>;
            if (props != null)
            {
                Console.WriteLine($"Edge properties found: {props.Count}");
                foreach (var prop in props)
                {
                    var extractedValue = ExtractPropertyValue(prop.Value);
                    Console.WriteLine($"Edge property {prop.Key}: {extractedValue} (type: {extractedValue?.GetType().Name})");
                }
            }
        }
    }

    /// <summary>
    /// Copy of the ExtractPropertyValue method for testing
    /// </summary>
    private static object ExtractPropertyValue(object value)
    {
        if (value == null)
            return null!;

        // Handle string values directly
        if (value is string stringValue)
            return stringValue;

        // Handle primitive types directly
        if (value is bool || value is int || value is long || value is float || value is double || value is DateTime)
            return value;

        // Handle arrays/collections
        if (value is IEnumerable<object> valueArray)
        {
            var firstValue = valueArray.FirstOrDefault();
            if (firstValue != null)
            {
                // Handle CosmosDB property format [{"id": "x", "value": "y"}]
                if (firstValue is IDictionary<string, object> propObj)
                {
                    if (propObj.ContainsKey("value"))
                    {
                        return propObj["value"];
                    }
                    // If no "value" key, return the whole object or try to extract meaningful data
                    return propObj.Count == 1 ? propObj.Values.FirstOrDefault() : propObj;
                }
                return firstValue;
            }
        }

        // Handle dictionary objects directly
        if (value is IDictionary<string, object> dictValue)
        {
            if (dictValue.ContainsKey("value"))
                return dictValue["value"];
            
            // If it's a single key-value pair, return the value
            if (dictValue.Count == 1)
                return dictValue.Values.FirstOrDefault();
                
            // Otherwise return the whole dictionary
            return dictValue;
        }

        return value;
    }
}