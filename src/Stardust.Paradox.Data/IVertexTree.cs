using Stardust.Paradox.Data.Tree;

namespace Stardust.Paradox.Data
{
    public interface IVertexTree<T> : IVertexTreeRoot<T>, IVertexTree where T : IVertex
    {
        T Key { get; }
    }
}