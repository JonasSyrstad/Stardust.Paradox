namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Represents the type of a Gremlin step parameter.
/// </summary>
public enum GremlinParamType
{
    String,
    Number,
    Boolean,
    Predicate,
    Traversal,
    Label,
    PropertyKey,
    Value,
    Enum,
    Any
}

/// <summary>
/// Describes a single parameter in a Gremlin step overload.
/// </summary>
public record GremlinParam(string Name, GremlinParamType Type, bool IsOptional = false, string? Description = null)
{
    /// <summary>
    /// Gets a human-readable type name.
    /// </summary>
    public string TypeName => Type switch
    {
        GremlinParamType.String => "string",
        GremlinParamType.Number => "number",
        GremlinParamType.Boolean => "boolean",
        GremlinParamType.Predicate => "predicate",
        GremlinParamType.Traversal => "traversal",
        GremlinParamType.Label => "label (string)",
        GremlinParamType.PropertyKey => "key (string)",
        GremlinParamType.Value => "value",
        GremlinParamType.Enum => "enum",
        GremlinParamType.Any => "object",
        _ => "object"
    };
}

/// <summary>
/// Describes a single overload (parameter permutation) of a Gremlin step.
/// </summary>
public record GremlinStepOverload(GremlinParam[] Parameters, string? Description = null)
{
    /// <summary>
    /// Gets a formatted signature string such as "(key, value)" or "(label)".
    /// </summary>
    public string Signature
    {
        get
        {
            if (Parameters.Length == 0)
                return "()";

            var parts = Parameters.Select(p => p.IsOptional ? $"[{p.Name}: {p.TypeName}]" : $"{p.Name}: {p.TypeName}");
            return $"({string.Join(", ", parts)})";
        }
    }
}

/// <summary>
/// Full definition of a Gremlin step including all overloads and metadata.
/// </summary>
public record GremlinStepDefinition(
    string Name,
    string Description,
    GremlinCompletionKind Kind,
    GremlinStepOverload[] Overloads)
{
    /// <summary>
    /// Returns true if the step has at least one overload that requires parameters.
    /// </summary>
    public bool HasRequiredParameters =>
        Overloads.Any(o => o.Parameters.Length > 0 && o.Parameters.Any(p => !p.IsOptional));

    /// <summary>
    /// Returns true if all overloads have zero parameters.
    /// </summary>
    public bool IsParameterless =>
        Overloads.All(o => o.Parameters.Length == 0);

    /// <summary>
    /// Builds a rich tooltip string with description and all overloads.
    /// </summary>
    public string GetTooltipText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Description);
        sb.AppendLine();

        if (Overloads.Length == 1)
        {
            sb.Append($"{Name}{Overloads[0].Signature}");
            if (Overloads[0].Description != null)
            {
                sb.AppendLine();
                sb.Append($"  {Overloads[0].Description}");
            }
        }
        else
        {
            sb.AppendLine("Overloads:");
            for (int i = 0; i < Overloads.Length; i++)
            {
                var ov = Overloads[i];
                sb.Append($"  {i + 1}. {Name}{ov.Signature}");
                if (ov.Description != null)
                {
                    sb.Append($" — {ov.Description}");
                }
                if (i < Overloads.Length - 1)
                    sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the text to insert when this step is selected in autocomplete.
    /// Steps with parameters insert "name()" with cursor between parens.
    /// Steps without parameters insert "name()".
    /// </summary>
    public string CompletionText => $"{Name}()";
}
