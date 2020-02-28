using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ToWayEdgeLabelAttribute : Attribute
    {
        public ToWayEdgeLabelAttribute(string label)
        {
            Label = label;
        }

        public string Label { get; set; }
    }
}