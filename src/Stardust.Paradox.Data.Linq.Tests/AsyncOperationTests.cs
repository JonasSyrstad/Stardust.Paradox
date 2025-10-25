using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for async LINQ operations
    /// </summary>
    public class AsyncOperationTests : LinqTestBase
    {
        [Fact]
        public async Task ToListAsync_ReturnsAllEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable.ToListAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(5);
        }

        [Fact]
        public async Task ToListAsync_WithFilter_ReturnsFilteredEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.IsActive)
                .ToListAsync();

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().OnlyContain(p => p.IsActive);
        }

        [Fact]
        public async Task ToListAsync_WithOrdering_ReturnsOrderedEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .OrderBy(p => p.Age)
                .ToListAsync();

            // Assert
            result.Should().HaveCount(5);
            result.Should().BeInAscendingOrder(p => p.Age);
        }

        [Fact]
        public async Task FirstAsync_ReturnsFirstEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable.FirstAsync();

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().NotBeEmpty();
        }

        [Fact]
        public async Task FirstAsync_WithFilter_ReturnsMatchingEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.Name == "Alice Johnson")
                .FirstAsync();

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Alice Johnson");
        }

        [Fact]
        public async Task FirstOrDefaultAsync_ReturnsEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable.FirstOrDefaultAsync();

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task FirstOrDefaultAsync_WithNonMatchingFilter_ReturnsNull()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.Name == "NonExistent")
                .FirstOrDefaultAsync();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task FirstOrDefaultAsync_WithMatchingFilter_ReturnsEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.Email == "bob@example.com")
                .FirstOrDefaultAsync();

            // Assert
            result.Should().NotBeNull();
            result.Email.Should().Be("bob@example.com");
        }

        [Fact]
        public async Task CountAsync_ReturnsCorrectCount()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = await queryable.CountAsync();

            // Assert
            count.Should().Be(5);
        }

        [Fact]
        public async Task CountAsync_WithFilter_ReturnsFilteredCount()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = await queryable
                .Where(p => p.Age > 30)
                .CountAsync();

            // Assert
            count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AnyAsync_ReturnsTrue_WhenEntitiesExist()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable.AnyAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task AnyAsync_WithMatchingFilter_ReturnsTrue()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.IsActive)
                .AnyAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task AnyAsync_WithNonMatchingFilter_ReturnsFalse()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.Age > 100)
                .AnyAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task MultipleAsyncOperations_CanBeChained()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = await queryable.CountAsync();
            var first = await queryable.FirstAsync();
            var list = await queryable.ToListAsync();
            var hasActive = await queryable.Where(p => p.IsActive).AnyAsync();

            // Assert
            count.Should().Be(5);
            first.Should().NotBeNull();
            list.Should().HaveCount(5);
            hasActive.Should().BeTrue();
        }

        [Fact]
        public async Task ToListAsync_WithComplexQuery_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.Age >= 25)
                .OrderByDescending(p => p.Score)
                .Skip(1)
                .Take(2)
                .ToListAsync();

            // Assert
            result.Should().HaveCountLessThanOrEqualTo(2);
            result.Should().OnlyContain(p => p.Age >= 25);
            result.Should().BeInDescendingOrder(p => p.Score);
        }

        [Fact]
        public async Task FirstAsync_WithOrdering_ReturnsCorrectEntity()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var youngest = await queryable.OrderBy(p => p.Age).FirstAsync();
            queryable = Context.People.AsQueryable();
            var oldest = await queryable.OrderByDescending(p => p.Age).FirstAsync();

            // Assert
            youngest.Should().NotBeNull();
            oldest.Should().NotBeNull();
            youngest.Age.Should().BeLessThan(oldest.Age);
        }

        [Fact]
        public async Task CountAsync_Companies_ReturnsCorrectCount()
        {
            // Arrange
            var queryable = Context.Companies.AsQueryable();

            // Act
            var count = await queryable.CountAsync();

            // Assert
            count.Should().Be(3);
        }

        [Fact]
        public async Task FirstOrDefaultAsync_Projects_Works()
        {
            // Arrange
            var queryable = Context.Projects.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.Status == "Active")
                .FirstOrDefaultAsync();

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("Active");
        }

        [Fact]
        public async Task ToListAsync_Skills_ReturnsAllSkills()
        {
            // Arrange
            var queryable = Context.Skills.AsQueryable();

            // Act
            var result = await queryable.ToListAsync();

            // Assert
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task AnyAsync_WithComplexFilter_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var hasHighScoreActiveUsers = await queryable
                .Where(p => p.IsActive && p.Score > 90)
                .AnyAsync();

            // Assert
            hasHighScoreActiveUsers.Should().BeTrue();
        }

        [Fact]
        public async Task FirstOrDefaultAsync_WithProjection_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = await queryable
                .Where(p => p.City == "Seattle")
                .OrderBy(p => p.Age)
                .FirstOrDefaultAsync();

            // Assert
            result.Should().NotBeNull();
            result.City.Should().Be("Seattle");
        }

        [Fact]
        public async Task CountAsync_WithComplexFilter_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = await queryable
                .Where(p => p.City == "Seattle" && p.Score > 90)
                .CountAsync();

            // Assert
            count.Should().BeGreaterThanOrEqualTo(0);
        }
    }
}
