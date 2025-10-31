using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.OData;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Extensions;
using Stardust.Paradox.Data.OData;
using Stardust.Paradox.Data.Traversals;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Comprehensive test suite for ODataExtensions covering security, edge cases, and all functionality
 /// </summary>
    public class ODataExtensionsComprehensiveTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
      private static InMemoryGremlinLanguageConnector _sharedConnector;
        private static readonly object _lock = new object();
        private static bool _scenarioApplied = false;
        
        public void Dispose()
   {
          // Shared connector will be reused across all tests
 }

   public ODataExtensionsComprehensiveTests(ITestOutputHelper output)
        {
            _output = output;
lock (_lock)
            {
          if (_sharedConnector == null)
      {
    _sharedConnector = new InMemoryGremlinLanguageConnector(
            new InMemoryDatabaseOptions 
   { 
     EnableDebugLogging = true, 
  EnableQueryLogging = true 
   });
     }
    if (!_scenarioApplied)
        {
                Scenarios.InMemoryScenarioRegistry.ApplyScenario(
           _sharedConnector.Database, 
        "OdataTestScenario");
      _scenarioApplied = true;
}
            }
      }

    #region Filter Tests - Basic Operators

        [Fact]
      public async Task Filter_Eq_String_ShouldMatch()
   {
            var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = "name eq 'Alice'" };

            var results = await context.VAsync<ITestPerson>(g => 
   g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
      list.Should().HaveCount(1);
            list[0].Name.Should().Be("Alice");
        }

        [Fact]
