using Stardust.Paradox.Data.InMemory;
using Xunit;

namespace Stardust.Paradox.Data.InMemory.Tests.CosmosDbMigrated
{
    /// <summary>
    /// Simple verification test to ensure the migrated CosmosDB tests compile and basic functionality works
    /// </summary>
    public class MigrationVerificationTests
    {
        [Fact]
        public void Class1_CanBeCreated()
        {
            // Arrange & Act
            var connector = new Class1();
            
            // Assert
            Assert.NotNull(connector);
            Assert.True(connector.CanParameterizeQueries);
        }

        [Fact]
        public void ServiceDefinition_CanBeCreated()
        {
            // Arrange & Act
            var service = new ServiceDefinition
            {
                id = "test",
                name = "Test Service",
                ocupation = "Developer"
            };
            
            // Assert
            Assert.Equal("test", service.id);
            Assert.Equal("Test Service", service.name);
            Assert.Equal("Developer", service.ocupation);
        }

        [Fact]
        public void EdgeReference1_CanBeCreated()
        {
            // Arrange & Act
            var edgeRef = new EdgeReference1
            {
                EdgeName = "testEdge",
                EdgeId = "edge123"
            };
            
            // Assert
            Assert.Equal("testEdge", edgeRef.EdgeName);
            Assert.Equal("edge123", edgeRef.EdgeId);
        }

        [Fact]
        public void MyProp_CanBeCreated()
        {
            // Arrange & Act
            var prop = new MyProp();
            
            // Assert
            Assert.NotNull(prop);
        }

        [Fact]
        public void GenderTypes_EnumExists()
        {
            // Arrange & Act
            var male = GenderTypes.Male;
            var female = GenderTypes.Female;
            var other = GenderTypes.Other;
            
            // Assert
            Assert.Equal(GenderTypes.Male, male);
            Assert.Equal(GenderTypes.Female, female);
            Assert.Equal(GenderTypes.Other, other);
        }

        [Fact]
        public void InMemoryConnector_CanBeCreated()
        {
            // Arrange & Act
            var connector = new InMemoryGremlinLanguageConnector();
            
            // Assert
            Assert.NotNull(connector);
            Assert.True(connector.CanParameterizeQueries);
        }
    }
}