using System;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Extensions;

namespace Stardust.Paradox.Data.InMemory.Factory;

/// <summary>
/// Factory methods for creating InMemory connectors with scenarios
/// </summary>
public static class InMemoryConnectorFactory
{
    /// <summary>
    /// Create a connector with a specific scenario
    /// </summary>
    /// <param name="scenarioName">Name of the scenario</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Configured connector</returns>
    public static InMemoryGremlinLanguageConnector CreateWithScenario(string scenarioName, Action<InMemoryDatabaseOptions> configure = null)
    {
        var options = new InMemoryDatabaseOptions();
        configure?.Invoke(options);
        return InMemoryScenarioExtensions.CreateWithScenario(scenarioName, options);
    }

    /// <summary>
    /// Create a connector with multiple scenarios
    /// </summary>
    /// <param name="scenarioNames">Names of the scenarios</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Configured connector</returns>
    public static InMemoryGremlinLanguageConnector CreateWithScenarios(string[] scenarioNames, Action<InMemoryDatabaseOptions> configure = null)
    {
        var options = new InMemoryDatabaseOptions();
        configure?.Invoke(options);
        return InMemoryScenarioExtensions.CreateWithScenarios(scenarioNames, options);
    }

    /// <summary>
    /// Create a connector with multiple scenarios
    /// </summary>
    /// <param name="configure">Configuration action</param>
    /// <param name="scenarioNames">Names of the scenarios</param>
    /// <returns>Configured connector</returns>
    public static InMemoryGremlinLanguageConnector CreateWithScenarios(Action<InMemoryDatabaseOptions> configure, params string[] scenarioNames)
    {
        return CreateWithScenarios(scenarioNames, configure);
    }

    /// <summary>
    /// Create a basic connector for testing (no scenarios)
    /// </summary>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Empty connector ready for custom setup</returns>
    public static InMemoryGremlinLanguageConnector CreateForTesting(Action<InMemoryDatabaseOptions> configure = null)
    {
        var options = new InMemoryDatabaseOptions();
        configure?.Invoke(options);
        return new InMemoryGremlinLanguageConnector(options);
    }

    /// <summary>
    /// Create a connector with the BasicSocialNetwork scenario (convenience method)
    /// </summary>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Connector with social network data</returns>
    public static InMemoryGremlinLanguageConnector CreateSocialNetwork(Action<InMemoryDatabaseOptions> configure = null)
    {
        return CreateWithScenario("BasicSocialNetwork", configure);
    }

    /// <summary>
    /// Create a connector with the SimpleECommerce scenario (convenience method)
    /// </summary>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Connector with e-commerce data</returns>
    public static InMemoryGremlinLanguageConnector CreateECommerce(Action<InMemoryDatabaseOptions> configure = null)
    {
        return CreateWithScenario("SimpleECommerce", configure);
    }

    /// <summary>
    /// Create a connector with the OrganizationHierarchy scenario (convenience method)
    /// </summary>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Connector with organization data</returns>
    public static InMemoryGremlinLanguageConnector CreateOrganization(Action<InMemoryDatabaseOptions> configure = null)
    {
        return CreateWithScenario("OrganizationHierarchy", configure);
    }

    /// <summary>
    /// Create a connector with the UserRoleManagement scenario (convenience method)
    /// </summary>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Connector with user management data</returns>
    public static InMemoryGremlinLanguageConnector CreateUserManagement(Action<InMemoryDatabaseOptions> configure = null)
    {
        return CreateWithScenario("UserRoleManagement", configure);
    }
}
