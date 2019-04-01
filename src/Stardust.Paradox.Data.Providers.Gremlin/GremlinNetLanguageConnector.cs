using Gremlin.Net.Driver;
using Gremlin.Net.Driver.Exceptions;
using Gremlin.Net.Driver.Messages;
using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json;
using Stardust.Paradox.Data.Internals;
using Stardust.Particles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Providers.Gremlin
{
	public class GremlinNetLanguageConnector : LanguageConnectorBase, IGremlinLanguageConnector//, IDisposable
	{
		public static ConnectionPoolSettings ConnectionPoolSettings { get; set; }
		public static Action<ClientWebSocketOptions> WebSocketConfiguration { get; set; }

		public static bool IsAspNetCore { get; set; }
		private readonly GremlinClient _client;
		private static readonly object lockObject = new object();
		private static readonly ConcurrentDictionary<string, GremlinClient> gremlinClients = new ConcurrentDictionary<string, GremlinClient>();
		private readonly string _key;


		/// <summary>
		/// Connecting to Cosmos DB
		/// </summary>
		/// <param name="gremlinHostname">DBAccountName.gremlin.cosmosdb.azure.com</param>
		/// <param name="databaseName"></param>
		/// <param name="graphName"></param>
		/// <param name="accessKey"></param>
		public GremlinNetLanguageConnector(string gremlinHostname, string databaseName, string graphName, string accessKey, ILogging logger = null) : base(logger)
		{
			_key = $"{gremlinHostname.ToLower()}.{databaseName.ToLower()}.{graphName.ToLower()}";
			if (gremlinClients.TryGetValue(_key, out _client)) return;
			lock (lockObject)
			{
				if (gremlinClients.TryGetValue(_key, out _client)) return;
				var server = new GremlinServer(gremlinHostname, 443, true, $"/dbs/{databaseName}/colls/{graphName}", accessKey);
				_client = new GremlinClient(server, new InternalGraphSONReader1(),
					new GraphSON2Writer(),
					GremlinClient.GraphSON2MimeType, ConnectionPoolSettings, WebSocketConfiguration);
				gremlinClients.TryAdd(_key, _client);
			}
		}

		public GremlinNetLanguageConnector(string gremlinHostname, string username, string password, int port = 8182, bool enableSsl = true, ILogging logger = null) : base(logger)
		{
			_key = $"{gremlinHostname.ToLower()}.{username.ToLower()}.{port}";
			if (gremlinClients.TryGetValue(_key, out _client)) return;
			lock (lockObject)
			{
				if (gremlinClients.TryGetValue(_key, out _client)) return;
				var server = new GremlinServer(gremlinHostname, port, enableSsl, username, password);
				_client = new GremlinClient(server, new InternalGraphSONReader1(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);
				gremlinClients.TryAdd(_key, _client);
			}

		}

		public virtual async Task<IEnumerable<dynamic>> ExecuteAsync(string compileQuery, Dictionary<string, object> parametrizedValues)
		{
			var executeQuery = true;
			var retry = 0;
			while (executeQuery)
			{
				try
				{
					var resp = await _client.SubmitAsync<dynamic>(compileQuery, parametrizedValues)
						.ConfigureAwait(false);
					if (resp.StatusAttributes.TryGetValue("x-ms-total-request-charge", out var ru))
					{
						Log(
							$"gremlin: {compileQuery} {JsonConvert.SerializeObject(parametrizedValues)} (ru cost: {ru})");
						ConsumedRU += (double) ru;
					}
					else
						Log($"gremlin: {compileQuery}");

					return resp;
				}
				catch (AggregateException aggregateException)
				{
					if (aggregateException.InnerException is IOException)
					{
						Log(aggregateException.InnerException);
						lock (lockObject)
						{
							if (gremlinClients.TryRemove(_key, out var c))
							{
								c?.Dispose();
							}
						}

						throw;
					}
					if (aggregateException.InnerException is ResponseException rex)
					{
						await HandleResponseException(compileQuery, rex, retry);
						retry++;
					}

				}
				catch (IOException ioException)
				{
					Log(ioException);
					lock (lockObject)
					{
						if (gremlinClients.TryRemove(_key, out var c))
						{
							c?.Dispose();
						}
					}
					throw;
				}
				catch (ResponseException responseException)
				{
					await HandleResponseException(compileQuery, responseException,retry);
					retry++;
				}
				catch (Exception ex)
				{
					Log(compileQuery, ex);
					throw;
				}
			}
			throw new Exception("Should not get there");
		}

		private async Task HandleResponseException(string compileQuery, ResponseException responseException,int retry)
		{
			if (responseException.StatusAttributes.TryGetValue("x-ms-status-code", out var s))
			{
				if ((long) s == 429)
				{
					var waitTime = responseException.StatusAttributes["x-ms-retry-after-ms"] as long? ?? 200;
					await Task.Delay((int) waitTime);
					if (retry > 5)
					{
						Log(compileQuery, responseException);
						throw responseException;
					}

				}
				else
				{
					Log(compileQuery, responseException);
					throw responseException;
				}
			}
			else
			{
				Log(compileQuery, responseException);
				throw responseException;
			}

		}

		public bool CanParameterizeQueries => true;
		public double ConsumedRU { get; private set; }

		//protected virtual void Dispose(bool disposing)
		//{
		//	if (disposing)
		//	{
		//		try
		//		{
		//			_client?.Dispose();
		//		}
		//		catch (Exception ex)
		//		{
		//			Log(ex);
		//		}

		//	}
		//}

		//public void Dispose()
		//{
		//	Dispose(true);
		//	GC.SuppressFinalize(this);
		//}
	}
}
