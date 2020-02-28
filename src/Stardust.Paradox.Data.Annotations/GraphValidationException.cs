using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Stardust.Paradox.Data.Annotations
{
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
