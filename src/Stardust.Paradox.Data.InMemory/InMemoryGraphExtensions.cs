using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Enhanced graph database implementation with additional utility methods
    /// </summary>
    public partial class InMemoryGraphDatabase
    {
        // Additional index structures for specialized use cases
        private readonly Dictionary<string, Dictionary<object, HashSet<InMemoryVertex>>> _specialVertexIndices =
            new Dictionary<string, Dictionary<object, HashSet<InMemoryVertex>>>();
        private readonly Dictionary<string, Dictionary<object, HashSet<InMemoryEdge>>> _specialEdgeIndices =
            new Dictionary<string, Dictionary<object, HashSet<InMemoryEdge>>>();

        /// <summary>
        /// Create a specialized index for a vertex property (in addition to the built-in indexing)
        /// </summary>
        public void CreateSpecialVertexIndex(string propertyKey)
        {
            if (!_specialVertexIndices.ContainsKey(propertyKey))
            {
                _specialVertexIndices[propertyKey] = new Dictionary<object, HashSet<InMemoryVertex>>();

                // Populate the index with existing vertices
                foreach (var vertex in _vertices.Values)
                {
                    if (vertex.HasProperty(propertyKey))
                    {
                        var value = vertex.GetProperty<object>(propertyKey);
                        AddToSpecialIndex(_specialVertexIndices[propertyKey], value, vertex);
                    }
                }
            }
        }

        /// <summary>
        /// Create a specialized index for an edge property (in addition to the built-in indexing)
        /// </summary>
        public void CreateSpecialEdgeIndex(string propertyKey)
        {
            if (!_specialEdgeIndices.ContainsKey(propertyKey))
            {
                _specialEdgeIndices[propertyKey] = new Dictionary<object, HashSet<InMemoryEdge>>();

                // Populate the index with existing edges
                foreach (var edge in _edges.Values)
                {
                    if (edge.HasProperty(propertyKey))
                    {
                        var value = edge.GetProperty<object>(propertyKey);
                        AddToSpecialIndex(_specialEdgeIndices[propertyKey], value, edge);
                    }
                }
            }
        }

        /// <summary>
        /// Get vertices using specialized index
        /// </summary>
        public IEnumerable<InMemoryVertex> GetVerticesBySpecialIndex(string key, object value)
        {
            if (_specialVertexIndices.TryGetValue(key, out var index) &&
                index.TryGetValue(value, out var vertices))
            {
                return vertices;
            }
            return Enumerable.Empty<InMemoryVertex>();
        }

        /// <summary>
        /// Get edges using specialized index
        /// </summary>
        public IEnumerable<InMemoryEdge> GetEdgesBySpecialIndex(string key, object value)
        {
            if (_specialEdgeIndices.TryGetValue(key, out var index) &&
                index.TryGetValue(value, out var edges))
            {
                return edges;
            }
            return Enumerable.Empty<InMemoryEdge>();
        }

        private void AddToSpecialIndex<T>(Dictionary<object, HashSet<T>> index, object value, T item)
        {
            if (!index.TryGetValue(value, out var set))
            {
                set = new HashSet<T>();
                index[value] = set;
            }
            set.Add(item);
        }

        private void RemoveFromSpecialIndex<T>(Dictionary<object, HashSet<T>> index, object value, T item)
        {
            if (index.TryGetValue(value, out var set))
            {
                set.Remove(item);
                if (set.Count == 0)
                {
                    index.Remove(value);
                }
            }
        }

        /// <summary>
        /// Get available specialized vertex indices
        /// </summary>
        public IEnumerable<string> GetSpecialVertexIndices()
        {
            return _specialVertexIndices.Keys;
        }

        /// <summary>
        /// Get available specialized edge indices
        /// </summary>
        public IEnumerable<string> GetSpecialEdgeIndices()
        {
            return _specialEdgeIndices.Keys;
        }

        /// <summary>
        /// Get comprehensive index statistics including specialized indices
        /// </summary>
        public Dictionary<string, object> GetComprehensiveIndexStatistics()
        {
            var stats = new Dictionary<string, object>
            {
                ["specialVertexIndices"] = _specialVertexIndices.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Values.Sum(set => set.Count)
                ),
                ["specialEdgeIndices"] = _specialEdgeIndices.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Values.Sum(set => set.Count)
                )
            };

            // Add built-in index stats
            var (vertexCount, edgeCount, builtInStats) = GetStatistics();
            foreach (var kvp in builtInStats)
            {
                stats[kvp.Key] = kvp.Value;
            }

            stats["totalVertices"] = vertexCount;
            stats["totalEdges"] = edgeCount;

            return stats;
        }

        /// <summary>
        /// Import data with enhanced processing
        /// </summary>
        public void ImportData(IEnumerable<InMemoryVertex> vertices, IEnumerable<InMemoryEdge> edges)
        {
            if (vertices != null)
            {
                foreach (var vertex in vertices)
                {
                    _vertices.TryAdd(vertex.Id, vertex);
                    UpdateVertexLabelIndex(vertex.Id, vertex.Label);
                    foreach (var prop in vertex.Properties)
                    {
                        UpdateVertexPropertyIndex(vertex.Id, prop.Key, prop.Value);
                    }
                }
            }

            if (edges != null)
            {
                foreach (var edge in edges)
                {
                    _edges.TryAdd(edge.Id, edge);
                    UpdateEdgeLabelIndex(edge.Id, edge.Label);
                    UpdateAdjacencyIndices(edge);
                    foreach (var prop in edge.Properties)
                    {
                        UpdateEdgePropertyIndex(edge.Id, prop.Key, prop.Value);
                    }
                }
            }
        }

    }
}