using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine;

/// <summary>
/// Represents a complete parsed Gremlin query with metadata
/// </summary>
public class ParsedGremlinQuery
{
    public List<GremlinStep> Steps { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public Dictionary<string, int> StepLabelIndex { get; set; }
    public bool HasSideEffects { get; set; }
    public bool HasBarriers { get; set; }

    public ParsedGremlinQuery()
    {
        Steps = new List<GremlinStep>();
        Parameters = new Dictionary<string, object>();
        StepLabelIndex = new Dictionary<string, int>();
        HasSideEffects = false;
        HasBarriers = false;
    }

    public void IndexStepLabels()
    {
        StepLabelIndex.Clear();
        for (int i = 0; i < Steps.Count; i++)
        {
            foreach (var label in Steps[i].StepLabels)
            {
                StepLabelIndex[label] = i;
            }
        }
    }

    public override string ToString()
    {
        return string.Join(".", Steps.Select(s => s.ToString()));
    }
}
