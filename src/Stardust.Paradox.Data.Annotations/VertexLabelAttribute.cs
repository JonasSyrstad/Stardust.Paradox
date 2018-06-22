using System;

namespace Stardust.Paradox.Data
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class VertexLabelAttribute : Attribute
    {
        public VertexLabelAttribute(string label)
        {
            Label = label;
        }

        public string Label { get; set; }
    }
}