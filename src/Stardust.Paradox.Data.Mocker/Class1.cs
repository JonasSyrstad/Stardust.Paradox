using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Internals;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Mock connector for simulating Gremlin query execution
    /// </summary>
    public class MockGremlinLanguageConnector : LanguageConnectorBase, IGremlinLanguageConnector
    {
        private readonly Dictionary<string, IResponseProvider> _queryResponses;
        private readonly InMemoryGraphStore _graphStore;
        private readonly MockConnectorOptions _options;

        public MockGremlinLanguageConnector(MockConnectorOptions options = null) : base(null)
        {
            _options = options ?? new MockConnectorOptions();
            _queryResponses = new Dictionary<string, IResponseProvider>();
            _graphStore = new InMemoryGraphStore();
            ConsumedRU = 0;
        }

        public bool CanParameterizeQueries => true;
        public double ConsumedRU { get; private set; }

        /// <summary>
        /// Configure a response for a specific query pattern
        /// </summary>
        /// <param name="queryPattern">Regex pattern to match queries</param>
        /// <param name="responseProvider">Response provider</param>
        public void ConfigureResponse(string queryPattern, IResponseProvider responseProvider)
        {
            _queryResponses[queryPattern] = responseProvider;
        }

        /// <summary>
        /// Configure a JSON string response for a query pattern
        /// </summary>
        public void ConfigureJsonResponse(string queryPattern, string jsonResponse)
        {
            ConfigureResponse(queryPattern, new JsonStringResponseProvider(jsonResponse));
        }

        /// <summary>
        /// Configure a JSON file response for a query pattern
        /// </summary>
        public void ConfigureJsonFileResponse(string queryPattern, string jsonFilePath)
        {
            ConfigureResponse(queryPattern, new JsonFileResponseProvider(jsonFilePath));
        }

        /// <summary>
        /// Configure a function-based response for a query pattern
        /// </summary>
        public void ConfigureFunctionResponse(string queryPattern, Func<string, Dictionary<string, object>, IEnumerable<dynamic>> responseFunction)
        {
            ConfigureResponse(queryPattern, new FunctionResponseProvider(responseFunction));
        }

        #region Quick Configuration Methods

        /// <summary>
        /// Quickly populate the graph with vertices and edges
        /// </summary>
        public void QuickPopulate(VertexDefinition[] vertices, EdgeDefinition[] edges = null)
        {
            // Add vertices
            if (vertices != null)
            {
                foreach (var vertex in vertices)
                {
                    var mockVertex = new InMemoryGraphStore.MockVertex
                    {
                        Id = vertex.Id,
                        Label = vertex.Label,
                        Properties = vertex.Properties?.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<string, object>()
                    };
                    _graphStore.AddVertexDirect(mockVertex);
                }
            }

            // Add edges
            if (edges != null)
            {
                foreach (var edge in edges)
                {
                    var mockEdge = new InMemoryGraphStore.MockEdge
                    {
                        Id = edge.Id,
                        Label = edge.Label,
                        OutVertexId = edge.From,
                        InVertexId = edge.To,
                        Properties = new Dictionary<string, object>()
                    };
                    _graphStore.AddEdgeDirect(mockEdge);
                }
            }
        }

        /// <summary>
        /// Auto-configure mock responses for common operations on specified labels
        /// </summary>
        public void AutoConfigureFor(params string[] labels)
        {
            foreach (var label in labels)
            {
                // Vertex queries
                ConfigureFunctionResponse($@"g\.V\(\)\.hasLabel\('{label}'\)", (q, p) =>
                    _graphStore.Vertices.Values
                        .Where(v => v.Label == label)
                        .Select(v => MockExtensions.CreateVertexResponse(v.Id, v.Label, v.Properties)));

                // Vertex creation
                ConfigureFunctionResponse($@"g\.addV\('{label}'\)", (q, p) =>
                {
                    var id = Guid.NewGuid().ToString();
                    var vertex = MockExtensions.CreateVertexResponse(id, label);
                    return new[] { vertex };
                });

                // Edge queries
                ConfigureFunctionResponse($@"g\.E\(\)\.hasLabel\('{label}'\)", (q, p) =>
                    _graphStore.Edges.Values
                        .Where(e => e.Label == label)
                        .Select(e => MockExtensions.CreateEdgeResponse(e.Id, e.Label, e.OutVertexId, e.InVertexId, e.Properties)));
            }
        }

        /// <summary>
        /// Mock vertices of a specific label with generated data
        /// </summary>
        public void MockVertices(string label, int count = 1, params string[] propertyNames)
        {
            var vertices = MockExtensions.CreateRandomVertices(label, count, propertyNames);
            
            ConfigureFunctionResponse($@"g\.V\(\)\.hasLabel\('{label}'\)", (q, p) => vertices);
        }

        /// <summary>
        /// Mock edges between vertex labels
        /// </summary>
        public void MockEdges(string label, string fromLabel, string toLabel, int count = 1)
        {
            var fromIds = Enumerable.Range(1, count).Select(i => $"{fromLabel}_{i}");
            var toIds = Enumerable.Range(1, count).Select(i => $"{toLabel}_{i}");
            var edges = MockExtensions.CreateRandomEdges(label, fromIds, toIds, count);

            ConfigureFunctionResponse($@"g\.E\(\)\.hasLabel\('{label}'\)", (q, p) => edges);
        }

        /// <summary>
        /// Mock a specific vertex by ID
        /// </summary>
        public void MockSpecificVertex(string id, string label, KeyValuePair<string, object>[] properties)
        {
            var vertex = MockExtensions.CreateVertexResponse(id, label, properties?.ToDictionary(p => p.Key, p => p.Value));
            
            ConfigureFunctionResponse($@"g\.V\('{Regex.Escape(id)}'\)", (q, p) => new[] { vertex });
        }

        /// <summary>
        /// Mock a parameterized query
        /// </summary>
        public void MockParameterizedQuery(string pattern, string paramName, object paramValue)
        {
            ConfigureFunctionResponse(pattern, (q, p) =>
            {
                var hasParam = p?.Any(kvp => kvp.Key.Contains(paramName) || kvp.Value?.Equals(paramValue) == true) == true;
                if (hasParam)
                {
                    return new[] { MockExtensions.CreateVertexResponse("1", "result", new Dictionary<string, object> { { paramName, paramValue } }) };
                }
                return new List<dynamic>();
            });
        }

        #endregion

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query, Dictionary<string, object> parametrizedValues)
        {
            // Simulate consumed RU
            ConsumedRU += _options.SimulatedRUPerQuery;

            // Log the query if enabled
            if (_options.LogQueries)
            {
                LogQuery($"Mock Gremlin Query: {query}");
                if (parametrizedValues?.Any() == true)
                {
                    LogQuery($"Parameters: {JsonConvert.SerializeObject(parametrizedValues, Formatting.Indented)}");
                }
            }

            // Check for configured responses first
            foreach (var kvp in _queryResponses)
            {
                if (Regex.IsMatch(query, kvp.Key, RegexOptions.IgnoreCase))
                {
                    var response = await kvp.Value.GetResponseAsync(query, parametrizedValues);
                    return response;
                }
            }

            // If no configured response, try to simulate the operation
            return await SimulateGraphOperation(query, parametrizedValues);
        }

        private async Task<IEnumerable<dynamic>> SimulateGraphOperation(string query, Dictionary<string, object> parametrizedValues)
        {
            var operation = DetectOperationType(query);
            
            switch (operation)
            {
                case OperationType.AddVertex:
                    return await _graphStore.AddVertex(query, parametrizedValues);
                
                case OperationType.AddEdge:
                    return await _graphStore.AddEdge(query, parametrizedValues);
                
                case OperationType.Update:
                    return await _graphStore.UpdateElements(query, parametrizedValues);
                
                case OperationType.Delete:
                    return await _graphStore.DeleteElements(query, parametrizedValues);
                
                case OperationType.Query:
                    return await _graphStore.QueryElements(query, parametrizedValues);
                
                default:
                    // Default to empty result for unknown operations
                    return new List<dynamic>();
            }
        }

        private OperationType DetectOperationType(string query)
        {
            var lowerQuery = query.ToLowerInvariant();
            
            if (lowerQuery.Contains("addv("))
                return OperationType.AddVertex;
            
            if (lowerQuery.Contains("adde("))
                return OperationType.AddEdge;
            
            if (lowerQuery.Contains("drop()"))
                return OperationType.Delete;
            
            if (lowerQuery.Contains("property("))
                return OperationType.Update;
            
            return OperationType.Query;
        }

        /// <summary>
        /// Reset the mock connector state
        /// </summary>
        public void Reset()
        {
            _queryResponses.Clear();
            _graphStore.Clear();
            ConsumedRU = 0;
        }

        /// <summary>
        /// Get the current state of the in-memory graph store
        /// </summary>
        public InMemoryGraphStore GetGraphStore()
        {
            return _graphStore;
        }

        /// <summary>
        /// Log a message to the console if logging is enabled
        /// </summary>
        private void LogQuery(string message)
        {
            if (_options.LogQueries)
            {
                Console.WriteLine($"[MockConnector] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
            }
        }
    }

    /// <summary>
    /// Vertex definition for quick population
    /// </summary>
    public class VertexDefinition
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        public VertexDefinition(string id, string label, params KeyValuePair<string, object>[] properties)
        {
            Id = id;
            Label = label;
            Properties = properties?.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Edge definition for quick population
    /// </summary>
    public class EdgeDefinition
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string From { get; set; }
        public string To { get; set; }

        public EdgeDefinition(string id, string label, string from, string to)
        {
            Id = id;
            Label = label;
            From = from;
            To = to;
        }
    }
}
