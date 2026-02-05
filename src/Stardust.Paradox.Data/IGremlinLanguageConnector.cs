using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data
{
    /// <summary>
    /// An abstraction allowing to use different language drivers, be it CosmosDB or Tinkerpop
    /// </summary>
    public interface IGremlinLanguageConnector
    {
        //IEnumerable<T> Execute<T>(string query);
        //Task<IEnumerable<T>> ExecuteAsync<T>(string query);
        Task<IEnumerable<dynamic>> ExecuteAsync(string query,Dictionary<string,object> parametrizedValues);

		bool CanParameterizeQueries { get; }

		/// <summary>
		/// Declares supported connector capabilities.
		/// </summary>
		Features Features { get; }
	    double ConsumedRU { get;}
    }
}
