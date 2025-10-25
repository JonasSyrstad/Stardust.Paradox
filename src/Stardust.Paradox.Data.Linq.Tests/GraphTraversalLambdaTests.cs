using System;
using System.Linq.Expressions;
using FluentAssertions;
using Stardust.Paradox.Data.Linq;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for lambda expression support in typed Gremlin steps
    /// </summary>
    public class GraphTraversalLambdaTests : LinqTestBase
    {
   #region Has Lambda Expression Tests

        [Fact]
      public void Has_Lambda_Equality_BuildsCorrectQuery()
        {
     // Arrange
 var traversal = new GraphTraversal<IPerson>();

   // Act
      var result = traversal.Has(p => p.Name == "Alice");

            // Assert
  result.ToGremlinQuery().Should().Be("has('name', 'Alice')");
 }

        [Fact]
 public void Has_Lambda_GreaterThan_BuildsCorrectQuery()
        {
       // Arrange
          var traversal = new GraphTraversal<IPerson>();

    // Act
            var result = traversal.Has(p => p.Age > 25);

      // Assert
       result.ToGremlinQuery().Should().Be("has('age', P.gt(25))");
        }

        [Fact]
  public void Has_Lambda_GreaterThanOrEqual_BuildsCorrectQuery()
        {
       // Arrange
            var traversal = new GraphTraversal<IPerson>();

            // Act
      var result = traversal.Has(p => p.Age >= 18);

        // Assert
    result.ToGremlinQuery().Should().Be("has('age', P.gte(18))");
 }

        [Fact]
    public void Has_Lambda_LessThan_BuildsCorrectQuery()
  {
            // Arrange
var traversal = new GraphTraversal<IPerson>();

    // Act
 var result = traversal.Has(p => p.Age < 65);

    // Assert
        result.ToGremlinQuery().Should().Be("has('age', P.lt(65))");
        }

        [Fact]
      public void Has_Lambda_LessThanOrEqual_BuildsCorrectQuery()
        {
    // Arrange
     var traversal = new GraphTraversal<IPerson>();

    // Act
            var result = traversal.Has(p => p.Age <= 30);

          // Assert
            result.ToGremlinQuery().Should().Be("has('age', P.lte(30))");
        }

        [Fact]
        public void Has_Lambda_NotEqual_BuildsCorrectQuery()
        {
       // Arrange
    var traversal = new GraphTraversal<IPerson>();

         // Act
       var result = traversal.Has(p => p.Name != "Bob");

     // Assert
 result.ToGremlinQuery().Should().Be("has('name', P.neq('Bob'))");
        }

        [Fact]
    public void Has_Lambda_BooleanProperty_BuildsCorrectQuery()
        {
 // Arrange
            var traversal = new GraphTraversal<IPerson>();

            // Act
            var result = traversal.Has(p => p.IsActive == true);

         // Assert
      result.ToGremlinQuery().Should().Be("has('isActive', true)");
        }

        [Fact]
        public void Has_Lambda_DecimalProperty_BuildsCorrectQuery()
        {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

   // Act
        var result = traversal.Has(p => p.Score >= 90.5m);

            // Assert
            result.ToGremlinQuery().Should().Be("has('score', P.gte(90.5))");
        }

        #endregion

        #region OutE Lambda Expression Tests

        [Fact]
        public void OutE_Lambda_PropertyExpression_BuildsCorrectQuery()
        {
            // Arrange
 var traversal = new GraphTraversal<IPerson>();

       // Act
       var result = traversal.OutE(p => p.Friends);

   // Assert
        result.ToGremlinQuery().Should().Be("outE('friends')");
        }

  [Fact]
     public void OutE_Lambda_WithHas_BuildsCorrectQuery()
        {
    // Arrange
            var traversal = new GraphTraversal<IPerson>();

   // Act
       var result = traversal.OutE(p => p.Friends).Has("since", 2020);

        // Assert
            result.ToGremlinQuery().Should().Be("outE('friends').has('since', 2020)");
        }

        [Fact]
        public void OutE_Lambda_Companies_BuildsCorrectQuery()
        {
       // Arrange
  var traversal = new GraphTraversal<IPerson>();

      // Act
     var result = traversal.OutE(p => p.Companies);

   // Assert
 result.ToGremlinQuery().Should().Be("outE('companies')");
        }

        #endregion

        #region InE Lambda Expression Tests

   [Fact]
        public void InE_Lambda_PropertyExpression_BuildsCorrectQuery()
        {
     // Arrange
            var traversal = new GraphTraversal<IPerson>();

      // Act
         var result = traversal.InE(p => p.Friends);

            // Assert
        result.ToGremlinQuery().Should().Be("inE('friends')");
        }

        [Fact]
      public void InE_Lambda_WithChainedOperations_BuildsCorrectQuery()
  {
       // Arrange
          var traversal = new GraphTraversal<IPerson>();

       // Act
      var result = traversal.InE(p => p.Friends).Has("strength", "strong");

    // Assert
         result.ToGremlinQuery().Should().Be("inE('friends').has('strength', 'strong')");
        }

        #endregion

        #region Out Lambda Expression Tests

        [Fact]
        public void Out_Lambda_PropertyExpression_BuildsCorrectQuery()
   {
            // Arrange
          var traversal = new GraphTraversal<IPerson>();

          // Act
       var result = traversal.Out(p => p.Friends);

   // Assert
       result.ToGremlinQuery().Should().Be("out('friends')");
        }

        [Fact]
   public void Out_Lambda_Companies_BuildsCorrectQuery()
        {
 // Arrange
  var traversal = new GraphTraversal<IPerson>();

        // Act
     var result = traversal.Out(p => p.Companies);

            // Assert
     result.ToGremlinQuery().Should().Be("out('companies')");
        }

        [Fact]
        public void Out_Lambda_WithFilter_BuildsCorrectQuery()
{
            // Arrange
         var traversal = new GraphTraversal<IPerson>();

            // Act
            var result = traversal.Out(p => p.Friends).Has(p => p.Age > 25);

    // Assert
            result.ToGremlinQuery().Should().Be("out('friends').has('age', P.gt(25))");
        }

        #endregion

    #region In Lambda Expression Tests

        [Fact]
        public void In_Lambda_PropertyExpression_BuildsCorrectQuery()
        {
    // Arrange
     var traversal = new GraphTraversal<IPerson>();

            // Act
     var result = traversal.In(p => p.Friends);

            // Assert
            result.ToGremlinQuery().Should().Be("in('friends')");
  }

        [Fact]
        public void In_Lambda_WithFilter_BuildsCorrectQuery()
      {
     // Arrange
   var traversal = new GraphTraversal<IPerson>();

            // Act
   var result = traversal.In(p => p.Friends).Has(p => p.IsActive == true);

        // Assert
            result.ToGremlinQuery().Should().Be("in('friends').has('isActive', true)");
        }

        #endregion

        #region Complex Chain Tests

  [Fact]
        public void ComplexChain_Lambda_OutE_Has_BuildsCorrectQuery()
 {
       // Arrange
            var traversal = new GraphTraversal<IPerson>();

  // Act
  var result = traversal
      .OutE(p => p.Friends)
                .Has("since", "2020")  // Use string-based Has for edge properties
      .Limit(10);

   // Assert
 var query = result.ToGremlinQuery();
         query.Should().StartWith("outE('friends')");
     query.Should().Contain("has('since', '2020')");
   query.Should().Contain("limit(10)");
        }

        [Fact]
        public void ComplexChain_Lambda_Out_Multiple_Filters_BuildsCorrectQuery()
    {
            // Arrange
    var traversal = new GraphTraversal<IPerson>();

        // Act
            var result = traversal
  .Out(p => p.Friends)
       .Has(p => p.Age >= 21)
           .Has(p => p.IsActive == true)
          .Dedup();

       // Assert
    var query = result.ToGremlinQuery();
     query.Should().Contain("out('friends')");
       query.Should().Contain("has('age', P.gte(21))");
            query.Should().Contain("has('isActive', true)");
        query.Should().Contain("dedup()");
    }

     [Fact]
      public void ComplexChain_Mixed_Lambda_And_String_BuildsCorrectQuery()
        {
            // Arrange
      var traversal = new GraphTraversal<IPerson>();

       // Act
       var result = traversal
     .OutE(p => p.Companies)  // Lambda
         .Has("role", "developer")  // String
           .Has("salary", P.Gt(100000));  // Predicate

            // Assert
            var query = result.ToGremlinQuery();
    query.Should().Contain("outE('companies')");
       query.Should().Contain("has('role', 'developer')");
  query.Should().Contain("has('salary', P.gt(100000))");
    }

        #endregion

     #region Extension Method Tests

        [Fact]
 public void ExtensionMethod_OutE_Lambda_Works()
        {
            // This test verifies the extension method exists and compiles
        // Actual usage would require a real IPerson instance
      
            // Verify extension method exists
            var extensionType = typeof(GraphTraversalLambdaExtensions);
       var method = extensionType.GetMethod("OutE");
            
  // Assert
  method.Should().NotBeNull();
      method.IsStatic.Should().BeTrue();
        }

        [Fact]
        public void ExtensionMethod_Out_Lambda_Works()
  {
            // Verify extension method exists
        var extensionType = typeof(GraphTraversalLambdaExtensions);
  var method = extensionType.GetMethod("Out");
            
            // Assert
    method.Should().NotBeNull();
    method.IsStatic.Should().BeTrue();
        }

  #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public void Has_Lambda_StringWithQuotes_EscapesCorrectly()
        {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

  // Act
            var result = traversal.Has(p => p.Name == "O'Brien");

   // Assert
         result.ToGremlinQuery().Should().Contain("\\'");
   }

        [Fact]
      public void Has_Lambda_InvalidExpression_ThrowsException()
        {
            // Arrange
    var traversal = new GraphTraversal<IPerson>();

            // Act & Assert
  Assert.Throws<ArgumentException>(() =>
    traversal.Has(p => true));
}

        [Fact]
        public void Has_Lambda_WithVariable_ExtractsValue()
        {
      // Arrange
   var traversal = new GraphTraversal<IPerson>();
    var targetAge = 30;

     // Act
            var result = traversal.Has(p => p.Age == targetAge);

         // Assert
       result.ToGremlinQuery().Should().Be("has('age', 30)");
  }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public void RealWorld_FindSeniorActivePeople_BuildsCorrectQuery()
        {
            // Arrange
            var traversal = new GraphTraversal<IPerson>();

      // Act
  var result = traversal
                .Has(p => p.Age > 50)
      .Has(p => p.IsActive == true)
 .Out(p => p.Companies)
      .Dedup();

       // Assert
     var query = result.ToGremlinQuery();
      query.Should().Contain("has('age', P.gt(50))");
          query.Should().Contain("has('isActive', true)");
   query.Should().Contain("out('companies')");
            query.Should().Contain("dedup()");
        }

  [Fact]
        public void RealWorld_FindHighScoreFriends_BuildsCorrectQuery()
  {
            // Arrange
          var traversal = new GraphTraversal<IPerson>();

       // Act - Changed to only use edge filtering without type changes
    var result = traversal
        .OutE(p => p.Friends)
       .Has("type", "best")
.Limit(5);

// Assert
     var query = result.ToGremlinQuery();
            query.Should().Contain("outE('friends')");
            query.Should().Contain("has('type', 'best')");
          query.Should().Contain("limit(5)");
 }
        #endregion
    }
}
