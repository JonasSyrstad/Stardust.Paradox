using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property)]
    [Obsolete("Use OutLabelAttribute for clarity",false)]
    public class ReverseEdgeLabelAttribute : Attribute
    {
        public ReverseEdgeLabelAttribute(string label)
        {
            ReverseLabel = label;
        }
        public string ReverseLabel { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class OutLabelAttribute : ReverseEdgeLabelAttribute
    {
        public OutLabelAttribute(string label) : base(label)
        {
        }
    }
}