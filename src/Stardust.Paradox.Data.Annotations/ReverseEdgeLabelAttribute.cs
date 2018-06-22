using System;

namespace Stardust.Paradox.Data
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ReverseEdgeLabelAttribute : Attribute
    {
        public ReverseEdgeLabelAttribute(string label)
        {
            ReverseLabel = label;
        }
        public string ReverseLabel { get; set; }
    }
}