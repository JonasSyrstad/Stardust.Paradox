namespace Stardust.Paradox.GremlinStudio.Core.Snippets;

public sealed record QuerySnippet(
    string Id,
    string Name,
    string Query,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt);
