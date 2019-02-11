using Stardust.Particles;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stardust.Paradox.Data.Internals
{
	public abstract class LanguageConnectorBase
	{
		private ILogging _logger;

		protected LanguageConnectorBase(ILogging logger)
		{
			_logger = logger;
		}
		public bool OutputDebugLog { get; set; }

		public bool OutputAllQueries { get; set; }

		protected void Log(string query, Exception ex)
		{
			if (!OutputDebugLog) return;
			if (_logger != null)
			{
				_logger.DebugMessage($"Failed query: {query}", LogType.Information, "CosmosDb gremlin connector");
				_logger.Exception(ex, "CosmosDb gremlin connector");
			}
			else
			{
				Logging.DebugMessage($"Failed query: {query}", LogType.Information, "CosmosDb gremlin connector");
				Logging.Exception(ex, "CosmosDb gremlin connector");
			}

			Console.WriteLine(query);
		}

		protected void Log(Exception ex)
		{
			if (!OutputDebugLog) return;
			if (_logger != null)
			{

				_logger.Exception(ex, "CosmosDb gremlin connector");
			}
			else
			{

				Logging.Exception(ex, "CosmosDb gremlin connector");
			}
		}

		protected void Log(string message)
		{
			if(!OutputAllQueries) return;
			if (_logger != null) _logger.DebugMessage(message, LogType.Information, "CosmosDb gremlin connector");
			else
				Logging.DebugMessage(message, LogType.Information, "CosmosDb gremlin connector");
		}
	}
}