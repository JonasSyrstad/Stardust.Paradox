using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.InMemory.Core
{
    /// <summary>
    /// Partial class extension for debug export functionality
    /// </summary>
    public partial class InMemoryGraphDatabase
    {
        /// <summary>
        /// Export internal database structures as JSON for debugging
        /// </summary>
        public string ExportInternalStructuresAsJson()
        {
            var structures = ExportInternalStructures();

            return JsonConvert.SerializeObject(structures, Formatting.Indented);
        }

        /// <summary>
        /// Exports the internal data structures of the in-memory graph database, 
        /// including vertex and edge indices, property indices, and summaries of vertices and edges.
        /// </summary>
        /// <returns>
        /// A dictionary containing the internal structures of the graph database. 
        /// The dictionary includes details such as vertex and edge label indices, 
        /// property indices, edge and vertex connectivity indices, vertex and edge counts, 
        /// and summaries of vertices and edges.
        /// </returns>
        public Dictionary<string, object> ExportInternalStructures()
        {
            var structures = new Dictionary<string, object>();

            // Export vertex label index
            structures["vertexLabelIndex"] = _vertexLabelIndex.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            );

            // Export edge label index
            structures["edgeLabelIndex"] = _edgeLabelIndex.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            );

            // Export vertex property index
            var vertexPropertyIndex = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (var propKey in _vertexPropertyIndex.Keys)
            {
                var valueDict = new Dictionary<string, List<string>>();
                if (_vertexPropertyIndex.TryGetValue(propKey, out var valueIndex))
                {
                    foreach (var valueKey in valueIndex.Keys)
                    {
                        if (valueIndex.TryGetValue(valueKey, out var vertexIds))
                        {
                            valueDict[valueKey?.ToString() ?? "null"] = vertexIds.ToList();
                        }
                    }
                }
                vertexPropertyIndex[propKey] = valueDict;
            }
            structures["vertexPropertyIndex"] = vertexPropertyIndex;

            // Export edge property index
            var edgePropertyIndex = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (var propKey in _edgePropertyIndex.Keys)
            {
                var valueDict = new Dictionary<string, List<string>>();
                if (_edgePropertyIndex.TryGetValue(propKey, out var valueIndex))
                {
                    foreach (var valueKey in valueIndex.Keys)
                    {
                        if (valueIndex.TryGetValue(valueKey, out var edgeIds))
                        {
                            valueDict[valueKey?.ToString() ?? "null"] = edgeIds.ToList();
                        }
                    }
                }
                edgePropertyIndex[propKey] = valueDict;
            }
            structures["edgePropertyIndex"] = edgePropertyIndex;

            // Export out edge index
            structures["outEdgeIndex"] = _outEdgeIndex.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            );

            // Export in edge index
            structures["inEdgeIndex"] = _inEdgeIndex.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            );

            // Export out vertex index
            var outVertexIndex = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (var vertexId in _outVertexIndex.Keys)
            {
                var labelDict = new Dictionary<string, List<string>>();
                if (_outVertexIndex.TryGetValue(vertexId, out var labels))
                {
                    foreach (var label in labels.Keys)
                    {
                        if (labels.TryGetValue(label, out var targetIds))
                        {
                            labelDict[label] = targetIds.ToList();
                        }
                    }
                }
                outVertexIndex[vertexId] = labelDict;
            }
            structures["outVertexIndex"] = outVertexIndex;

            // Export in vertex index
            var inVertexIndex = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (var vertexId in _inVertexIndex.Keys)
            {
                var labelDict = new Dictionary<string, List<string>>();
                if (_inVertexIndex.TryGetValue(vertexId, out var labels))
                {
                    foreach (var label in labels.Keys)
                    {
                        if (labels.TryGetValue(label, out var targetIds))
                        {
                            labelDict[label] = targetIds.ToList();
                        }
                    }
                }
                inVertexIndex[vertexId] = labelDict;
            }
            structures["inVertexIndex"] = inVertexIndex;

            // Export vertex count and edge count
            structures["vertexCount"] = _vertices.Count;
            structures["edgeCount"] = _edges.Count;

            // Export vertices summary
            structures["vertices"] = _vertices.Values.Select(v => new
            {
                id = v.Id,
                label = v.Label,
                properties = v.Properties.Keys.ToList()
            }).ToList();

            // Export edges summary
            structures["edges"] = _edges.Values.Select(e => new
            {
                id = e.Id,
                label = e.Label,
                outVertexId = e.OutVertexId,
                inVertexId = e.InVertexId,
                properties = e.Properties.Keys.ToList()
            }).ToList();
            return structures;
        }
    }
}
