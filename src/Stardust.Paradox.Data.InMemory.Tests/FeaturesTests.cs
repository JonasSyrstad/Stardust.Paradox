using FluentAssertions;
using Stardust.Paradox.Data.InMemory.Factory;
using Xunit;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    public class FeaturesTests
    {
        [Fact]
        public void Features_CanParameterizeQueries_MatchesConnectorFlag()
        {
            var database = new InMemory.Core.InMemoryGraphDatabase();
            var connector = InMemoryConnectorFactory.Create(database);

            connector.Features.Should().NotBeNull();
            connector.Features.CanParameterizeQueries.Should().Be(connector.CanParameterizeQueries);
        }

        [Fact]
        public void Features_Default_IsConsistent()
        {
            Features.Default.CanParameterizeQueries.Should().BeFalse();
            Features.Default.SupportsServerSideProjection.Should().BeTrue();
            Features.Default.SupportsDedup.Should().BeTrue();
            Features.Default.SupportsOrdering.Should().BeTrue();
            Features.Default.SupportsPaging.Should().BeTrue();
        }
    }
}
