using System.Collections.Generic;

namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Interface for providing pre-built test scenarios
    /// </summary>
    public interface IScenarioProvider
    {
        /// <summary>
        /// The unique name/identifier for this scenario
        /// </summary>
        string ScenarioName { get; }
        
        /// <summary>
        /// A brief description of what this scenario provides
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Configure the connector with scenario-specific data and responses
        /// </summary>
        /// <param name="connector">The connector to configure</param>
        void ConfigureScenario(MockGremlinLanguageConnector connector);
    }

    /// <summary>
    /// Base class for scenario providers with common functionality
    /// </summary>
    public abstract class ScenarioProviderBase : IScenarioProvider
    {
        public abstract string ScenarioName { get; }
        public abstract string Description { get; }
        
        public virtual void ConfigureScenario(MockGremlinLanguageConnector connector)
        {
            // Configure vertices and edges
            var (vertices, edges) = GetScenarioData();
            if (vertices?.Length > 0 || edges?.Length > 0)
            {
                connector.QuickPopulate(vertices, edges);
            }

            // Configure custom responses
            ConfigureCustomResponses(connector);
        }

        /// <summary>
        /// Override to provide vertex and edge definitions for the scenario
        /// </summary>
        /// <returns>Tuple of vertices and edges</returns>
        protected virtual (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            return (null, null);
        }

        /// <summary>
        /// Override to configure custom response patterns beyond basic data
        /// </summary>
        /// <param name="connector">The connector to configure</param>
        protected virtual void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Override in derived classes for custom response patterns
        }

        /// <summary>
        /// Helper method to create property KeyValuePairs
        /// </summary>
        protected KeyValuePair<string, object> Prop(string key, object value)
        {
            return new KeyValuePair<string, object>(key, value);
        }

        /// <summary>
        /// Helper method to create multiple properties
        /// </summary>
        protected KeyValuePair<string, object>[] Props(params (string key, object value)[] properties)
        {
            var result = new KeyValuePair<string, object>[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                result[i] = new KeyValuePair<string, object>(properties[i].key, properties[i].value);
            }
            return result;
        }
    }
}