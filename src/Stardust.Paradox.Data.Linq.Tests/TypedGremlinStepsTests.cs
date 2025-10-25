using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Stardust.Paradox.Data.Linq;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for typed Gremlin steps in LINQ queries
    /// </summary>
    public class TypedGremlinStepsTests : LinqTestBase
    {
      [Fact]
     public void GraphTraversal_OutE_Builds_Correct_Query()
        {
            // Arrange
    var traversal = new GraphTraversal<IPerson>();

       // Act
            var result = traversal.OutE("knows");

       // Assert
     result.ToGremlinQuery().Should().Be("outE('knows')");
        }

        [Fact]
        public void GraphTraversal_OutE_With_Has_Builds_Correct_Query()
  {
          // Arrange
    var traversal = new GraphTraversal<IPerson>();

            // Act
      var result = traversal.OutE("knows").Has("since", 2010);

            // Assert
            result.ToGremlinQuery().Should().Be("outE('knows').has('since', 2010)");
      }

      [Fact]
        public void GraphTraversal_OutE_With_Predicate_Builds_Correct_Query()
      {
     // Arrange
       var traversal = new GraphTraversal<IPerson>();

   // Act
     var result = traversal.OutE("knows").Has("years", P.Gt(2));

   // Assert
     result.ToGremlinQuery().Should().Be("outE('knows').has('years', P.gt(2))");
  }

   [Fact]
     public void GraphTraversal_InE_Builds_Correct_Query()
 {
            // Arrange
        var traversal = new GraphTraversal<IPerson>();

    // Act
   var result = traversal.InE("knows");

// Assert
    result.ToGremlinQuery().Should().Be("inE('knows')");
        }

    [Fact]
        public void GraphTraversal_Chained_Operations_Build_Correct_Query()
        {
       // Arrange
            var traversal = new GraphTraversal<IPerson>();

    // Act
 var result = traversal
  .OutE("knows")
          .Has("years", P.Gt(5))
.Limit(10);

            // Assert
      result.ToGremlinQuery().Should().Be("outE('knows').has('years', P.gt(5)).limit(10)");
        }

        [Fact]
    public void Predicate_Gt_Formats_Correctly()
 {
 // Act
  var predicate = P.Gt(25);

     // Assert
            predicate.PredicateString.Should().Be("P.gt(25)");
        }

        [Fact]
     public void Predicate_Within_Formats_Correctly()
        {
    // Act
        var predicate = P.Within("Alice", "Bob", "Charlie");

  // Assert
            predicate.PredicateString.Should().Be("P.within('Alice', 'Bob', 'Charlie')");
        }

    [Fact]
        public void Predicate_Between_Formats_Correctly()
        {
 // Act
            var predicate = P.Between(18, 65);

            // Assert
            predicate.PredicateString.Should().Be("P.between(18, 65)");
        }

      [Fact]
    public void GraphTraversal_And_Builds_Correct_Query()
        {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

   // Act
      var result = traversal.And(
                t => t.Has("age", P.Gt(18)),
         t => t.Has("active", true)
  );

  // Assert
       result.ToGremlinQuery().Should().Contain("and(");
   result.ToGremlinQuery().Should().Contain("has('age', P.gt(18))");
          result.ToGremlinQuery().Should().Contain("has('active', true)");
        }

        [Fact]
        public void GraphTraversal_Or_Builds_Correct_Query()
        {
      // Arrange
        var traversal = new GraphTraversal<IPerson>();

// Act
      var result = traversal.Or(
      t => t.Has("name", "Alice"),
     t => t.Has("name", "Bob")
      );

      // Assert
      result.ToGremlinQuery().Should().Contain("or(");
            result.ToGremlinQuery().Should().Contain("has('name', 'Alice')");
    result.ToGremlinQuery().Should().Contain("has('name', 'Bob')");
        }

        [Fact]
        public void GraphTraversal_As_Builds_Correct_Query()
        {
            // Arrange
      var traversal = new GraphTraversal<IPerson>();

            // Act
        var result = traversal.OutE("knows").As("relationship");

        // Assert
   result.ToGremlinQuery().Should().Be("outE('knows').as('relationship')");
     }

        [Fact]
      public void GraphTraversal_HasLabel_Builds_Correct_Query()
    {
         // Arrange
   var traversal = new GraphTraversal<IPerson>();

            // Act
            var result = traversal.HasLabel("person", "user");

      // Assert
          result.ToGremlinQuery().Should().Be("hasLabel('person', 'user')");
        }

        [Fact]
   public void GraphTraversal_HasId_Builds_Correct_Query()
        {
    // Arrange
            var traversal = new GraphTraversal<IPerson>();

            // Act
            var result = traversal.HasId("user1", "user2");

            // Assert
         result.ToGremlinQuery().Should().Be("hasId('user1', 'user2')");
        }

        [Fact]
        public void GraphTraversal_Dedup_Builds_Correct_Query()
   {
// Arrange
            var traversal = new GraphTraversal<IPerson>();

            // Act
            var result = traversal.OutE("knows").Dedup();

            // Assert
       result.ToGremlinQuery().Should().Be("outE('knows').dedup()");
  }

        [Fact]
        public void GraphTraversal_DedupBy_Builds_Correct_Query()
{
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

  // Act
         var result = traversal.OutE("knows").DedupBy("years");

    // Assert
        result.ToGremlinQuery().Should().Be("outE('knows').dedup().by('years')");
        }

   [Fact]
  public void GraphTraversal_Complex_Chain_Builds_Correct_Query()
   {
          // Arrange
var traversal = new GraphTraversal<IPerson>();

         // Act
            var result = traversal
       .OutE("knows")
     .Has("years", P.Gt(5))
                .Has("active", true)
    .Dedup()
   .Limit(10);

            // Assert
  var query = result.ToGremlinQuery();
   query.Should().StartWith("outE('knows')");
            query.Should().Contain("has('years', P.gt(5))");
   query.Should().Contain("has('active', true)");
       query.Should().Contain("dedup()");
            query.Should().EndWith("limit(10)");
    }

     [Fact]
        public void Predicate_String_Escapes_Quotes()
        {
   // Act
            var predicate = P.Eq("O'Brien");

       // Assert
            predicate.PredicateString.Should().Contain("\\'");
        }

      [Fact]
        public void Predicate_Bool_Formats_Lowercase()
        {
     // Act
  var predicateTrue = P.Eq(true);
      var predicateFalse = P.Eq(false);

  // Assert
 predicateTrue.PredicateString.Should().Contain("true");
       predicateFalse.PredicateString.Should().Contain("false");
      }

    [Fact]
        public void GraphTraversal_Extension_OutE_Creates_Correct_Traversal()
  {
     // Arrange - using a mock person instance would require actual entity
      // For now, test the extension method signature existence
    
    // This test validates that the extension method exists and compiles
      var traversalType = typeof(GraphTraversalEntityExtensions);
        var methods = traversalType.GetMethods().Where(m => m.Name == "OutE" && m.IsGenericMethod);

// Assert
      methods.Should().NotBeEmpty();
  methods.First().IsGenericMethod.Should().BeTrue();
 }
        // TODO: Integration tests that actually execute queries
        // These would require:
     // 1. Integration with GremlinQueryTranslator to detect and translate traversals
        // 2. End-to-end query execution
        // 3. Verification of results

        /*
        [Fact]
    public async Task LINQ_Where_With_OutE_Traversal_Filters_Correctly()
        {
  // Arrange
            var queryable = Context.People.AsQueryable();

   // Act
       var result = await queryable
  .Where(p => p.OutE("knows").Any())
       .ToListAsync();

  // Assert
            result.Should().NotBeEmpty();
      // Additional assertions based on test data
        }

        [Fact]
    public async Task LINQ_Where_With_OutE_Has_Predicate_Filters_Correctly()
        {
        // Arrange
       var queryable = Context.People.AsQueryable();

  // Act
          var result = await queryable
    .Where(p => p.OutE("knows").Has("years", P.Gt(2)).Any())
         .ToListAsync();

            // Assert
            result.Should().NotBeEmpty();
      // Verify only people with long-term relationships
        }

    [Fact]
        public async Task LINQ_Where_With_Multiple_Conditions_Works()
        {
          // Arrange
     var queryable = Context.People.AsQueryable();

   // Act
        var result = await queryable
                .Where(p => p.Name == "Alice" && 
          p.OutE("knows").Has("years", P.Gt(5)).Count() > 2)
      .ToListAsync();

            // Assert
            result.Should().NotBeEmpty();
         result.All(p => p.Name == "Alice").Should().BeTrue();
        }
    */
    }
}
