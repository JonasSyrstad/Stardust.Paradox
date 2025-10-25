using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for basic LINQ query operations (Where, Select, Count, etc.)
    /// </summary>
    public class BasicLinqQueryTests : LinqTestBase
    {
        [Fact]
        public void Where_FilterByName_ReturnsMatchingEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Name == "Alice Johnson").ToList();

            // Assert
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Alice Johnson");
        }

        [Fact]
        public void Where_FilterByAge_ReturnsMatchingEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age > 30).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.Age > 30);
        }

        [Fact]
        public void Where_FilterByBoolean_ReturnsActiveUsers()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.IsActive == true).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.IsActive);
        }

        [Fact]
        public void Where_FilterByDecimal_ReturnsHighScorers()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Score >= 90).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.Score >= 90);
        }

        [Fact]
        public void Where_StringContains_ReturnsMatchingEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Email.Contains("@example.com")).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().OnlyContain(p => p.Email.Contains("@example.com"));
        }

        [Fact]
        public async Task Where_StringStartsWith_ReturnsMatchingEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable.Where(p => p.Name.StartsWith("A")).ToListAsync();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.Name.StartsWith("A"));
        }

        [Fact]
        public void Where_StringEndsWith_ReturnsMatchingEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Email.EndsWith(".com")).ToList();

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public void Where_ComplexPredicate_WithAndCondition_ReturnsCorrectResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age > 25 && p.IsActive).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.Age > 25 && p.IsActive);
        }

        [Fact]
        public void Where_NotCondition_ReturnsCorrectResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => !p.IsActive).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => !p.IsActive);
        }

        [Fact]
        public void Where_GreaterThanOrEqual_ReturnsCorrectResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age >= 30).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.Age >= 30);
        }

        [Fact]
        public void Where_LessThan_ReturnsCorrectResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age < 30).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.Age < 30);
        }

        [Fact]
        public void Where_LessThanOrEqual_ReturnsCorrectResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age <= 30).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.Age <= 30);
        }

        [Fact]
        public void Where_NotEqual_ReturnsCorrectResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age != 30).ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().NotContain(p => p.Age == 30);
        }

        [Fact]
        public void Count_ReturnsCorrectCount()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = queryable.Count();

            // Assert
            count.Should().Be(5);
        }

        [Fact]
        public void Count_WithPredicate_ReturnsCorrectCount()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = queryable.Count(p => p.IsActive);

            // Assert
            count.Should().Be(3);
        }

        [Fact]
        public void Any_ReturnsTrue_WhenEntitiesExist()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Any();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void Any_WithPredicate_ReturnsCorrectResult()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var hasActiveUsers = queryable.Any(p => p.IsActive);
            var hasInactiveUsers = queryable.Any(p => !p.IsActive);

            // Assert
            hasActiveUsers.Should().BeTrue();
            hasInactiveUsers.Should().BeTrue();
        }

        [Fact]
        public void First_ReturnsFirstEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.First();

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().NotBeEmpty();
        }

        [Fact]
        public void First_WithPredicate_ReturnsCorrectEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.First(p => p.Name == "Bob Smith");

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Bob Smith");
        }

        [Fact]
        public void FirstOrDefault_ReturnsFirstEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.FirstOrDefault();

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void FirstOrDefault_WithNonMatchingPredicate_ReturnsNull()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.FirstOrDefault(p => p.Name == "NonExistent");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Single_WithUniquePredicate_ReturnsEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Single(p => p.Email == "alice@example.com");

            // Assert
            result.Should().NotBeNull();
            result.Email.Should().Be("alice@example.com");
        }

        [Fact]
        public void SingleOrDefault_WithUniquePredicate_ReturnsEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.SingleOrDefault(p => p.Email == "alice@example.com");

            // Assert
            result.Should().NotBeNull();
            result.Email.Should().Be("alice@example.com");
        }

        [Fact]
        public void SingleOrDefault_WithNonMatchingPredicate_ReturnsNull()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.SingleOrDefault(p => p.Email == "nonexistent@example.com");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Distinct_RemovesDuplicates()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Distinct().ToList();

            // Assert
            result.Should().HaveCount(5);
            // Verify we have 5 unique instances
            var uniqueCount = result.Distinct().Count();
            uniqueCount.Should().Be(5);
        }

        [Fact]
        public void ToList_MaterializesQuery()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.ToList();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(5);
            result.Should().AllBeAssignableTo<IPerson>();
        }
    }
}
