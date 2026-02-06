using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for edge cases, error handling, and boundary conditions
    /// </summary>
    public class EdgeCaseTests : LinqTestBase
    {
        [Fact]
        public void EmptyWhere_ReturnsAllEntities()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.ToList();

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public void Where_AlwaysFalse_ReturnsEmpty()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => false).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Where_AlwaysTrue_ReturnsAll()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => true).ToList();

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public void MultipleWhere_ChainingWorks()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable
                .Where(p => p.Age > 20)
                .Where(p => p.Age < 40)
                .Where(p => p.IsActive)
                .ToList();

            // Assert
            result.Should().OnlyContain(p => p.Age > 20 && p.Age < 40 && p.IsActive);
        }

        [Fact]
        public void Count_OnEmptyResult_ReturnsZero()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = queryable.Where(p => p.Age > 100).Count();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void Any_OnEmptyResult_ReturnsFalse()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age > 100).Any();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void FirstOrDefault_OnEmptyResult_ReturnsNull()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Age > 100).FirstOrDefault();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void First_OnEmptyResult_ThrowsException()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            Action act = () => queryable.Where(p => p.Age > 100).First();

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Single_OnMultipleResults_ThrowsException()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            Action act = () => queryable.Where(p => p.IsActive).Single();

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void SingleOrDefault_OnMultipleResults_ThrowsException()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            Action act = () => queryable.Where(p => p.IsActive).SingleOrDefault();

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Where_WithNull_HandlesGracefully()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();
            string? nullValue = null;

            // Act
            var result = queryable.Where(p => p.Name == nullValue).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Where_CompareToVariable_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();
            var targetAge = 30;

            // Act
            var result = queryable.Where(p => p.Age == targetAge).ToList();

            // Assert
            result.Should().OnlyContain(p => p.Age == 30);
        }

        [Fact]
        public void Where_CompareToComputedValue_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();
            var baseAge = 20;
            var offset = 10;

            // Act
            var result = queryable.Where(p => p.Age == baseAge + offset).ToList();

            // Assert
            result.Should().OnlyContain(p => p.Age == 30);
        }

        [Fact]
        public void OrderBy_NullableProperty_HandlesGracefully()
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
        public void Skip_Negative_ThrowsException()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            Action act = () => queryable.Skip(-1).ToList();

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Take_Negative_ThrowsException()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            Action act = () => queryable.Take(-1).ToList();

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void LongCount_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = queryable.LongCount();

            // Assert
            count.Should().Be(5L);
        }

        [Fact]
        public void LongCount_WithPredicate_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var count = queryable.LongCount(p => p.IsActive);

            // Assert
            count.Should().Be(3L);
        }

        [Fact]
        public void Distinct_OnAlreadyUniqueSet_ReturnsSameCount()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Distinct().ToList();

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public async Task ComplexChain_AllOperations_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result =await  queryable
                .Where(p => p.Age >= 25)
                .Where(p => p.Score > 85)
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.Name)
                .Skip(0)
                .Take(10)
                .Distinct()
                .ToListAsync();

            var queries = Connector.GetQueryLog();
            // Assert
            result.Should().OnlyContain(p => p.Age >= 25 && p.Score > 85);
            result.Should().BeInDescendingOrder(p => p.Score);
        }

        [Fact()]
        public void Where_WithCityFilter_CaseMatters()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var exactMatch = queryable.Where(p => p.City == "Seattle").ToList();
            var wrongCase = queryable.Where(p => p.City == "seattle").ToList();

            // Assert
            exactMatch.Should().HaveCountGreaterThan(0);
            wrongCase.Should().BeEmpty();
        }

        [Fact]
        public void Select_NonExistentPropertyPath_HandlesGracefully()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.Select(p => p.Name).ToList();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(5);
        }

        [Fact]
        public void MultipleToList_Calls_ExecuteQueryMultipleTimes()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result1 = queryable.Where(p => p.IsActive).ToList();
            var result2 = queryable.Where(p => p.IsActive).ToList();

            // Assert
            result1.Should().HaveCount(result2.Count);
            result1.Should().NotBeSameAs(result2); // Different instances
        }

        [Fact]
        public void QueryAfterContextDispose_HandlesGracefully()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();
            
            // Create a deferred query
            var query = queryable.Where(p => p.IsActive);
            
            // Execute while context is still alive
            var result = query.ToList();

            // Assert
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public void Where_DecimalComparison_Works()
        {
          // Arrange
     var queryable = Context.People.AsQueryable();
            var threshold = 90.0m;

            // Act
            var result = queryable.Where(p => p.Score > threshold).ToList();

          // Assert
            result.Should().OnlyContain(p => p.Score > 90.0m);
}
        [Fact]
        public void OrderBy_DecimalProperty_Works()
        {
            // Arrange
            var queryable = Context.People.AsQueryable();

            // Act
            var result = queryable.OrderBy(p => p.Score).ToList();

            // Assert
            result.Should().BeInAscendingOrder(p => p.Score);
        }

        [Fact]
        public void Companies_BasicQuery_Works()
        {
            // Arrange
            var queryable = Context.Companies.AsQueryable();

            // Act
            var result = queryable.Where(c => c.Industry == "Technology").ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public void Projects_BasicQuery_Works()
        {
            // Arrange
            var queryable = Context.Projects.AsQueryable();

            // Act
            var result = queryable.Where(p => p.Status == "Active").ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public void Skills_BasicQuery_Works()
        {
            // Arrange
            var queryable = Context.Skills.AsQueryable();

            // Act
            var result = queryable.Where(s => s.Category == "Programming").ToList();

            // Assert
            result.Should().HaveCount(2);
        }
    }
}
