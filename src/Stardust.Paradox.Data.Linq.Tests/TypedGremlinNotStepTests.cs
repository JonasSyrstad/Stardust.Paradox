using FluentAssertions;
using Stardust.Paradox.Data.Linq;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for the NOT step in typed Gremlin queries
    /// </summary>
    public class TypedGremlinNotStepTests : LinqTestBase
    {
        #region Not Step Tests

      [Fact]
        public void GraphTraversal_Not_Builds_Correct_Query()
        {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

       // Act
            var result = traversal.Not(t => t.Has("name", "Alice"));

    // Assert
            result.ToGremlinQuery().Should().Be("not(has('name', 'Alice'))");
 }

        [Fact]
        public void GraphTraversal_Not_With_Complex_Predicate_Builds_Correct_Query()
 {
         // Arrange
        var traversal = new GraphTraversal<IPerson>();

     // Act
 var result = traversal
             .OutE("knows")
                .Not(t => t.Has("years", P.Gt(5)));

 // Assert
  result.ToGremlinQuery().Should().Be("outE('knows').not(has('years', P.gt(5)))");
        }

      [Fact]
      public void GraphTraversal_Not_With_Multiple_Conditions_Builds_Correct_Query()
 {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

        // Act
 var result = traversal.Not(t => t.Has("age", P.Lt(18)).Has("active", false));

      // Assert
            var query = result.ToGremlinQuery();
            query.Should().Contain("not(");
            query.Should().Contain("has('age', P.lt(18))");
            query.Should().Contain("has('active', false)");
   }

      [Fact]
   public void GraphTraversal_Not_Chained_With_Other_Steps_Builds_Correct_Query()
        {
            // Arrange
      var traversal = new GraphTraversal<IPerson>();

     // Act
            var result = traversal
    .OutE("knows")
                .Not(t => t.Has("years", 0))
       .Has("active", true)
    .Limit(10);

        // Assert
        var query = result.ToGremlinQuery();
            query.Should().Contain("outE('knows')");
       query.Should().Contain("not(has('years', 0))");
            query.Should().Contain("has('active', true)");
    query.Should().Contain("limit(10)");
        }

        [Fact]
 public void GraphTraversal_Not_With_OutE_Inside_Builds_Correct_Query()
        {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

        // Act
            var result = traversal.Not(t => t.OutE("blocked"));

  // Assert
   result.ToGremlinQuery().Should().Be("not(outE('blocked'))");
        }

   [Fact]
        public void GraphTraversal_Not_Combined_With_And_Builds_Correct_Query()
        {
    // Arrange
    var traversal = new GraphTraversal<IPerson>();

    // Act
            var result = traversal.And(
t => t.Has("age", P.Gt(18)),
     t => t.Not(sub => sub.Has("name", "Admin"))
            );

 // Assert
    var query = result.ToGremlinQuery();
    query.Should().Contain("and(");
  query.Should().Contain("has('age', P.gt(18))");
            query.Should().Contain("not(has('name', 'Admin'))");
        }

        [Fact]
        public void GraphTraversal_Not_With_InE_Builds_Correct_Query()
    {
       // Arrange
   var traversal = new GraphTraversal<IPerson>();

            // Act
            var result = traversal.Not(t => t.InE("blockedBy"));

    // Assert
  result.ToGremlinQuery().Should().Be("not(inE('blockedBy'))");
      }

     [Fact]
    public void GraphTraversal_Not_With_Boolean_Value_Builds_Correct_Query()
        {
       // Arrange
        var traversal = new GraphTraversal<IPerson>();

          // Act
          var result = traversal.Not(t => t.Has("deleted", true));

    // Assert
        result.ToGremlinQuery().Should().Be("not(has('deleted', true))");
        }

        [Fact]
        public void GraphTraversal_Not_With_String_Value_Builds_Correct_Query()
        {
    // Arrange
            var traversal = new GraphTraversal<IPerson>();

          // Act
    var result = traversal.Not(t => t.Has("status", "banned"));

            // Assert
 result.ToGremlinQuery().Should().Be("not(has('status', 'banned'))");
        }

        [Fact]
        public void GraphTraversal_Not_With_Numeric_Predicate_Builds_Correct_Query()
        {
   // Arrange
var traversal = new GraphTraversal<IPerson>();

            // Act
      var result = traversal.Not(t => t.Has("score", P.Lte(0)));

       // Assert
  result.ToGremlinQuery().Should().Be("not(has('score', P.lte(0)))");
        }

        #endregion

        #region Complex Chains with Not

        [Fact]
     public void GraphTraversal_Complex_Not_With_Multiple_Chained_Steps()
        {
      // Arrange
            var traversal = new GraphTraversal<IPerson>();

// Act
          var result = traversal
      .OutE("knows")
        .Not(t => t
         .Has("years", P.Gt(10))
        .Has("active", true)
   .Dedup()
            )
   .Limit(20);

  // Assert
       var query = result.ToGremlinQuery();
            query.Should().StartWith("outE('knows')");
  query.Should().Contain("not(");
     query.Should().Contain("has('years', P.gt(10))");
            query.Should().Contain("has('active', true)");
   query.Should().Contain("dedup()");
      query.Should().EndWith("limit(20)");
        }

      [Fact]
      public void GraphTraversal_Not_With_And_Inside()
        {
      // Arrange
            var traversal = new GraphTraversal<IPerson>();

     // Act
     var result = traversal.Not(t => t.And(
        sub1 => sub1.Has("role", "admin"),
 sub2 => sub2.Has("level", P.Gt(5))
 ));

        // Assert
            var query = result.ToGremlinQuery();
     query.Should().Contain("not(and(");
    query.Should().Contain("has('role', 'admin')");
query.Should().Contain("has('level', P.gt(5))");
        }

        [Fact]
        public void GraphTraversal_Not_With_Or_Inside()
   {
       // Arrange
     var traversal = new GraphTraversal<IPerson>();

            // Act
         var result = traversal.Not(t => t.Or(
 sub1 => sub1.Has("status", "deleted"),
                sub2 => sub2.Has("status", "banned")
            ));

       // Assert
    var query = result.ToGremlinQuery();
        query.Should().Contain("not(or(");
            query.Should().Contain("has('status', 'deleted')");
 query.Should().Contain("has('status', 'banned')");
        }

    [Fact]
   public void GraphTraversal_Multiple_Not_Chained()
        {
            // Arrange
       var traversal = new GraphTraversal<IPerson>();

        // Act
    var result = traversal
.Not(t => t.Has("role", "bot"))
.Not(t => t.Has("deleted", true));

    // Assert
            var query = result.ToGremlinQuery();
  query.Should().Contain("not(has('role', 'bot'))");
            query.Should().Contain("not(has('deleted', true))");
  }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public void GraphTraversal_RealWorld_Not_Blocked_Friends()
        {
        // Arrange
       var traversal = new GraphTraversal<IPerson>();

 // Act - Find friends who are NOT blocked
            var result = traversal
    .OutE("friends")
    .Not(t => t.Has("status", "blocked"))
    .Has("active", true);

          // Assert
      var query = result.ToGremlinQuery();
            query.Should().Contain("outE('friends')");
      query.Should().Contain("not(has('status', 'blocked'))");
     query.Should().Contain("has('active', true)");
        }

        [Fact]
        public void GraphTraversal_RealWorld_Active_Adults_Not_Admin()
        {
        // Arrange
            var traversal = new GraphTraversal<IPerson>();

        // Act
            var result = traversal
       .Has("age", P.Gte(18))
      .Has("active", true)
           .Not(t => t.Has("role", "admin"))
          .Limit(100);

       // Assert
  var query = result.ToGremlinQuery();
   query.Should().Contain("has('age', P.gte(18))");
        query.Should().Contain("has('active', true)");
            query.Should().Contain("not(has('role', 'admin'))");
            query.Should().Contain("limit(100)");
        }

        [Fact]
        public void GraphTraversal_RealWorld_People_Without_Outgoing_Relationships()
        {
     // Arrange
       var traversal = new GraphTraversal<IPerson>();

       // Act - Find people with no outgoing "knows" edges
            var result = traversal.Not(t => t.OutE("knows"));

            // Assert
    result.ToGremlinQuery().Should().Be("not(outE('knows'))");
  }

        [Fact]
        public void GraphTraversal_RealWorld_Non_Junior_Developers()
        {
 // Arrange
         var traversal = new GraphTraversal<IPerson>();

            // Act - Find developers who are NOT junior (experience > 2 years)
            var result = traversal
                .Has("role", "developer")
   .Not(t => t.Has("experience", P.Lte(2)));

            // Assert
            var query = result.ToGremlinQuery();
   query.Should().Contain("has('role', 'developer')");
     query.Should().Contain("not(has('experience', P.lte(2)))");
  }

        [Fact]
        public void GraphTraversal_RealWorld_Filter_Inactive_Or_Deleted()
      {
            // Arrange
      var traversal = new GraphTraversal<IPerson>();

            // Act - Exclude inactive or deleted users
      var result = traversal
   .Not(t => t.Or(
          sub1 => sub1.Has("active", false),
       sub2 => sub2.Has("deleted", true)
     ))
                .Skip(20)
      .Limit(50);

      // Assert
          var query = result.ToGremlinQuery();
            query.Should().Contain("not(or(");
            query.Should().Contain("has('active', false)");
   query.Should().Contain("has('deleted', true)");
            query.Should().Contain("skip(20)");
            query.Should().Contain("limit(50)");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void GraphTraversal_Not_Empty_Predicate()
        {
     // Arrange
        var traversal = new GraphTraversal<IPerson>();

 // Act - Not with essentially empty traversal
            var result = traversal.Not(t => new GraphTraversal<IPerson>());

          // Assert
       result.ToGremlinQuery().Should().Be("not()");
        }

        [Fact]
 public void GraphTraversal_Not_With_Nested_Not()
  {
            // Arrange
         var traversal = new GraphTraversal<IPerson>();

            // Act - Double negative (NOT NOT has)
            var result = traversal.Not(t => t.Not(sub => sub.Has("verified", true)));

       // Assert
        var query = result.ToGremlinQuery();
       query.Should().Contain("not(not(has('verified', true)))");
        }

      [Fact]
     public void GraphTraversal_Not_With_As_And_Select()
        {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

// Act
            var result = traversal
              .As("person")
                .Not(t => t.OutE("blocked").Has("permanent", true));

  // Assert
      var query = result.ToGremlinQuery();
  query.Should().Contain("as('person')");
        query.Should().Contain("not(outE('blocked').has('permanent', true))");
      }

        #endregion
    }
}
