using System;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.Scenarios
{
    /// <summary>
    /// Social network test scenario with tenant admin group for authorization testing
    /// </summary>
    public class SocialNetworkTestScenario : InMemoryScenarioProviderBase
    {
        public override string ScenarioName => "SocialNetworkTest";
        public override string Description => "Social network with NetworkAdmins group for testing tenant admin queries";

        protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
        {
            var networkId = "550e8401-e29b-41d4-a716-446655440001";
            var defaultUserId = "550e8400-e29b-41d4-a716-446655440000";

            var vertices = new ScenarioVertexDefinition[]
            {
                // NetworkAdmins group
                new ScenarioVertexDefinition("networkAdminsGroup", "socialEntity", Props(
                    ("pk", networkId),
                    ("name", "NetworkAdmins"),
                    ("builtIn", "true"),
                    ("entityType", "interestGroup")
                )),
                
                // Default test user
                new ScenarioVertexDefinition("defaultUser", "socialEntity", Props(
                    ("pk", networkId),
                    ("name", "Default Admin User"),
                    ("principalId", defaultUserId),
                    ("entityType", "person")
                ))
            };

            var edges = new ScenarioEdgeDefinition[]
            {
                // Membership edge from NetworkAdmins group to default user
                new ScenarioEdgeDefinition("members", "networkAdminsGroup", "defaultUser", Props(
                    ("principalId", defaultUserId)
                ))
            };

            return (vertices, edges);
        }
    }
}
