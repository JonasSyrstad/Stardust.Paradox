using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property|AttributeTargets.Interface)]
    public class EdgeLabelAttribute : VertexLabelAttribute
    {
        public EdgeLabelAttribute(string label) : base(label)
        {
        }
    }
}