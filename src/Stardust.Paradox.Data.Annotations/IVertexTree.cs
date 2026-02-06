using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IVertexTree<T> : IVertexTreeRoot<T>, IVertexTree where T : IVertex
    {
        T Key { get; }
    }
}