using Stardust.Paradox.Data;

namespace Stardust.Paradox.GremlinStudio.Core.Export;

/// <summary>
/// Service for exporting query results to scenario files.
/// </summary>
public interface IScenarioExportService
{
    /// <summary>
    /// Parses query result JSON into scenario data (vertices only from JSON).
    /// </summary>
    /// <param name="resultJson">The raw JSON result from a query.</param>
    /// <param name="scenarioName">Name for the scenario.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="sourceConnection">Source connection name.</param>
    /// <param name="query">The query that produced the results.</param>
    ScenarioData ParseQueryResults(string resultJson, string scenarioName, string? description, string? sourceConnection, string? query);

    /// <summary>
    /// Fetches edges between the vertices in the scenario data from the database.
    /// This matches the behavior of Stardust.Paradox.Data.ScenarioConnector.
    /// </summary>
    /// <param name="data">The scenario data containing vertices.</param>
    /// <param name="connector">The Gremlin connector to use for queries.</param>
    /// <param name="progress">Optional progress reporter for batch status updates.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The updated scenario data with edges fetched from the database.</returns>
    Task<ScenarioData> FetchEdgesAsync(ScenarioData data, IGremlinLanguageConnector connector, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports scenario data to the specified format.
    /// </summary>
    /// <param name="data">The scenario data to export.</param>
    /// <param name="format">The export format.</param>
    /// <returns>The formatted content string.</returns>
    string Export(ScenarioData data, ScenarioExportFormat format);

    /// <summary>
    /// Saves the exported scenario to a file.
    /// </summary>
    /// <param name="data">The scenario data.</param>
    /// <param name="format">The export format.</param>
    /// <param name="filePath">The file path to save to.</param>
    Task SaveAsync(ScenarioData data, ScenarioExportFormat format, string filePath);
}
