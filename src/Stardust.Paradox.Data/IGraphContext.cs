using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using Stardust.Paradox.Data.Tree;

namespace Stardust.Paradox.Data
{
    public interface IGraphContext
    {
        void Delete<T>(T toBeDeleted) where T : IGraphEntity;
        void ResetChanges<T>(T entityToReset) where T : IGraphEntity;
        T CreateEntity<T>(string id) where T : IGraphEntity;
        GremlinQuery V<T>() where T : IVertex;
        GremlinQuery V<T>(string id) where T : IVertex;
        Task<T> VAsync<T>(string id) where T : IVertex;

        Task<T> GetOrCreate<T>(string id) where T : IVertex;

        Task<IEnumerable<T>> VAsync<T>(Func<GremlinContext, GremlinQuery> g) where T : IVertex;
        Task<IEnumerable<T>> VAsync<T>(GremlinQuery g) where T : IVertex;
        Task SaveChangesAsync();
        Task<IEnumerable<dynamic>> ExecuteAsync<T>(Func<GremlinContext, GremlinQuery> func);

        Task<IVertexTreeRoot<T>> GetTreeAsync<T>(string rootId, string edgeLabel, bool incommingEdge = false) where T : IVertex;

        Task<IVertexTreeRoot<T>> GetTreeAsync<T>(string rootId, Expression<Func<T, object>> byProperty, bool incommingEdge = false) where T : IVertex;
        void Attach<T>(T item);

        void Clear();

        event SavingChangesHandler SavingChanges;
        event SavingChangesHandler ChangesSaved;
        event SavingChangesHandler SaveChangesError;

        Task<IEnumerable<T>> EAsync<T>(Func<GremlinContext, GremlinQuery> g) where T : IEdgeEntity;

        Task<T> EAsync<T>(string id) where T : IEdgeEntity;

        Task<IEnumerable<T>> EAsync<T>(GremlinQuery g) where T : IEdgeEntity;
        
    }
}