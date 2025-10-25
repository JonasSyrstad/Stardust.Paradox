using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for LINQ ordering and pagination operations
    /// </summary>
    public class OrderingAndPagingTests : LinqTestBase
    {
        [Fact]
        public void OrderBy_Age_ReturnsOrderedResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.OrderBy(p => p.Age).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().BeInAscendingOrder(p => p.Age);
        }

        [Fact]
        public void OrderByDescending_Age_ReturnsOrderedResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.OrderByDescending(p => p.Age).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().BeInDescendingOrder(p => p.Age);
        }

        [Fact]
        public void OrderBy_Name_ReturnsAlphabeticallyOrdered()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.OrderBy(p => p.Name).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().BeInAscendingOrder(p => p.Name);
        }

        [Fact]
        public void OrderByDescending_Score_ReturnsOrderedResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.OrderByDescending(p => p.Score).ToList();

            // Assert
            result.Should().HaveCount(5);
            result.Should().BeInDescendingOrder(p => p.Score);
        }

        [Fact]
        public void OrderBy_ThenBy_MultipleProperties_ReturnsCorrectOrder()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .OrderBy(p => p.City)
                .ThenBy(p => p.Age)
                .ToList();

            // Assert
            result.Should().HaveCount(5);
            
            // Verify primary sort
            var cities = result.Select(p => p.City).ToList();
            cities.Should().BeInAscendingOrder();
            
            // Verify secondary sort within same city
            var seattleUsers = result.Where(p => p.City == "Seattle").ToList();
            if (seattleUsers.Count > 1)
            {
                seattleUsers.Should().BeInAscendingOrder(p => p.Age);
            }
        }

        [Fact]
        public void OrderBy_ThenByDescending_MixedOrdering_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .OrderBy(p => p.City)
                .ThenByDescending(p => p.Score)
                .ToList();

            // Assert
            result.Should().HaveCount(5);
            
            // Verify within same city, scores are descending
            var seattleUsers = result.Where(p => p.City == "Seattle").ToList();
            if (seattleUsers.Count > 1)
            {
                seattleUsers.Should().BeInDescendingOrder(p => p.Score);
            }
        }

        [Fact]
        public void OrderByDescending_ThenBy_MixedOrdering_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .OrderByDescending(p => p.IsActive)
                .ThenBy(p => p.Name)
                .ToList();

            // Assert
            result.Should().HaveCount(5);
            
            // Active users should come first
            var activeUsers = result.TakeWhile(p => p.IsActive).ToList();
            var inactiveUsers = result.SkipWhile(p => p.IsActive).ToList();
            
            activeUsers.Should().BeInAscendingOrder(p => p.Name);
            inactiveUsers.Should().BeInAscendingOrder(p => p.Name);
        }

        [Fact]
        public void Skip_ReturnsCorrectSubset()
        {
            // Arrange
            var queryable = Context.People.AsQueryable().OrderBy(p => p.Age);

            // Act
            var result = queryable.Skip(2).ToList();

            // Assert
            result.Should().HaveCount(3);
        }

        [Fact]
        public void Take_ReturnsCorrectSubset()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Take(3).ToList();

            // Assert
            result.Should().HaveCount(3);
        }

        [Fact]
        public void Skip_AndTake_Pagination_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable().OrderBy(p => p.Name);

            // Act - Page 1
            var page1 = queryable.Skip(0).Take(2).ToList();
            
            // Act - Page 2
            var page2 = queryable.Skip(2).Take(2).ToList();
            
            // Act - Page 3
            var page3 = queryable.Skip(4).Take(2).ToList();

            // Assert
            page1.Should().HaveCount(2);
            page2.Should().HaveCount(2);
            page3.Should().HaveCount(1);
            
            // Verify no overlap - use names since we don't have Id exposed
            var allNames = page1.Concat(page2).Concat(page3).Select(p => p.Name).ToList();
            allNames.Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void OrderBy_WithFilter_ReturnsOrderedFilteredResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .Where(p => p.IsActive)
                .OrderBy(p => p.Age)
                .ToList();

            // Assert
            result.Should().OnlyContain(p => p.IsActive);
            result.Should().BeInAscendingOrder(p => p.Age);
        }

        [Fact]
        public void OrderBy_WithFilterAndPaging_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .Where(p => p.Score > 85)
                .OrderByDescending(p => p.Score)
                .Skip(1)
                .Take(2)
                .ToList();

            // Assert
            result.Should().OnlyContain(p => p.Score > 85);
            result.Should().HaveCountLessThanOrEqualTo(2);
            result.Should().BeInDescendingOrder(p => p.Score);
        }

        [Fact]
        public void Skip_Zero_ReturnsAllResults()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Skip(0).ToList();

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public void Skip_BeyondCount_ReturnsEmpty()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Skip(100).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Take_Zero_ReturnsEmpty()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Take(0).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Take_MoreThanAvailable_ReturnsAll()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Take(100).ToList();

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public void OrderBy_Companies_ByEmployeeCount_Works()
        {
            // Arrange
            var queryable = Context.Companies.AsQueryable();

            // Act
            var result = queryable.OrderBy(c => c.EmployeeCount).ToList();

            // Assert
            result.Should().HaveCount(3);
            result.Should().BeInAscendingOrder(c => c.EmployeeCount);
        }

        [Fact]
        public void OrderByDescending_Projects_ByBudget_Works()
        {
            // Arrange
            var queryable = Context.Projects.AsQueryable();

            // Act
            var result = queryable.OrderByDescending(p => p.Budget).ToList();

            // Assert
            result.Should().HaveCount(3);
            result.Should().BeInDescendingOrder(p => p.Budget);
        }
    }
}
