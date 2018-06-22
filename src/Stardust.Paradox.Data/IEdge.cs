namespace Stardust.Paradox.Data
{
    public interface IEdge
    {
    }

    public interface IEdge<T> : IEdge where T : IVertex
    {
        T Vertex { get; set; }

        string EdgeType { get; }
    }
}