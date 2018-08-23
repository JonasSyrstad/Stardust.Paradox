using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Annotations.DataTypes
{
    public interface INullable<T> where T : IVertex
    {
        bool HasValue { get; }

        Task<bool> HasValueAsync();
    }
}