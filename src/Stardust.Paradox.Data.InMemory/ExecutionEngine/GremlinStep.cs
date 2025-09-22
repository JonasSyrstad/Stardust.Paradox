using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine;

/// <summary>
/// Represents a parsed Gremlin step with enhanced metadata
/// </summary>
public class GremlinStep
{
    public string StepName { get; set; }
    public List<object> Arguments { get; set; }
    public Dictionary<string, object> Modifiers { get; set; }
    public List<string> StepLabels { get; set; }
    public bool IsBarrier { get; set; }
    public bool IsLocalStep { get; set; }

    public GremlinStep(string stepName)
    {
        StepName = stepName;
        Arguments = new List<object>();
        Modifiers = new Dictionary<string, object>();
        StepLabels = new List<string>();
        IsBarrier = false;
        IsLocalStep = false;
    }

    public void AddLabel(string label)
    {
        if (!string.IsNullOrEmpty(label) && !StepLabels.Contains(label))
        {
            StepLabels.Add(label);
        }
    }

    public override string ToString()
    {
        var args = Arguments.Count > 0 ? $"({string.Join(", ", Arguments)})" : "()";
        var labels = StepLabels.Count > 0 ? $".as('{string.Join("', '", StepLabels)}')" : "";
        return $"{StepName}{args}{labels}";
    }
}
