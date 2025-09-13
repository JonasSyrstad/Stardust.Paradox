using Stardust.Paradox.Data;
using Stardust.Paradox.Data.InMemory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.InMemory.Tests.CosmosDbMigrated
{
    public class Class1 : IGremlinLanguageConnector
    {
        private readonly InMemoryGremlinLanguageConnector _inMemoryConnector;

        public Class1()
        {
            _inMemoryConnector = new InMemoryGremlinLanguageConnector();
        }

        public async Task<IEnumerable<dynamic>> ExecuteAsync(string query,
            Dictionary<string, object> parametrizedValues)
        {
            return await _inMemoryConnector.ExecuteAsync(query, parametrizedValues);
        }

        public bool CanParameterizeQueries => _inMemoryConnector.CanParameterizeQueries;
        public double ConsumedRU => _inMemoryConnector.ConsumedRU;
    }
}