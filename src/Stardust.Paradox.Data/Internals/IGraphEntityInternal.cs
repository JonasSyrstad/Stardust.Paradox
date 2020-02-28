using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Internals
{
    public interface IGraphEntityInternal : IGraphEntity
    {
        bool IsDirty { get; }
        bool IsDeleted { get; }
        string EntityKey { get; set; }
        bool EagerLoading { get; set; }
        string _EntityType { get; }
        string GetUpdateStatement(bool parameterized);
        void Reset(bool b);
        void Delete();
        void SetContext(GraphContextBase graphContextBase, bool connectorCanParameterizeQueries);
        Task Eager(bool doEagerLoad);
        void DoLoad(dynamic o);
        void OnPropertyChanged(object value, string propertyName = null);
        bool OnPropertyChanging(object newValue, object oldValue, string propertyName = null);

        Dictionary<string, object> GetParameterizedValues();

        void RegisterNotifiable(string propName, IComplexProperty notifiable);
    }
}