using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class InlineSerializationAttribute : Attribute
    {
        public InlineSerializationAttribute(SerializationType type)
        {
            Type = type;
        }

        public SerializationType Type { get; }
    }
}