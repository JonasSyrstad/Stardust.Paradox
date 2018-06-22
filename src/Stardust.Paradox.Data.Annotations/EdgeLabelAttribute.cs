using System;

namespace Stardust.Paradox.Data
{
    [AttributeUsage(AttributeTargets.Property)]
    public class EdgeLabelAttribute : Attribute
    {
        public EdgeLabelAttribute(string label)
        {
            Label = label;
        }
        public string Label { get; set; }
    }
}