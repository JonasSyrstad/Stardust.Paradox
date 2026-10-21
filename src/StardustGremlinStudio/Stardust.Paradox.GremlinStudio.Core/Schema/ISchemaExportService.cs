namespace Stardust.Paradox.GremlinStudio.Core.Schema;

/// <summary>
/// Service for exporting graph schema to Stardust.Paradox entity interfaces.
/// </summary>
public interface ISchemaExportService
{
    /// <summary>
    /// Exports the discovered schema to Stardust.Paradox interface code.
    /// </summary>
    /// <param name="schema">The discovered graph schema.</param>
    /// <param name="options">Export options.</param>
    /// <returns>The generated C# code.</returns>
    string ExportToCode(GraphSchema schema, SchemaExportOptions options);

    /// <summary>
    /// Exports the schema to a JSON representation.
    /// </summary>
    /// <param name="schema">The discovered graph schema.</param>
    /// <returns>JSON representation of the schema.</returns>
    string ExportToJson(GraphSchema schema);
}

/// <summary>
/// Options for schema export.
/// </summary>
public class SchemaExportOptions
{
    /// <summary>
    /// The namespace for generated interfaces.
    /// </summary>
    public string Namespace { get; set; } = "MyApp.Graph.Entities";

    /// <summary>
    /// The name for the generated GraphContext class.
    /// </summary>
    public string ContextClassName { get; set; } = "MyGraphContext";

    /// <summary>
    /// Whether to generate edge interfaces for edges with properties.
    /// </summary>
    public bool GenerateTypedEdges { get; set; } = true;

    /// <summary>
    /// Whether to include navigation properties on vertex interfaces.
    /// </summary>
    public bool GenerateNavigationProperties { get; set; } = true;

    /// <summary>
    /// Whether to add XML documentation comments.
    /// </summary>
    public bool IncludeXmlDocumentation { get; set; } = true;

    /// <summary>
    /// Whether to include sample values in comments.
    /// </summary>
    public bool IncludeSampleValuesInComments { get; set; } = false;

    /// <summary>
    /// Whether to generate the GraphContext class.
    /// </summary>
    public bool GenerateContext { get; set; } = true;

    /// <summary>
    /// Whether to use file-scoped namespaces (C# 10+).
    /// </summary>
    public bool UseFileScopedNamespace { get; set; } = true;

    /// <summary>
    /// Whether to use nullable reference types.
    /// </summary>
    public bool UseNullableReferenceTypes { get; set; } = true;
}
