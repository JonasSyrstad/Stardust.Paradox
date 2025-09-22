using System;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.Scenarios
{
    /// <summary>
    /// Interface for providing pre-built test scenarios for InMemory database
    /// </summary>
    public interface IInMemoryScenarioProvider
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
        /// Configure the InMemory database with scenario-specific data and responses
        /// </summary>
        /// <param name="database">The database to configure</param>
        void ConfigureScenario(InMemoryGraphDatabase database);
    }
}
