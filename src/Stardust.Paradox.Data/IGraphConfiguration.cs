namespace Stardust.Paradox.Data
{
    public interface IGraphConfiguration
    {
        IGraphConfiguration AddCollection<T>() where T : IVertex;
        IGraphConfiguration AddCollection<T>(string label) where T : IVertex;
    }
}