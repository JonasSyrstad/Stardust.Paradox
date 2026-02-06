using Stardust.Paradox.Data.Annotations.Annotations;
using Stardust.Paradox.Data.InMemory.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the mergeV() step which provides upsert-like functionality for vertices.
    /// 
    /// TinkerPop Reference: https://tinkerpop.apache.org/docs/current/dev/provider/#_mergeV
    /// 
    /// Behavior:
    /// - mergeV(map): Searches for a vertex matching the map criteria
    /// - If found: returns the existing vertex (and optionally applies onMatch)
    /// - If not found: creates a new vertex with the map properties (and optionally applies onCreate)
    /// </summary>
    [UsedImplicitly]
    public class MergeVStepExecutor : StepExecutorBase
    {
        public MergeVStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "mergev";

        public override string StepDescription =>
            "Provides upsert functionality for vertices. " +
            "Searches for a vertex matching the criteria in the provided map. " +
            "If found, returns it; if not, creates a new vertex with those properties.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var matchMap = ExtractMatchMap(step, context);
            
            // Try to find existing vertex matching the criteria
            var existingVertex = FindMatchingVertex(matchMap);
            
            InMemoryVertex vertex;
            
            if (existingVertex != null)
            {
                vertex = existingVertex;
            }
            else
            {
                // Create new vertex
                vertex = CreateVertex(matchMap);
            }
            
            // Replace traversers with the merged vertex
            var newTraversers = new List<Traverser>();
            
            if (context.Traversers.Any())
            {
                foreach (var existingTraverser in context.Traversers)
                {
                    var newTraverser = existingTraverser.Split();
                    newTraverser.Value = vertex.ToGremlinResponse();
                    newTraverser.AddToPath(vertex.ToGremlinResponse());
                    newTraversers.Add(newTraverser);
                }
            }
            else
            {
                var traverser = new Traverser(vertex.ToGremlinResponse());
                traverser.AddToPath(vertex.ToGremlinResponse());
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
            
            // Handle dictionary argument
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
            
            // Handle parameter references from Variables
            if (value.StartsWith("__p") && context.Variables != null && 
                context.Variables.TryGetValue(value, out var paramValue))
            {
                return paramValue;
            }
            
            return value;
        }

        private InMemoryVertex FindMatchingVertex(Dictionary<string, object> matchMap)
        {
            if (!matchMap.Any())
            {
                return Database.GetAllVertices().FirstOrDefault();
            }
            
            IEnumerable<InMemoryVertex> candidates = Database.GetAllVertices();
            
            // Filter by label if specified
            if (matchMap.TryGetValue("label", out var labelObj))
            {
                var label = labelObj?.ToString();
                if (!string.IsNullOrEmpty(label))
                {
                    candidates = candidates.Where(v => v.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            // Filter by id if specified
            if (matchMap.TryGetValue("id", out var idObj))
            {
                var id = idObj?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    candidates = candidates.Where(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                }
            }
            
            // Filter by other properties
            foreach (var kvp in matchMap)
            {
                if (kvp.Key.Equals("label", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                var propKey = kvp.Key;
                var propVal = kvp.Value;
                candidates = candidates.Where(v =>
                {
                    object vertexPropValue = null;
                    v.Properties.TryGetValue(propKey, out vertexPropValue);
                    if (vertexPropValue == null && propVal == null) return true;
                    if (vertexPropValue == null || propVal == null) return false;
                    return vertexPropValue.ToString().Equals(propVal.ToString(), StringComparison.OrdinalIgnoreCase);
                });
            }
            
            return candidates.FirstOrDefault();
        }

        private InMemoryVertex CreateVertex(Dictionary<string, object> matchMap)
        {
            var label = "vertex";
            if (matchMap.TryGetValue("label", out var labelObj))
            {
                label = labelObj?.ToString() ?? "vertex";
            }
            
            string id = null;
            if (matchMap.TryGetValue("id", out var idObj))
            {
                id = idObj?.ToString();
            }
            
            var vertex = Database.AddVertex(label, id);
            
            foreach (var kvp in matchMap)
            {
                if (!kvp.Key.Equals("label", StringComparison.OrdinalIgnoreCase) &&
                    !kvp.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    vertex.SetProperty(kvp.Key, kvp.Value);
                }
            }
            
            return vertex;
        }
    }
}
