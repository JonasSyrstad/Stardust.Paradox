using System;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Scenarios
{
    /// <summary>
    /// Basic social network scenario with users, friendships, and posts
    /// </summary>
    public class BasicSocialNetworkScenario : InMemoryScenarioProviderBase
    {
        public override string ScenarioName => "BasicSocialNetwork";
        public override string Description => "Simple social network with users, friendships, and posts for testing traversals";

        protected override (ScenarioVertexDefinition[] vertices, SenarioEdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new ScenarioVertexDefinition[]
            {
                new ScenarioVertexDefinition("john", "person", Props(
                    ("name", "John Doe"),
                    ("age", 30),
                    ("city", "New York"),
                    ("verified", true)
                )),
                new ScenarioVertexDefinition("jane", "person", Props(
                    ("name", "Jane Smith"),
                    ("age", 28),
                    ("city", "Los Angeles"),
                    ("verified", false)
                )),
                new ScenarioVertexDefinition("bob", "person", Props(
                    ("name", "Bob Johnson"),
                    ("age", 35),
                    ("city", "Chicago"),
                    ("verified", false)
                )),
                new ScenarioVertexDefinition("alice", "person", Props(
                    ("name", "Alice Brown"),
                    ("age", 25),
                    ("city", "Seattle"),
                    ("verified", true)
                )),
                new ScenarioVertexDefinition("post1", "post", Props(
                    ("title", "Hello World"),
                    ("content", "My first post!"),
                    ("likes", 15),
                    ("timestamp", DateTime.UtcNow.AddHours(-2))
                )),
                new ScenarioVertexDefinition("post2", "post", Props(
                    ("title", "Travel Blog"),
                    ("content", "Amazing trip to Europe"),
                    ("likes", 42),
                    ("timestamp", DateTime.UtcNow.AddHours(-8))
                ))
            };

            var edges = new SenarioEdgeDefinition[]
            {
                new SenarioEdgeDefinition("knows", "john", "jane"),
                new SenarioEdgeDefinition("knows", "jane", "bob"),
                new SenarioEdgeDefinition("knows", "john", "alice"),
                new SenarioEdgeDefinition("knows", "bob", "alice"),
                new SenarioEdgeDefinition("authored", "john", "post1"),
                new SenarioEdgeDefinition("authored", "jane", "post2"),
                new SenarioEdgeDefinition("likes", "jane", "post1"),
                new SenarioEdgeDefinition("likes", "alice", "post1"),
                new SenarioEdgeDefinition("likes", "john", "post2"),
                new SenarioEdgeDefinition("likes", "bob", "post2")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
        {
            // Friends of friends query
            database.RegisterCustomResponse(@"g\.V\('john'\)\.out\('knows'\)\.out\('knows'\)\.dedup\(\)", (query, parameters) =>
            {
                return new[] { database.GetVertex("bob")?.ToGremlinResponse() };
            });

            // Verified users
            database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('person'\)\.has\('verified', true\)", (query, parameters) =>
            {
                var john = database.GetVertex("john")?.ToGremlinResponse();
                var alice = database.GetVertex("alice")?.ToGremlinResponse();
                return new[] { john, alice }.Where(x => x != null);
            });
        }
    }
}