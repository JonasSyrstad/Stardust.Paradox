using System.Collections.Generic;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Represents a graph path result from path() step
    /// </summary>
    public interface IGraphPath
    {
        /// <summary>
        /// Get objects in the path
        /// </summary>
        IReadOnlyList<object> Objects { get; }

        /// <summary>
        /// Get labels for steps in the path
        /// </summary>
        IReadOnlyList<IReadOnlyList<string>> Labels { get; }
    }
}