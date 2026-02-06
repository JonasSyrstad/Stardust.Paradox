using System;
using System.Runtime.Serialization;

namespace Stardust.Paradox.Data.Annotations
{
    [Serializable]
    public class GraphValidationException : Exception
    {
        public GraphValidationException()
        {
        }

        public GraphValidationException(string message) : base(message)
        {
        }

        public GraphValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected GraphValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}