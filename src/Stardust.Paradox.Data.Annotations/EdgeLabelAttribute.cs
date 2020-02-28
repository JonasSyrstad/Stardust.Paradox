using System;

namespace Stardust.Paradox.Data.Annotations
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Interface)]
    [Obsolete("Use InLabelAttribute for clarity", false)]
    public class EdgeLabelAttribute : VertexLabelAttribute
    {
        public EdgeLabelAttribute(string label) : base(label)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Interface)]
    public class InLabelAttribute : EdgeLabelAttribute
    {
        public InLabelAttribute(string label) : base(label)
        {
        }
    }
}