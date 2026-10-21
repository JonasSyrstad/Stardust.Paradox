namespace Stardust.Paradox.GremlinStudio.Core.Snippets;

public interface IQuerySnippetStore
{
    Task<IReadOnlyList<QuerySnippet>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<QuerySnippet?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(QuerySnippet snippet, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
