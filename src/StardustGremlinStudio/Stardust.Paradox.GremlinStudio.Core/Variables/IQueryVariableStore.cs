namespace Stardust.Paradox.GremlinStudio.Core.Variables;

public interface IQueryVariableStore
{
    Task<IReadOnlyList<QueryVariableSet>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<QueryVariableSet?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(QueryVariableSet variables, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
