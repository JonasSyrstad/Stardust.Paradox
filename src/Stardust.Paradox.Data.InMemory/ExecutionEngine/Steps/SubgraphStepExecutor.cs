using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the subgraph() step - extracts a subgraph from the current traversal
    /// TinkerPop spec: subgraph(sideEffectKey) aggregates edges into a subgraph
    /// </summary>
    public class SubgraphStepExecutor : StepExecutorBase
    {
        public SubgraphStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "subgraph";

        public override string StepDescription => "Extracts a subgraph from the current traversal and stores it in side effects";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                throw new ArgumentException("subgraph() step requires a side effect key argument");
            }

            var sideEffectKey = step.GetFirstStringArgument();
            if (string.IsNullOrEmpty(sideEffectKey))
            {
                throw new ArgumentException("subgraph() step requires a valid side effect key");
            }

            // Collect all edges encountered in the traversal
            var subgraphEdges = new List<dynamic>();
            var subgraphVertices = new HashSet<string>();

            foreach (var traverser in context.Traversers)
            {
                // If current element is an edge, add it to the subgraph
                if (IsEdge(traverser.Current))
                {
                    subgraphEdges.Add(traverser.Current);
                    
                    // Track vertices connected by this edge
                    var inV = GetProperty(traverser.Current, "inV") ?? GetProperty(traverser.Current, "inVLabel");
                    var outV = GetProperty(traverser.Current, "outV") ?? GetProperty(traverser.Current, "outVLabel");
                    
                    if (inV != null)
                        subgraphVertices.Add(inV.ToString());
                    if (outV != null)
                        subgraphVertices.Add(outV.ToString());
                }
                // If current element is a vertex, track it
                else if (IsVertex(traverser.Current))
                {
                    var id = GetProperty(traverser.Current, "id");
                    if (id != null)
                        subgraphVertices.Add(id.ToString());
                }
            }

            // Store the subgraph in the side effects as a list
            var subgraphList = new List<dynamic>();
            subgraphList.Add(new
            {
                edges = subgraphEdges,
                vertices = subgraphVertices.ToList(),
                edgeCount = subgraphEdges.Count,
                vertexCount = subgraphVertices.Count
            });

            context.SideEffects[sideEffectKey] = subgraphList;

            // The subgraph step is a side-effect step, so traversers pass through unchanged
        }

        private bool IsEdge(dynamic element)
        {
            if (element == null) return false;

            try
            {
                if (element is JObject jobj)
                {
                    return jobj.ContainsKey("inV") || jobj.ContainsKey("outV") || 
                           jobj.ContainsKey("inVLabel") || jobj.ContainsKey("outVLabel");
                }

                var type = element.GetType();
                var typeProperty = type.GetProperty("type") ?? type.GetProperty("Type");
                if (typeProperty != null)
                {
                    var typeValue = typeProperty.GetValue(element)?.ToString();
                    return typeValue?.ToLower() == "edge";
                }

                // Check for edge-specific properties
                var hasInV = type.GetProperty("inV") != null || type.GetProperty("InV") != null;
                var hasOutV = type.GetProperty("outV") != null || type.GetProperty("OutV") != null;
                return hasInV && hasOutV;
            }
            catch
            {
                return false;
            }
        }

        private bool IsVertex(dynamic element)
        {
            if (element == null) return false;

            try
            {
                if (element is JObject jobj)
                {
                    return jobj.ContainsKey("label") && !jobj.ContainsKey("inV") && !jobj.ContainsKey("outV");
                }

                var type = element.GetType();
                var typeProperty = type.GetProperty("type") ?? type.GetProperty("Type");
                if (typeProperty != null)
                {
                    var typeValue = typeProperty.GetValue(element)?.ToString();
                    return typeValue?.ToLower() == "vertex";
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private object GetProperty(dynamic element, string propertyName)
        {
            if (element == null) return null;

            try
            {
                if (element is JObject jobj)
                {
                    if (jobj.TryGetValue(propertyName, out var value))
                        return value;
                    return null;
                }

                var type = element.GetType();
                var prop = type.GetProperty(propertyName) ?? 
                           type.GetProperty(char.ToUpper(propertyName[0]) + propertyName.Substring(1));
                
                return prop?.GetValue(element);
            }
            catch
            {
                return null;
            }
        }
    }
}
