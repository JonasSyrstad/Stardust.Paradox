using System;

namespace Stardust.Paradox.Data
{
    /// <summary>
    /// Use with care, may cause entire graph to be loaded!!
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EagerAttribute : Attribute
    {
    }
}