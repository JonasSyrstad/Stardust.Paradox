using System;
using Stardust.Particles;

namespace Stardust.Paradox.Data.Internals
{
    public abstract class LanguageConnectorBase
    {
        private readonly ILogging _logger;

        protected LanguageConnectorBase(ILogging logger)
        {
            _logger = logger;
        }

        public bool OutputDebugLog { get; set; }

        public bool OutputAllQueries { get; set; }

        protected bool Log(string query, Exception ex)
        {
            if (!OutputDebugLog) return false;
            if (_logger != null)
            {
                _logger.DebugMessage($"Failed query: {query}", LogType.Information, GetType().FullName);
                _logger.Exception(ex, $"{GetType().FullName}({ex.GetType()})");
            }
            else
            {
                Logging.DebugMessage($"Failed query: {query}", LogType.Information, GetType().FullName);
                Logging.Exception(ex, $"{GetType().FullName}({ex.GetType()})");
            }

            Console.WriteLine(query);
            return false;
        }

        protected void Log(Exception ex)
        {
            if (!OutputDebugLog) return;
            if (_logger != null)
                _logger.Exception(ex, GetType().FullName);
            else
                Logging.Exception(ex, GetType().FullName);
        }

        protected void Log(string message)
        {
            if (!OutputAllQueries) return;
            if (_logger != null) _logger.DebugMessage(message, LogType.Information, GetType().FullName);
            else
                Logging.DebugMessage(message, LogType.Information, GetType().FullName);
        }
    }
}