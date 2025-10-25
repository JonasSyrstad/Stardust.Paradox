using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for LINQ Select and projection operations
    /// </summary>
    public class ProjectionTests : LinqTestBase
    {
        [Fact]
        public void Select_SingleProperty_ReturnsPropertyValues()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Select(p => p.Name).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().Contain("Alice Johnson");
            result.Should().Contain("Bob Smith");
            result.Should().AllBeOfType<string>();
        }

        [Fact]
        public void Select_IntProperty_ReturnsPropertyValues()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Select(p => p.Age).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().AllBeOfType<int>();
            result.Should().Contain(30);
            result.Should().Contain(25);
        }

        [Fact]
        public void Select_BoolProperty_ReturnsPropertyValues()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Select(p => p.IsActive).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().AllBeOfType<bool>();
            result.Should().Contain(true);
            result.Should().Contain(false);
        }

        [Fact]
        public void Select_DecimalProperty_ReturnsPropertyValues()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Select(p => p.Score).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().AllBeOfType<decimal>();
            result.Should().Contain(95.5m);
        }

        [Fact]
        public void Select_AnonymousType_SingleProperty_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Select(p => new { p.Name }).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.First().Name.Should().NotBeEmpty();
        }

        [Fact]
        public void Select_AnonymousType_MultipleProperties_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Select(p => new { p.Name, p.Age, p.Email }).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.First().Name.Should().NotBeEmpty();
            result.First().Age.Should().BeGreaterThan(0);
            result.First().Email.Should().NotBeEmpty();
        }

        [Fact]
        public void Select_WithWhere_ReturnsFilteredProjection()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .Where(p => p.Age > 30)
                .Select(p => p.Name)
                .ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().AllBeOfType<string>();
        }

        [Fact]
        public void Select_WithOrderBy_ReturnsOrderedProjection()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .OrderBy(p => p.Age)
                .Select(p => new { p.Name, p.Age })
                .ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().BeInAscendingOrder(r => r.Age);
        }

        [Fact]
        public void Select_CompanyNames_Works()
        {
            // Arrange
            var queryable = Context.Companies.AsQueryable();

            // Act
            var result = queryable.Select(c => c.Name).ToList();

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain("TechCorp");
            result.Should().Contain("DataSystems");
            result.Should().Contain("CloudServices");
        }

        [Fact]
        public void Select_ProjectNames_AndStatus_Works()
        {
            // Arrange
            var queryable = Context.Projects.AsQueryable();

            // Act
            var result = queryable
                .Select(p => new { p.Name, p.Status })
                .ToList();

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(p => p.Name == "Project Alpha");
            result.Should().Contain(p => p.Status == "Active");
            result.Should().Contain(p => p.Status == "Completed");
        }

        [Fact]
        public void Select_WithComplexFilter_AndProjection_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .Where(p => p.Age >= 25 && p.Age <= 35)
                .OrderBy(p => p.Age)
                .Select(p => new { p.Name, p.Age, p.City })
                .ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(r => r.Age >= 25 && r.Age <= 35);
            result.Should().BeInAscendingOrder(r => r.Age);
        }

        [Fact]
        public void Select_DistinctCities_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .Select(p => p.City)
                .Distinct()
                .ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyHaveUniqueItems();
            result.Should().Contain("Seattle");
            result.Should().Contain("Portland");
            result.Should().Contain("Boston");
        }

        [Fact]
        public void Select_Count_OfProjectedValues_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = queryable.Select(p => p.Name).Count();

            // Assert
            count.Should().Be(5);
        }

        [Fact]
        public void Select_WithTake_ReturnsLimitedProjection()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .OrderBy(p => p.Age)
                .Select(p => new { p.Name, p.Age })
                .Take(3)
                .ToList();

            // Assert
            result.Should().HaveCount(3);
        }

        [Fact]
        public void Select_WithSkipAndTake_ReturnsPaginatedProjection()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .OrderBy(p => p.Name)
                .Select(p => p.Name)
                .Skip(1)
                .Take(2)
                .ToList();

            // Assert
            result.Should().HaveCount(2);
            result.Should().AllBeOfType<string>();
        }

        [Fact]
        public void Select_First_OfProjection_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .OrderBy(p => p.Age)
                .Select(p => p.Name)
                .First();

            // Assert
            result.Should().NotBeEmpty();
            result.Should().BeOfType<string>();
        }

        [Fact]
        public void Select_Single_WithFilter_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .Where(p => p.Email == "alice@example.com")
                .Select(p => new { p.Name, p.Email })
                .Single();

            // Assert
            result.Should().NotBeNull();
            result.Email.Should().Be("alice@example.com");
        }

        [Fact]
        public void Select_Any_WithProjection_ReturnsTrueIfExists()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var hasActiveUsers = queryable
                .Where(p => p.IsActive)
                .Select(p => p.Name)
                .Any();

            // Assert
            hasActiveUsers.Should().BeTrue();
        }
    }
}
