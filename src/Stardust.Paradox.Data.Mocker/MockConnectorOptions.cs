namespace Stardust.Paradox.Data.Mocker
{
    /// <summary>
    /// Configuration options for the mock connector
    /// </summary>
    public class MockConnectorOptions
    {
        /// <summary>
        /// Whether to log queries and parameters
        /// </summary>
        public bool LogQueries { get; set; } = false;

        /// <summary>
        /// Simulated RU cost per query execution
        /// </summary>
        public double SimulatedRUPerQuery { get; set; } = 2.5;

        /// <summary>
        /// Whether to enable automatic operation simulation
        /// </summary>
        public bool EnableOperationSimulation { get; set; } = true;

        /// <summary>
        /// Whether to maintain an in-memory graph state
        /// </summary>
        public bool MaintainGraphState { get; set; } = true;

        /// <summary>
        /// Whether to auto-generate IDs for new elements
        /// </summary>
        public bool AutoGenerateIds { get; set; } = true;

        /// <summary>
        /// Default properties to include in auto-generated vertices
        /// </summary>
        public string[] DefaultVertexProperties { get; set; } = new[] { "name", "createdAt" };

        /// <summary>
        /// Whether to enable intelligent pattern matching
        /// </summary>
        public bool EnableSmartMatching { get; set; } = true;
    }

    /// <summary>
    /// Types of Gremlin operations
    /// </summary>
    public enum OperationType
    {
        Query,
        AddVertex,
        AddEdge,
        Update,
        Delete
    }
}