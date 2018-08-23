using System.Collections.Generic;

namespace Stardust.Paradox.Data.Annotations.DataTypes
{
    public interface IVertexTree
    {
        List<IVertexTree> ToList();
        object GetKey();
    }
}