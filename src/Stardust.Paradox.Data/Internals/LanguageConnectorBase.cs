using Stardust.Particles;
using System;
using Newtonsoft.Json;

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

		protected bool Log(string query, Exception ex, object properties)
		{
			if (!OutputDebugLog) return false;
			if (_logger != null)
			{
				_logger.DebugMessage($"Failed query: {query}\n, props: \n{JsonConvert.SerializeObject(properties ?? new object())}", LogType.Information, this.GetType().FullName);
				_logger.Exception(ex, $"{GetType().FullName}({ex.GetType()})");
				LoggInner(ex);
            }
			else
			{
				Logging.DebugMessage($"Failed query: {query}\n, props: \n{JsonConvert.SerializeObject(properties?? new object())}", LogType.Information, this.GetType().FullName);
				Logging.Exception(ex, $"{GetType().FullName}({ex.GetType()})");
                LoggInner(ex);
            }

			Console.WriteLine(query);
			return false;
		}

        private void LoggInner(Exception ex)
        {
            if (ex.InnerException != null)
            {
				if(_logger != null)
                    _logger.Exception(ex.InnerException, $"{GetType().FullName}({ex.InnerException.GetType()})");
				else
                    Logging.Exception(ex.InnerException, $"{GetType().FullName}({ex.GetType()})");
				LoggInner(ex);
            }
        }

        protected void Log(Exception ex)
		{
			if (!OutputDebugLog) return;
			if (_logger != null)
			{

				_logger.Exception(ex, this.GetType().FullName);
				LoggInner(ex);
			}
			else
			{

				Logging.Exception(ex, this.GetType().FullName);
                LoggInner(ex);
            }
		}

		protected void Log(string message)
		{
			if(!OutputAllQueries) return;
			if (_logger != null) _logger.DebugMessage(message, LogType.Information, this.GetType().FullName);
			else
				Logging.DebugMessage(message, LogType.Information, this.GetType().FullName);
		}
	}
}