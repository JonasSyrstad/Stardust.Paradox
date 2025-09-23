using System;
using System.Collections.Generic;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace GeneratedScenarios
{
    /// <summary>
    /// Sample exported user network scenario for demonstration
    /// Exported from: DemoCosmosDB
    /// Export Date: 2025-01-27 10:00:00 UTC
    /// </summary>
    public class SampleUserNetworkScenario : InMemoryScenarioProviderBase
    {
        public override string ScenarioName => "SampleUserNetwork";
        public override string Description => "Sample exported user network scenario for demonstration";

        protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new ScenarioVertexDefinition[]
            {
                new ScenarioVertexDefinition("user1", "user", Props(
                ("name", "Alice Johnson"),
                ("email", "alice@example.com"),
                ("verified", true),
                ("joinDate", "2023-01-15"),
                ("age", 28),
                ("department", "Engineering"),
                ("location", "Seattle"),
                ("lastLogin", "2025-01-26T15:30:00Z")
                )),
                new ScenarioVertexDefinition("user2", "user", Props(
                ("name", "Bob Smith"),
                ("email", "bob@example.com"),
                ("verified", true),
                ("joinDate", "2023-02-20"),
                ("age", 35),
                ("department", "Marketing"),
                ("location", "San Francisco"),
                ("lastLogin", "2025-01-27T09:15:00Z")
                )),
                new ScenarioVertexDefinition("user3", "user", Props(
                ("name", "Carol Davis"),
                ("email", "carol@example.com"),
                ("verified", false),
                ("joinDate", "2023-03-10"),
                ("age", 31),
                ("department", "Sales"),
                ("location", "New York"),
                ("lastLogin", "2025-01-25T18:45:00Z")
                ))
            };

            var edges = new ScenarioEdgeDefinition[]
            {
                new ScenarioEdgeDefinition("follows", "user1", "user2", "edge1", Props(
                ("since", "2023-01-20"),
                ("strength", 0.8d),
                ("interactionCount", 45),
                ("lastInteraction", "2025-01-26T14:22:00Z"),
                ("type", "professional")
                )),
                new ScenarioEdgeDefinition("follows", "user2", "user3", "edge2", Props(
                ("since", "2023-03-15"),
                ("strength", 0.6d),
                ("interactionCount", 23),
                ("lastInteraction", "2025-01-25T11:30:00Z"),
                ("type", "personal")
                )),
                new ScenarioEdgeDefinition("collaborates", "user1", "user3", "edge3", Props(
                ("projectName", "DataMigration2024"),
                ("startDate", "2024-06-01"),
                ("status", "active"),
                ("priority", "high")
                ))
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
        {
            // Add any custom query responses here if needed
            base.ConfigureCustomResponses(database);
        }
    }
}