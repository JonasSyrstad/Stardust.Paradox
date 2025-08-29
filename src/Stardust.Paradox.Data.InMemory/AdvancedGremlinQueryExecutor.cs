using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Advanced Gremlin query executor supporting CosmosDB-compatible steps
    /// Simplified to use TinkerGraph implementation
    /// </summary>
    public class AdvancedGremlinQueryExecutor
    {
        private readonly InMemoryGraphDatabase _database;

        public AdvancedGremlinQueryExecutor(InMemoryGraphDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Execute a parsed Gremlin query using TinkerGraph executor
        /// </summary>
        public IEnumerable<dynamic> Execute(ParsedGremlinQuery query)
        {
            // Convert to TinkerGraph traversal for execution
            var tinkerTraversal = ConvertToTinkerTraversal(query);
            var executor = new TinkerGraphQueryExecutor(_database);
            return executor.Execute(tinkerTraversal);
        }

        /// <summary>
        /// Convert ParsedGremlinQuery to TinkerGraphTraversal
        /// </summary>
        private TinkerGraphTraversal ConvertToTinkerTraversal(ParsedGremlinQuery query)
        {
            var traversal = new TinkerGraphTraversal();
            traversal.Parameters = query.Parameters;

            foreach (var step in query.Steps)
            {
                var tinkerStep = new TinkerGraphStep(step.StepName);
                tinkerStep.Arguments.AddRange(step.Arguments);
                
                foreach (var label in step.StepLabels)
                {
                    tinkerStep.AddLabel(label);
                }

                traversal.AddStep(tinkerStep);
            }

            return traversal;
        }
    }
}