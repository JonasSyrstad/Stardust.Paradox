using System;
using System.Runtime.Serialization;

namespace Stardust.Paradox.Data
{
    public class EntityDeletedException : Exception
    {
        public EntityDeletedException()
        {
        }

        public EntityDeletedException(string message):base(message)
        {
        }

        public EntityDeletedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected EntityDeletedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}