namespace Stardust.Paradox.GremlinStudio.Core.Variables;

public sealed record QueryVariableSet(
    string Id,
    string Name,
    string Json,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt);
