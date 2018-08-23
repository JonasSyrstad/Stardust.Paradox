using System.Collections.Generic;

namespace Stardust.Paradox.Data.Annotations
{
    public interface IEdge
    {
    }

    public interface IEdge<T> : IEdge where T : IVertex
    {
        T Vertex { get; set; }

        string EdgeType { get; }

        IDictionary<string ,object> Properties { get; }
    }
}