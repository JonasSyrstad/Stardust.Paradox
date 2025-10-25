using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Comprehensive tests for aggregation and grouping operations in LINQ to Gremlin
    /// </summary>
    public class AggregationTests : LinqTestBase
    {
        [Fact]
        public void Count_ReturnsCorrectCount()
        {
         // Arrange & Act
        var count = Context.People.AsQueryable().Count();

          // Assert
  count.Should().Be(5);
     }

        [Fact]
 public void Count_WithPredicate_ReturnsFilteredCount()
        {
            // Arrange & Act
     var count = Context.People.AsQueryable()
    .Where(p => p.IsActive)
  .Count();

       // Assert
          count.Should().Be(3);
        }

        [Fact]
public void Sum_ReturnsCorrectTotal()
        {
            // Arrange & Act
            var totalAge = Context.People.AsQueryable()
     .Sum(p => p.Age);

 // Assert
        totalAge.Should().Be(160); // 30 + 25 + 35 + 28 + 42
 }

        [Fact]
     public void Average_ReturnsCorrectAverage()
{
   // Arrange & Act
 var avgAge = Context.People.AsQueryable()
  .Average(p => p.Age);

  // Assert
     avgAge.Should().Be(32); // 160 / 5
        }

        [Fact]
        public void Min_ReturnsSmallestValue()
     {
 // Arrange & Act
        var minAge = Context.People.AsQueryable()
.Min(p => p.Age);

        // Assert
   minAge.Should().Be(25);
   }

 [Fact]
    public void Max_ReturnsLargestValue()
     {
    // Arrange & Act
          var maxAge = Context.People.AsQueryable()
 .Max(p => p.Age);

// Assert
        maxAge.Should().Be(42);
      }

   [Fact]
 public void GroupBy_GroupsByKey()
{
   // Arrange & Act
         var groupedByCity = Context.People.AsQueryable()
  .GroupBy(p => p.City)
.ToList();

 // Assert
    groupedByCity.Should().NotBeEmpty();
        groupedByCity.Should().Contain(g => g.Key == "Seattle");
        }

    [Fact]
        public void GroupBy_WithCount_CountsPerGroup()
    {
     // Arrange & Act
          var cityCounts = Context.People.AsQueryable()
      .GroupBy(p => p.City)
       .Select(g => new { City = g.Key, Count = g.Count() })
   .ToList();

 // Assert
            cityCounts.Should().NotBeEmpty();
   var seattleCount = cityCounts.FirstOrDefault(c => c.City == "Seattle");
   seattleCount.Should().NotBeNull();
    seattleCount!.Count.Should().Be(3);
  }

    [Fact]
        public void GroupBy_WithAggregate_AggregatesPerGroup()
        {
     // Arrange & Act
            var cityAverages = Context.People.AsQueryable()
 .GroupBy(p => p.City)
.Select(g => new { City = g.Key, AvgAge = g.Average(p => p.Age) })
     .ToList();

     // Assert
      cityAverages.Should().NotBeEmpty();
     }

      [Fact]
      public void GroupBy_WithBooleanKey_GroupsByBoolean()
        {
        // Arrange & Act
      var activeCounts = Context.People.AsQueryable()
    .GroupBy(p => p.IsActive)
        .Select(g => new { IsActive = g.Key, Count = g.Count() })
 .ToList();

   // Assert
 activeCounts.Should().HaveCount(2);
  activeCounts.Should().Contain(r => r.IsActive && r.Count == 3);
      activeCounts.Should().Contain(r => !r.IsActive && r.Count == 2);
      }

      [Fact]
      public void Distinct_RemovesDuplicates()
        {
            // Arrange & Act
      var uniqueCities = Context.People.AsQueryable()
  .Select(p => p.City)
   .Distinct()
    .ToList();

   // Assert
 uniqueCities.Should().HaveCount(3);
uniqueCities.Should().Contain("Seattle");
      uniqueCities.Should().Contain("Portland");
   uniqueCities.Should().Contain("Boston");
   }

  [Fact]
      public void Take_LimitsResults()
    {
   // Arrange & Act
 var sampled = Context.People.AsQueryable()
  .OrderBy(p => p.Age)
    .Take(2)
  .ToList();

 // Assert
          sampled.Should().HaveCount(2);
   }

        [Fact]
        public void Skip_SkipsElements()
 {
     // Arrange & Act
        var last = Context.People.AsQueryable()
 .OrderBy(p => p.Age)
.Skip(3)
       .ToList();

  // Assert
          last.Should().HaveCount(2);
 }

  [Fact]
   public void ComplexAggregation_MultipleOperations()
  {
// Arrange & Act
    var result = Context.People.AsQueryable()
        .Where(p => p.IsActive)
 .GroupBy(p => p.City)
  .Select(g => new
      {
     City = g.Key,
        Count = g.Count(),
 AvgAge = g.Average(p => p.Age),
         MaxScore = g.Max(p => p.Score)
        })
  .OrderByDescending(x => x.AvgAge)
    .ToList();

        // Assert
    result.Should().NotBeEmpty();
     }

        [Fact]
        public void GroupBy_ThenOrderBy_SortsGroups()
   {
     // Arrange & Act
      var result = Context.People.AsQueryable()
   .GroupBy(p => p.City)
        .Select(g => new { City = g.Key, Count = g.Count() })
.OrderBy(g => g.City)
         .ToList();

   // Assert
   result.Should().NotBeEmpty();
        }

        [Fact]
        public void Any_WithElements_ReturnsTrue()
  {
 // Arrange & Act
      var hasElements = Context.People.AsQueryable().Any();

    // Assert
 hasElements.Should().BeTrue();
        }

    [Fact]
   public void Any_WithPredicate_ReturnsTrue()
      {
      // Arrange & Act
     var hasActiveUsers = Context.People.AsQueryable()
    .Any(p => p.IsActive);

  // Assert
      hasActiveUsers.Should().BeTrue();
  }

        [Fact]
        public void Any_WithNoMatch_ReturnsFalse()
        {
   // Arrange & Act
   var hasOldUsers = Context.People.AsQueryable()
         .Any(p => p.Age > 100);

          // Assert
 hasOldUsers.Should().BeFalse();
  }

      [Fact]
  public void All_ChecksCondition_ReturnsTrue()
        {
     // Arrange & Act
 var allHaveEmail = Context.People.AsQueryable()
  .All(p => p.Email != null);

     // Assert
     allHaveEmail.Should().BeTrue();
   }

        [Fact]
   public void GroupBy_WithMultipleAggregates()
        {
      // Arrange & Act
   var result = Context.People.AsQueryable()
 .GroupBy(p => p.IsActive)
          .Select(g => new
   {
     IsActive = g.Key,
     Count = g.Count(),
     AvgAge = g.Average(p => p.Age),
 MinAge = g.Min(p => p.Age),
    MaxAge = g.Max(p => p.Age)
  })
       .ToList();

      // Assert
       result.Should().HaveCount(2);
 }

        [Fact]
    public void OrderBy_ThenGroupBy_OrdersBeforeGrouping()
   {
         // Arrange & Act
       var result = Context.People.AsQueryable()
    .OrderBy(p => p.Age)
    .GroupBy(p => p.City)
   .Select(g => new { City = g.Key, Count = g.Count() })
       .ToList();

   // Assert
result.Should().NotBeEmpty();
        }

        [Fact]
public void Select_ThenDistinct_RemovesDuplicateProjections()
   {
  // Arrange & Act
 var result = Context.People.AsQueryable()
   .Select(p => new { p.City, p.IsActive })
       .Distinct()
           .ToList();

       // Assert
  result.Should().NotBeEmpty();
        }

   [Fact]
        public void GroupBy_WithFilter_FiltersBeforeGrouping()
        {
     // Arrange & Act
          var result = Context.People.AsQueryable()
      .Where(p => p.Age > 25)
     .GroupBy(p => p.City)
      .Select(g => new { City = g.Key, Count = g.Count() })
    .ToList();

   // Assert
   result.Should().NotBeEmpty();
   }

     [Fact]
        public void Count_WithComplexFilter()
   {
  // Arrange & Act
var count = Context.People.AsQueryable()
     .Where(p => p.IsActive && p.Age > 25 && p.Score > 90)
      .Count();

// Assert
      count.Should().BeGreaterThan(0);
  }

    [Fact]
  public void Sum_WithFilter()
        {
   // Arrange & Act
   var totalScore = Context.People.AsQueryable()
       .Where(p => p.IsActive)
        .Sum(p => p.Score);

        // Assert
   totalScore.Should().BeGreaterThan(0);
        }

 [Fact]
        public void Average_WithGroupBy()
        {
   // Arrange & Act
       var result = Context.People.AsQueryable()
     .GroupBy(p => p.IsActive)
     .Select(g => new { IsActive = g.Key, AvgScore = g.Average(p => p.Score) })
      .ToList();

   // Assert
 result.Should().HaveCount(2);
        }

   [Fact]
     public void Min_WithGroupBy()
        {
   // Arrange & Act
   var result = Context.People.AsQueryable()
  .GroupBy(p => p.City)
      .Select(g => new { City = g.Key, MinAge = g.Min(p => p.Age) })
    .ToList();

   // Assert
  result.Should().NotBeEmpty();
        }

   [Fact]
    public void Max_WithGroupBy()
        {
       // Arrange & Act
            var result = Context.People.AsQueryable()
    .GroupBy(p => p.City)
   .Select(g => new { City = g.Key, MaxScore = g.Max(p => p.Score) })
       .ToList();

// Assert
  result.Should().NotBeEmpty();
        }

      [Fact]
    public void Skip_AndTake_Pagination()
{
       // Arrange & Act
  var result = Context.People.AsQueryable()
    .OrderBy(p => p.Name)
       .Skip(2)
     .Take(2)
    .ToList();

    // Assert
 result.Should().HaveCount(2);
        }

 [Fact]
   public void OrderBy_ThenSelect_SortsThenProjects()
        {
     // Arrange & Act
      var result = Context.People.AsQueryable()
  .OrderBy(p => p.Age)
   .Select(p => new { p.Name, p.Age })
  .ToList();

 // Assert
     result.Should().HaveCount(5);
   result.Should().BeInAscendingOrder(r => r.Age);
     }

        [Fact]
public void GroupBy_WithSum()
      {
      // Arrange & Act
            var result = Context.People.AsQueryable()
    .GroupBy(p => p.City)
              .Select(g => new { City = g.Key, TotalAge = g.Sum(p => p.Age) })
     .ToList();

     // Assert
result.Should().NotBeEmpty();
  }

   [Fact]
      public void First_WithOrdering()
        {
   // Arrange & Act
   var result = Context.People.AsQueryable()
.OrderBy(p => p.Age)
      .First();

   // Assert
   result.Should().NotBeNull();
    result.Age.Should().Be(25); // Bob is youngest
 }

  [Fact]
        public void Last_WithOrdering()
        {
     // Arrange & Act
var result = Context.People.AsQueryable()
    .OrderBy(p => p.Age)
     .ToList()
   .Last();

// Assert
result.Should().NotBeNull();
  result.Age.Should().Be(42); // Eve is oldest
      }
    }
}
