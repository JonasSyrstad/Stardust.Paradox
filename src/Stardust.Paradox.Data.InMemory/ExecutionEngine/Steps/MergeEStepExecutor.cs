using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the mergeE() step which provides upsert-like functionality for edges.
    /// 
    /// TinkerPop Reference: https://tinkerpop.apache.org/docs/current/dev/provider/#_mergeE
    /// 
    /// Behavior:
    /// - mergeE(map): Searches for an edge matching the map criteria
    /// - If found: returns the existing edge
    /// - If not found: creates a new edge with the map properties
    /// </summary>
    [UsedImplicitly]
    public class MergeEStepExecutor : StepExecutorBase
    {
        public MergeEStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "mergee";

        public override string StepDescription =>
            "Provides upsert functionality for edges. " +
            "Searches for an edge matching the criteria in the provided map. " +
            "If found, returns it; if not, creates a new edge with those properties.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var matchMap = ExtractMatchMap(step, context);
            
            // Try to find existing edge matching the criteria
            var existingEdge = FindMatchingEdge(matchMap);
            
            InMemoryEdge edge;
            
            if (existingEdge != null)
            {
                edge = existingEdge;
            }
            else
            {
                // Create new edge
                edge = CreateEdge(matchMap, context);
                
                if (edge == null)
                {
                    // Could not create edge (missing from/to vertices)
                    context.Traversers = new List<Traverser>();
                    return;
                }
            }
            
            // Replace traversers with the merged edge
            var newTraversers = new List<Traverser>();
            
            if (context.Traversers.Any())
            {
                foreach (var existingTraverser in context.Traversers)
                {
                    var newTraverser = existingTraverser.Split();
                    newTraverser.Value = edge.ToGremlinResponse();
                    newTraverser.AddToPath(edge.ToGremlinResponse());
                    newTraversers.Add(newTraverser);
                }
            }
            else
            {
                var traverser = new Traverser(edge.ToGremlinResponse());
                traverser.AddToPath(edge.ToGremlinResponse());
                newTraversers.Add(traverser);
            }
            
            context.Traversers = newTraversers;
        }

        private Dictionary<string, object> ExtractMatchMap(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var matchMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            if (step.Arguments == null || !step.Arguments.Any())
            {
                return matchMap;
            }
            
            var arg = step.Arguments.First();
            
            if (arg is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    matchMap[kvp.Key] = kvp.Value;
                }
            }
            else if (arg is string mapStr)
            {
                matchMap = ParseMapString(mapStr, context);
            }
            
            return matchMap;
        }

        private Dictionary<string, object> ParseMapString(string mapStr, TinkerTraversalContext context)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            mapStr = mapStr.Trim();
            if (mapStr.StartsWith("[") && mapStr.EndsWith("]"))
            {
                mapStr = mapStr.Substring(1, mapStr.Length - 2);
            }
            
            if (string.IsNullOrWhiteSpace(mapStr) || mapStr == ":")
            {
                return result;
            }
            
            var parts = SplitMapParts(mapStr);
            
            foreach (var part in parts)
            {
                var colonIndex = part.IndexOf(':');
                if (colonIndex <= 0) continue;
                
                var key = NormalizeKey(part.Substring(0, colonIndex).Trim());
                var value = ParseValue(part.Substring(colonIndex + 1).Trim(), context);
                
                result[key] = value;
            }
            
            return result;
        }

        private List<string> SplitMapParts(string mapStr)
        {
            var parts = new List<string>();
            var current = "";
            var depth = 0;
            var inString = false;
            var stringChar = '\0';
            
            foreach (var c in mapStr)
            {
                if (!inString && (c == '\'' || c == '"'))
                {
                    inString = true;
                    stringChar = c;
                    current += c;
                }
                else if (inString && c == stringChar)
                {
                    inString = false;
                    current += c;
                }
                else if (!inString && (c == '[' || c == '(' || c == '{'))
                {
                    depth++;
                    current += c;
                }
                else if (!inString && (c == ']' || c == ')' || c == '}'))
                {
                    depth--;
                    current += c;
                }
                else if (!inString && c == ',' && depth == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        parts.Add(current.Trim());
                    }
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(current))
            {
                parts.Add(current.Trim());
            }
            
            return parts;
        }

        private string NormalizeKey(string key)
        {
            key = key.Trim().TrimStart('(').TrimEnd(')');

            if (key.EndsWith(".from", StringComparison.OrdinalIgnoreCase))
            {
                return "from";
            }

            if (key.EndsWith(".to", StringComparison.OrdinalIgnoreCase))
            {
                return "to";
            }
            
            if (key.Equals("T.label", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("label", StringComparison.OrdinalIgnoreCase))
            {
                return "label";
            }
            
            if (key.Equals("T.id", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return "id";
            }
            
            if (key.Equals("Direction.from", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("from", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Direction.OUT", StringComparison.OrdinalIgnoreCase))
            {
                return "from";
            }
            
            if (key.Equals("Direction.to", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("to", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Direction.IN", StringComparison.OrdinalIgnoreCase))
            {
                return "to";
            }
            
            return key.Trim('\'', '"');
        }

        private object ParseValue(string value, TinkerTraversalContext context)
        {
            value = value.Trim();
            
            if ((value.StartsWith("'") && value.EndsWith("'")) ||
                (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                return value.Substring(1, value.Length - 2);
            }
            
            if (int.TryParse(value, out var intVal))
            {
                return intVal;
            }
            
            if (double.TryParse(value, out var doubleVal))
            {
                return doubleVal;
            }
            
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            
            if (value.StartsWith("__p") && context.Variables != null && 
                context.Variables.TryGetValue(value, out var paramValue))
            {
                return paramValue;
            }
            
            return value;
        }

        private InMemoryEdge FindMatchingEdge(Dictionary<string, object> matchMap)
        {
            if (!matchMap.Any())
            {
                return Database.GetAllEdges().FirstOrDefault();
            }
            
            IEnumerable<InMemoryEdge> candidates = Database.GetAllEdges();
            
            // Filter by label if specified
            if (matchMap.TryGetValue("label", out var labelObj))
            {
                var label = labelObj?.ToString();
                if (!string.IsNullOrEmpty(label))
                {
                    candidates = candidates.Where(e => e.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            // Filter by id if specified
            if (matchMap.TryGetValue("id", out var idObj))
            {
                var id = idObj?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    candidates = candidates.Where(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            // Filter by from vertex
            if (matchMap.TryGetValue("from", out var fromObj))
            {
                var fromId = fromObj?.ToString();
                if (!string.IsNullOrEmpty(fromId))
                {
                    candidates = candidates.Where(e => 
                        e.OutVertexId != null && e.OutVertexId.Equals(fromId, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            // Filter by to vertex
            if (matchMap.TryGetValue("to", out var toObj))
            {
                var toId = toObj?.ToString();
                if (!string.IsNullOrEmpty(toId))
                {
                    candidates = candidates.Where(e => 
                        e.InVertexId != null && e.InVertexId.Equals(toId, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            // Filter by other properties
            foreach (var kvp in matchMap)
            {
                if (kvp.Key.Equals("label", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("from", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("to", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                var propKey = kvp.Key;
                var propVal = kvp.Value;
                candidates = candidates.Where(e =>
                {
                    object edgePropValue = null;
                    e.Properties.TryGetValue(propKey, out edgePropValue);
                    if (edgePropValue == null && propVal == null) return true;
                    if (edgePropValue == null || propVal == null) return false;
                    return edgePropValue.ToString().Equals(propVal.ToString(), StringComparison.OrdinalIgnoreCase);
                });
            }
            
            return candidates.FirstOrDefault();
        }

        private InMemoryEdge CreateEdge(Dictionary<string, object> matchMap, TinkerTraversalContext context)
        {
            var label = "edge";
            if (matchMap.TryGetValue("label", out var labelObj))
            {
                label = labelObj?.ToString() ?? "edge";
            }
            
            // Get from/to vertices
            string fromId = null;
            string toId = null;
            
            if (matchMap.TryGetValue("from", out var fromObj))
            {
                fromId = ExtractId(fromObj);
            }

            if (string.IsNullOrEmpty(fromId) && matchMap.TryGetValue("Direction.from", out var dirFromObj))
            {
                fromId = ExtractId(dirFromObj);
            }
            
            if (matchMap.TryGetValue("to", out var toObj))
            {
                toId = ExtractId(toObj);
            }

            if (string.IsNullOrEmpty(toId) && matchMap.TryGetValue("Direction.to", out var dirToObj))
            {
                toId = ExtractId(dirToObj);
            }
            
            // If we don't have from/to, try to get from current traverser
            if (string.IsNullOrEmpty(fromId) && context.Traversers.Any())
            {
                var currentValue = context.Traversers.First().Value;
                fromId = ExtractId(currentValue);
            }
            
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
            {
                // Fallback for cases where Direction keys are not normalized as expected.
                // If the map provides exactly two candidate vertex references besides label/id,
                // treat the first as from and the second as to.
                var candidates = matchMap
                    .Where(kvp => !kvp.Key.Equals("label", StringComparison.OrdinalIgnoreCase) &&
                                  !kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (candidates.Count >= 2)
                {
                    if (string.IsNullOrEmpty(fromId))
                    {
                        fromId = ExtractId(candidates[0].Value);
                    }

                    if (string.IsNullOrEmpty(toId))
                    {
                        toId = ExtractId(candidates[1].Value);
                    }
                }

                if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
                {
                    return null;
                }
            }
            
            string id = null;
            if (matchMap.TryGetValue("id", out var idObj))
            {
                id = idObj?.ToString();
            }
            
            // Create edge using the database API
            var edge = Database.AddEdge(label, fromId, toId, id);
            
            if (edge == null)
            {
                return null;
            }
            
            // Set properties
            foreach (var kvp in matchMap)
            {
                if (!kvp.Key.Equals("label", StringComparison.OrdinalIgnoreCase) &&
                    !kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase) &&
                    !kvp.Key.Equals("from", StringComparison.OrdinalIgnoreCase) &&
                    !kvp.Key.Equals("to", StringComparison.OrdinalIgnoreCase))
                {
                    edge.SetProperty(kvp.Key, kvp.Value);
                }
            }
            
            return edge;
        }
    }
}
