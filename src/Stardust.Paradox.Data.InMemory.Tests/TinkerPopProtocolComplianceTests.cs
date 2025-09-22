using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests for TinkerPop protocol compliance and session management
    /// Validates proper handling of TinkerPop operations, authentication, and session state
    /// </summary>
    public class TinkerPopProtocolComplianceTests : IDisposable
    {
        private readonly InMemoryGremlinLanguageConnector _connector;
        private readonly TinkerPopSessionManager _sessionManager;
        private readonly GremlinServerOptions _options;

        public TinkerPopProtocolComplianceTests()
        {
            _options = new GremlinServerOptions
            {
                EnableLogging = false,
                EnableDebugLogging = false
            };
            _connector = new InMemoryGremlinLanguageConnector();
            _sessionManager = new TinkerPopSessionManager(_options, _connector);
        }

        [Fact]
        public void TinkerPopSessionManager_Should_Create_And_Manage_Sessions()
        {
            // Arrange
            var connectionId = "test-connection-1";

            // Act
            var session = _sessionManager.CreateSession(connectionId);

            // Assert
            session.Should().NotBeNull();
            session.ConnectionId.Should().Be(connectionId);
            session.IsAuthenticated.Should().BeTrue(); // No auth configured
            session.GraphSONVersion.Should().Be(GraphSONVersion.V3);
            session.Serializer.Should().NotBeNull();
            session.Variables.Should().NotBeNull();
            session.Configuration.Should().NotBeNull();

            // Test session retrieval
            var retrievedSession = _sessionManager.GetSession(connectionId);
            retrievedSession.Should().Be(session);

            // Test session removal
            _sessionManager.RemoveSession(connectionId);
            var removedSession = _sessionManager.GetSession(connectionId);
            removedSession.Should().BeNull();
        }

        [Theory]
        [InlineData(TinkerPopOperations.Eval)]
        [InlineData(TinkerPopOperations.Authentication)]
        [InlineData(TinkerPopOperations.Close)]
        [InlineData("evaluate")] // Alternative operation name
        [InlineData("bytecode")] // Bytecode operation
        public async Task TinkerPopSessionManager_Should_Handle_Different_Operations(string operation)
        {
            // Arrange
            var connectionId = "test-connection-2";
            var session = _sessionManager.CreateSession(connectionId);
            var requestId = Guid.NewGuid();

            var message = new TinkerPopMessage
            {
                RequestId = requestId,
                Op = operation,
                Processor = "",
                Args = new Dictionary<string, object>()
            };

            // Add appropriate arguments based on operation
            if (operation == TinkerPopOperations.Eval || operation == "evaluate" || operation == "bytecode")
            {
                message.Args["gremlin"] = "g.inject(42)";
                message.Args["bindings"] = new Dictionary<string, object>();
                message.Args["language"] = "gremlin-groovy";
            }

            // Act
            var response = await _sessionManager.ProcessMessageAsync(connectionId, message);

            // Assert
            response.Should().NotBeNull();
            response.RequestId.Should().Be(requestId);
            response.Status.Should().NotBeNull();

            if (operation == TinkerPopOperations.Eval || operation == "evaluate" || operation == "bytecode")
            {
                response.Status.Code.Should().Be(TinkerPopStatusCodes.Success);
                response.Result.Should().NotBeNull();
                response.Result.Data.Should().NotBeNull();
            }
            else if (operation == TinkerPopOperations.Close)
            {
                response.Status.Code.Should().Be(TinkerPopStatusCodes.Success);
            }
        }

        [Fact]
        public async Task TinkerPopSessionManager_Should_Handle_Authentication_Flow()
        {
            // Arrange
            var authOptions = new GremlinServerOptions
            {
                Authentication = new GremlinAuthenticationOptions
                {
                    EnableBasicAuth = true,
                    Username = "testuser",
                    Password = "testpass"
                }
            };

            var authSessionManager = new TinkerPopSessionManager(authOptions, _connector);
            var connectionId = "auth-test-connection";
            var session = authSessionManager.CreateSession(connectionId);
            
            session.IsAuthenticated.Should().BeFalse(); // Auth required

            // Test unauthenticated eval request
            var evalMessage = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Eval,
                Args = new Dictionary<string, object>
                {
                    ["gremlin"] = "g.inject(42)"
                }
            };

            var evalResponse = await authSessionManager.ProcessMessageAsync(connectionId, evalMessage);
            evalResponse.Status.Code.Should().Be(TinkerPopStatusCodes.Unauthorized);

            // Test authentication challenge
            var authMessage = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Authentication,
                Args = new Dictionary<string, object>()
            };

            var authChallengeResponse = await authSessionManager.ProcessMessageAsync(connectionId, authMessage);
            authChallengeResponse.Status.Code.Should().Be(TinkerPopStatusCodes.Authenticate);
            authChallengeResponse.Status.Attributes.Should().ContainKey("sasl");

            // Test successful authentication
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0testuser\0testpass"));
            var authSuccessMessage = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Authentication,
                Args = new Dictionary<string, object>
                {
                    ["sasl"] = "PLAIN",
                    ["saslMechanism"] = credentials
                }
            };

            var authSuccessResponse = await authSessionManager.ProcessMessageAsync(connectionId, authSuccessMessage);
            authSuccessResponse.Status.Code.Should().Be(TinkerPopStatusCodes.Success);

            // Verify session is now authenticated
            var updatedSession = authSessionManager.GetSession(connectionId);
            updatedSession.IsAuthenticated.Should().BeTrue();
            updatedSession.Username.Should().Be("testuser");

            // Test eval request after authentication
            var postAuthEvalResponse = await authSessionManager.ProcessMessageAsync(connectionId, evalMessage);
            postAuthEvalResponse.Status.Code.Should().Be(TinkerPopStatusCodes.Success);
        }

        [Theory]
        [InlineData("gremlin", "script", "query", "traversal")] // Different query argument names
        public async Task TinkerPopSessionManager_Should_Extract_Gremlin_From_Various_Argument_Names(params string[] argumentNames)
        {
            // Arrange
            var connectionId = "arg-test-connection";
            var session = _sessionManager.CreateSession(connectionId);

            foreach (var argName in argumentNames)
            {
                var message = new TinkerPopMessage
                {
                    RequestId = Guid.NewGuid(),
                    Op = TinkerPopOperations.Eval,
                    Args = new Dictionary<string, object>
                    {
                        [argName] = "g.inject(42)",
                        ["bindings"] = new Dictionary<string, object>()
                    }
                };

                // Act
                var response = await _sessionManager.ProcessMessageAsync(connectionId, message);

                // Assert
                response.Should().NotBeNull();
                response.Status.Code.Should().Be(TinkerPopStatusCodes.Success);
                response.Result.Data.Should().NotBeEmpty();
                response.Result.Data.First().Should().Be(42);
            }
        }

        [Theory]
        [InlineData("bindings", "parameters")] // Different parameter argument names
        public async Task TinkerPopSessionManager_Should_Extract_Bindings_From_Various_Argument_Names(params string[] parameterNames)
        {
            // Arrange
            var connectionId = "param-test-connection";
            var session = _sessionManager.CreateSession(connectionId);

            foreach (var paramName in parameterNames)
            {
                var message = new TinkerPopMessage
                {
                    RequestId = Guid.NewGuid(),
                    Op = TinkerPopOperations.Eval,
                    Args = new Dictionary<string, object>
                    {
                        ["gremlin"] = "g.inject(x)",
                        [paramName] = new Dictionary<string, object> { ["x"] = 123 }
                    }
                };

                // Act
                var response = await _sessionManager.ProcessMessageAsync(connectionId, message);

                // Assert
                response.Should().NotBeNull();
                response.Status.Code.Should().Be(TinkerPopStatusCodes.Success);
                response.Result.Data.Should().NotBeEmpty();
                response.Result.Data.First().Should().Be(123);
            }
        }

        [Theory]
        [InlineData("gremlin-groovy")]
        [InlineData("gremlin")]
        [InlineData("groovy")]
        [InlineData("gremlin-dotnet")]
        [InlineData("gremlin-java")]
        [InlineData("gremlin-python")]
        public async Task TinkerPopSessionManager_Should_Support_Multiple_Languages(string language)
        {
            // Arrange
            var connectionId = "lang-test-connection";
            var session = _sessionManager.CreateSession(connectionId);

            var message = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Eval,
                Args = new Dictionary<string, object>
                {
                    ["gremlin"] = "g.inject(42)",
                    ["language"] = language,
                    ["bindings"] = new Dictionary<string, object>()
                }
            };

            // Act
            var response = await _sessionManager.ProcessMessageAsync(connectionId, message);

            // Assert
            response.Should().NotBeNull();
            response.Status.Code.Should().Be(TinkerPopStatusCodes.Success);
            response.Result.Data.Should().NotBeEmpty();
        }

        [Fact]
        public async Task TinkerPopSessionManager_Should_Handle_Complex_Graph_Operations()
        {
            // Arrange
            var connectionId = "graph-test-connection";
            var session = _sessionManager.CreateSession(connectionId);

            var operations = new[]
            {
                // Create vertices
                "g.addV('person').property('name', 'Alice').property('age', 30)",
                "g.addV('person').property('name', 'Bob').property('age', 25)",
                
                // Create edge
                "g.V().has('name', 'Alice').addE('knows').to(g.V().has('name', 'Bob'))",
                
                // Query operations
                "g.V().count()",
                "g.E().count()",
                "g.V().has('name', 'Alice').out('knows').values('name')",
                "g.V().has('name', 'Alice').values('age')"
            };

            var expectedResults = new object[] { null, null, null, 2L, 1L, "Bob", 30 };

            for (int i = 0; i < operations.Length; i++)
            {
                var message = new TinkerPopMessage
                {
                    RequestId = Guid.NewGuid(),
                    Op = TinkerPopOperations.Eval,
                    Args = new Dictionary<string, object>
                    {
                        ["gremlin"] = operations[i],
                        ["bindings"] = new Dictionary<string, object>()
                    }
                };

                // Act
                var response = await _sessionManager.ProcessMessageAsync(connectionId, message);

                // Assert
                response.Should().NotBeNull();
                response.Status.Code.Should().Be(TinkerPopStatusCodes.Success);

                if (expectedResults[i] != null)
                {
                    response.Result.Data.Should().NotBeEmpty();
                    response.Result.Data.First().Should().Be(expectedResults[i]);
                }
            }
        }

        [Fact]
        public async Task TinkerPopSessionManager_Should_Handle_Parameterized_Queries()
        {
            // Arrange
            var connectionId = "param-query-test";
            var session = _sessionManager.CreateSession(connectionId);

            // Create test data
            await _sessionManager.ProcessMessageAsync(connectionId, new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Eval,
                Args = new Dictionary<string, object>
                {
                    ["gremlin"] = "g.addV('person').property('name', 'Alice').property('age', 30)",
                    ["bindings"] = new Dictionary<string, object>()
                }
            });

            // Test parameterized query
            var paramMessage = new TinkerPopMessage
            {
                RequestId = Guid.NewGuid(),
                Op = TinkerPopOperations.Eval,
                Args = new Dictionary<string, object>
                {
                    ["gremlin"] = "g.V().has('name', username).values('age')",
                    ["bindings"] = new Dictionary<string, object>
                    {
                        ["username"] = "Alice"
                    }
                }
            };

            // Act
            var response = await _sessionManager.ProcessMessageAsync(connectionId, paramMessage);

            // Assert
            response.Should().NotBeNull();
            response.Status.Code.Should().Be(TinkerPopStatusCodes.Success);
            response.Result.Data.Should().NotBeEmpty();
            response.Result.Data.First().Should().Be(30);
        }

        [Fact]
        public async Task TinkerPopSessionManager_Should_Handle_Error_Scenarios()
        {
            // Arrange
            var connectionId = "error-test-connection";
            var session = _sessionManager.CreateSession(connectionId);

            var errorScenarios = new[]
            {
                // Missing gremlin query
                (message: new TinkerPopMessage
                {
                    RequestId = Guid.NewGuid(),
                    Op = TinkerPopOperations.Eval,
                    Args = new Dictionary<string, object>
                    {
                        ["bindings"] = new Dictionary<string, object>()
                    }
                }, expectedCode: TinkerPopStatusCodes.InvalidRequestArguments),

                // Invalid gremlin syntax
                (message: new TinkerPopMessage
                {
                    RequestId = Guid.NewGuid(),
                    Op = TinkerPopOperations.Eval,
                    Args = new Dictionary<string, object>
                    {
                        ["gremlin"] = "invalid.syntax.query()",
                        ["bindings"] = new Dictionary<string, object>()
                    }
                }, expectedCode: TinkerPopStatusCodes.ScriptEvaluationError),

                // Unsupported operation
                (message: new TinkerPopMessage
                {
                    RequestId = Guid.NewGuid(),
                    Op = "unsupported-operation",
                    Args = new Dictionary<string, object>()
                }, expectedCode: TinkerPopStatusCodes.MalformedRequest)
            };

            foreach (var (message, expectedCode) in errorScenarios)
            {
                // Act
                var response = await _sessionManager.ProcessMessageAsync(connectionId, message);

                // Assert
                response.Should().NotBeNull();
                response.RequestId.Should().Be(message.RequestId);
                response.Status.Code.Should().Be(expectedCode);
                response.Status.Message.Should().NotBeEmpty();
            }
        }

        [Fact]
        public void TinkerPopSessionManager_Should_Cleanup_Expired_Sessions()
        {
            // Arrange
            var connectionIds = new[] { "session1", "session2", "session3" };
            var sessions = connectionIds.Select(id => _sessionManager.CreateSession(id)).ToArray();

            // Manually set last activity to simulate expired sessions
            sessions[0].LastActivity = DateTime.UtcNow.AddHours(-2); // Expired
            sessions[1].LastActivity = DateTime.UtcNow.AddMinutes(-30); // Not expired
            sessions[2].LastActivity = DateTime.UtcNow.AddHours(-3); // Expired

            // Act
            _sessionManager.CleanupExpiredSessions(TimeSpan.FromHours(1));

            // Assert
            _sessionManager.GetSession(connectionIds[0]).Should().BeNull(); // Expired and removed
            _sessionManager.GetSession(connectionIds[1]).Should().NotBeNull(); // Still active
            _sessionManager.GetSession(connectionIds[2]).Should().BeNull(); // Expired and removed
        }

        [Fact]
        public void TinkerPopSessionManager_Should_Provide_Statistics()
        {
            // Arrange
            var connectionIds = new[] { "stats1", "stats2", "stats3" };
            var sessions = connectionIds.Select(id => _sessionManager.CreateSession(id)).ToArray();

            // Set different authentication states
            sessions[0].IsAuthenticated = true;
            sessions[1].IsAuthenticated = false;
            sessions[2].IsAuthenticated = true;

            // Set different activity times
            sessions[0].LastActivity = DateTime.UtcNow; // Active
            sessions[1].LastActivity = DateTime.UtcNow.AddMinutes(-10); // Less active
            sessions[2].LastActivity = DateTime.UtcNow.AddMinutes(-1); // Active

            // Act
            var stats = _sessionManager.GetStatistics();

            // Assert
            stats.Should().NotBeNull();
            stats.Should().ContainKey("totalSessions");
            stats.Should().ContainKey("authenticatedSessions");
            stats.Should().ContainKey("activeSessions");

            stats["totalSessions"].Should().Be(3);
            stats["authenticatedSessions"].Should().Be(2);
            stats["activeSessions"].Should().Be(3); // All are within 5 minutes
        }

        [Theory]
        [InlineData(TinkerPopStatusCodes.Success)]
        [InlineData(TinkerPopStatusCodes.NoContent)]
        [InlineData(TinkerPopStatusCodes.PartialContent)]
        [InlineData(TinkerPopStatusCodes.Unauthorized)]
        [InlineData(TinkerPopStatusCodes.Authenticate)]
        [InlineData(TinkerPopStatusCodes.MalformedRequest)]
        [InlineData(TinkerPopStatusCodes.InvalidRequestArguments)]
        [InlineData(TinkerPopStatusCodes.ServerError)]
        [InlineData(TinkerPopStatusCodes.ScriptEvaluationError)]
        [InlineData(TinkerPopStatusCodes.ServerTimeout)]
        [InlineData(TinkerPopStatusCodes.ServerSerializationError)]
        public void TinkerPopStatusCodes_Should_Have_Correct_Values(int statusCode)
        {
            // Assert - Verify TinkerPop standard status codes
            statusCode.Should().BeOneOf(200, 204, 206, 401, 407, 498, 499, 500, 597, 598, 599);
        }

        [Fact]
        public void TinkerPopMimeTypes_Should_Have_Correct_Values()
        {
            // Assert
            TinkerPopMimeTypes.GraphSONV1.Should().Be("application/vnd.gremlin-v1.0+json");
            TinkerPopMimeTypes.GraphSONV2.Should().Be("application/vnd.gremlin-v2.0+json");
            TinkerPopMimeTypes.GraphSONV3.Should().Be("application/vnd.gremlin-v3.0+json");
            TinkerPopMimeTypes.Json.Should().Be("application/json");
        }

        [Fact]
        public void TinkerPopOperations_Should_Have_Standard_Values()
        {
            // Assert
            TinkerPopOperations.Eval.Should().Be("eval");
            TinkerPopOperations.Close.Should().Be("close");
            TinkerPopOperations.Authentication.Should().Be("authentication");
            TinkerPopOperations.Invalid.Should().Be("invalid");
        }

        public void Dispose()
        {
            // _connector doesn't implement IDisposable, so we don't need to dispose it
        }
    }
}