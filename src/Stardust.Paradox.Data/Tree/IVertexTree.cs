using System.Collections.Generic;

namespace Stardust.Paradox.Data.Tree
{
    public interface IVertexTree
    {
        List<IVertexTree> ToList();
        object GetKey();
    }
}