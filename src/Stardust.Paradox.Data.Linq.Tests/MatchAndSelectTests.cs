using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Comprehensive tests for selection, projection, and pattern matching operations in LINQ to Gremlin
    /// </summary>
 public class MatchAndSelectTests : LinqTestBase
    {
  [Fact]
        public void Select_SingleProperty_ReturnsPropertyValue()
      {
            // Arrange & Act
    var names = Context.People.AsQueryable()
      .Where(p => p.IsActive)
     .Select(p => p.Name)
      .ToList();

            // Assert
       names.Should().NotBeEmpty();
            names.Should().HaveCount(3);
       names.Should().Contain("Alice Johnson");
        }

        [Fact]
        public void Select_MultipleProperties_CreatesProjection()
   {
   // Arrange & Act
        var projections = Context.People.AsQueryable()
      .Select(p => new { p.Name, p.Email, p.Age })
       .ToList();

        // Assert
  projections.Should().HaveCount(5);
      projections.Should().AllSatisfy(p =>
       {
         p.Name.Should().NotBeNullOrEmpty();
       p.Email.Should().NotBeNullOrEmpty();
    p.Age.Should().BeGreaterThan(0);
    });
  }

        [Fact]
        public void Select_ComplexProjection_WithConditionalLogic()
   {
   // Arrange & Act
        var result = Context.People.AsQueryable()
      .Select(p => new
  {
        p.Name,
   p.Age,
 AgeGroup = p.Age < 30 ? "Young" : "Mature",
            Status = p.IsActive ? "Active" : "Inactive",
     Score = p.Score
  })
  .ToList();

     // Assert
  result.Should().HaveCount(5);
        result.Should().Contain(r => r.AgeGroup == "Young");
   result.Should().Contain(r => r.AgeGroup == "Mature");
        }

        [Fact]
public void Where_ThenSelect_FiltersAndProjects()
   {
    // Arrange & Act
            var result = Context.People.AsQueryable()
        .Where(p => p.IsActive)
     .Select(p => new { p.Name, p.Age, p.City })
    .ToList();

       // Assert
  result.Should().HaveCount(3);
result.Should().AllSatisfy(r => r.Name.Should().NotBeNullOrEmpty());
        }

        [Fact]
   public void Select_WithOrderBy_OrdersThenProjects()
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
        public void GroupBy_ThenSelect_AggregatesData()
      {
     // Arrange & Act
            var result = Context.People.AsQueryable()
      .GroupBy(p => p.City)
    .Select(g => new
     {
     City = g.Key,
  Count = g.Count(),
     AverageAge = g.Average(p => p.Age),
        MaxScore = g.Max(p => p.Score)
       })
   .ToList();

     // Assert
     result.Should().NotBeEmpty();
     var seattleGroup = result.FirstOrDefault(r => r.City == "Seattle");
         seattleGroup.Should().NotBeNull();
seattleGroup!.Count.Should().Be(3);
  }

        [Fact]
        public void SelectMany_FlattensCollections()
        {
  // Arrange & Act
var result = Context.People.AsQueryable()
     .Where(p => p.Age > 25)
        .SelectMany(p => new[] 
        {
        new { p.Name, Property = "Age", Value = p.Age.ToString() },
          new { p.Name, Property = "Score", Value = p.Score.ToString() }
        })
     .ToList();

   // Assert
   result.Should().NotBeEmpty();
     result.Should().HaveCountGreaterOrEqualTo(6); // At least 3 people * 2 properties
  }

        [Fact]
        public void Select_WithStringManipulation()
        {
      // Arrange & Act
  var result = Context.People.AsQueryable()
.Select(p => new
         {
          OriginalName = p.Name,
   UpperName = p.Name.ToUpper(),
    EmailDomain = p.Email.Contains("@") ? p.Email.Substring(p.Email.IndexOf("@")) : ""
   })
    .ToList();

// Assert
       result.Should().HaveCount(5);
 result.Should().AllSatisfy(r =>
      {
     r.OriginalName.Should().NotBeNullOrEmpty();
      r.UpperName.Should().NotBeNullOrEmpty();
  });
        }

        [Fact]
   public void Where_WithMultipleConditions_ThenSelect()
        {
     // Arrange & Act
       var result = Context.People.AsQueryable()
       .Where(p => p.IsActive && p.Age > 25 && p.Score > 90)
         .Select(p => new { p.Name, p.Age, p.Score })
           .ToList();

    // Assert
 result.Should().NotBeEmpty();
    result.Should().AllSatisfy(r =>
       {
        r.Age.Should().BeGreaterThan(25);
        r.Score.Should().BeGreaterThan(90);
         });
        }

        [Fact]
  public void Select_WithCalculatedFields()
        {
   // Arrange & Act
        var result = Context.People.AsQueryable()
     .Select(p => new
      {
      p.Name,
   p.Age,
        AgePlusScore = p.Age + (int)p.Score,
        IsEligible = p.Age >= 30 && p.IsActive
        })
        .ToList();

    // Assert
  result.Should().HaveCount(5);
     result.Should().Contain(r => r.IsEligible);
        }

        [Fact]
  public void FirstOrDefault_WithProjection()
{
      // Arrange & Act
 var result = Context.People.AsQueryable()
     .Where(p => p.Name == "Alice Johnson")
     .Select(p => new { p.Name, p.Age, p.Email })
    .FirstOrDefault();

    // Assert
   result.Should().NotBeNull();
  result!.Name.Should().Be("Alice Johnson");
 result.Age.Should().Be(30);
        }

        [Fact]
  public void Select_WithConditionalProjection_AndGrouping()
{
    // Arrange & Act
 var result = Context.People.AsQueryable()
     .Select(p => new
       {
    p.Name,
    Category = p.Age < 30 ? "Young" : p.Age < 40 ? "Middle" : "Senior"
  })
      .GroupBy(p => p.Category)
         .Select(g => new
       {
   Category = g.Key,
   Count = g.Count()
      })
   .ToList();

// Assert
       result.Should().HaveCount(3);
     result.Should().Contain(r => r.Category == "Young");
}

     [Fact]
 public void Select_ThenOrderBy_ProjectionThenSort()
 {
            // Arrange & Act
         var result = Context.People.AsQueryable()
  .Select(p => new { p.Name, p.Age })
  .OrderByDescending(p => p.Age)
  .ToList();

// Assert
       result.Should().HaveCount(5);
  result.First().Age.Should().Be(42); // Eve
     result.Last().Age.Should().Be(25); // Bob
}

[Fact]
   public void Select_WithTake_LimitsProjection()
        {
  // Arrange & Act
  var result = Context.People.AsQueryable()
     .OrderBy(p => p.Name)
   .Select(p => new { p.Name, p.Email })
 .Take(3)
  .ToList();

        // Assert
   result.Should().HaveCount(3);
        }

 [Fact]
   public void Select_WithSkipAndTake_PaginatedProjection()
     {
            // Arrange & Act
  var result = Context.People.AsQueryable()
   .OrderBy(p => p.Name)
      .Select(p => new { p.Name, p.Age })
   .Skip(1)
      .Take(2)
    .ToList();

  // Assert
            result.Should().HaveCount(2);
  }

   [Fact]
  public void Select_Distinct_RemovesDuplicateProjections()
        {
   // Arrange & Act
var result = Context.People.AsQueryable()
      .Select(p => p.City)
     .Distinct()
  .ToList();

       // Assert
            result.Should().HaveCount(3); // Seattle, Portland, Boston
  result.Should().Contain("Seattle");
       result.Should().Contain("Portland");
     result.Should().Contain("Boston");
        }

        [Fact]
        public void Select_WithMultipleWhere_FiltersThenProjects()
        {
   // Arrange & Act
     var result = Context.People.AsQueryable()
    .Where(p => p.IsActive)
              .Where(p => p.Age > 25)
 .Where(p => p.City == "Seattle")
     .Select(p => new { p.Name, p.Age })
         .ToList();

       // Assert
  result.Should().ContainSingle();
   result.First().Name.Should().Be("Alice Johnson");
        }

 [Fact]
        public void GroupBy_SelectMultipleAggregates()
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
 MaxAge = g.Max(p => p.Age),
        SumScore = g.Sum(p => p.Score)
        })
      .ToList();

