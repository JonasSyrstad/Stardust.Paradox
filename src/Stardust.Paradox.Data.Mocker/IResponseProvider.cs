using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Interface for providing responses to mock queries
    /// </summary>
    public interface IResponseProvider
    {
        Task<IEnumerable<dynamic>> GetResponseAsync(string query, Dictionary<string, object> parameters);
    }
}