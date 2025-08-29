using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Advanced Gremlin query parser using proper traversal state management
    /// Inspired by Apache TinkerPop's TinkerGraph implementation
    /// </summary>
    public class AdvancedGremlinQueryParser
    {
        private readonly InMemoryGraphDatabase _database;
        private readonly AdvancedGremlinQueryExecutor _executor;

        // TinkerPop step categories for better step handling
        private static readonly HashSet<string> BarrierSteps = new HashSet<string>
        {
            "group", "groupCount", "cap", "barrier", "fold", "order", "dedup"
        };

        private static readonly HashSet<string> SideEffectSteps = new HashSet<string>
        {
            "aggregate", "store", "sack", "tree"
        };

        public AdvancedGremlinQueryParser(InMemoryGraphDatabase database)
        {
            _database = database;
            _executor = new AdvancedGremlinQueryExecutor(database);
        }

        /// <summary>
        /// Parse and execute a Gremlin query with advanced traversal state management
        /// </summary>
        public IEnumerable<dynamic> ParseAndExecute(string query, Dictionary<string, object> parameters)
        {
            try
            {
                // Check for custom responses first
                var customResponse = _database.GetCustomResponse(query, parameters);
                if (customResponse != null)
                {
                    return customResponse;
                }

                // For now, use the TinkerGraph parser which has the most complete implementation
                var tinkerParser = new TinkerGraphQueryParser(_database);
                return tinkerParser.ParseAndExecute(query, parameters);
            }
            catch (Exception)
            {
                // Fallback to simple regex parsing for unsupported queries
                return ExecuteFallback(query, parameters);
            }
        }

        private IEnumerable<dynamic> ExecuteFallback(string query, Dictionary<string, object> parameters)
        {
            // Use the original simple parser as fallback
            var simpleParser = new GremlinQueryParser(_database);
            return simpleParser.ParseAndExecuteAsync(query, parameters).Result;
        }
    }
}