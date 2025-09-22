using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Extensions
{
    /// <summary>
    /// Extension methods for InMemoryGremlinLanguageConnector
    /// </summary>
    public static class InMemoryGremlinExtensions
    {
        /// <summary>
        /// Populate the database with sample data for testing
        /// </summary>
        public static InMemoryGremlinLanguageConnector WithSampleData(this InMemoryGremlinLanguageConnector connector)
        {
            // Add sample vertices
            var person1 = connector.AddVertex("person", "person1");
            person1.Properties["name"] = "John Doe";
            person1.Properties["age"] = 30;
            person1.Properties["email"] = "john.doe@example.com";

            var person2 = connector.AddVertex("person", "person2");
            person2.Properties["name"] = "Jane Smith";
            person2.Properties["age"] = 28;
            person2.Properties["email"] = "jane.smith@example.com";

            var company = connector.AddVertex("company", "company1");
            company.Properties["name"] = "Tech Corp";
            company.Properties["founded"] = 2010;

            // Add sample edges
            var employment1 = connector.AddEdge("works_for", "person1", "company1");
            employment1.Properties["position"] = "Software Engineer";
            employment1.Properties["startDate"] = DateTime.Now.AddYears(-2);

            var employment2 = connector.AddEdge("works_for", "person2", "company1");
            employment2.Properties["position"] = "Product Manager";
            employment2.Properties["startDate"] = DateTime.Now.AddYears(-1);

            var friendship = connector.AddEdge("knows", "person1", "person2");
            friendship.Properties["since"] = DateTime.Now.AddYears(-5);

            return connector;
        }

        /// <summary>
        /// Configure common query responses for testing
        /// </summary>
        public static InMemoryGremlinLanguageConnector WithCommonResponses(this InMemoryGremlinLanguageConnector connector)
        {
            // Register response for complex aggregation queries
            connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)\.values\('age'\)\.sum\(\)", 
                (query, parameters) => new dynamic[] { 58L }); // Sum of ages in sample data

            // Register response for grouping queries
            connector.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)\.group\(\)\.by\('position'\)", 
                (query, parameters) => new dynamic[]
                {
                    new Dictionary<string, object>
                    {
                        { "Software Engineer", new[] { "person1" } },
                        { "Product Manager", new[] { "person2" } }
                    }
                });

            // Register response for path queries
            connector.RegisterCustomResponse(@"g\.V\('person1'\)\.out\('works_for'\)\.path\(\)", 
                (query, parameters) => new dynamic[]
                {
                    new[] { "person1", "company1" }
                });

            return connector;
        }

        /// <summary>
        /// Load data from a simple data structure
        /// </summary>
        public static InMemoryGremlinLanguageConnector LoadData(this InMemoryGremlinLanguageConnector connector, 
            IEnumerable<(string id, string label, Dictionary<string, object> properties)> vertices,
            IEnumerable<(string id, string label, string outV, string inV, Dictionary<string, object> properties)> edges = null)
        {
            // Load vertices
            foreach (var (id, label, properties) in vertices)
            {
                var vertex = connector.AddVertex(label, id);
                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        vertex.Properties[prop.Key] = prop.Value;
                    }
                }
            }

            // Load edges
            if (edges != null)
            {
                foreach (var (id, label, outV, inV, properties) in edges)
                {
                    var edge = connector.AddEdge(label, outV, inV, id);
                    if (edge != null && properties != null)
                    {
                        foreach (var prop in properties)
                        {
                            edge.Properties[prop.Key] = prop.Value;
                        }
                    }
                }
            }

            return connector;
        }

        /// <summary>
        /// Configure the connector for high-performance scenarios
        /// </summary>
        public static InMemoryGremlinLanguageConnector WithPerformanceSettings(this InMemoryGremlinLanguageConnector connector)
        {
            // Register optimized responses for common bulk operations
            connector.RegisterCustomResponse(@"g\.V\(\)\.count\(\)", 
                (query, parameters) => new dynamic[] { (long)connector.GetAllVertices().Count() });

            connector.RegisterCustomResponse(@"g\.E\(\)\.count\(\)", 
                (query, parameters) => new dynamic[] { (long)connector.GetAllEdges().Count() });

            return connector;
        }

        /// <summary>
        /// Add bulk data for performance testing
        /// </summary>
        public static InMemoryGremlinLanguageConnector WithBulkData(this InMemoryGremlinLanguageConnector connector, 
            int vertexCount = 1000, int edgeCount = 2000)
        {
            var random = new Random();
            var vertexIds = new List<string>();

            // Add vertices
            for (int i = 0; i < vertexCount; i++)
            {
                var id = $"vertex_{i}";
                vertexIds.Add(id);
                
                var vertex = connector.AddVertex("node", id);
                vertex.Properties["name"] = $"Node {i}";
                vertex.Properties["value"] = random.Next(1, 1000);
                vertex.Properties["category"] = random.Next(1, 10).ToString();
            }

            // Add edges
            for (int i = 0; i < edgeCount; i++)
            {
                var outV = vertexIds[random.Next(vertexIds.Count)];
                var inV = vertexIds[random.Next(vertexIds.Count)];
                
                if (outV != inV) // Avoid self-loops
                {
                    var edge = connector.AddEdge("connected", outV, inV);
                    if (edge != null)
                    {
                        edge.Properties["weight"] = random.NextDouble();
                        edge.Properties["created"] = DateTime.Now.AddDays(-random.Next(365));
                    }
                }
            }

            return connector;
        }

        /// <summary>
        /// Export data to a simple format for debugging
        /// </summary>
        public static string ToDebugString(this InMemoryGremlinLanguageConnector connector)
        {
            var (vertices, edges) = connector.ExportData();
            var result = new List<string>();
            
            result.Add("=== VERTICES ===");
            foreach (var vertex in vertices)
            {
                var props = string.Join(", ", vertex.Properties.Select(p => $"{p.Key}={p.Value}"));
                result.Add($"{vertex.Id} [{vertex.Label}] {props}");
            }
            
            result.Add("\n=== EDGES ===");
            foreach (var edge in edges)
            {
                var props = string.Join(", ", edge.Properties.Select(p => $"{p.Key}={p.Value}"));
                result.Add($"{edge.Id} [{edge.Label}] {edge.OutVertexId} -> {edge.InVertexId} {props}");
            }
            
            return string.Join("\n", result);
        }

        /// <summary>
        /// Check if the database is empty
        /// </summary>
        public static bool IsEmpty(this InMemoryGremlinLanguageConnector connector)
        {
            var (vertexCount, edgeCount) = connector.GetStatistics();
            return vertexCount == 0 && edgeCount == 0;
        }

        /// <summary>
        /// Get a summary of the database contents
        /// </summary>
        public static string GetSummary(this InMemoryGremlinLanguageConnector connector)
        {
            var (vertexCount, edgeCount) = connector.GetStatistics();
            var vertices = connector.GetAllVertices();
            var edges = connector.GetAllEdges();
            
            var vertexLabels = vertices.GroupBy(v => v.Label).ToDictionary(g => g.Key, g => g.Count());
            var edgeLabels = edges.GroupBy(e => e.Label).ToDictionary(g => g.Key, g => g.Count());
            
            var summary = new List<string>
            {
                $"Database Summary:",
                $"  Total Vertices: {vertexCount}",
                $"  Total Edges: {edgeCount}",
                $"  Consumed RU: {connector.ConsumedRU:F2}"
            };
            
            if (vertexLabels.Any())
            {
                summary.Add("  Vertex Labels:");
                foreach (var label in vertexLabels)
                {
                    summary.Add($"    {label.Key}: {label.Value}");
                }
            }
            
            if (edgeLabels.Any())
            {
                summary.Add("  Edge Labels:");
                foreach (var label in edgeLabels)
                {
                    summary.Add($"    {label.Key}: {label.Value}");
                }
            }
            
            return string.Join("\n", summary);
        }
    }
}