// Assert
     result.Should().HaveCount(2);
  }

        [Fact]
   public void Select_WithComplexNestedProjection()
        {
   // Arrange & Act
       var result = Context.People.AsQueryable()
                .Select(p => new
    {
     PersonInfo = new
 {
        p.Name,
   p.Age
   },
            ContactInfo = new
  {
   p.Email,
 p.City
   },
      Metrics = new
       {
    p.Score,
      p.IsActive
    }
       })
    .ToList();

// Assert
   result.Should().HaveCount(5);
       result.Should().AllSatisfy(r =>
{
     r.PersonInfo.Should().NotBeNull();
   r.ContactInfo.Should().NotBeNull();
  r.Metrics.Should().NotBeNull();
       });
   }

      [Fact]
public void Select_WithAny_ProjectsWithConditionCheck()
   {
   // Arrange & Act
            var result = Context.People.AsQueryable()
    .Select(p => new
      {
   p.Name,
    HasLongName = p.Name.Length > 10,
        IsAdult = p.Age >= 18,
    HighScorer = p.Score > 90
    })
                .ToList();

// Assert
 result.Should().HaveCount(5);
  result.Should().Contain(r => r.HasLongName);
        }

      [Fact]
 public void Select_WithMathOperations()
{
  // Arrange & Act
     var result = Context.People.AsQueryable()
       .Select(p => new
      {
   p.Name,
       p.Age,
  AgeSquared = p.Age * p.Age,
      ScorePercentage = p.Score / 100m,
 Combined = p.Age + (int)p.Score
          })
            .ToList();

   // Assert
    result.Should().HaveCount(5);
 }

        [Fact]
        public void Select_WithOrderByMultipleKeys()
        {
   // Arrange & Act
            var result = Context.People.AsQueryable()
 .OrderBy(p => p.IsActive)
     .ThenByDescending(p => p.Age)
.Select(p => new { p.Name, p.IsActive, p.Age })
    .ToList();

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public void Count_AfterSelect_CountsProjections()
        {
       // Arrange & Act
        var count = Context.People.AsQueryable()
.Select(p => new { p.Name, p.Age })
           .Count();

      // Assert
     count.Should().Be(5);
   }

        [Fact]
        public void Any_AfterSelect_ChecksProjectionExists()
        {
   // Arrange & Act
          var exists = Context.People.AsQueryable()
             .Select(p => new { p.Name, p.Age })
    .Any(p => p.Age > 40);

     // Assert
   exists.Should().BeTrue();
    }
    }
}
