//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace Stardust.Paradox.Data.InMemory.Core
//{
//    /// <summary>
//    /// In-memory vertex implementation inspired by Apache TinkerPop's TinkerGraph
//    /// </summary>
//    public class InMemoryVertex
//    {
//        public string Id { get; }
//        public string Label { get; }
//        public Dictionary<string, object> Properties { get; }

//        public InMemoryVertex(string id, string label)
//        {
//            Id = id ?? throw new ArgumentNullException(nameof(id));
//            Label = label ?? throw new ArgumentNullException(nameof(label));
//            Properties = new Dictionary<string, object>();
//        }

//        /// <summary>
//        /// Convert to Gremlin response format compatible with Paradox framework
//        /// </summary>
//        public dynamic ToGremlinResponse()
//        {
//            var response = new Dictionary<string, object>
//            {
//                ["id"] = Id,
//                ["label"] = Label,
//                ["type"] = "vertex",
//                ["properties"] = Properties.ToDictionary(
//                    kvp => kvp.Key,
//                    kvp => new { value = kvp.Value }
//                )
//            };

//            return response;
//        }

//        /// <summary>
//        /// Add or update a property
//        /// </summary>
//        public void SetProperty(string key, object value)
//        {
//            if (string.IsNullOrEmpty(key))
//                throw new ArgumentException("Property key cannot be null or empty", nameof(key));

//            Properties[key] = value;
//        }

//        /// <summary>
//        /// Get a property value
//        /// </summary>
//        public T GetProperty<T>(string key, T defaultValue = default)
//        {
//            if (Properties.TryGetValue(key, out var value))
//            {
//                if (value is T typedValue)
//                    return typedValue;
                
//                try
//                {
//                    return (T)Convert.ChangeType(value, typeof(T));
//                }
//                catch
//                {
//                    return defaultValue;
//                }
//            }
//            return defaultValue;
//        }

//        /// <summary>
//        /// Check if vertex has a property
//        /// </summary>
//        public bool HasProperty(string key)
//        {
//            return Properties.ContainsKey(key);
//        }

//        /// <summary>
//        /// Remove a property
//        /// </summary>
//        public bool RemoveProperty(string key)
//        {
//            return Properties.Remove(key);
//        }

//        public override string ToString()
//        {
//            return $"v[{Id}]";
//        }

//        public override bool Equals(object obj)
//        {
//            if (obj is InMemoryVertex other)
//            {
//                return Id.Equals(other.Id);
//            }
//            return false;
//        }

//        public override int GetHashCode()
//        {
//            return Id.GetHashCode();
//        }
//    }

//    /// <summary>
//    /// In-memory edge implementation inspired by Apache TinkerPop's TinkerGraph
//    /// </summary>
//    public class InMemoryEdge
//    {
//        public string Id { get; }
//        public string Label { get; }
//        public string OutVertexId { get; }
//        public string InVertexId { get; }
//        public Dictionary<string, object> Properties { get; }

//        public InMemoryEdge(string id, string label, string outVertexId, string inVertexId)
//        {
//            Id = id ?? throw new ArgumentNullException(nameof(id));
//            Label = label ?? throw new ArgumentNullException(nameof(label));
//            OutVertexId = outVertexId ?? throw new ArgumentNullException(nameof(outVertexId));
//            InVertexId = inVertexId ?? throw new ArgumentNullException(nameof(inVertexId));
//            Properties = new Dictionary<string, object>();
//        }

//        /// <summary>
//        /// Convert to Gremlin response format compatible with Paradox framework
//        /// </summary>
//        public dynamic ToGremlinResponse()
//        {
//            var response = new Dictionary<string, object>
//            {
//                ["id"] = Id,
//                ["label"] = Label,
//                ["type"] = "edge",
//                ["inV"] = InVertexId,
//                ["outV"] = OutVertexId,
//                ["inVLabel"] = "vertex", // Could be enhanced to track actual vertex labels
//                ["outVLabel"] = "vertex",
//                ["properties"] = Properties.ToDictionary(
//                    kvp => kvp.Key,
//                    kvp => kvp.Value
//                )
//            };

//            return response;
//        }

//        /// <summary>
//        /// Add or update a property
//        /// </summary>
//        public void SetProperty(string key, object value)
//        {
//            if (string.IsNullOrEmpty(key))
//                throw new ArgumentException("Property key cannot be null or empty", nameof(key));

//            Properties[key] = value;
//        }

//        /// <summary>
//        /// Get a property value
//        /// </summary>
//        public T GetProperty<T>(string key, T defaultValue = default)
//        {
//            if (Properties.TryGetValue(key, out var value))
//            {
//                if (value is T typedValue)
//                    return typedValue;
                
//                try
//                {
//                    return (T)Convert.ChangeType(value, typeof(T));
//                }
//                catch
//                {
//                    return defaultValue;
//                }
//            }
//            return defaultValue;
//        }

//        /// <summary>
//        /// Check if edge has a property
//        /// </summary>
//        public bool HasProperty(string key)
//        {
//            return Properties.ContainsKey(key);
//        }

//        /// <summary>
//        /// Remove a property
//        /// </summary>
//        public bool RemoveProperty(string key)
//        {
//            return Properties.Remove(key);
//        }

//        /// <summary>
//        /// Get the "other" vertex ID from the perspective of a given vertex
//        /// </summary>
//        public string GetOtherVertex(string fromVertexId)
//        {
//            if (fromVertexId == OutVertexId)
//                return InVertexId;
//            else if (fromVertexId == InVertexId)
//                return OutVertexId;
//            else
//                throw new ArgumentException($"Vertex {fromVertexId} is not connected to this edge", nameof(fromVertexId));
//        }

//        public override string ToString()
//        {
//            return $"e[{Id}][{OutVertexId}-{Label}->{InVertexId}]";
//        }

//        public override bool Equals(object obj)
//        {
//            if (obj is InMemoryEdge other)
//            {
//                return Id.Equals(other.Id);
//            }
//            return false;
//        }

//        public override int GetHashCode()
//        {
//            return Id.GetHashCode();
//        }
//    }
//}
