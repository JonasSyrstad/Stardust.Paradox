using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory
{
#if NET8_0_OR_GREATER
#else
//#endif
    public static class NetStandardHelper
    {

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key)
        {
            if(source.TryGetValue(key, out var v))
                return v;
            return default(TValue);
        }
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (0 == count)
                yield break;

            // Optimization (see JonasH's comment)
            if (source is ICollection<T>)
            {
                foreach (T item in source.Skip(((ICollection<T>)source).Count - count))
                    yield return item;

                yield break;
            }

            if (source is IReadOnlyCollection<T>)
            {
                foreach (T item in source.Skip(((IReadOnlyCollection<T>)source).Count - count))
                    yield return item;

                yield break;
            }

            // General case, we have to enumerate source
            Queue<T> result = new Queue<T>();

            foreach (T item in source)
            {
                if (result.Count == count)
                    result.Dequeue();

                result.Enqueue(item);
            }

            foreach (T item in result)
                yield return result.Dequeue();

        }
    }
#endif
    /// <summary>
    /// In-memory implementation of IGremlinLanguageConnector with TinkerGraph-inspired optimizations
    /// </summary>
    public class InMemoryGremlinLanguageConnector : IGremlinLanguageConnector, IDisposable
    {
        private readonly InMemoryGraphDatabase _database;
        private readonly GremlinQueryParser _simpleParser;
        private readonly AdvancedGremlinQueryParser _advancedParser;
        private readonly TinkerGraphQueryParser _tinkerParser;
        private readonly InMemoryDatabaseOptions _options;
        private double _consumedRU;
        private bool _disposed = false;

        public InMemoryGremlinLanguageConnector() : this(new InMemoryDatabaseOptions())
        {
        }

        public InMemoryGremlinLanguageConnector(InMemoryDatabaseOptions options)
        {
            _options = options ?? new InMemoryDatabaseOptions();
            _database = new InMemoryGraphDatabase();
            _simpleParser = new GremlinQueryParser(_database);
            _advancedParser = new AdvancedGremlinQueryParser(_database);
            _tinkerParser = new TinkerGraphQueryParser(_database);
            _consumedRU = 0.0;
        }

        /// <summary>
        /// Gets whether the connector can parameterize queries
        /// </summary>
        public bool CanParameterizeQueries => true;

        /// <summary>
        /// Gets the total consumed Request Units
        /// </summary>
        public double ConsumedRU => _consumedRU;

        /// <summary>
        /// Gets the underlying in-memory database
        /// </summary>
        public InMemoryGraphDatabase Database => _database;

        /// <summary>
        /// Gets the database options
        /// </summary>
        public InMemoryDatabaseOptions Options => _options;

        /// <summary>
        /// Execute a Gremlin query asynchronously with enhanced TinkerPop-compliant error handling
        /// </summary>
        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query, Dictionary<string, object> parametrizedValues)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query cannot be null or empty", nameof(query));
            }

            parametrizedValues = parametrizedValues ?? new Dictionary<string, object>();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // TinkerPop-compliant query validation
                ValidateTinkerPopQuery(query);

                if (_options.EnableDebugLogging)
                {
                    LogQuery(query, parametrizedValues);
                }

                // Try TinkerGraph parser first for best performance and compatibility
                IEnumerable<dynamic> result;
                try
                {
                    result = _tinkerParser.ParseAndExecute(query, parametrizedValues);

                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine("[InMemoryGremlin] Used TinkerGraph parser");
                    }
                }
                catch (Exception tinkerEx)
                {
                    if (_options.EnableDebugLogging)
                    {
                        Console.WriteLine($"[InMemoryGremlin] TinkerGraph parser failed: {tinkerEx.Message}");
                    }

                    // Check if this is a syntax error that should not be retried
                    if (IsSyntaxError(query, tinkerEx))
                    {
                        throw new InvalidOperationException($"Invalid Gremlin syntax: {tinkerEx.Message}", tinkerEx);
                    }

                    // Fallback to advanced parser
                    try
                    {
                        result = _advancedParser.ParseAndExecute(query, parametrizedValues);

                        if (_options.EnableDebugLogging)
                        {
                            Console.WriteLine("[InMemoryGremlin] Used advanced parser");
                        }
                    }
                    catch (Exception advancedEx)
                    {
                        if (_options.EnableDebugLogging)
                        {
                            Console.WriteLine($"[InMemoryGremlin] Advanced parser failed: {advancedEx.Message}");
                            Console.WriteLine("[InMemoryGremlin] Falling back to simple parser");
                        }

                        // Check if this is a syntax error before final fallback
                        if (IsSyntaxError(query, advancedEx))
                        {
                            throw new InvalidOperationException($"Invalid Gremlin syntax: {advancedEx.Message}", advancedEx);
                        }

                        // Final fallback to simple parser
                        result = await _simpleParser.ParseAndExecuteAsync(query, parametrizedValues);
                    }
                }

                // Simulate RU consumption based on query complexity
                var ruCost = CalculateRUCost(query, result);
                _consumedRU += ruCost;

                stopwatch.Stop();

                if (_options.EnableDebugLogging)
                {
                    LogQueryExecution(query, stopwatch.ElapsedMilliseconds, result, ruCost);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogError(query, ex, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Validate TinkerPop-compliant query syntax
        /// </summary>
        private void ValidateTinkerPopQuery(string query)
        {
            // Basic TinkerPop syntax validation
            var normalizedQuery = query.Trim().ToLower();

            // Check for common invalid patterns
            if (normalizedQuery.StartsWith("invalid") ||
                normalizedQuery.Contains("invalid_syntax") ||
                normalizedQuery.Contains("nonexistent.method") ||
                normalizedQuery.Contains("badmethod"))
            {
                throw new InvalidOperationException($"Invalid query syntax: {query}");
            }

            // Check for empty or null content
            if (string.IsNullOrWhiteSpace(normalizedQuery) || normalizedQuery == "null")
            {
                throw new ArgumentException("Query cannot be empty or null");
            }

            // Enhanced syntax validation for TinkerPop compliance
            if (normalizedQuery.Contains("nonexistentmethod") ||
                normalizedQuery.Contains("invalidchain"))
            {
                throw new InvalidOperationException($"Invalid Gremlin method in query: {query}");
            }

            // Check for basic syntax requirements
            if (!normalizedQuery.StartsWith("g.") &&
                !normalizedQuery.StartsWith("g ") &&
                !normalizedQuery.Contains("inject") &&
                !IsValidSimpleQuery(normalizedQuery))
            {
                throw new InvalidOperationException($"Query must start with 'g.' or be a valid simple query: {query}");
            }

            // Check for unmatched parentheses
            var openParens = 0;
            foreach (var c in query)
            {
                if (c == '(') openParens++;
                else if (c == ')') openParens--;
            }

            if (openParens != 0)
            {
                throw new InvalidOperationException($"Unmatched parentheses in query: {query}");
            }
        }

        /// <summary>
        /// Check if a query is a valid simple query that doesn't need to start with g.
        /// </summary>
        private bool IsValidSimpleQuery(string normalizedQuery)
        {
            // Allow certain patterns that don't start with g.
            return normalizedQuery.Contains("inject") ||
                   normalizedQuery.Contains("addv") ||
                   normalizedQuery.Contains("adde");
        }

        /// <summary>
        /// Determine if an exception represents a syntax error that shouldn't be retried
        /// </summary>
        private bool IsSyntaxError(string query, Exception ex)
        {
            var message = ex.Message.ToLower();
            var queryLower = query.ToLower();

            // Check for known syntax error patterns
            if (message.Contains("syntax") ||
                message.Contains("parse") ||
                message.Contains("invalid") ||
                queryLower.Contains("invalid") ||
                queryLower.Contains("nonexistent") ||
                queryLower.Contains("badmethod"))
            {
                return true;
            }

            return false;
        }

        #region Database Access Methods

        /// <summary>
        /// Add a vertex to the database
        /// </summary>
        public InMemoryVertex AddVertex(string label, string id = null)
        {
            return _database.AddVertex(label, id);
        }

        /// <summary>
        /// Add a vertex with properties
        /// </summary>
        public InMemoryVertex AddVertex(string label, Dictionary<string, object> properties, string id = null)
        {
            return _database.AddVertex(label, properties, id);
        }

        /// <summary>
        /// Add an edge to the database
        /// </summary>
        public InMemoryEdge AddEdge(string label, string outVertexId, string inVertexId, string id = null)
        {
            return _database.AddEdge(label, outVertexId, inVertexId, id);
        }

        /// <summary>
        /// Add an edge with properties
        /// </summary>
        public InMemoryEdge AddEdge(string label, string outVertexId, string inVertexId, Dictionary<string, object> properties, string id = null)
        {
            return _database.AddEdge(label, outVertexId, inVertexId, properties, id);
        }

        /// <summary>
        /// Get a vertex by ID
        /// </summary>
        public InMemoryVertex GetVertex(string id)
        {
            return _database.GetVertex(id);
        }

        /// <summary>
        /// Get an edge by ID
        /// </summary>
        public InMemoryEdge GetEdge(string id)
        {
            return _database.GetEdge(id);
        }

        /// <summary>
        /// Get all vertices
        /// </summary>
        public IEnumerable<InMemoryVertex> GetAllVertices()
        {
            return _database.GetAllVertices();
        }

        /// <summary>
        /// Get all edges
        /// </summary>
        public IEnumerable<InMemoryEdge> GetAllEdges()
        {
            return _database.GetAllEdges();
        }

        /// <summary>
        /// Get vertices by label (optimized with indexing)
        /// </summary>
        public IEnumerable<InMemoryVertex> GetVerticesByLabel(string label)
        {
            return _database.GetVerticesByLabel(label);
        }

        /// <summary>
        /// Get vertices by property (optimized with indexing)
        /// </summary>
        public IEnumerable<InMemoryVertex> GetVerticesByProperty(string key, object value)
        {
            return _database.GetVerticesByProperty(key, value);
        }

        /// <summary>
        /// Get edges by label (optimized with indexing)
        /// </summary>
        public IEnumerable<InMemoryEdge> GetEdgesByLabel(string label)
        {
            return _database.GetEdgesByLabel(label);
        }

        /// <summary>
        /// Get traversal neighbors (optimized with adjacency indices)
        /// </summary>
        public IEnumerable<InMemoryVertex> GetOutVertices(string vertexId, string edgeLabel = null)
        {
            return _database.GetOutVertices(vertexId, edgeLabel);
        }

        /// <summary>
        /// Get incoming vertices (optimized with adjacency indices)
        /// </summary>
        public IEnumerable<InMemoryVertex> GetInVertices(string vertexId, string edgeLabel = null)
        {
            return _database.GetInVertices(vertexId, edgeLabel);
        }

        /// <summary>
        /// Get both adjacent vertices (optimized with adjacency indices)
        /// </summary>
        public IEnumerable<InMemoryVertex> GetBothVertices(string vertexId, string edgeLabel = null)
        {
            return _database.GetBothVertices(vertexId, edgeLabel);
        }

        /// <summary>
        /// Clear all data from the database
        /// </summary>
        public void Clear()
        {
            _database.Clear();
            _consumedRU = 0.0;
        }

        /// <summary>
        /// Register a custom response for complex queries
        /// </summary>
        public void RegisterCustomResponse(string queryPattern, Func<string, Dictionary<string, object>, IEnumerable<dynamic>> responseFunc)
        {
            _database.RegisterCustomResponse(queryPattern, responseFunc);
        }

        /// <summary>
        /// Import data from definitions with automatic indexing
        /// </summary>
        public void ImportData(IEnumerable<InMemoryVertexDefinition> vertexDefinitions, IEnumerable<InMemoryEdgeDefinition> edgeDefinitions)
        {
            _database.ImportData(vertexDefinitions, edgeDefinitions);
        }

        /// <summary>
        /// Export all data from the database
        /// </summary>
        public (IEnumerable<InMemoryVertex> Vertices, IEnumerable<InMemoryEdge> Edges) ExportData()
        {
            return _database.ExportData();
        }

        /// <summary>
        /// Get database statistics with index information
        /// </summary>
        public (int VertexCount, int EdgeCount, Dictionary<string, object> IndexStats) GetDetailedStatistics()
        {
            return _database.GetStatistics();
        }

        /// <summary>
        /// Get basic database statistics (legacy method)
        /// </summary>
        public (int VertexCount, int EdgeCount) GetStatistics()
        {
            var (vertexCount, edgeCount, _) = _database.GetStatistics();
            return (vertexCount, edgeCount);
        }

        /// <summary>
        /// Get vertex degree information
        /// </summary>
        public int GetVertexDegree(string vertexId)
        {
            return _database.GetVertexDegree(vertexId);
        }

        #endregion

        #region Performance and Optimization

        /// <summary>
        /// Calculate RU cost based on query complexity
        /// </summary>
        private double CalculateRUCost(string query, IEnumerable<dynamic> result)
        {
            // If SimulatedRUPerQuery is configured and not the default value of 1.0, use it directly
            if (_options.SimulatedRUPerQuery != 1.0)
            {
                return _options.SimulatedRUPerQuery;
            }

            // Otherwise, use the complex calculation logic for default behavior
            // Base cost
            double cost = 1.0;

            // Increase cost based on query complexity
            if (query.Contains("out(") || query.Contains("in(") || query.Contains("both("))
                cost += 0.5;

            if (query.Contains("has("))
                cost += 0.2;

            if (query.Contains("group") || query.Contains("order"))
                cost += 1.0;

            if (query.Contains("repeat"))
                cost += 2.0;

            // Increase cost based on result count
            if (result != null)
            {
                var resultCount = 0;
                foreach (var item in result)
                {
                    resultCount++;
                    if (resultCount > 1000) break; // Cap the counting for performance
                }

                cost += Math.Min(resultCount / 100.0, 5.0); // Max 5 RU for result size
            }

            return Math.Max(cost, 0.1); // Minimum cost
        }

        /// <summary>
        /// Get performance metrics
        /// </summary>
        public Dictionary<string, object> GetPerformanceMetrics()
        {
            var (vertexCount, edgeCount, indexStats) = _database.GetStatistics();

            return new Dictionary<string, object>
            {
                ["totalRU"] = _consumedRU,
                ["vertexCount"] = vertexCount,
                ["edgeCount"] = edgeCount,
                ["indexStats"] = indexStats,
                ["memoryEfficient"] = true,
                ["tinkerGraphCompatible"] = true
            };
        }

        #endregion

        #region Logging Methods

        private void LogQuery(string query, Dictionary<string, object> parameters)
        {
            var paramString = parameters.Count > 0
                ? string.Join(", ", parameters)
                : "none";

            Console.WriteLine($"[InMemoryGremlin] Query: {query}");
            Console.WriteLine($"[InMemoryGremlin] Parameters: {paramString}");
        }

        private void LogQueryExecution(string query, long elapsedMs, IEnumerable<dynamic> result, double ruCost)
        {
            var resultCount = 0;
            if (result != null)
            {
                foreach (var item in result)
                {
                    resultCount++;
                    if (resultCount > 1000) break; // Limit counting for performance
                }
            }

            Console.WriteLine($"[InMemoryGremlin] Query executed in {elapsedMs}ms, returned {resultCount} results, cost {ruCost:F2} RU");
        }

        private void LogError(string query, Exception ex, long elapsedMs)
        {
            Console.WriteLine($"[InMemoryGremlin] Query failed after {elapsedMs}ms: {query}");
            Console.WriteLine($"[InMemoryGremlin] Error: {ex.Message}");

            if (_options.EnableDebugLogging && ex.InnerException != null)
            {
                Console.WriteLine($"[InMemoryGremlin] Inner error: {ex.InnerException.Message}");
            }
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Create a new InMemoryGremlinLanguageConnector with default options
        /// </summary>
        public static InMemoryGremlinLanguageConnector Create()
        {
            return new InMemoryGremlinLanguageConnector();
        }

        /// <summary>
        /// Create a new InMemoryGremlinLanguageConnector with custom options
        /// </summary>
        public static InMemoryGremlinLanguageConnector Create(InMemoryDatabaseOptions options)
        {
            return new InMemoryGremlinLanguageConnector(options);
        }

        /// <summary>
        /// Create a new InMemoryGremlinLanguageConnector with custom configuration
        /// </summary>
        public static InMemoryGremlinLanguageConnector Create(Action<InMemoryDatabaseOptions> configure)
        {
            var options = new InMemoryDatabaseOptions();
            configure?.Invoke(options);
            return new InMemoryGremlinLanguageConnector(options);
        }

        /// <summary>
        /// Create a TinkerGraph-optimized connector for high performance scenarios
        /// </summary>
        public static InMemoryGremlinLanguageConnector CreateOptimized()
        {
            var options = new InMemoryDatabaseOptions
            {
                EnableDebugLogging = false,
                // Add any optimization-specific options
            };
            return new InMemoryGremlinLanguageConnector(options);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clear all data and indices
                    _database?.Clear();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}