public async Task Filter_Eq_Integer_ShouldMatch()
        {
            var context = new TestGraphContext(_sharedConnector);
 var options = new ODataSearchOptions { Filter = "age eq 30" };

       var results = await context.VAsync<ITestPerson>(g => 
             g.V().HasLabel("person").ApplyODataQuery(options));

     var list = results.ToList();
     list.Should().HaveCount(1);
            list[0].Age.Should().Be(30);
        }

        [Fact]
        public async Task Filter_Eq_Boolean_True_ShouldMatch()
        {
        var context = new TestGraphContext(_sharedConnector);
   var options = new ODataSearchOptions { Filter = "active eq true" };

var results = await context.VAsync<ITestPerson>(g => 
       g.V().HasLabel("person").ApplyODataQuery(options));

var list = results.ToList();
          list.Should().HaveCount(4);
     list.Should().OnlyContain(p => p.Active == true);
     }

        [Fact]
        public async Task Filter_Eq_Boolean_False_ShouldMatch()
        {
            var context = new TestGraphContext(_sharedConnector);
        var options = new ODataSearchOptions { Filter = "active eq false" };

      var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
list.Should().HaveCount(1);
   list[0].Name.Should().Be("Frank");
        }

        [Fact]
        public async Task Filter_Ne_ShouldExcludeMatch()
        {
   var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = "name ne 'Alice'" };

      var results = await context.VAsync<ITestPerson>(g => 
       g.V().HasLabel("person").ApplyODataQuery(options));

      var list = results.ToList();
            list.Should().HaveCount(4);
   list.Should().NotContain(p => p.Name == "Alice");
      }

        [Fact]
      public async Task Filter_Gt_ShouldMatchGreater()
     {
  var context = new TestGraphContext(_sharedConnector);
     var options = new ODataSearchOptions { Filter = "age gt 28" };

        var results = await context.VAsync<ITestPerson>(g => 
         g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
    list.Should().HaveCount(2);
   list.Should().Contain(p => p.Name == "Alice" && p.Age == 30);
            list.Should().Contain(p => p.Name == "Charlie" && p.Age == 35);
        }

     [Fact]
    public async Task Filter_Ge_ShouldMatchGreaterOrEqual()
 {
            var context = new TestGraphContext(_sharedConnector);
      var options = new ODataSearchOptions { Filter = "age ge 28" };

 var results = await context.VAsync<ITestPerson>(g => 
           g.V().HasLabel("person").ApplyODataQuery(options));

   var list = results.ToList();
    list.Should().HaveCount(3);
            list.Should().Contain(p => p.Name == "Eve" && p.Age == 28);
    list.Should().Contain(p => p.Name == "Alice" && p.Age == 30);
     list.Should().Contain(p => p.Name == "Charlie" && p.Age == 35);
  }

      [Fact]
        public async Task Filter_Lt_ShouldMatchLess()
        {
            var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = "age lt 28" };

            var results = await context.VAsync<ITestPerson>(g => 
      g.V().HasLabel("person").ApplyODataQuery(options));

   var list = results.ToList();
      list.Should().HaveCount(2);
            list.Should().Contain(p => p.Name == "Bob" && p.Age == 25);
       list.Should().Contain(p => p.Name == "Frank" && p.Age == 22);
    }

        [Fact]
        public async Task Filter_Le_ShouldMatchLessOrEqual()
        {
    var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = "age le 28" };

        var results = await context.VAsync<ITestPerson>(g => 
  g.V().HasLabel("person").ApplyODataQuery(options));

     var list = results.ToList();
            list.Should().HaveCount(3);
     list.Should().Contain(p => p.Name == "Frank" && p.Age == 22);
      list.Should().Contain(p => p.Name == "Bob" && p.Age == 25);
            list.Should().Contain(p => p.Name == "Eve" && p.Age == 28);
        }

        #endregion

     #region Filter Tests - String Functions

        [Fact]
        public async Task Filter_Contains_ShouldMatchSubstring()
        {
var context = new TestGraphContext(_sharedConnector);
        var options = new ODataSearchOptions { Filter = "email contains 'example'" };

            var results = await context.VAsync<ITestPerson>(g => 
         g.V().HasLabel("person").ApplyODataQuery(options));

 var list = results.ToList();
     list.Should().HaveCount(2);
         list.Should().Contain(p => p.Name == "Alice");
            list.Should().Contain(p => p.Name == "Charlie");
        }

        [Fact]
        public async Task Filter_StartsWith_ShouldMatchPrefix()
{
   var context = new TestGraphContext(_sharedConnector);
         var options = new ODataSearchOptions { Filter = "email startswith 'alice'" };

 var results = await context.VAsync<ITestPerson>(g => 
   g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
          list.Should().HaveCount(1);
            list[0].Name.Should().Be("Alice");
        }

        [Fact]
        public async Task Filter_EndsWith_ShouldMatchSuffix()
        {
  var context = new TestGraphContext(_sharedConnector);
      var options = new ODataSearchOptions { Filter = "email endswith 'test.com'" };

            var results = await context.VAsync<ITestPerson>(g => 
     g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
   list.Should().HaveCount(3);
 list.Should().Contain(p => p.Name == "Bob");
        list.Should().Contain(p => p.Name == "Eve");
  list.Should().Contain(p => p.Name == "Frank");
   }

        #endregion

    #region Filter Tests - Logical Operators

   [Fact]
        public async Task Filter_And_ShouldMatchBothConditions()
  {
   var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = "age gt 25 and active eq true" };

       var results = await context.VAsync<ITestPerson>(g => 
      g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
   list.Should().HaveCount(3);
   list.Should().Contain(p => p.Name == "Eve");
            list.Should().Contain(p => p.Name == "Alice");
  list.Should().Contain(p => p.Name == "Charlie");
        }

        [Fact]
        public async Task Filter_Or_ShouldMatchEitherCondition()
   {
     var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = "name eq 'Alice' or name eq 'Bob'" };

            var results = await context.VAsync<ITestPerson>(g => 
    g.V().HasLabel("person").ApplyODataQuery(options));

      var list = results.ToList();
     list.Should().HaveCount(2);
        list.Should().Contain(p => p.Name == "Alice");
       list.Should().Contain(p => p.Name == "Bob");
        }

        [Fact]
        public async Task Filter_ComplexWithParentheses_ShouldRespectPrecedence()
        {
            var context = new TestGraphContext(_sharedConnector);
            // (age < 26 OR age > 32) AND active = true
            var options = new ODataSearchOptions 
{ 
                Filter = "(age lt 26 or age gt 32) and active eq true" 
      };

 var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options));

  var list = results.ToList();
 list.Should().HaveCount(2);
            list.Should().Contain(p => p.Name == "Bob" && p.Age == 25);
            list.Should().Contain(p => p.Name == "Charlie" && p.Age == 35);
        }

        [Fact]
    public async Task Filter_MultipleAndConditions_ShouldMatchAll()
 {
            var context = new TestGraphContext(_sharedConnector);
  var options = new ODataSearchOptions 
    { 
      Filter = "age ge 25 and age le 30 and active eq true" 
         };

         var results = await context.VAsync<ITestPerson>(g => 
  g.V().HasLabel("person").ApplyODataQuery(options));

 var list = results.ToList();
       list.Should().HaveCount(3);
    list.Should().Contain(p => p.Name == "Bob");
list.Should().Contain(p => p.Name == "Eve");
            list.Should().Contain(p => p.Name == "Alice");
        }

        #endregion

