using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine
{
    /// <summary>
    /// Simple Gremlin query parser for basic operations
    /// </summary>
    public class GremlinQueryParser
    {
        private readonly InMemoryGraphDatabase _database;

        public GremlinQueryParser(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Parse and execute a Gremlin query
        /// </summary>
        public async Task<IEnumerable<dynamic>> ParseAndExecuteAsync(string query, Dictionary<string, object> parameters)
        {
            await Task.Delay(1); // Simulate async operation

            // Handle parameterized queries
            var processedQuery = SubstituteParameters(query, parameters);

            // Check for custom responses first (before parameter substitution to allow pattern matching)
            var customResponse = _database.GetCustomResponse(query, parameters);
            if (customResponse != null)
            {
                return customResponse;
            }

            // Enhanced inject pattern handling (critical for TinkerPop compatibility)
            if (IsInjectQuery(processedQuery))
            {
                return ExecuteInjectQuery(processedQuery);
            }

            // Basic query patterns - order matters for proper matching
            if (IsAddVertexQuery(processedQuery))
            {
                return ExecuteAddVertex(processedQuery);
            }
            else if (IsAddEdgeQuery(processedQuery))
            {
                return ExecuteAddEdge(processedQuery);
            }
            else if (IsPropertyUpdateQuery(processedQuery))
            {
                return ExecutePropertyUpdate(processedQuery);
            }
            else if (IsDropQuery(processedQuery))
            {
                return ExecuteDrop(processedQuery);
            }
            else if (IsUpdateQuery(processedQuery))
            {
                return ExecuteUpdate(processedQuery);
            }
            else if (IsAggregationQuery(processedQuery))
            {
                return ExecuteAggregation(processedQuery);
            }
            else if (IsValueQuery(processedQuery))
            {
                return ExecuteValueQuery(processedQuery);
            }
            else if (IsLimitingQuery(processedQuery))
            {
                return ExecuteLimitingQuery(processedQuery);
            }
            else if (IsComplexTraversalQuery(processedQuery))
            {
                return ExecuteComplexTraversal(processedQuery);
            }
            else if (IsTraversalQuery(processedQuery))
            {
                return ExecuteTraversalQuery(processedQuery);
            }
            else if (IsVertexQuery(processedQuery))
            {
                return ExecuteVertexQuery(processedQuery);
            }
            else if (IsEdgeQuery(processedQuery))
            {
                return ExecuteEdgeQuery(processedQuery);
            }
            else
            {
                // For complex queries, check if there's a custom response registered (with processed query)
                var fallbackCustomResponse = _database.GetCustomResponse(processedQuery, parameters);
                if (fallbackCustomResponse != null)
                {
                    return fallbackCustomResponse;
                }

                // Default fallback for unrecognized queries
                return new List<dynamic>();
            }
        }

        private string SubstituteParameters(string query, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return query;

            var result = query;
            foreach (var param in parameters)
            {
                var value = FormatParameterValue(param.Value);
                result = result.Replace(param.Key, value);
            }
            return result;
        }

        private string FormatParameterValue(object value)
        {
            if (value == null) return "null";
            if (value is string) return $"'{value}'";
            if (value is bool) return value.ToString().ToLower();
            if (value is double d) return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (value is float f) return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (value is decimal dec) return dec.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return value.ToString();
        }

        private object ParseValue(string valueStr)
        {
            if (string.IsNullOrEmpty(valueStr) || valueStr == "null")
                return null;

            // Remove quotes if present (both single and double quotes)
            if ((valueStr.StartsWith("'") && valueStr.EndsWith("'")) || 
                (valueStr.StartsWith("\"") && valueStr.EndsWith("\"")))
            {
                valueStr = valueStr.Substring(1, valueStr.Length - 2);
            }

            // Try boolean first (before numeric parsing)
            if (bool.TryParse(valueStr, out bool boolVal))
            {
                return boolVal;
            }

            // Handle numbers - be smart about int vs double
            // If the string contains a decimal point, try double first
            if (valueStr.Contains("."))
            {
                if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                {
                    return doubleVal;
                }
            }
            else
            {
                // No decimal point, try integer first
                if (int.TryParse(valueStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int intVal))
                {
                    return intVal;
                }
                
                // Fallback to double for large numbers
                if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                {
                    return doubleVal;
                }
            }
                
            // Try DateTime
            if (DateTime.TryParse(valueStr, out DateTime dateVal))
            {
                return dateVal;
            }
            
            return valueStr;
        }

        #region Query Type Detection

        private bool IsAddVertexQuery(string query)
        {
            return Regex.IsMatch(query, @"g\.addV\s*\(", RegexOptions.IgnoreCase);
        }

        private bool IsAddEdgeQuery(string query)
        {
            return Regex.IsMatch(query, @"\.addE\s*\(", RegexOptions.IgnoreCase);
        }

        private bool IsDropQuery(string query)
        {
            return Regex.IsMatch(query, @"\.drop\s*\(\s*\)", RegexOptions.IgnoreCase);
        }

        private bool IsUpdateQuery(string query)
        {
            return Regex.IsMatch(query, @"\.property\s*\(", RegexOptions.IgnoreCase) && 
                   !IsAddVertexQuery(query) && !IsAddEdgeQuery(query);
        }

        private bool IsPropertyUpdateQuery(string query)
        {
            // An update query is considered a property update if it contains .property() after a vertex/edge descriptor
            return Regex.IsMatch(query, @"(g\.V|g\.E)\s*\(\s*['""][^'""]+['""]\s*\)\.property\s*\(", RegexOptions.IgnoreCase);
        }

        private bool IsAggregationQuery(string query)
        {
            return Regex.IsMatch(query, @"\.(count|sum|max|min|mean|fold|unfold)\s*\(\s*\)", RegexOptions.IgnoreCase);
        }

        private bool IsVertexQuery(string query)
        {
            return Regex.IsMatch(query, @"g\.V\s*\(", RegexOptions.IgnoreCase) && 
                   !IsTraversalQuery(query) && !IsAddEdgeQuery(query);
        }

        private bool IsEdgeQuery(string query)
        {
            return Regex.IsMatch(query, @"g\.E\s*\(", RegexOptions.IgnoreCase);
        }

        private bool IsTraversalQuery(string query)
        {
            return Regex.IsMatch(query, @"\.(out|in|both|outE|inE|bothE|inV|outV|bothV)\s*\(", RegexOptions.IgnoreCase);
        }

        private bool IsComplexTraversalQuery(string query)
        {
            // Multi-step traversals like g.V(id).out().in().hasLabel()
            return Regex.IsMatch(query, @"g\.V\s*\([^)]+\)\.(out|in|both)\s*\([^)]*\)\.(out|in|both|hasLabel|has)", RegexOptions.IgnoreCase);
        }

        private bool IsLimitingQuery(string query)
        {
            return Regex.IsMatch(query, @"\.(limit|skip|range|order|sample|tail)\s*\(", RegexOptions.IgnoreCase);
        }

        private bool IsValueQuery(string query)
        {
            return Regex.IsMatch(query, @"\.(values|valueMap|elementMap)\s*\(", RegexOptions.IgnoreCase);
        }

        #endregion

        #region Query Execution

        private IEnumerable<dynamic> ExecuteAddVertex(string query)
        {
            var labelMatch = Regex.Match(query, @"g\.addV\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            var label = labelMatch.Success ? labelMatch.Groups[1].Value : "vertex";

            // Extract properties first to see if there's an 'id' property
            var propertyMatches = Regex.Matches(query, @"\.property\s*\(\s*['""]([^'""]+)['""],\s*([^)]+)\)", RegexOptions.IgnoreCase);
            
            string vertexId = null;
            var properties = new Dictionary<string, object>();
            
            foreach (Match match in propertyMatches)
            {
                var key = match.Groups[1].Value;
                var valueStr = match.Groups[2].Value.Trim();
                
                // Parse value without stripping quotes since the regex now captures the raw value
                object value = ParseValue(valueStr);
                
                // If this is an 'id' property, use it as the vertex ID
                if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    vertexId = value?.ToString();
                }
                
                // Always store the property as well (for consistency with Gremlin behavior)
                properties[key] = value;
            }

            // Create vertex with the specified ID (if any)
            var vertex = _database.AddVertex(label, vertexId);

            // Set remaining properties
            foreach (var prop in properties)
            {
                vertex.Properties[prop.Key] = prop.Value;
            }

            return new[] { vertex.ToGremlinResponse() };
        }

        private IEnumerable<dynamic> ExecuteAddEdge(string query)
        {
            var edgeLabelMatch = Regex.Match(query, @"\.addE\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            var edgeLabel = edgeLabelMatch.Success ? edgeLabelMatch.Groups[1].Value : "edge";

            // Extract source vertex (the vertex before .addE)
            var sourceMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.addE", RegexOptions.IgnoreCase);
            if (!sourceMatch.Success)
            {
                return new List<dynamic>();
            }

            var sourceId = sourceMatch.Groups[1].Value;

            // Extract target vertex (.to()) - handle nested g.V() pattern
            var targetMatch = Regex.Match(query, @"\.to\s*\(\s*g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\s*\)", RegexOptions.IgnoreCase);
            if (!targetMatch.Success)
            {
                return new List<dynamic>();
            }

            var targetId = targetMatch.Groups[1].Value;

            // Verify both vertices exist
            var sourceVertex = _database.GetVertex(sourceId);
            var targetVertex = _database.GetVertex(targetId);
            
            if (sourceVertex == null || targetVertex == null)
            {
                return new List<dynamic>();
            }

            var edge = _database.AddEdge(edgeLabel, sourceId, targetId);
            if (edge == null)
            {
                return new List<dynamic>();
            }

            // Extract properties from edge (if any)
            var propertyMatches = Regex.Matches(query, @"\.property\s*\(\s*['""]([^'""]+)['""],\s*([^)]+)\)", RegexOptions.IgnoreCase);
            foreach (Match match in propertyMatches)
            {
                var key = match.Groups[1].Value;
                var valueStr = match.Groups[2].Value.Trim();
                
                edge.Properties[key] = ParseValue(valueStr);
            }

            return new[] { edge.ToGremlinResponse() };
        }

        private IEnumerable<dynamic> ExecuteDrop(string query)
        {
            // Extract vertex/edge ID from query
            var vertexMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.drop", RegexOptions.IgnoreCase);
            if (vertexMatch.Success)
            {
                var vertexId = vertexMatch.Groups[1].Value;
                _database.RemoveVertex(vertexId);
                return new List<dynamic>();
            }

            var edgeMatch = Regex.Match(query, @"g\.E\s*\(\s*['""]([^'""]+)['""]?\s*\)\.drop", RegexOptions.IgnoreCase);
            if (edgeMatch.Success)
            {
                var edgeId = edgeMatch.Groups[1].Value;
                _database.RemoveEdge(edgeId);
                return new List<dynamic>();
            }

            return new List<dynamic>();
        }

        private IEnumerable<dynamic> ExecuteUpdate(string query)
        {
            // Simple property update - extract vertex ID and properties
            var vertexMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (!vertexMatch.Success)
            {
                return new List<dynamic>();
            }

            var vertexId = vertexMatch.Groups[1].Value;
            var vertex = _database.GetVertex(vertexId);
            if (vertex == null)
            {
                return new List<dynamic>();
            }

            // Extract properties
            var propertyMatches = Regex.Matches(query, @"\.property\s*\(\s*['""]([^'""]+)['""],\s*([^)]+)\)", RegexOptions.IgnoreCase);
            foreach (Match match in propertyMatches)
            {
                var key = match.Groups[1].Value;
                var valueStr = match.Groups[2].Value.Trim();
                
                vertex.Properties[key] = ParseValue(valueStr);
            }

            return new[] { vertex.ToGremlinResponse() };
        }

        private IEnumerable<dynamic> ExecutePropertyUpdate(string query)
        {
            // Property updates are executed as upserts - if the vertex/edge exists, properties are updated, otherwise, it's a no-op
            var vertexMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (vertexMatch.Success)
            {
                var vertexId = vertexMatch.Groups[1].Value;
                var vertex = _database.GetVertex(vertexId);
                if (vertex != null)
                {
                    // Extract properties
                    var propertyMatches = Regex.Matches(query, @"\.property\s*\(\s*['""]([^'""]+)['""],\s*([^)]+)\)", RegexOptions.IgnoreCase);
                    foreach (Match match in propertyMatches)
                    {
                        var key = match.Groups[1].Value;
                        var valueStr = match.Groups[2].Value.Trim();
                        
                        vertex.Properties[key] = ParseValue(valueStr);
                    }

                    return new[] { vertex.ToGremlinResponse() };
                }
            }

            var edgeMatch = Regex.Match(query, @"g\.E\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (edgeMatch.Success)
            {
                var edgeId = edgeMatch.Groups[1].Value;
                var edge = _database.GetEdge(edgeId);
                if (edge != null)
                {
                    // Extract properties from edge (if any)
                    var propertyMatches = Regex.Matches(query, @"\.property\s*\(\s*['""]([^'""]+)['""],\s*([^)]+)\)", RegexOptions.IgnoreCase);
                    foreach (Match match in propertyMatches)
                    {
                        var key = match.Groups[1].Value;
                        var valueStr = match.Groups[2].Value.Trim();
                        
                        edge.Properties[key] = ParseValue(valueStr);
                    }

                    return new[] { edge.ToGremlinResponse() };
                }
            }

            return new List<dynamic>();
        }

        private IEnumerable<dynamic> ExecuteAggregation(string query)
        {
            // Handle values().fold() aggregation
            var valuesFoldMatch = Regex.Match(query, @"(.+?)\.values\s*\(\s*['""]([^'""]+)['""]?\s*\)\.fold\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (valuesFoldMatch.Success)
            {
                var baseQuery = valuesFoldMatch.Groups[1].Value;
                var propertyName = valuesFoldMatch.Groups[2].Value;
                
                // Get base vertices/edges
                IEnumerable<dynamic> baseResults = null;
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                
                if (baseResults != null)
                {
                    var values = new List<dynamic>();
                    foreach (var item in baseResults)
                    {
                        if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                        {
                            try
                            {
                                dynamic dynamicProps = props;
                                var value = GetPropertyValue(dynamicProps, propertyName);
                                if (value != null)
                                {
                                    values.Add(value);
                                }
                            }
                            catch
                            {
                                // Ignore access errors
                            }
                        }
                    }
                    // Return a single list containing all values
                    return new dynamic[] { values };
                }
                else
                {
                    // Return empty list if no base results
                    return new dynamic[] { new List<dynamic>() };
                }
            }

            // Handle values().sum() aggregation
            var valuesSumMatch = Regex.Match(query, @"(.+?)\.values\s*\(\s*['""]([^'""]+)['""]?\s*\)\.sum\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (valuesSumMatch.Success)
            {
                var baseQuery = valuesSumMatch.Groups[1].Value;
                var propertyName = valuesSumMatch.Groups[2].Value;
                
                // Get base vertices/edges
                IEnumerable<dynamic> baseResults = null;
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                
                if (baseResults != null)
                {
                    var sum = 0.0;
                    foreach (var item in baseResults)
                    {
                        if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                        {
                            try
                            {
                                dynamic dynamicProps = props;
                                var value = GetPropertyValue(dynamicProps, propertyName);
                                if (value != null)
                                {
                                    if (double.TryParse(value.ToString(), out double numValue))
                                    {
                                        sum += numValue;
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore access errors
                            }
                        }
                    }
                    return new dynamic[] { sum };
                }
            }

            // Handle values().mean() aggregation
            var valuesMeanMatch = Regex.Match(query, @"(.+?)\.values\s*\(\s*['""]([^'""]+)['""]?\s*\)\.mean\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (valuesMeanMatch.Success)
            {
                var baseQuery = valuesMeanMatch.Groups[1].Value;
                var propertyName = valuesMeanMatch.Groups[2].Value;
                
                // Get base vertices/edges
                IEnumerable<dynamic> baseResults = null;
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                
                if (baseResults != null)
                {
                    var sum = 0.0;
                    var count = 0;
                    foreach (var item in baseResults)
                    {
                        if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                        {
                            try
                            {
                                dynamic dynamicProps = props;
                                var value = GetPropertyValue(dynamicProps, propertyName);
                                if (value != null)
                                {
                                    if (double.TryParse(value.ToString(), out double numValue))
                                    {
                                        sum += numValue;
                                        count++;
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore access errors
                            }
                        }
                    }
                    
                    if (count == 0)
                    {
                        return new List<dynamic>();
                    }
                    
                    return new dynamic[] { sum / count };
                }
            }

            // Handle values().max() aggregation
            var valuesMaxMatch = Regex.Match(query, @"(.+?)\.values\s*\(\s*['""]([^'""]+)['""]?\s*\)\.max\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (valuesMaxMatch.Success)
            {
                var baseQuery = valuesMaxMatch.Groups[1].Value;
                var propertyName = valuesMaxMatch.Groups[2].Value;
                
                // Get base vertices/edges
                IEnumerable<dynamic> baseResults = null;
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                
                if (baseResults != null)
                {
                    double? max = null;
                    foreach (var item in baseResults)
                    {
                        if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                        {
                            try
                            {
                                dynamic dynamicProps = props;
                                var value = GetPropertyValue(dynamicProps, propertyName);
                                if (value != null)
                                {
                                    if (double.TryParse(value.ToString(), out double numValue))
                                    {
                                        if (!max.HasValue || numValue > max.Value)
                                        {
                                            max = numValue;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore access errors
                            }
                        }
                    }
                    
                    if (!max.HasValue)
                    {
                        return new List<dynamic>();
                    }
                    
                    return new dynamic[] { max.Value };
                }
            }

            // Handle values().min() aggregation
            var valuesMinMatch = Regex.Match(query, @"(.+?)\.values\s*\(\s*['""]([^'""]+)['""]?\s*\)\.min\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (valuesMinMatch.Success)
            {
                var baseQuery = valuesMinMatch.Groups[1].Value;
                var propertyName = valuesMinMatch.Groups[2].Value;
                
                // Get base vertices/edges
                IEnumerable<dynamic> baseResults = null;
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                
                if (baseResults != null)
                {
                    double? min = null;
                    foreach (var item in baseResults)
                    {
                        if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                        {
                            try
                            {
                                dynamic dynamicProps = props;
                                var value = GetPropertyValue(dynamicProps, propertyName);
                                if (value != null)
                                {
                                    if (double.TryParse(value.ToString(), out double numValue))
                                    {
                                        if (!min.HasValue || numValue < min.Value)
                                        {
                                            min = numValue;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore access errors
                            }
                        }
                    }
                    
                    if (!min.HasValue)
                    {
                        return new List<dynamic>();
                    }
                    
                    return new dynamic[] { min.Value };
                }
            }

            // Handle basic count queries
            if (Regex.IsMatch(query, @"g\.V\s*\(\s*\)\.count", RegexOptions.IgnoreCase))
            {
                return new dynamic[] { (long)_database.GetAllVertices().Count() };
            }
            else if (Regex.IsMatch(query, @"g\.E\s*\(\s*\)\.count", RegexOptions.IgnoreCase))
            {
                return new dynamic[] { (long)_database.GetAllEdges().Count() };
            }
            
            // Handle count with filtering
            var countFilterMatch = Regex.Match(query, @"(.+?)\.count\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (countFilterMatch.Success)
            {
                var baseQuery = countFilterMatch.Groups[1].Value;
                
                // Get base vertices/edges
                IEnumerable<dynamic> baseResults = null;
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                else if (IsEdgeQuery(baseQuery))
                {
                    baseResults = ExecuteEdgeQuery(baseQuery);
                }
                
                if (baseResults != null)
                {
                    return new dynamic[] { (long)baseResults.Count() };
                }
            }

            return new dynamic[] { 0L };
        }

        private IEnumerable<dynamic> ExecuteVertexQuery(string query)
        {
            // g.V() - get all vertices
            if (Regex.IsMatch(query, @"^g\.V\s*\(\s*\)$", RegexOptions.IgnoreCase))
            {
                return _database.GetAllVertices().Select(v => v.ToGremlinResponse());
            }

            // g.V(id) - get specific vertex
            var idMatch = Regex.Match(query, @"^g\.V\s*\(\s*['""]?([^'""]+)['""]?\s*\)$", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                var id = idMatch.Groups[1].Value;
                var vertex = _database.GetVertex(id);
                if (vertex != null)
                {
                    return new dynamic[] { vertex.ToGremlinResponse() };
                }
                return new List<dynamic>();
            }

            // g.V().hasLabel() with optional additional filters
            var labelMatch = Regex.Match(query, @"g\.V\s*\(\s*\)\.hasLabel\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (labelMatch.Success)
            {
                var label = labelMatch.Groups[1].Value;
                var vertices = _database.GetVerticesByLabel(label);
                
                return ApplyVertexFilters(vertices, query);
            }

            // g.V().has() patterns
            var hasDirectMatch = Regex.Match(query, @"g\.V\s*\(\s*\)\.has\s*\(\s*['""]([^'""]+)['""](?:,\s*([^)]+))?\)", RegexOptions.IgnoreCase);
            if (hasDirectMatch.Success)
            {
                var key = hasDirectMatch.Groups[1].Value;
                var vertices = _database.GetAllVertices();
                
                if (hasDirectMatch.Groups[2].Success)
                {
                    // has(key, value)
                    var valueStr = hasDirectMatch.Groups[2].Value.Trim();
                    if ((valueStr.StartsWith("'") && valueStr.EndsWith("'")) || 
                        (valueStr.StartsWith("\"") && valueStr.EndsWith("\"")))
                    {
                        valueStr = valueStr.Substring(1, valueStr.Length - 2);
                    }
                    vertices = vertices.Where(v => v.Properties.ContainsKey(key) && 
                                                  v.Properties[key]?.ToString() == valueStr);
                }
                else
                {
                    // has(key) - check if property exists
                    vertices = vertices.Where(v => v.Properties.ContainsKey(key));
                }

                return vertices.Select(v => v.ToGremlinResponse());
            }

            return new List<dynamic>();
        }

        private IEnumerable<dynamic> ApplyVertexFilters(IEnumerable<InMemoryVertex> vertices, string query)
        {
            var filteredVertices = vertices;

            // Apply has(key) filter - check for property existence
            var hasExistsMatches = Regex.Matches(query, @"\.has\s*\(\s*['""]([^'""]+)['""](?!\s*,)", RegexOptions.IgnoreCase);
            foreach (Match match in hasExistsMatches)
            {
                var key = match.Groups[1].Value;
                filteredVertices = filteredVertices.Where(v => v.Properties.ContainsKey(key));
            }

            // Apply has(key, value) filter
            var hasValueMatches = Regex.Matches(query, @"\.has\s*\(\s*['""]([^'""]+)['""],\s*([^)]+)\)", RegexOptions.IgnoreCase);
            foreach (Match match in hasValueMatches)
            {
                var key = match.Groups[1].Value;
                var valueStr = match.Groups[2].Value.Trim();
                if ((valueStr.StartsWith("'") && valueStr.EndsWith("'")) || 
                    (valueStr.StartsWith("\"") && valueStr.EndsWith("\"")))
                {
                    valueStr = valueStr.Substring(1, valueStr.Length - 2);
                }
                filteredVertices = filteredVertices.Where(v => v.Properties.ContainsKey(key) && 
                                                              v.Properties[key]?.ToString() == valueStr);
            }

            return filteredVertices.Select(v => v.ToGremlinResponse());
        }

        private IEnumerable<dynamic> ExecuteEdgeQuery(string query)
        {
            // g.E() - get all edges
            if (Regex.IsMatch(query, @"^g\.E\s*\(\s*\)$", RegexOptions.IgnoreCase))
            {
                return _database.GetAllEdges().Select(e => e.ToGremlinResponse());
            }

            // g.E(id) - get specific edge
            var idMatch = Regex.Match(query, @"^g\.E\s*\(\s*['""]([^'""]+)['""]?\s*\)$", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                var id = idMatch.Groups[1].Value;
                var edge = _database.GetEdge(id);
                if (edge != null)
                {
                    return new dynamic[] { edge.ToGremlinResponse() };
                }
                return new List<dynamic>();
            }

            // g.E().hasLabel()
            var labelMatch = Regex.Match(query, @"g\.E\s*\(\s*\)\.hasLabel\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (labelMatch.Success)
            {
                var label = labelMatch.Groups[1].Value;
                return _database.GetEdgesByLabel(label).Select(e => e.ToGremlinResponse());
            }

            return new List<dynamic>();
        }

        private IEnumerable<dynamic> ExecuteTraversalQuery(string query)
        {
            // Multi-step traversal with hasLabel: g.V(id).out(label).in(label).hasLabel(label)
            var multiStepMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.out\s*\(\s*['""]([^'""]+)['""]?\s*\)\.in\s*\(\s*['""]([^'""]+)['""]?\s*\)\.hasLabel\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (multiStepMatch.Success)
            {
                var startVertexId = multiStepMatch.Groups[1].Value;
                var outEdgeLabel = multiStepMatch.Groups[2].Value;
                var inEdgeLabel = multiStepMatch.Groups[3].Value;
                var targetLabel = multiStepMatch.Groups[4].Value;

                var intermediateVertices = _database.GetOutVertices(startVertexId, outEdgeLabel);
                var results = new List<InMemoryVertex>();

                foreach (var intermediate in intermediateVertices)
                {
                    var finalVertices = _database.GetInVertices(intermediate.Id, inEdgeLabel);
                    results.AddRange(finalVertices.Where(v => v.Label == targetLabel));
                }

                return results.Select(v => v.ToGremlinResponse());
            }

            // Complex edge-to-vertex traversals: g.V(id).outE(label).inV()
            var outEInVMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.outE\s*\(\s*['""]([^'""]+)['""]?\s*\)\.inV\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (outEInVMatch.Success)
            {
                var vertexId = outEInVMatch.Groups[1].Value;
                var edgeLabel = outEInVMatch.Groups[2].Value;
                return _database.GetOutVertices(vertexId, edgeLabel).Select(v => v.ToGremlinResponse());
            }

            // Complex edge-to-vertex traversals: g.V(id).inE(label).outV()
            var inEOutVMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.inE\s*\(\s*['""]([^'""]+)['""]?\s*\)\.outV\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (inEOutVMatch.Success)
            {
                var vertexId = inEOutVMatch.Groups[1].Value;
                var edgeLabel = inEOutVMatch.Groups[2].Value;
                return _database.GetInVertices(vertexId, edgeLabel).Select(v => v.ToGremlinResponse());
            }

            // g.E().hasLabel().bothV()
            var eBothVMatch = Regex.Match(query, @"g\.E\s*\(\s*\)\.hasLabel\s*\(\s*['""]([^'""]+)['""]?\s*\)\.bothV\s*\(\s*\)", RegexOptions.IgnoreCase);
            if (eBothVMatch.Success)
            {
                var edgeLabel = eBothVMatch.Groups[1].Value;
                var edges = _database.GetEdgesByLabel(edgeLabel);
                var vertices = new List<InMemoryVertex>();
                
                foreach (var edge in edges)
                {
                    var inVertex = _database.GetVertex(edge.InVertexId);
                    var outVertex = _database.GetVertex(edge.OutVertexId);
                    if (inVertex != null) vertices.Add(inVertex);
                    if (outVertex != null) vertices.Add(outVertex);
                }
                
                return vertices.Distinct().Select(v => v.ToGremlinResponse());
            }

            // g.V(id).outE() - get outgoing edges
            var outEMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.outE\s*\(\s*(?:['""]([^'""]+)['""]?)?\s*\)", RegexOptions.IgnoreCase);
            if (outEMatch.Success)
            {
                var vertexId = outEMatch.Groups[1].Value;
                var edgeLabel = outEMatch.Groups[2].Success ? outEMatch.Groups[2].Value : null;
                return _database.GetOutEdges(vertexId, edgeLabel).Select(e => e.ToGremlinResponse());
            }

            // g.V(id).inE() - get incoming edges
            var inEMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.inE\s*\(\s*(?:['""]([^'""]+)['""]?)?\s*\)", RegexOptions.IgnoreCase);
            if (inEMatch.Success)
            {
                var vertexId = inEMatch.Groups[1].Value;
                var edgeLabel = inEMatch.Groups[2].Success ? inEMatch.Groups[2].Value : null;
                return _database.GetInEdges(vertexId, edgeLabel).Select(e => e.ToGremlinResponse());
            }

            // g.V(id).bothE() - get edges in both directions
            var bothEMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.bothE\s*\(\s*(?:['""]([^'""]+)['""]?)?\s*\)", RegexOptions.IgnoreCase);
            if (bothEMatch.Success)
            {
                var vertexId = bothEMatch.Groups[1].Value;
                var edgeLabel = bothEMatch.Groups[2].Success ? bothEMatch.Groups[2].Value : null;
                var outEdges = _database.GetOutEdges(vertexId, edgeLabel);
                var inEdges = _database.GetInEdges(vertexId, edgeLabel);
                return outEdges.Concat(inEdges).Distinct().Select(e => e.ToGremlinResponse());
            }

            // Simple traversal patterns
            
            // g.V(id).out(label)
            var outMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.out\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (outMatch.Success)
            {
                var vertexId = outMatch.Groups[1].Value;
                var edgeLabel = outMatch.Groups[2].Value;
                return _database.GetOutVertices(vertexId, edgeLabel).Select(v => v.ToGremlinResponse());
            }

            // g.V(id).in(label)
            var inMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.in\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (inMatch.Success)
            {
                var vertexId = inMatch.Groups[1].Value;
                var edgeLabel = inMatch.Groups[2].Value;
                return _database.GetInVertices(vertexId, edgeLabel).Select(v => v.ToGremlinResponse());
            }

            // g.V(id).both(label)
            var bothMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.both\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (bothMatch.Success)
            {
                var vertexId = bothMatch.Groups[1].Value;
                var edgeLabel = bothMatch.Groups[2].Value;
                var outVertices = _database.GetOutVertices(vertexId, edgeLabel);
                var inVertices = _database.GetInVertices(vertexId, edgeLabel);
                return outVertices.Concat(inVertices).Distinct().Select(v => v.ToGremlinResponse());
            }

            return new List<dynamic>();
        }

        private IEnumerable<dynamic> ExecuteLimitingQuery(string query)
        {
            // First get the base vertices
            IEnumerable<dynamic> baseResults = null;
            
            // Extract base query (everything before the limiting operation)
            var limitMatch = Regex.Match(query, @"^(.+?)\.(limit|skip|range|order|sample|tail)", RegexOptions.IgnoreCase);
            if (limitMatch.Success)
            {
                var baseQuery = limitMatch.Groups[1].Value;
                
                // Execute base query recursively
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                else
                {
                    return new List<dynamic>();
                }
            }
            else
            {
                return new List<dynamic>();
            }

            var results = baseResults.ToList();

            // Apply limiting operations
            
            // limit(n)
            var limitPattern = Regex.Match(query, @"\.limit\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            if (limitPattern.Success)
            {
                var limitCount = int.Parse(limitPattern.Groups[1].Value);
                results = results.Take(limitCount).ToList();
            }

            // skip(n)
            var skipPattern = Regex.Match(query, @"\.skip\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            if (skipPattern.Success)
            {
                var skipCount = int.Parse(skipPattern.Groups[1].Value);
                results = results.Skip(skipCount).ToList();
            }

            // range(start, end)
            var rangePattern = Regex.Match(query, @"\.range\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            if (rangePattern.Success)
            {
                var start = int.Parse(rangePattern.Groups[1].Value);
                var end = int.Parse(rangePattern.Groups[2].Value);
                results = results.Skip(start).Take(end - start).ToList();
            }

            // tail(n)
            var tailPattern = Regex.Match(query, @"\.tail\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            if (tailPattern.Success)
            {
                var tailCount = int.Parse(tailPattern.Groups[1].Value);
                results = results.TakeLast(tailCount).ToList();
            }

            // sample(n)
            var samplePattern = Regex.Match(query, @"\.sample\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            if (samplePattern.Success)
            {
                var sampleCount = int.Parse(samplePattern.Groups[1].Value);
                var random = new Random();
                results = results.OrderBy(x => random.Next()).Take(sampleCount).ToList();
            }

            // order() - basic ordering by string representation
            if (Regex.IsMatch(query, @"\.order\s*\(\s*\)", RegexOptions.IgnoreCase))
            {
                results = results.OrderBy(r => r.ToString()).ToList();
            }

            return results;
        }

        private IEnumerable<dynamic> ExecuteValueQuery(string query)
        {
            // First get the base vertices
            IEnumerable<dynamic> baseResults = null;
            
            // Extract base query (everything before the value operation)
            var valueMatch = Regex.Match(query, @"^(.+?)\.(values|valueMap|elementMap)", RegexOptions.IgnoreCase);
            if (valueMatch.Success)
            {
                var baseQuery = valueMatch.Groups[1].Value;
                
                // Execute base query recursively
                if (IsVertexQuery(baseQuery))
                {
                    baseResults = ExecuteVertexQuery(baseQuery);
                }
                else if (IsTraversalQuery(baseQuery))
                {
                    baseResults = ExecuteTraversalQuery(baseQuery);
                }
                else
                {
                    return new List<dynamic>();
                }
            }
            else
            {
                return new List<dynamic>();
            }

            var results = baseResults.ToList();

            // Apply value operations
            
            // values() - get all property values
            if (Regex.IsMatch(query, @"\.values\s*\(\s*\)", RegexOptions.IgnoreCase))
            {
                var values = new List<dynamic>();
                foreach (var item in results)
                {
                    if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                    {
                        foreach (var propName in props.GetDynamicMemberNames())
                        {
                            dynamic dynamicProps = props;
                            try
                            {
                                var value = GetPropertyValue(dynamicProps, propName);
                                if (value != null)
                                {
                                    values.Add(value);
                                }
                            }
                            catch
                            {
                                // Ignore access errors
                            }
                        }
                    }
                }
                return values;
            }

            // values('propertyName') - get specific property values
            var valuesPropertyMatch = Regex.Match(query, @"\.values\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (valuesPropertyMatch.Success)
            {
                var propertyName = valuesPropertyMatch.Groups[1].Value;
                var values = new List<dynamic>();
                foreach (var item in results)
                {
                    if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                    {
                        try
                        {
                            dynamic dynamicProps = props;
                            var value = GetPropertyValue(dynamicProps, propertyName);
                            if (value != null)
                            {
                                values.Add(value);
                            }
                        }
                        catch
                        {
                            // Ignore access errors
                        }
                    }
                }
                return values;
            }

            // valueMap() - return property maps
            if (Regex.IsMatch(query, @"\.valueMap\s*\(\s*\)", RegexOptions.IgnoreCase))
            {
                var maps = new List<dynamic>();
                foreach (var item in results)
                {
                    if (item is GremlinResponseObject gro && gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                    {
                        var map = new Dictionary<string, object>();
                        foreach (var propName in props.GetDynamicMemberNames())
                        {
                            try
                            {
                                dynamic dynamicProps = props;
                                var value = GetPropertyValue(dynamicProps, propName);
                                if (value != null)
                                {
                                    map[propName] = value;
                                }
                            }
                            catch
                            {
                                // Ignore access errors
                            }
                        }
                        maps.Add(map);
                    }
                }
                return maps;
            }

            // elementMap() - return complete element info
            if (Regex.IsMatch(query, @"\.elementMap\s*\(\s*\)", RegexOptions.IgnoreCase))
            {
                var maps = new List<dynamic>();
                foreach (var item in results)
                {
                    if (item is GremlinResponseObject gro)
                    {
                        var map = new Dictionary<string, object>
                        {
                            ["id"] = gro.Get<object>("id"),
                            ["label"] = gro.Get<object>("label"),
                            ["type"] = gro.Get<object>("type")
                        };
                        
                        if (gro.Get<DynamicProperties>("properties") is DynamicProperties props)
                        {
                            foreach (var propName in props.GetDynamicMemberNames())
                            {
                                try
                                {
                                    dynamic dynamicProps = props;
                                    var value = GetPropertyValue(dynamicProps, propName);
                                    if (value != null)
                                    {
                                        map[propName] = value;
                                    }
                                }
                                catch
                                {
                                    // Ignore access errors
                                }
                            }
                        }
                        maps.Add(map);
                    }
                }
                return maps;
            }

            return results;
        }

        private object GetPropertyValue(dynamic props, string propertyName)
        {
            try
            {
                if (props is DynamicProperties dynamicProps)
                {
                    var dict = dynamicProps.GetProperties();
                    return dict.TryGetValue(propertyName, out var value) ? value : null;
                }
            }
            catch
            {
                // Fallback - ignore errors
            }
            return null;
        }

        #endregion

        private IEnumerable<dynamic> ExecuteComplexTraversal(string query)
        {
            // Handle patterns like g.V(id).out(label).in(label).hasLabel(label)
            var complexMatch = Regex.Match(query, @"g\.V\s*\(\s*['""]([^'""]+)['""]?\s*\)\.out\s*\(\s*['""]([^'""]+)['""]?\s*\)\.in\s*\(\s*['""]([^'""]+)['""]?\s*\)\.hasLabel\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (complexMatch.Success)
            {
                var startVertexId = complexMatch.Groups[1].Value;
                var outEdgeLabel = complexMatch.Groups[2].Value;
                var inEdgeLabel = complexMatch.Groups[3].Value;
                var targetLabel = complexMatch.Groups[4].Value;

                var intermediateVertices = _database.GetOutVertices(startVertexId, outEdgeLabel);
                var results = new List<InMemoryVertex>();

                foreach (var intermediate in intermediateVertices)
                {
                    var finalVertices = _database.GetInVertices(intermediate.Id, inEdgeLabel);
                    results.AddRange(finalVertices.Where(v => v.Label.Equals(targetLabel, StringComparison.OrdinalIgnoreCase)));
                }

                return results.Select(v => v.ToGremlinResponse());
            }

            // Handle dedup operations
            if (query.Contains(".dedup()"))
            {
                var baseQuery = query.Replace(".dedup()", "");
                var baseResults = ExecuteComplexTraversal(baseQuery);
                return baseResults.Distinct();
            }

            return new List<dynamic>();
        }

        /// <summary>
        /// Check if query is an inject query (TinkerPop start step)
        /// </summary>
        private bool IsInjectQuery(string query)
        {
            return Regex.IsMatch(query, @"g\.inject\s*\(", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Execute inject query with proper TinkerPop multiple value support
        /// </summary>
        private IEnumerable<dynamic> ExecuteInjectQuery(string query)
        {
            // Extract inject arguments using proper comma-aware parsing
            var injectMatch = Regex.Match(query, @"g\.inject\s*\(([^)]*)\)", RegexOptions.IgnoreCase);
            if (!injectMatch.Success)
            {
                return new List<dynamic>();
            }

            var argsString = injectMatch.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(argsString))
            {
                return new List<dynamic>();
            }

            // Parse multiple arguments with proper comma handling
            var arguments = ParseInjectArguments(argsString);
            var results = new List<dynamic>();

            // Process each argument and return all values (TinkerPop standard)
            foreach (var arg in arguments)
            {
                var value = ParseValue(arg);
                results.Add(value);
            }

            // Handle chained operations after inject
            var chainedQuery = query.Substring(injectMatch.Index + injectMatch.Length);
            if (!string.IsNullOrEmpty(chainedQuery) && chainedQuery.StartsWith("."))
            {
                return ApplyChainedOperations(results, chainedQuery);
            }

            return results;
        }

        /// <summary>
        /// Parse inject arguments handling comma separation correctly
        /// </summary>
        private List<string> ParseInjectArguments(string argsString)
        {
            var arguments = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';
            var parenLevel = 0;

            for (int i = 0; i < argsString.Length; i++)
            {
                char c = argsString[i];

                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    current += c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    current += c;
                }
                else if (!inQuotes && c == '(')
                {
                    parenLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenLevel--;
                    current += c;
                }
                else if (!inQuotes && c == ',' && parenLevel == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        arguments.Add(current.Trim());
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                arguments.Add(current.Trim());
            }

            return arguments;
        }

        /// <summary>
        /// Apply chained operations after inject (like .identity(), .count(), etc.)
        /// </summary>
        private IEnumerable<dynamic> ApplyChainedOperations(List<dynamic> baseResults, string chainedQuery)
        {
            var results = baseResults.AsEnumerable();

            // identity() - pass through unchanged (TinkerPop standard)
            if (Regex.IsMatch(chainedQuery, @"\.identity\s*\(\s*\)", RegexOptions.IgnoreCase))
            {
                return results;
            }

            // count() - return count of elements
            if (Regex.IsMatch(chainedQuery, @"\.count\s*\(\s*\)", RegexOptions.IgnoreCase))
            {
                return new dynamic[] { (long)results.Count() };
            }

            // fold() - return as single list
            if (Regex.IsMatch(chainedQuery, @"\.fold\s*\(\s*\)", RegexOptions.IgnoreCase))
            {
                return new dynamic[] { results.ToList() };
            }

            // as('label').select('label') pattern
            var asSelectMatch = Regex.Match(chainedQuery, @"\.as\s*\(\s*['""]([^'""]+)['""]?\s*\)\.select\s*\(\s*['""]([^'""]+)['""]?\s*\)", RegexOptions.IgnoreCase);
            if (asSelectMatch.Success)
            {
                var asLabel = asSelectMatch.Groups[1].Value;
                var selectLabel = asSelectMatch.Groups[2].Value;
                
                // In TinkerPop, as() creates a label for the current traverser, select() retrieves it
                if (asLabel.Equals(selectLabel, StringComparison.OrdinalIgnoreCase))
                {
                    return results; // Return the labeled values
                }
            }

            // Default: return unchanged results
            return results;
        }
    }
}
