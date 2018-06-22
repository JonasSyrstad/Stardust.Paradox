using System.Collections.Generic;
using Stardust.Paradox.Data.Tree;

namespace Stardust.Paradox.Data
{
    public interface IVertexTreeRoot<T>: IEnumerable<VertexTree<T>> where T : IVertex
    {
        VertexTree<T> this[int index] { get; }
    }
}