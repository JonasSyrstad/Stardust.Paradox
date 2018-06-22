using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data
{
    /// <summary>
    /// An abstraction allowing to use different language drivers, be it CosmosDB or Tinkerpop
    /// </summary>
    public interface IGremlinLanguageConnector
    {
        IEnumerable<T> Execute<T>(string query);
        Task<IEnumerable<T>> ExecuteAsync<T>(string query);
        Task<IEnumerable<dynamic>> ExecuteAsync(string compileQuery);
    }
}
