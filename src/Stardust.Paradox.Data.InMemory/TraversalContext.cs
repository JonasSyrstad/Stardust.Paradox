using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory;

/// <summary>
/// Execution context for Gremlin traversals (legacy - use TinkerTraversalContext for new code)
/// </summary>
public class TraversalContext
{
    public IEnumerable<dynamic> CurrentResults { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public Dictionary<string, List<dynamic>> Aggregates { get; set; }
    public List<List<dynamic>> Paths { get; set; }

    public TraversalContext()
    {
        CurrentResults = new List<dynamic>();
        Variables = new Dictionary<string, object>();
        Aggregates = new Dictionary<string, List<dynamic>>();
        Paths = new List<List<dynamic>>();
    }
}