using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class InlineSerializationAttribute : Attribute
    {
        public SerializationType Type { get; }

        public InlineSerializationAttribute(SerializationType type)
        {
            Type = type;
        }
    }
}