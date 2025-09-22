using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory.Core
{
    /// <summary>
    /// In-memory graph database implementation inspired by Apache TinkerPop's TinkerGraph
    /// Enhanced with proper indexing and TinkerGraph-compatible features
    /// </summary>
    public partial class InMemoryGraphDatabase
    {
        private readonly ConcurrentDictionary<string, InMemoryVertex> _vertices;
        private readonly ConcurrentDictionary<string, InMemoryEdge> _edges;
        private readonly Dictionary<string, Func<string, Dictionary<string, object>, IEnumerable<dynamic>>> _customResponses;
        
        // TinkerGraph-inspired indices for optimization
        private readonly ConcurrentDictionary<string, HashSet<string>> _vertexLabelIndex;
        private readonly ConcurrentDictionary<string, HashSet<string>> _edgeLabelIndex;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<string>>> _vertexPropertyIndex;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<string>>> _edgePropertyIndex;
        
        // TinkerGraph adjacency indices for fast traversal
        private readonly ConcurrentDictionary<string, HashSet<string>> _outEdgeIndex; // vertexId -> outgoing edge IDs
        private readonly ConcurrentDictionary<string, HashSet<string>> _inEdgeIndex;  // vertexId -> incoming edge IDs
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<string>>> _outVertexIndex; // vertexId -> edgeLabel -> target vertex IDs
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<string>>> _inVertexIndex;  // vertexId -> edgeLabel -> source vertex IDs
        
        private readonly object _lockObject = new object();
        private long _vertexIdCounter = 1;
        private long _edgeIdCounter = 1;

        public InMemoryGraphDatabase()
        {
            _vertices = new ConcurrentDictionary<string, InMemoryVertex>();
            _edges = new ConcurrentDictionary<string, InMemoryEdge>();
            _customResponses = new Dictionary<string, Func<string, Dictionary<string, object>, IEnumerable<dynamic>>>();
            
            // Initialize TinkerGraph-style indices
            _vertexLabelIndex = new ConcurrentDictionary<string, HashSet<string>>();
            _edgeLabelIndex = new ConcurrentDictionary<string, HashSet<string>>();
            _vertexPropertyIndex = new ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<string>>>();
            _edgePropertyIndex = new ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<string>>>();
            
            _outEdgeIndex = new ConcurrentDictionary<string, HashSet<string>>();
            _inEdgeIndex = new ConcurrentDictionary<string, HashSet<string>>();
            _outVertexIndex = new ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<string>>>();
            _inVertexIndex = new ConcurrentDictionary<string, ConcurrentDictionary<string, HashSet<string>>>();
        }

        #region Vertex Operations

        /// <summary>
        /// Add a new vertex to the graph with TinkerGraph-style indexing
        /// </summary>
        public InMemoryVertex AddVertex(string label, string id = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                lock (_lockObject)
                {
                    id = _vertexIdCounter.ToString();
                    _vertexIdCounter++;
                }
            }

            var vertex = new InMemoryVertex(id, label);
            _vertices.AddOrUpdate(id, vertex, (key, oldValue) => vertex);
            
            // Update indices
            UpdateVertexLabelIndex(id, label);
            InitializeVertexAdjacencyIndices(id);
            
            return vertex;
        }

        /// <summary>
        /// Add vertex with properties and automatic indexing
        /// </summary>
        public InMemoryVertex AddVertex(string label, Dictionary<string, object> properties, string id = null)
        {
            var vertex = AddVertex(label, id);
            
            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    vertex.SetProperty(prop.Key, prop.Value);
                    UpdateVertexPropertyIndex(vertex.Id, prop.Key, prop.Value);
                }
            }
            
            return vertex;
        }

        /// <summary>
        /// Get a vertex by ID with O(1) lookup
        /// </summary>
        public InMemoryVertex GetVertex(string id)
        {
            _vertices.TryGetValue(id, out var vertex);
            return vertex;
        }

        /// <summary>
        /// Get all vertices
        /// </summary>
        public IEnumerable<InMemoryVertex> GetAllVertices()
        {
            return _vertices.Values;
        }

        /// <summary>
        /// Get vertices by label using index for O(1) lookup
        /// </summary>
        public IEnumerable<InMemoryVertex> GetVerticesByLabel(string label)
        {
            if (_vertexLabelIndex.TryGetValue(label, out var vertexIds))
            {
                return vertexIds.Select(id => GetVertex(id)).Where(v => v != null);
            }
            return Enumerable.Empty<InMemoryVertex>();
        }

        /// <summary>
        /// Get vertices by property value using index for fast lookup
        /// </summary>
        public IEnumerable<InMemoryVertex> GetVerticesByProperty(string key, object value)
        {
            if (_vertexPropertyIndex.TryGetValue(key, out var valueIndex) &&
                valueIndex.TryGetValue(value, out var vertexIds))
            {
                return vertexIds.Select(id => GetVertex(id)).Where(v => v != null);
            }
            return Enumerable.Empty<InMemoryVertex>();
        }

        /// <summary>
        /// Get vertices by multiple property filters
        /// </summary>
        public IEnumerable<InMemoryVertex> GetVerticesByProperties(Dictionary<string, object> propertyFilters)
        {
            if (propertyFilters == null || !propertyFilters.Any())
                return GetAllVertices();

            IEnumerable<string> candidateIds = null;
            
            // Find intersection of vertex IDs for all property filters
            foreach (var filter in propertyFilters)
            {
                if (_vertexPropertyIndex.TryGetValue(filter.Key, out var valueIndex) &&
                    valueIndex.TryGetValue(filter.Value, out var vertexIds))
                {
                    candidateIds = candidateIds?.Intersect(vertexIds) ?? vertexIds;
                }
                else
                {
                    return Enumerable.Empty<InMemoryVertex>(); // No matches for this filter
                }
            }
            
            return candidateIds?.Select(id => GetVertex(id)).Where(v => v != null) ?? Enumerable.Empty<InMemoryVertex>();
        }

        /// <summary>
        /// Remove a vertex and all connected edges with proper index cleanup
        /// </summary>
        public bool RemoveVertex(string id)
        {
            var vertex = GetVertex(id);
            if (vertex == null)
                return false;

            // Remove all connected edges
            var connectedEdges = GetBothEdges(id).ToList();
            foreach (var edge in connectedEdges)
            {
                RemoveEdge(edge.Id);
            }

            // Remove from indices
            RemoveVertexFromIndices(vertex);
            
            return _vertices.TryRemove(id, out _);
        }

        #endregion

        #region Edge Operations

        /// <summary>
        /// Add a new edge to the graph with TinkerGraph-style indexing
        /// </summary>
        public InMemoryEdge AddEdge(string label, string outVertexId, string inVertexId, string id = null)
        {
            // Verify that both vertices exist
            if (!_vertices.ContainsKey(outVertexId) || !_vertices.ContainsKey(inVertexId))
            {
                return null;
            }

            if (string.IsNullOrEmpty(id))
            {
                lock (_lockObject)
                {
                    id = _edgeIdCounter.ToString();
                    _edgeIdCounter++;
                }
            }

            var edge = new InMemoryEdge(id, label, outVertexId, inVertexId);
            
            // Use AddOrUpdate to ensure edge is stored even if ID already exists
            _edges.AddOrUpdate(id, edge, (key, oldValue) => edge);
            
            // Update indices
            UpdateEdgeLabelIndex(id, label);
            UpdateAdjacencyIndices(edge);
            
            return edge;
        }

        /// <summary>
        /// Add edge with properties and automatic indexing
        /// </summary>
        public InMemoryEdge AddEdge(string label, string outVertexId, string inVertexId, Dictionary<string, object> properties, string id = null)
        {
            var edge = AddEdge(label, outVertexId, inVertexId, id);
            if (edge != null && properties != null)
            {
                foreach (var prop in properties)
                {
                    edge.SetProperty(prop.Key, prop.Value);
                    UpdateEdgePropertyIndex(edge.Id, prop.Key, prop.Value);
                }
            }
            return edge;
        }

        /// <summary>
        /// Get an edge by ID with O(1) lookup
        /// </summary>
        public InMemoryEdge GetEdge(string id)
        {
            _edges.TryGetValue(id, out var edge);
            return edge;
        }

        /// <summary>
        /// Get all edges
        /// </summary>
        public IEnumerable<InMemoryEdge> GetAllEdges()
        {
            return _edges.Values;
        }

        /// <summary>
        /// Get edges by label using index for O(1) lookup
        /// </summary>
        public IEnumerable<InMemoryEdge> GetEdgesByLabel(string label)
        {
            if (_edgeLabelIndex.TryGetValue(label, out var edgeIds))
            {
                return edgeIds.Select(id => GetEdge(id)).Where(e => e != null);
            }
            return Enumerable.Empty<InMemoryEdge>();
        }

        /// <summary>
        /// Get edges by property value using index for fast lookup
        /// </summary>
        public IEnumerable<InMemoryEdge> GetEdgesByProperty(string key, object value)
        {
            if (_edgePropertyIndex.TryGetValue(key, out var valueIndex) &&
                valueIndex.TryGetValue(value, out var edgeIds))
            {
                return edgeIds.Select(id => GetEdge(id)).Where(e => e != null);
            }
            return Enumerable.Empty<InMemoryEdge>();
        }

        /// <summary>
        /// Remove an edge with proper index cleanup
        /// </summary>
        public bool RemoveEdge(string id)
        {
            var edge = GetEdge(id);
            if (edge == null)
                return false;

            // Remove from indices
            RemoveEdgeFromIndices(edge);
            
            return _edges.TryRemove(id, out _);
        }

        #endregion

        #region High-Performance Traversal Operations

        /// <summary>
        /// Get vertices connected via outgoing edges with index-optimized lookup
        /// </summary>
        public IEnumerable<InMemoryVertex> GetOutVertices(string vertexId, string edgeLabel = null)
        {
            if (_outVertexIndex.TryGetValue(vertexId, out var edgeLabelIndex))
            {
                if (string.IsNullOrEmpty(edgeLabel))
                {
                    // Get all outgoing vertices regardless of edge label
                    var allVertexIds = edgeLabelIndex.Values.SelectMany(ids => ids);
                    return allVertexIds.Select(id => GetVertex(id)).Where(v => v != null);
                }
                else if (edgeLabelIndex.TryGetValue(edgeLabel, out var vertexIds))
                {
                    return vertexIds.Select(id => GetVertex(id)).Where(v => v != null);
                }
            }
            return Enumerable.Empty<InMemoryVertex>();
        }

        /// <summary>
        /// Get vertices connected via incoming edges with index-optimized lookup
        /// </summary>
        public IEnumerable<InMemoryVertex> GetInVertices(string vertexId, string edgeLabel = null)
        {
            if (_inVertexIndex.TryGetValue(vertexId, out var edgeLabelIndex))
            {
                if (string.IsNullOrEmpty(edgeLabel))
                {
                    // Get all incoming vertices regardless of edge label
                    var allVertexIds = edgeLabelIndex.Values.SelectMany(ids => ids);
                    return allVertexIds.Select(id => GetVertex(id)).Where(v => v != null);
                }
                else if (edgeLabelIndex.TryGetValue(edgeLabel, out var vertexIds))
                {
                    return vertexIds.Select(id => GetVertex(id)).Where(v => v != null);
                }
            }
            return Enumerable.Empty<InMemoryVertex>();
        }

        /// <summary>
        /// Get outgoing edges from a vertex with index-optimized lookup
        /// </summary>
        public IEnumerable<InMemoryEdge> GetOutEdges(string vertexId, string edgeLabel = null)
        {
            if (_outEdgeIndex.TryGetValue(vertexId, out var edgeIds))
            {
                var edges = edgeIds.Select(id => GetEdge(id)).Where(e => e != null);
                
                if (!string.IsNullOrEmpty(edgeLabel))
                {
                    edges = edges.Where(e => e.Label.Equals(edgeLabel, StringComparison.OrdinalIgnoreCase));
                }
                
                return edges;
            }
            return Enumerable.Empty<InMemoryEdge>();
        }

        /// <summary>
        /// Get incoming edges to a vertex with index-optimized lookup
        /// </summary>
        public IEnumerable<InMemoryEdge> GetInEdges(string vertexId, string edgeLabel = null)
        {
            if (_inEdgeIndex.TryGetValue(vertexId, out var edgeIds))
            {
                var edges = edgeIds.Select(id => GetEdge(id)).Where(e => e != null);
                
                if (!string.IsNullOrEmpty(edgeLabel))
                {
                    edges = edges.Where(e => e.Label.Equals(edgeLabel, StringComparison.OrdinalIgnoreCase));
                }
                
                return edges;
            }
            return Enumerable.Empty<InMemoryEdge>();
        }

        /// <summary>
        /// Get both incoming and outgoing edges for a vertex
        /// </summary>
        public IEnumerable<InMemoryEdge> GetBothEdges(string vertexId, string edgeLabel = null)
        {
            return GetOutEdges(vertexId, edgeLabel).Concat(GetInEdges(vertexId, edgeLabel));
        }

        /// <summary>
        /// Get both incoming and outgoing vertices for a vertex
        /// </summary>
        public IEnumerable<InMemoryVertex> GetBothVertices(string vertexId, string edgeLabel = null)
        {
            return GetOutVertices(vertexId, edgeLabel).Concat(GetInVertices(vertexId, edgeLabel));
        }

        /// <summary>
        /// Get vertex degree (number of connected edges)
        /// </summary>
        public int GetVertexDegree(string vertexId)
        {
            var outDegree = _outEdgeIndex.TryGetValue(vertexId, out var outEdges) ? outEdges.Count : 0;
            var inDegree = _inEdgeIndex.TryGetValue(vertexId, out var inEdges) ? inEdges.Count : 0;
            return outDegree + inDegree;
        }

        /// <summary>
        /// Get vertex out-degree
        /// </summary>
        public int GetVertexOutDegree(string vertexId)
        {
            return _outEdgeIndex.TryGetValue(vertexId, out var outEdges) ? outEdges.Count : 0;
        }

        /// <summary>
        /// Get vertex in-degree
        /// </summary>
        public int GetVertexInDegree(string vertexId)
        {
            return _inEdgeIndex.TryGetValue(vertexId, out var inEdges) ? inEdges.Count : 0;
        }

        #endregion

        #region Index Management

        private void UpdateVertexLabelIndex(string vertexId, string label)
        {
            _vertexLabelIndex.AddOrUpdate(label, 
                new HashSet<string> { vertexId },
                (key, existing) => { existing.Add(vertexId); return existing; });
        }

        private void UpdateEdgeLabelIndex(string edgeId, string label)
        {
            _edgeLabelIndex.AddOrUpdate(label,
                new HashSet<string> { edgeId },
                (key, existing) => { existing.Add(edgeId); return existing; });
        }

        private void UpdateVertexPropertyIndex(string vertexId, string propertyKey, object propertyValue)
        {
            _vertexPropertyIndex.AddOrUpdate(propertyKey,
                new ConcurrentDictionary<object, HashSet<string>>(),
                (key, existing) => existing);
                
            _vertexPropertyIndex[propertyKey].AddOrUpdate(propertyValue,
                new HashSet<string> { vertexId },
                (key, existing) => { existing.Add(vertexId); return existing; });
        }

        private void UpdateEdgePropertyIndex(string edgeId, string propertyKey, object propertyValue)
        {
            _edgePropertyIndex.AddOrUpdate(propertyKey,
                new ConcurrentDictionary<object, HashSet<string>>(),
                (key, existing) => existing);
                
            _edgePropertyIndex[propertyKey].AddOrUpdate(propertyValue,
                new HashSet<string> { edgeId },
                (key, existing) => { existing.Add(edgeId); return existing; });
        }

        private void InitializeVertexAdjacencyIndices(string vertexId)
        {
            _outEdgeIndex.TryAdd(vertexId, new HashSet<string>());
            _inEdgeIndex.TryAdd(vertexId, new HashSet<string>());
            _outVertexIndex.TryAdd(vertexId, new ConcurrentDictionary<string, HashSet<string>>());
            _inVertexIndex.TryAdd(vertexId, new ConcurrentDictionary<string, HashSet<string>>());
        }

        private void UpdateAdjacencyIndices(InMemoryEdge edge)
        {
            // Ensure vertices have initialized adjacency indices
            InitializeVertexAdjacencyIndices(edge.OutVertexId);
            InitializeVertexAdjacencyIndices(edge.InVertexId);
            
            // Update edge indices
            _outEdgeIndex.AddOrUpdate(edge.OutVertexId,
                new HashSet<string> { edge.Id },
                (key, existing) => { existing.Add(edge.Id); return existing; });
                
            _inEdgeIndex.AddOrUpdate(edge.InVertexId,
                new HashSet<string> { edge.Id },
                (key, existing) => { existing.Add(edge.Id); return existing; });

            // Update vertex adjacency indices
            _outVertexIndex.AddOrUpdate(edge.OutVertexId,
                new ConcurrentDictionary<string, HashSet<string>>(),
                (key, existing) => existing);
                
            _outVertexIndex[edge.OutVertexId].AddOrUpdate(edge.Label,
                new HashSet<string> { edge.InVertexId },
                (key, existing) => { existing.Add(edge.InVertexId); return existing; });

            _inVertexIndex.AddOrUpdate(edge.InVertexId,
                new ConcurrentDictionary<string, HashSet<string>>(),
                (key, existing) => existing);
                
            _inVertexIndex[edge.InVertexId].AddOrUpdate(edge.Label,
                new HashSet<string> { edge.OutVertexId },
                (key, existing) => { existing.Add(edge.OutVertexId); return existing; });
        }

        private void RemoveVertexFromIndices(InMemoryVertex vertex)
        {
            // Remove from label index
            if (_vertexLabelIndex.TryGetValue(vertex.Label, out var labelSet))
            {
                labelSet.Remove(vertex.Id);
                if (!labelSet.Any())
                {
                    _vertexLabelIndex.TryRemove(vertex.Label, out _);
                }
            }

            // Remove from property indices
            foreach (var prop in vertex.Properties)
            {
                if (_vertexPropertyIndex.TryGetValue(prop.Key, out var valueIndex) &&
                    valueIndex.TryGetValue(prop.Value, out var vertexIds))
                {
                    vertexIds.Remove(vertex.Id);
                    if (!vertexIds.Any())
                    {
                        valueIndex.TryRemove(prop.Value, out _);
                    }
                }
            }

            // Remove adjacency indices
            _outEdgeIndex.TryRemove(vertex.Id, out _);
            _inEdgeIndex.TryRemove(vertex.Id, out _);
            _outVertexIndex.TryRemove(vertex.Id, out _);
            _inVertexIndex.TryRemove(vertex.Id, out _);
        }

        private void RemoveEdgeFromIndices(InMemoryEdge edge)
        {
            // Remove from label index
            if (_edgeLabelIndex.TryGetValue(edge.Label, out var labelSet))
            {
                labelSet.Remove(edge.Id);
                if (!labelSet.Any())
                {
                    _edgeLabelIndex.TryRemove(edge.Label, out _);
                }
            }

            // Remove from property indices
            foreach (var prop in edge.Properties)
            {
                if (_edgePropertyIndex.TryGetValue(prop.Key, out var valueIndex) &&
                    valueIndex.TryGetValue(prop.Value, out var edgeIds))
                {
                    edgeIds.Remove(edge.Id);
                    if (!edgeIds.Any())
                    {
                        valueIndex.TryRemove(prop.Value, out _);
                    }
                }
            }

            // Remove from adjacency indices
            if (_outEdgeIndex.TryGetValue(edge.OutVertexId, out var outEdges))
            {
                outEdges.Remove(edge.Id);
            }

            if (_inEdgeIndex.TryGetValue(edge.InVertexId, out var inEdges))
            {
                inEdges.Remove(edge.Id);
            }

            // Remove from vertex adjacency indices
            if (_outVertexIndex.TryGetValue(edge.OutVertexId, out var outVertexLabels) &&
                outVertexLabels.TryGetValue(edge.Label, out var outVertices))
            {
                outVertices.Remove(edge.InVertexId);
                if (!outVertices.Any())
                {
                    outVertexLabels.TryRemove(edge.Label, out _);
                }
            }

            if (_inVertexIndex.TryGetValue(edge.InVertexId, out var inVertexLabels) &&
                inVertexLabels.TryGetValue(edge.Label, out var inVertices))
            {
                inVertices.Remove(edge.OutVertexId);
                if (!inVertices.Any())
                {
                    inVertexLabels.TryRemove(edge.Label, out _);
                }
            }
        }

        #endregion

        #region Custom Response Handling

        /// <summary>
        /// Register a custom response for complex queries
        /// </summary>
        public void RegisterCustomResponse(string queryPattern, Func<string, Dictionary<string, object>, IEnumerable<dynamic>> responseFunc)
        {
            _customResponses[queryPattern] = responseFunc;
        }

        /// <summary>
        /// Get custom response for a query if available
        /// </summary>
        public IEnumerable<dynamic> GetCustomResponse(string query)
        {
            return GetCustomResponse(query, new Dictionary<string, object>());
        }

        /// <summary>
        /// Get custom response for a query with parameters if available
        /// </summary>
        public IEnumerable<dynamic> GetCustomResponse(string query, Dictionary<string, object> parameters)
        {
            foreach (var pattern in _customResponses.Keys)
            {
                if (Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase))
                {
                    try
                    {
                        return _customResponses[pattern](query, parameters ?? new Dictionary<string, object>());
                    }
                    catch (Exception)
                    {
                        // If custom response fails, return null to allow fallback to normal parsing
                        return null;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Utility Operations

        /// <summary>
        /// Clear all data from the database and indices
        /// </summary>
        public void Clear()
        {
            _vertices.Clear();
            _edges.Clear();
            _customResponses.Clear();
            
            // Clear all indices
            _vertexLabelIndex.Clear();
            _edgeLabelIndex.Clear();
            _vertexPropertyIndex.Clear();
            _edgePropertyIndex.Clear();
            _outEdgeIndex.Clear();
            _inEdgeIndex.Clear();
            _outVertexIndex.Clear();
            _inVertexIndex.Clear();
            
            lock (_lockObject)
            {
                _vertexIdCounter = 1;
                _edgeIdCounter = 1;
            }
        }

        /// <summary>
        /// Get database statistics with index information
        /// </summary>
        public (int VertexCount, int EdgeCount, Dictionary<string, object> IndexStats) GetStatistics()
        {
            var indexStats = new Dictionary<string, object>
            {
                ["vertexLabels"] = _vertexLabelIndex.Count,
                ["edgeLabels"] = _edgeLabelIndex.Count,
                ["vertexPropertyIndices"] = _vertexPropertyIndex.Count,
                ["edgePropertyIndices"] = _edgePropertyIndex.Count,
                ["vertexLabelDistribution"] = _vertexLabelIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count),
                ["edgeLabelDistribution"] = _edgeLabelIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count)
            };
            
            return (_vertices.Count, _edges.Count, indexStats);
        }

        /// <summary>
        /// Import data from vertex and edge definitions with automatic indexing
        /// </summary>
        public void ImportData(IEnumerable<InMemoryVertexDefinition> vertexDefinitions, IEnumerable<InMemoryEdgeDefinition> edgeDefinitions)
        {
            if (vertexDefinitions != null)
            {
                foreach (var vertexDef in vertexDefinitions)
                {
                    var vertex = AddVertex(vertexDef.Label, vertexDef.Properties, vertexDef.Id);
                }
            }

            if (edgeDefinitions != null)
            {
                foreach (var edgeDef in edgeDefinitions)
                {
                    var edge = AddEdge(edgeDef.Label, edgeDef.OutVertexId, edgeDef.InVertexId, edgeDef.Properties, edgeDef.Id);
                }
            }
        }

        /// <summary>
        /// Clone the entire database including indices
        /// </summary>
        public InMemoryGraphDatabase Clone()
        {
            var clonedDb = new InMemoryGraphDatabase();
            
            // Clone vertices with properties
            foreach (var vertex in _vertices.Values)
            {
                clonedDb.AddVertex(vertex.Label, vertex.Properties.ToDictionary(p => p.Key, p => p.Value), vertex.Id);
            }
            
            // Clone edges with properties
            foreach (var edge in _edges.Values)
            {
                clonedDb.AddEdge(edge.Label, edge.OutVertexId, edge.InVertexId, edge.Properties.ToDictionary(p => p.Key, p => p.Value), edge.Id);
            }
            
            // Clone custom responses
            foreach (var customResponse in _customResponses)
            {
                clonedDb._customResponses[customResponse.Key] = customResponse.Value;
            }
            
            return clonedDb;
        }

        /// <summary>
        /// Export all data from the database for inspection
        /// </summary>
        public (IEnumerable<InMemoryVertex> Vertices, IEnumerable<InMemoryEdge> Edges) ExportData()
        {
            return (_vertices.Values.ToList(), _edges.Values.ToList());
        }

        /// <summary>
        /// Validate database integrity and edge persistence
        /// </summary>
        public Dictionary<string, object> ValidateIntegrity()
        {
            var issues = new List<string>();
            var edgeCount = 0;
            var orphanedEdges = 0;
            
            foreach (var edge in _edges.Values)
            {
                edgeCount++;
                
                // Check if referenced vertices exist
                if (!_vertices.ContainsKey(edge.OutVertexId))
                {
                    issues.Add($"Edge {edge.Id} references non-existent outVertex {edge.OutVertexId}");
                    orphanedEdges++;
                }
                
                if (!_vertices.ContainsKey(edge.InVertexId))
                {
                    issues.Add($"Edge {edge.Id} references non-existent inVertex {edge.InVertexId}");
                    orphanedEdges++;
                }
                
                // Check if edge is in adjacency indices
                if (!_outEdgeIndex.ContainsKey(edge.OutVertexId) || 
                    !_outEdgeIndex[edge.OutVertexId].Contains(edge.Id))
                {
                    issues.Add($"Edge {edge.Id} missing from outEdgeIndex for vertex {edge.OutVertexId}");
                }
                
                if (!_inEdgeIndex.ContainsKey(edge.InVertexId) || 
                    !_inEdgeIndex[edge.InVertexId].Contains(edge.Id))
                {
                    issues.Add($"Edge {edge.Id} missing from inEdgeIndex for vertex {edge.InVertexId}");
                }
            }
            
            return new Dictionary<string, object>
            {
                ["totalEdges"] = edgeCount,
                ["orphanedEdges"] = orphanedEdges,
                ["integrityIssues"] = issues,
                ["isValid"] = issues.Count == 0
            };
        }

        #endregion
    }
}
