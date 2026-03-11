using Microsoft.Extensions.DependencyInjection;
using Stardust.Paradox.GremlinStudio.Core.Connections;
using Stardust.Paradox.GremlinStudio.Core.Execution;
using Stardust.Paradox.GremlinStudio.Core.Export;
using Stardust.Paradox.GremlinStudio.Core.History;
using Stardust.Paradox.GremlinStudio.Core.Playground;
using Stardust.Paradox.GremlinStudio.Core.Schema;
using Stardust.Paradox.GremlinStudio.Core.Snippets;
using Stardust.Paradox.GremlinStudio.Core.Variables;
using Stardust.Paradox.GremlinStudio.Core.WelcomeGuide;

namespace Stardust.Paradox.GremlinStudio.Core;

/// <summary>
/// Extension methods for registering Gremlin Studio Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Gremlin Studio Core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGremlinStudioCore(this IServiceCollection services)
    {
        // Connection management
        services.AddSingleton<ISecureSecretStore, DpapiSecureSecretStore>();
        services.AddSingleton<IGremlinConnectionStore, FileGremlinConnectionStore>();
        services.AddSingleton<IGremlinConnectorFactory, GremlinNetConnectorFactory>();
        services.AddSingleton<IGremlinConnectionTester, GremlinConnectionTester>();
        services.AddSingleton<IGremlinConnectionExporter, GremlinConnectionExporter>();

        // Cosmos DB discovery
        services.AddHttpClient<ICosmosDbDiscoveryService, CosmosDbDiscoveryService>();

        // Query execution
        services.AddSingleton<IGremlinQueryExecutor, GremlinQueryExecutor>();

        // Playground
        services.AddSingleton<IPlaygroundService, PlaygroundService>();

        // Query history
        services.AddSingleton<IQueryHistoryService, FileQueryHistoryService>();

        // Scenario export
        services.AddSingleton<IScenarioExportService, ScenarioExportService>();

        // Agent skills download
        services.AddHttpClient<IAgentSkillsDownloadService, AgentSkillsDownloadService>();

        // Schema discovery and export
        services.AddSingleton<ISchemaDiscoveryService, SchemaDiscoveryService>();
        services.AddSingleton<ISchemaExportService, SchemaExportService>();

        // Welcome guide
        services.AddSingleton<IWelcomeGuideService, FileWelcomeGuideService>();

        // Snippets and variables
        services.AddSingleton<IQuerySnippetStore, FileQuerySnippetStore>();
        services.AddSingleton<IQueryVariableStore, FileQueryVariableStore>();

        return services;
    }
}
