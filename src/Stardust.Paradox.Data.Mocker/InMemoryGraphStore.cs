using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// In-memory graph store for simulating graph operations
    /// </summary>
    public class InMemoryGraphStore
    {
        private readonly Dictionary<string, MockVertex> _vertices;
        private readonly Dictionary<string, MockEdge> _edges;
        private int _nextVertexId = 1;
        private int _nextEdgeId = 1;

        public InMemoryGraphStore()
        {
            _vertices = new Dictionary<string, MockVertex>();
            _edges = new Dictionary<string, MockEdge>();
        }

        public async Task<IEnumerable<dynamic>> AddVertex(string query, Dictionary<string, object> parameters)
        {
            var vertex = new MockVertex
            {
                Id = _nextVertexId++.ToString(),
                Label = ExtractVertexLabel(query, parameters),
                Properties = new Dictionary<string, object>()
            };

            _vertices[vertex.Id] = vertex;

            // Return the created vertex in Gremlin format
            var result = new List<dynamic> { CreateVertexResult(vertex) };
            return await Task.FromResult(result);
        }

        public async Task<IEnumerable<dynamic>> AddEdge(string query, Dictionary<string, object> parameters)
        {
            var edge = new MockEdge
            {
                Id = _nextEdgeId++.ToString(),
                Label = ExtractEdgeLabel(query, parameters),
                Properties = new Dictionary<string, object>(),
                OutVertexId = ExtractFromVertex(query, parameters),
                InVertexId = ExtractToVertex(query, parameters)
            };

            _edges[edge.Id] = edge;

            // Return the created edge in Gremlin format
            var result = new List<dynamic> { CreateEdgeResult(edge) };
            return await Task.FromResult(result);
        }

        public async Task<IEnumerable<dynamic>> UpdateElements(string query, Dictionary<string, object> parameters)
        {
            var results = new List<dynamic>();

            // Simple property update simulation
            if (query.Contains("property("))
            {
                var propertyUpdates = ExtractPropertyUpdates(query, parameters);
                
                // Apply to all vertices that match (simplified)
                foreach (var vertex in _vertices.Values)
                {
                    foreach (var prop in propertyUpdates)
                    {
                        vertex.Properties[prop.Key] = prop.Value;
                    }
                    results.Add(CreateVertexResult(vertex));
                }
            }

            return await Task.FromResult(results);
        }

        public async Task<IEnumerable<dynamic>> DeleteElements(string query, Dictionary<string, object> parameters)
        {
            var results = new List<dynamic>();

            if (query.Contains("drop()"))
            {
                // Clear all elements
                _vertices.Clear();
                _edges.Clear();
            }

            return await Task.FromResult(results);
        }

        public async Task<IEnumerable<dynamic>> QueryElements(string query, Dictionary<string, object> parameters)
        {
            var results = new List<dynamic>();

            // Simple vertex query simulation
            if (query.StartsWith("g.V()", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var vertex in _vertices.Values)
                {
                    results.Add(CreateVertexResult(vertex));
                }
            }
            // Simple edge query simulation
            else if (query.StartsWith("g.E()", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var edge in _edges.Values)
                {
                    results.Add(CreateEdgeResult(edge));
                }
            }

            return await Task.FromResult(results);
        }

        /// <summary>
        /// Directly add a vertex to the store
        /// </summary>
        public void AddVertexDirect(MockVertex vertex)
        {
            _vertices[vertex.Id] = vertex;
        }

        /// <summary>
        /// Directly add an edge to the store
        /// </summary>
        public void AddEdgeDirect(MockEdge edge)
        {
            _edges[edge.Id] = edge;
        }

        public void Clear()
        {
            _vertices.Clear();
            _edges.Clear();
            _nextVertexId = 1;
            _nextEdgeId = 1;
        }

        public IReadOnlyDictionary<string, MockVertex> Vertices => _vertices;
        public IReadOnlyDictionary<string, MockEdge> Edges => _edges;

        private string ExtractVertexLabel(string query, Dictionary<string, object> parameters)
        {
            var match = Regex.Match(query, @"addV\(([^)]+)\)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var labelParam = match.Groups[1].Value;
                if (parameters?.ContainsKey(labelParam) == true)
                {
                    return parameters[labelParam]?.ToString();
                }
                return labelParam.Trim('\'', '"');
            }
            return "vertex";
        }

        private string ExtractEdgeLabel(string query, Dictionary<string, object> parameters)
        {
            var match = Regex.Match(query, @"addE\(([^)]+)\)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var labelParam = match.Groups[1].Value;
                if (parameters?.ContainsKey(labelParam) == true)
                {
                    return parameters[labelParam]?.ToString();
                }
                return labelParam.Trim('\'', '"');
            }
            return "edge";
        }

        private string ExtractFromVertex(string query, Dictionary<string, object> parameters)
        {
            // Simplified extraction - in real implementation would be more sophisticated
            return _vertices.Keys.FirstOrDefault() ?? "1";
        }

        private string ExtractToVertex(string query, Dictionary<string, object> parameters)
        {
            // Simplified extraction - in real implementation would be more sophisticated  
            return _vertices.Keys.Skip(1).FirstOrDefault() ?? "2";
        }

        private Dictionary<string, object> ExtractPropertyUpdates(string query, Dictionary<string, object> parameters)
        {
            var properties = new Dictionary<string, object>();
            
            var matches = Regex.Matches(query, @"property\(['""]([^'""]+)['""],\s*([^)]+)\)", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var propName = match.Groups[1].Value;
                var propValue = match.Groups[2].Value;
                
                if (parameters?.ContainsKey(propValue) == true)
                {
                    properties[propName] = parameters[propValue];
                }
                else
                {
                    properties[propName] = propValue.Trim('\'', '"');
                }
            }
            
            return properties;
        }

        private dynamic CreateVertexResult(MockVertex vertex)
        {
            dynamic result = new ExpandoObject();
            result.id = vertex.Id;
            result.label = vertex.Label;
            result.type = "vertex";
            result.properties = vertex.Properties;
            return result;
        }

        private dynamic CreateEdgeResult(MockEdge edge)
        {
            dynamic result = new ExpandoObject();
            result.id = edge.Id;
            result.label = edge.Label;
            result.type = "edge";
            result.inV = edge.InVertexId;
            result.outV = edge.OutVertexId;
            result.properties = edge.Properties;
            return result;
        }

        /// <summary>
        /// Mock vertex representation
        /// </summary>
        public class MockVertex
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        }

        /// <summary>
        /// Mock edge representation
        /// </summary>
        public class MockEdge
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public string OutVertexId { get; set; }
            public string InVertexId { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        }
    }
}