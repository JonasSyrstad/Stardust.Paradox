using Stardust.Paradox.Data.Annotations;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data.Internals
{
    public interface IGraphEntityInternal : IGraphEntity
    {
        bool IsDirty { get; }
        bool IsDeleted { get; }
        string EntityKey { get; set; }
        bool EagerLoading { get; set; }
        string GetUpdateStatement();
        void Reset(bool b);
        void Delete();
        void SetContext(GraphContextBase graphContextBase);
        Task Eager(bool doEagerLoad);
        void DoLoad(dynamic o);
    }
}