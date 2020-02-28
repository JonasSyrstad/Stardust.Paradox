using System;

namespace Stardust.Paradox.Data.Annotations
{
    /// <summary>
    ///     Use with care, may cause entire graph to be loaded!!
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EagerAttribute : Attribute
    {
    }
}