using System.Collections.Generic;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IVertexTreeRoot<T> : IEnumerable<IVertexTree<T>> where T : IVertex
    {
        IVertexTree<T> this[int index] { get; }
    }
}