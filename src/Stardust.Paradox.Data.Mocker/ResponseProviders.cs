using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Response provider that returns a JSON string as response
    /// </summary>
    public class JsonStringResponseProvider : IResponseProvider
    {
        private readonly string _jsonResponse;

        public JsonStringResponseProvider(string jsonResponse)
        {
            _jsonResponse = jsonResponse ?? throw new ArgumentNullException(nameof(jsonResponse));
        }

        public Task<IEnumerable<dynamic>> GetResponseAsync(string query, Dictionary<string, object> parameters)
        {
            var result = JsonConvert.DeserializeObject<IEnumerable<dynamic>>(_jsonResponse);
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Response provider that reads response from a JSON file
    /// </summary>
    public class JsonFileResponseProvider : IResponseProvider
    {
        private readonly string _filePath;

        public JsonFileResponseProvider(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public async Task<IEnumerable<dynamic>> GetResponseAsync(string query, Dictionary<string, object> parameters)
        {
            if (!File.Exists(_filePath))
            {
                throw new FileNotFoundException($"Mock response file not found: {_filePath}");
            }

            string jsonContent;
            using (var reader = new StreamReader(_filePath))
            {
                jsonContent = await reader.ReadToEndAsync();
            }
            
            var result = JsonConvert.DeserializeObject<IEnumerable<dynamic>>(jsonContent);
            return result;
        }
    }

    /// <summary>
    /// Response provider that uses a function to generate responses
    /// </summary>
    public class FunctionResponseProvider : IResponseProvider
    {
        private readonly Func<string, Dictionary<string, object>, IEnumerable<dynamic>> _responseFunction;

        public FunctionResponseProvider(Func<string, Dictionary<string, object>, IEnumerable<dynamic>> responseFunction)
        {
            _responseFunction = responseFunction ?? throw new ArgumentNullException(nameof(responseFunction));
        }

        public Task<IEnumerable<dynamic>> GetResponseAsync(string query, Dictionary<string, object> parameters)
        {
            var result = _responseFunction(query, parameters);
            return Task.FromResult(result);
        }
    }
}