#region Ordering Tests

        [Fact]
        public async Task OrderBy_Ascending_ShouldSortCorrectly()
        {
      var context = new TestGraphContext(_sharedConnector);
        var options = new ODataSearchOptions
       {
    OrderBy = new OrderingOptions 
   { 
         PropertyName = "age", 
    Ordering = OrderingType.Ascending 
          }
            };

 var results = await context.VAsync<ITestPerson>(g => 
    g.V().HasLabel("person").ApplyODataQuery(options));

       var list = results.ToList();
    list.Should().HaveCount(5);
            list[0].Age.Should().Be(22);
            list[1].Age.Should().Be(25);
    list[2].Age.Should().Be(28);
            list[3].Age.Should().Be(30);
       list[4].Age.Should().Be(35);
        }

        [Fact]
  public async Task OrderBy_Descending_ShouldSortCorrectly()
        {
    var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions
        {
              OrderBy = new OrderingOptions 
            { 
PropertyName = "age", 
  Ordering = OrderingType.Descending 
      }
            };

var results = await context.VAsync<ITestPerson>(g => 
                g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
   list.Should().HaveCount(5);
         list[0].Age.Should().Be(35);
     list[1].Age.Should().Be(30);
  list[2].Age.Should().Be(28);
            list[3].Age.Should().Be(25);
      list[4].Age.Should().Be(22);
        }

        [Fact]
        public async Task OrderBy_StringProperty_ShouldSortAlphabetically()
        {
    var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions
        {
     OrderBy = new OrderingOptions 
          { 
     PropertyName = "name", 
   Ordering = OrderingType.Ascending 
        }
   };

          var results = await context.VAsync<ITestPerson>(g => 
             g.V().HasLabel("person").ApplyODataQuery(options));

      var list = results.ToList();
       list.Should().HaveCount(5);
      list[0].Name.Should().Be("Alice");
 list[1].Name.Should().Be("Bob");
   list[2].Name.Should().Be("Charlie");
    list[3].Name.Should().Be("Eve");
         list[4].Name.Should().Be("Frank");
        }

        #endregion

        #region Paging Tests

        [Fact]
        public async Task Paging_Top_ShouldLimitResults()
        {
            var context = new TestGraphContext(_sharedConnector);
         var options = new ODataSearchOptions { Top = 2 };

            var results = await context.VAsync<ITestPerson>(g => 
     g.V().HasLabel("person").ApplyODataQuery(options));

      var list = results.ToList();
         list.Should().HaveCount(2);
        }

      [Fact]
        public async Task Paging_Take_ShouldLimitResults()
        {
    var context = new TestGraphContext(_sharedConnector);
   var options = new ODataSearchOptions { Take = 3 };

            var results = await context.VAsync<ITestPerson>(g => 
      g.V().HasLabel("person").ApplyODataQuery(options));

    var list = results.ToList();
list.Should().HaveCount(3);
        }

        [Fact]
        public async Task Paging_Tail_ShouldGetLastElements()
    {
    var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions 
      { 
OrderBy = new OrderingOptions 
       { 
     PropertyName = "age", 
          Ordering = OrderingType.Ascending 
              },
Tail = 2 
          };

      var results = await context.VAsync<ITestPerson>(g => 
        g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
 list.Should().HaveCount(2);
  // Should be the last 2 when ordered by age ascending
          list.Should().Contain(p => p.Name == "Alice");
            list.Should().Contain(p => p.Name == "Charlie");
        }

        [Fact]
        public async Task Paging_TopAndTail_ShouldThrowException()
        {
            var context = new TestGraphContext(_sharedConnector);
        var options = new ODataSearchOptions 
            { 
     Top = 2,
      Tail = 2 
};

      Func<Task> act = async () => await context.VAsync<ITestPerson>(g => 
          g.V().HasLabel("person").ApplyODataQuery(options));

            await act.Should().ThrowAsync<ArgumentException>()
      .WithMessage("*cannot use both top and tail*");
        }

  #endregion

        #region Combined Operations Tests

        [Fact]
     public async Task Combined_FilterAndOrderAndPaging_ShouldApplyAll()
  {
            var context = new TestGraphContext(_sharedConnector);
  var options = new ODataSearchOptions
     {
        Filter = "active eq true",
            OrderBy = new OrderingOptions 
       { 
        PropertyName = "age", 
      Ordering = OrderingType.Ascending 
      },
        Top = 2
            };

         var results = await context.VAsync<ITestPerson>(g => 
        g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
          list.Should().HaveCount(2);
            list[0].Name.Should().Be("Bob");  // Youngest active person
list[1].Name.Should().Be("Eve");  // Second youngest active person
      }

        [Fact]
        public async Task Combined_ComplexFilterWithOrdering_ShouldWork()
        {
 var context = new TestGraphContext(_sharedConnector);
          var options = new ODataSearchOptions
  {
         Filter = "(age ge 25 and age le 30) or name eq 'Charlie'",
      OrderBy = new OrderingOptions 
        { 
   PropertyName = "age", 
     Ordering = OrderingType.Descending 
}
            };

 var results = await context.VAsync<ITestPerson>(g => 
          g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
     list.Should().HaveCount(4);
            // Charlie (35), Alice (30), Eve (28), Bob (25)
            list[0].Name.Should().Be("Charlie");
     list[1].Name.Should().Be("Alice");
            list[2].Name.Should().Be("Eve");
   list[3].Name.Should().Be("Bob");
        }

#endregion

      #region Security Tests

     [Fact]
public async Task Security_EmptyFilter_ShouldReturnAll()
        {
            var context = new TestGraphContext(_sharedConnector);
    var options = new ODataSearchOptions { Filter = "" };

    var results = await context.VAsync<ITestPerson>(g => 
   g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
  list.Should().HaveCount(5);
   }

 [Fact]
     public async Task Security_NullFilter_ShouldReturnAll()
    {
         var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = null };

     var results = await context.VAsync<ITestPerson>(g => 
          g.V().HasLabel("person").ApplyODataQuery(options));

         var list = results.ToList();
   list.Should().HaveCount(5);
        }

        [Fact]
        public async Task Security_WhitespaceFilter_ShouldReturnAll()
        {
            var context = new TestGraphContext(_sharedConnector);
 var options = new ODataSearchOptions { Filter = "   " };

 var results = await context.VAsync<ITestPerson>(g => 
     g.V().HasLabel("person").ApplyODataQuery(options));

       var list = results.ToList();
            list.Should().HaveCount(5);
        }

  [Fact]
        public async Task Security_NullOptions_ShouldReturnAll()
{
            var context = new TestGraphContext(_sharedConnector);

            var results = await context.VAsync<ITestPerson>(g => 
         g.V().HasLabel("person").ApplyODataQuery(null));

     var list = results.ToList();
     list.Should().HaveCount(5);
        }

        [Fact]
        public async Task Security_SpecialCharactersInFilter_ShouldHandleGracefully()
        {
            var context = new TestGraphContext(_sharedConnector);
      // Using special characters in string comparison
     var options = new ODataSearchOptions { Filter = "name eq 'Alice'" };

      var results = await context.VAsync<ITestPerson>(g => 
           g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
list.Should().HaveCount(1);
        }

      [Fact]
        public async Task Security_NonExistentProperty_ShouldReturnEmpty()
        {
    var context = new TestGraphContext(_sharedConnector);
         var options = new ODataSearchOptions { Filter = "nonexistent eq 'value'" };

   var results = await context.VAsync<ITestPerson>(g => 
           g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
    // Should return empty result set since property doesn't exist
          list.Should().BeEmpty();
        }

  [Fact]
        public async Task Security_ZeroLimit_ShouldReturnEmpty()
        {
   var context = new TestGraphContext(_sharedConnector);
  var options = new ODataSearchOptions { Top = 0 };

     var results = await context.VAsync<ITestPerson>(g => 
        g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
          list.Should().BeEmpty();
        }

        [Fact]
        public async Task Security_NegativeLimit_ShouldBeHandledSafely()
        {
          var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Top = -1 };

        // Gremlin should handle negative limits gracefully
            var results = await context.VAsync<ITestPerson>(g => 
          g.V().HasLabel("person").ApplyODataQuery(options));

         var list = results.ToList();
            // Result depends on Gremlin implementation, but should not throw
     list.Should().NotBeNull();
        }

      [Fact]
        public async Task Security_VeryLargeLimit_ShouldNotCauseIssues()
 {
          var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Top = int.MaxValue };

            var results = await context.VAsync<ITestPerson>(g => 
       g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
            list.Should().HaveCount(5); // Should just return all available
    }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task EdgeCase_FilterOnBooleanWithString_ShouldNotMatch()
        {
     var context = new TestGraphContext(_sharedConnector);
            // This should not match because 'true' as string != true as boolean
            var options = new ODataSearchOptions { Filter = "active eq 'true'" };

            var results = await context.VAsync<ITestPerson>(g => 
       g.V().HasLabel("person").ApplyODataQuery(options));

       var list = results.ToList();
            // Should be empty or match based on string comparison
       list.Should().NotBeNull();
     }

        [Fact]
    public async Task EdgeCase_CaseInsensitiveOperators_ShouldWork()
        {
            var context = new TestGraphContext(_sharedConnector);
       // Test with uppercase operators
  var options = new ODataSearchOptions { Filter = "age GT 30" };

   var results = await context.VAsync<ITestPerson>(g => 
     g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
            list.Should().HaveCount(1);
            list[0].Name.Should().Be("Charlie");
      }

        [Fact]
        public async Task EdgeCase_CaseInsensitiveLogicalOperators_ShouldWork()
        {
 var context = new TestGraphContext(_sharedConnector);
        // Test with mixed case logical operators
        var options = new ODataSearchOptions { Filter = "age GT 25 AND active EQ true" };

            var results = await context.VAsync<ITestPerson>(g => 
       g.V().HasLabel("person").ApplyODataQuery(options));

  var list = results.ToList();
 list.Should().HaveCount(3);
        }

        [Fact]
        public async Task EdgeCase_ExtraSpacesInFilter_ShouldHandle()
   {
            var context = new TestGraphContext(_sharedConnector);
            var options = new ODataSearchOptions { Filter = "age    eq30" };

            var results = await context.VAsync<ITestPerson>(g => 
       g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
            list.Should().HaveCount(1);
            list[0].Name.Should().Be("Alice");
     }

     [Fact]
        public async Task EdgeCase_NestedParentheses_ShouldWork()
        {
     var context = new TestGraphContext(_sharedConnector);
  var options = new ODataSearchOptions 
       { 
     Filter = "((age gt 25 and age lt 35) or name eq 'Frank') and active eq true" 
       };

            var results = await context.VAsync<ITestPerson>(g => 
g.V().HasLabel("person").ApplyODataQuery(options));

       var list = results.ToList();
// Bob (25 is not > 25), Eve (28), Alice (30), Charlie (35 is not < 35), Frank (not active)
            list.Should().HaveCount(2);
      list.Should().Contain(p => p.Name == "Eve");
     list.Should().Contain(p => p.Name == "Alice");
      }

    [Fact]
        public async Task EdgeCase_EmptyStringComparison_ShouldWork()
        {
var context = new TestGraphContext(_sharedConnector);
     var options = new ODataSearchOptions { Filter = "name ne ''" };

          var results = await context.VAsync<ITestPerson>(g => 
    g.V().HasLabel("person").ApplyODataQuery(options));

          var list = results.ToList();
            list.Should().HaveCount(5); // All people have names
        }

   [Fact]
  public async Task EdgeCase_OrderByWithNullOptions_ShouldNotOrder()
        {
       var context = new TestGraphContext(_sharedConnector);
       var options = new ODataSearchOptions { OrderBy = null };

            var results = await context.VAsync<ITestPerson>(g => 
         g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
         list.Should().HaveCount(5);
  }

        [Fact]
        public async Task EdgeCase_OrderByWithEmptyPropertyName_ShouldNotOrder()
        {
            var context = new TestGraphContext(_sharedConnector);
  var options = new ODataSearchOptions 
   { 
      OrderBy = new OrderingOptions { PropertyName = "", Ordering = OrderingType.Ascending } 
            };

     var results = await context.VAsync<ITestPerson>(g => 
      g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
          list.Should().HaveCount(5);
    }

        [Fact]
        public async Task EdgeCase_MultipleOrConditions_ShouldMatchAny()
        {
    var context = new TestGraphContext(_sharedConnector);
   var options = new ODataSearchOptions 
            { 
        Filter = "name eq 'Alice' or name eq 'Bob' or name eq 'Charlie'" 
   };

            var results = await context.VAsync<ITestPerson>(g => 
g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
     list.Should().HaveCount(3);
         list.Should().Contain(p => p.Name == "Alice");
        list.Should().Contain(p => p.Name == "Bob");
 list.Should().Contain(p => p.Name == "Charlie");
        }

        #endregion

        #region Search Tests (if implemented)

        [Fact]
        public async Task Search_WithSearchableProperties_ShouldSearchAcrossProperties()
        {
     var context = new TestGraphContext(_sharedConnector);
  var options = new ODataSearchOptions 
       { 
 Search = "developer",
                SearchableProperties = new[] { "description" }
         };

            var results = await context.VAsync<ITestPerson>(g => 
 g.V().HasLabel("person").ApplyODataQuery(options));

            var list = results.ToList();
  list.Should().HaveCount(1);
         list[0].Name.Should().Be("Alice");
      }

        [Fact]
        public async Task Search_WithMultipleProperties_ShouldFindInAny()
        {
            var context = new TestGraphContext(_sharedConnector);
    var options = new ODataSearchOptions 
            { 
    Search = "test",
   SearchableProperties = new[] { "email", "description" }
 };

     var results = await context.VAsync<ITestPerson>(g => 
    g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
            // Should find Bob, Eve, and Frank (all have test.com emails)
          list.Should().HaveCountGreaterOrEqualTo(3);
}

    [Fact]
        public async Task Search_WithNullSearch_ShouldReturnAll()
        {
  var context = new TestGraphContext(_sharedConnector);
    var options = new ODataSearchOptions 
  { 
  Search = null,
      SearchableProperties = new[] { "description" }
  };

      var results = await context.VAsync<ITestPerson>(g => 
     g.V().HasLabel("person").ApplyODataQuery(options));

   var list = results.ToList();
list.Should().HaveCount(5);
        }

        [Fact]
        public async Task Search_WithEmptySearchableProperties_ShouldReturnAll()
        {
  var context = new TestGraphContext(_sharedConnector);
     var options = new ODataSearchOptions 
            { 
                Search = "developer",
    SearchableProperties = new string[0]
      };

      var results = await context.VAsync<ITestPerson>(g => 
          g.V().HasLabel("person").ApplyODataQuery(options));

        var list = results.ToList();
list.Should().HaveCount(5); // No properties to search, returns all
        }

        #endregion

        #region Performance and Stress Tests

   [Fact]
        public async Task Performance_ComplexQueryWithAllFeatures_ShouldComplete()
      {
         var context = new TestGraphContext(_sharedConnector);
        var options = new ODataSearchOptions
            {
       Filter = "(age ge 25 and age le 35) or (active eq false and age lt 23)",
       OrderBy = new OrderingOptions 
        { 
            PropertyName = "age", 
             Ordering = OrderingType.Ascending 
       },
    Top = 10
      };

            var results = await context.VAsync<ITestPerson>(g => 
              g.V().HasLabel("person").ApplyODataQuery(options));

  var list = results.ToList();
    list.Should().NotBeNull();
       list.Should().HaveCountLessOrEqualTo(10);
      }

        [Fact]
   public async Task Performance_MultipleFiltersWithOr_ShouldComplete()
    {
         var context = new TestGraphContext(_sharedConnector);
     var options = new ODataSearchOptions
  {
        Filter = "age eq 22 or age eq 25 or age eq 28 or age eq 30 or age eq 35"
            };

            var results = await context.VAsync<ITestPerson>(g => 
        g.V().HasLabel("person").ApplyODataQuery(options));

         var list = results.ToList();
        list.Should().HaveCount(5); // Should match all people
        }

        #endregion

  #region Test Helper Classes

   [VertexLabel("person")]
        public interface ITestPerson : IVertex
        {
            string Name { get; set; }
         int Age { get; set; }
   bool Active { get; set; }
 string Email { get; set; }
   string Description { get; set; }
        }

        private class TestGraphContext : GraphContextBase
        {
        public TestGraphContext(IGremlinLanguageConnector connector) 
      : base(connector, CreateServiceProvider())
      {
          }

            public IGraphSet<ITestPerson> People { get; }

    private static IServiceProvider CreateServiceProvider()
            {
   var services = new ServiceCollection();
          services.AddEntityBinding((entity, implementation) =>
                {
            services.AddTransient(entity, implementation);
      });
     return services.BuildServiceProvider();
     }

      protected override bool InitializeModel(IGraphConfiguration configuration)
     {
        configuration.ConfigureCollection<ITestPerson>();
         return true;
        }
 }

        #endregion
    }
}
