using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Extension methods for working with mock responses
    /// </summary>
    public static class MockExtensions
    {
        /// <summary>
        /// Create a mock vertex response
        /// </summary>
        public static dynamic CreateVertexResponse(string id, string label, Dictionary<string, object> properties = null)
        {
            dynamic vertex = new ExpandoObject();
            vertex.id = id;
            vertex.label = label;
            vertex.type = "vertex";
            vertex.properties = properties ?? new Dictionary<string, object>();
            return vertex;
        }

        /// <summary>
        /// Create a mock edge response
        /// </summary>
        public static dynamic CreateEdgeResponse(string id, string label, string outV, string inV, Dictionary<string, object> properties = null)
        {
            dynamic edge = new ExpandoObject();
            edge.id = id;
            edge.label = label;
            edge.type = "edge";
            edge.outV = outV;
            edge.inV = inV;
            edge.properties = properties ?? new Dictionary<string, object>();
            return edge;
        }

        /// <summary>
        /// Create a collection of mock vertices
        /// </summary>
        public static IEnumerable<dynamic> CreateVertexCollection(params (string id, string label, Dictionary<string, object> properties)[] vertices)
        {
            return vertices.Select(v => CreateVertexResponse(v.id, v.label, v.properties));
        }

        /// <summary>
        /// Create a collection of mock edges
        /// </summary>
        public static IEnumerable<dynamic> CreateEdgeCollection(params (string id, string label, string outV, string inV, Dictionary<string, object> properties)[] edges)
        {
            return edges.Select(e => CreateEdgeResponse(e.id, e.label, e.outV, e.inV, e.properties));
        }

        /// <summary>
        /// Create a vertex with specific properties using tuple syntax
        /// </summary>
        public static dynamic CreateVertexWithId(string id, string label, params (string key, object value)[] properties)
        {
            var propDict = properties?.ToDictionary(p => p.key, p => p.value) ?? new Dictionary<string, object>();
            return CreateVertexResponse(id, label, propDict);
        }

        /// <summary>
        /// Create random vertices for testing purposes
        /// </summary>
        public static IEnumerable<dynamic> CreateRandomVertices(string label, int count, params string[] propertyNames)
        {
            var random = new Random();
            var vertices = new List<dynamic>();

            for (int i = 0; i < count; i++)
            {
                var properties = new Dictionary<string, object>();
                
                foreach (var propName in propertyNames)
                {
                    properties[propName] = GenerateRandomPropertyValue(propName, random);
                }

                vertices.Add(CreateVertexResponse($"{label}_{i + 1}", label, properties));
            }

            return vertices;
        }

        /// <summary>
        /// Create random edges between vertices
        /// </summary>
        public static IEnumerable<dynamic> CreateRandomEdges(string label, IEnumerable<string> fromIds, IEnumerable<string> toIds, int count)
        {
            var random = new Random();
            var fromList = fromIds.ToList();
            var toList = toIds.ToList();
            var edges = new List<dynamic>();

            for (int i = 0; i < count; i++)
            {
                var fromId = fromList[random.Next(fromList.Count)];
                var toId = toList[random.Next(toList.Count)];
                
                edges.Add(CreateEdgeResponse($"{label}_{i + 1}", label, fromId, toId));
            }

            return edges;
        }

        private static object GenerateRandomPropertyValue(string propertyName, Random random)
        {
            var lowerPropName = propertyName.ToLowerInvariant();
            
            if (lowerPropName.Contains("name"))
                return $"Test{random.Next(1000)}";
            
            if (lowerPropName.Contains("email"))
                return $"test{random.Next(1000)}@example.com";
            
            if (lowerPropName.Contains("age"))
                return random.Next(18, 80);
            
            if (lowerPropName.Contains("date") || lowerPropName.Contains("time"))
                return DateTime.UtcNow.AddDays(-random.Next(365)).ToString("O");
            
            if (lowerPropName.Contains("id"))
                return Guid.NewGuid().ToString();
            
            // Default to string
            return $"Value_{random.Next(1000)}";
        }
    }

    /// <summary>
    /// Builder for creating mock scenarios
    /// </summary>
    public class MockScenarioBuilder
    {
        private readonly MockGremlinLanguageConnector _connector;

        public MockScenarioBuilder(MockGremlinLanguageConnector connector)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        }

        /// <summary>
        /// Configure a response for vertex queries
        /// </summary>
        public MockScenarioBuilder ForVertexQuery(string pattern = @"g\.V\(\)")
        {
            return ForQuery(pattern);
        }

        /// <summary>
        /// Configure a response for edge queries
        /// </summary>
        public MockScenarioBuilder ForEdgeQuery(string pattern = @"g\.E\(\)")
        {
            return ForQuery(pattern);
        }

        /// <summary>
        /// Configure a response for any query pattern
        /// </summary>
        public MockScenarioBuilder ForQuery(string pattern)
        {
            _currentPattern = pattern;
            return this;
        }

        /// <summary>
        /// Return JSON response
        /// </summary>
        public MockScenarioBuilder ReturnJson(string json)
        {
            if (string.IsNullOrEmpty(_currentPattern))
                throw new InvalidOperationException("Must specify a query pattern first");
            
            _connector.ConfigureJsonResponse(_currentPattern, json);
            return this;
        }

        /// <summary>
        /// Return response from JSON file
        /// </summary>
        public MockScenarioBuilder ReturnJsonFile(string filePath)
        {
            if (string.IsNullOrEmpty(_currentPattern))
                throw new InvalidOperationException("Must specify a query pattern first");
            
            _connector.ConfigureJsonFileResponse(_currentPattern, filePath);
            return this;
        }

        /// <summary>
        /// Return response from function
        /// </summary>
        public MockScenarioBuilder ReturnFromFunction(Func<string, Dictionary<string, object>, IEnumerable<dynamic>> function)
        {
            if (string.IsNullOrEmpty(_currentPattern))
                throw new InvalidOperationException("Must specify a query pattern first");
            
            _connector.ConfigureFunctionResponse(_currentPattern, function);
            return this;
        }

        /// <summary>
        /// Return empty result
        /// </summary>
        public MockScenarioBuilder ReturnEmpty()
        {
            return ReturnFromFunction((q, p) => new List<dynamic>());
        }

        /// <summary>
        /// Return error
        /// </summary>
        public MockScenarioBuilder ThrowError(Exception exception)
        {
            return ReturnFromFunction((q, p) => throw exception);
        }

        /// <summary>
        /// Quickly add vertices to the scenario
        /// </summary>
        public MockScenarioBuilder WithVertices(params (string id, string label, Dictionary<string, object> properties)[] vertices)
        {
            foreach (var vertex in vertices)
            {
                _connector.GetGraphStore().AddVertex($"g.addV('{vertex.label}')", new Dictionary<string, object>());
            }
            return this;
        }

        /// <summary>
        /// Quickly add edges to the scenario
        /// </summary>
        public MockScenarioBuilder WithEdges(params (string id, string label, string from, string to)[] edges)
        {
            foreach (var edge in edges)
            {
                _connector.GetGraphStore().AddEdge($"g.addE('{edge.label}')", new Dictionary<string, object>());
            }
            return this;
        }

        /// <summary>
        /// Apply a pre-built template
        /// </summary>
        public MockScenarioBuilder WithTemplate(string templateName)
        {
            ApplyTemplate(templateName);
            return this;
        }

        private void ApplyTemplate(string templateName)
        {
            switch (templateName.ToLowerInvariant())
            {
                case "socialnetwork":
                    ApplySocialNetworkTemplate();
                    break;
                case "organization":
                    ApplyOrganizationTemplate();
                    break;
                case "ecommerce":
                    ApplyECommerceTemplate();
                    break;
                default:
                    throw new ArgumentException($"Unknown template: {templateName}");
            }
        }

        private void ApplySocialNetworkTemplate()
        {
            // Configure common social network patterns
            _connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('user'\)", (q, p) =>
                MockExtensions.CreateVertexCollection(
                    ("user1", "user", new Dictionary<string, object> { { "name", "John" }, { "email", "john@example.com" } }),
                    ("user2", "user", new Dictionary<string, object> { { "name", "Jane" }, { "email", "jane@example.com" } }),
                    ("user3", "user", new Dictionary<string, object> { { "name", "Bob" }, { "email", "bob@example.com" } })
                ));

            _connector.ConfigureFunctionResponse(@"g\.E\(\)\.hasLabel\('friends'\)", (q, p) =>
                MockExtensions.CreateEdgeCollection(
                    ("f1", "friends", "user1", "user2", new Dictionary<string, object>()),
                    ("f2", "friends", "user2", "user3", new Dictionary<string, object>())
                ));
        }

        private void ApplyOrganizationTemplate()
        {
            // Configure common organization patterns
            _connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('employee'\)", (q, p) =>
                MockExtensions.CreateVertexCollection(
                    ("emp1", "employee", new Dictionary<string, object> { { "name", "Alice" }, { "department", "Engineering" } }),
                    ("emp2", "employee", new Dictionary<string, object> { { "name", "Bob" }, { "department", "Sales" } })
                ));

            _connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('department'\)", (q, p) =>
                MockExtensions.CreateVertexCollection(
                    ("dept1", "department", new Dictionary<string, object> { { "name", "Engineering" } }),
                    ("dept2", "department", new Dictionary<string, object> { { "name", "Sales" } })
                ));
        }

        private void ApplyECommerceTemplate()
        {
            // Configure common e-commerce patterns
            _connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('customer'\)", (q, p) =>
                MockExtensions.CreateVertexCollection(
                    ("cust1", "customer", new Dictionary<string, object> { { "name", "Customer1" }, { "email", "cust1@example.com" } }),
                    ("cust2", "customer", new Dictionary<string, object> { { "name", "Customer2" }, { "email", "cust2@example.com" } })
                ));

            _connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('product'\)", (q, p) =>
                MockExtensions.CreateVertexCollection(
                    ("prod1", "product", new Dictionary<string, object> { { "name", "Product1" }, { "price", 99.99 } }),
                    ("prod2", "product", new Dictionary<string, object> { { "name", "Product2" }, { "price", 149.99 } })
                ));
        }

        private string _currentPattern;
    }
}