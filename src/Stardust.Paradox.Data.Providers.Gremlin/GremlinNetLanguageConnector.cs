using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Stardust.Paradox.Data.Internals;
using Stardust.Particles;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gremlin.Net.Driver.Exceptions;
using Gremlin.Net.Driver.Messages;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.Providers.Gremlin
{
	public class GremlinNetLanguageConnector : LanguageConnectorBase, IGremlinLanguageConnector, IDisposable
	{
		public static bool IsAspNetCore { get; set; }
		private GremlinClient _client;

		/// <summary>
		/// Connecting to Cosmos DB
		/// </summary>
		/// <param name="gremlinHostname">DBAccountName.gremlin.cosmosdb.azure.com</param>
		/// <param name="databaseName"></param>
		/// <param name="graphName"></param>
		/// <param name="accessKey"></param>
		public GremlinNetLanguageConnector(string gremlinHostname, string databaseName, string graphName, string accessKey, ILogging logger = null) : base(logger)
		{
			var server = new GremlinServer(gremlinHostname, 443, true, $"/dbs/{databaseName}/colls/{graphName}", accessKey);

			_client = new GremlinClient(server, new InternalGraphSONReader1(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);
		}

		public GremlinNetLanguageConnector(string gremlinHostname, string username, string password, int port = 8182, bool enableSsl = true, ILogging logger = null) : base(logger)
		{
			var server = new GremlinServer(gremlinHostname, port, enableSsl, username, password);

			_client = new GremlinClient(server, new InternalGraphSONReader1(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);
		}

		public virtual async Task<IEnumerable<dynamic>> ExecuteAsync(string compileQuery, Dictionary<string, object> parametrizedValues)
		{
			var executeQuery = true;
			var retry = 0;
			while (executeQuery)
			{
				try
				{
					var resp = await _client.SubmitAsync<dynamic>(compileQuery, parametrizedValues).ConfigureAwait(false);
					Log(resp.StatusAttributes.TryGetValue("x-ms-request-charge", out var ru)
						? $"gremlin: {compileQuery} {JsonConvert.SerializeObject(parametrizedValues)} (ru cost: {ru})"
						: $"gremlin: {compileQuery}");
					return resp;
				}
				catch (ResponseException responseException)
				{
					if (responseException.StatusAttributes.TryGetValue("x-ms-status-code", out var s))
					{
						if ((int)s == 429)
						{
							var waitTime = responseException.StatusAttributes["x-ms-retry-after-ms"] as string??"200";
							await Task.Delay(int.Parse(waitTime));
							if (retry > 5)
							{
								Log(compileQuery, responseException);
								throw;
							}
							retry++;
						}
						else
						{
							Log(compileQuery, responseException);
							throw;
						}
					}
					else
					{
						Log(compileQuery, responseException);
						throw;
					}
				}
				catch (Exception ex)
				{
					Log(compileQuery, ex);
					throw;
				} 
			}
			throw new Exception("Should not get there");
		}

		public bool CanParameterizeQueries => true;

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				try
				{
					_client?.Dispose();
				}
				catch (Exception ex)
				{
					Log(ex);
				}

			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
