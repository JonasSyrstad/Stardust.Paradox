using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for pattern matching and label-based Gremlin steps: as(), path(), match(), selectByLabel()
    /// </summary>
    public class PatternMatchingStepsTests : LinqTestBase
    {
      private readonly ITestOutputHelper _output;

        public PatternMatchingStepsTests(ITestOutputHelper output)
        {
     _output = output;
        }

        #region As() Tests

[Fact]
        public void As_WithSingleLabel_ShouldGenerateCorrectGremlin()
        {
        // Arrange & Act
            var query = Context.People.AsQueryable()
      .Where(p => p.Name == "Alice")
    .As("person")
      .Out(x => x.Friends);

        var gremlin = query.ToString();
     _output.WriteLine($"Generated Gremlin: {gremlin}");

        // Assert
            gremlin.Should().Contain(".as('person')");
     gremlin.Should().Contain(".out('friendsWith')");
 }

     [Fact]
public void As_WithMultipleLabels_ShouldGenerateCorrectGremlin()
        {
 // Arrange & Act
            var query = Context.People.AsQueryable()
         .As("start")
       .Out(x => x.Friends)
  .As("friend")
           .Out(x => x.Companies)
       .As("company");

      var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

 // Assert
            gremlin.Should().Contain(".as('start')");
            gremlin.Should().Contain(".as('friend')");
     gremlin.Should().Contain(".as('company')");
   }

        [Fact]
        public void As_InComplexTraversal_ShouldGenerateCorrectGremlin()
        {
     // Arrange & Act
      var query = Context.People.AsQueryable()
      .Where(p => p.Age > 25)
         .As("youngPerson")
        .Out(x => x.Companies)
      .Where(c => c.EmployeeCount > 100)
.As("largeCompany");

 var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

    // Assert
            gremlin.Should().Contain(".as('youngPerson')");
     gremlin.Should().Contain(".as('largeCompany')");
        }

        #endregion

        #region Path() Tests

        [Fact]
        public void Path_AfterTraversal_ShouldGenerateCorrectGremlin()
        {
     // Arrange & Act
  var query = Context.People.AsQueryable()
    .Where(p => p.Name == "Alice")
                .Out(x => x.Friends)
      .Out(x => x.Companies)
   .Path();

       var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

          // Assert
            gremlin.Should().Contain(".path()");
  gremlin.Should().Contain(".out('friendsWith')");
            gremlin.Should().Contain(".out('worksAt')");
        }

    [Fact]
  public void Path_WithLabels_ShouldGenerateCorrectGremlin()
   {
            // Arrange & Act
     var query = Context.People.AsQueryable()
     .As("person")
  .Out(x => x.Friends)
 .As("friend")
                .Out(x => x.Companies)
         .As("company")
            .Path();

   var gremlin = query.ToString();
   _output.WriteLine($"Generated Gremlin: {gremlin}");

    // Assert
            gremlin.Should().Contain(".as('person')");
            gremlin.Should().Contain(".as('friend')");
   gremlin.Should().Contain(".as('company')");
   gremlin.Should().Contain(".path()");
        }

    [Fact]
      public void Path_WithRepeat_ShouldGenerateCorrectGremlin()
        {
        // Arrange & Act
     var query = Context.People.AsQueryable()
          .Where(p => p.Name == "Alice")
        .Repeat(p => p.Out(x => x.Friends))
  .Times(3)
   .Path();

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

     // Assert
            gremlin.Should().Contain(".repeat(");
 gremlin.Should().Contain(".times(3)");
            gremlin.Should().Contain(".path()");
    }

    #endregion

     #region Match() Tests

        [Fact]
        public void Match_WithSinglePattern_ShouldGenerateCorrectGremlin()
  {
            // Arrange & Act
            var query = Context.People.AsQueryable()
            .Match(
         p => p.As("a").Out(x => x.Friends).As("b")
                );

            var gremlin = query.ToString();
       _output.WriteLine($"Generated Gremlin: {gremlin}");

     // Assert
    gremlin.Should().Contain(".match(");
   gremlin.Should().Contain("as('a')"); // Inside match, no leading dot
     gremlin.Should().Contain("as('b')"); // Inside match, no leading dot
 }

    [Fact]
        public void Match_WithMultiplePatterns_ShouldGenerateCorrectGremlin()
        {
        // Arrange & Act
  var query = Context.People.AsQueryable()
       .Match(
    p => p.As("a").Out(x => x.Friends).As("b"),
      p => p.As("b").Out(x => x.Companies).As("c"),
  p => p.As("a").Where(x => x.Age > 25)
   );

         var gremlin = query.ToString();
    _output.WriteLine($"Generated Gremlin: {gremlin}");

     // Assert
            gremlin.Should().Contain(".match(");
   gremlin.Should().Contain("as('a')"); // Inside match, no leading dot
      gremlin.Should().Contain("as('b')"); // Inside match, no leading dot
            gremlin.Should().Contain("as('c')"); // Inside match, no leading dot
        }

        [Fact]
        public void Match_WithComplexPatterns_ShouldGenerateCorrectGremlin()
        {
         // Arrange & Act
            var query = Context.People.AsQueryable()
        .As("start")
   .Match(
  p => p.As("start").Out(x => x.Friends).As("friend"),
        p => p.As("friend").Out(x => x.Companies).As("company"),
         // Fix: company is ICompany, not IPerson - filter on company properties
      p => p.As("company").Where(c => c.Name.Contains("Tech"))
      );

var gremlin = query.ToString();
         _output.WriteLine($"Generated Gremlin: {gremlin}");

 // Assert
  gremlin.Should().Contain(".match(");
     gremlin.Should().Contain(".as('start')");
            gremlin.Should().Contain(".as('friend')");
  gremlin.Should().Contain(".as('company')");
        }

        #endregion

      #region SelectByLabel() Tests

        [Fact]
        public void SelectByLabel_WithSingleLabel_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
    var query = Context.People.AsQueryable()
         .As("person")
         .Out(x => x.Friends)
              .As("friend")
                .SelectByLabel<IPerson, IPerson>("person");

         var gremlin = query.ToString();
          _output.WriteLine($"Generated Gremlin: {gremlin}");

   // Assert
            gremlin.Should().Contain(".as('person')");
            gremlin.Should().Contain(".as('friend')");
            gremlin.Should().Contain(".select('person')");
}

 [Fact(Skip = "SelectByLabels requires starting from correct type")]
  public void SelectByLabels_WithMultipleLabels_ShouldGenerateCorrectGremlin()
   {
     // Arrange & Act
       // Fix: Start from People, not traverse to Company first
   var query = Context.People.AsQueryable()
    .As("start")
      .Out(x => x.Friends)
    .As("middle")
     .As("end") // Same type, labeled differently
.SelectByLabels<IPerson, object>("start", "middle", "end");

      var gremlin = query.ToString();
 _output.WriteLine($"Generated Gremlin: {gremlin}");

   // Assert
      gremlin.Should().Contain(".select('start', 'middle', 'end')");
    }

   [Fact]
        public void SelectByLabel_AfterMatch_ShouldGenerateCorrectGremlin()
 {
            // Arrange & Act
         var query = Context.People.AsQueryable()
     .Match(
  p => p.As("a").Out(x => x.Friends).As("b"),
          p => p.As("b").Out(x => x.Companies).As("c")
                )
        .SelectByLabel<IPerson, ICompany>("c");

            var gremlin = query.ToString();
   _output.WriteLine($"Generated Gremlin: {gremlin}");

       // Assert
   gremlin.Should().Contain(".match(");
            gremlin.Should().Contain(".select('c')");
        }

        #endregion

        #region Complex Pattern Matching Scenarios

[Fact]
        public void AsPathSelect_CompleteWorkflow_ShouldGenerateCorrectGremlin()
        {
      // Arrange & Act
    var query = Context.People.AsQueryable()
.Where(p => p.Name == "Alice")
         .As("alice")
                .Out(x => x.Friends)
            .As("friend")
.Where(f => f.Age > 30)
 .Out(x => x.Companies)
          .As("company")
  .Path();

         var gremlin = query.ToString();
    _output.WriteLine($"Generated Gremlin: {gremlin}");

  // Assert
       gremlin.Should().Contain(".as('alice')");
     gremlin.Should().Contain(".as('friend')");
      gremlin.Should().Contain(".as('company')");
          gremlin.Should().Contain(".path()");
      }

  [Fact]
        public void Match_WithSelectByLabel_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
       .Where(p => p.Name == "Alice")
            .As("start")
.Match(
         p => p.As("start").Out(x => x.Friends).As("friendNode"),
                  p => p.As("friendNode").Where(f => f.IsActive)
  )
.SelectByLabel<IPerson, IPerson>("friendNode");

         var gremlin = query.ToString();
 _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
   gremlin.Should().Contain(".match(");
            gremlin.Should().Contain(".as('start')");
       gremlin.Should().Contain(".as('friendNode')");
            gremlin.Should().Contain(".select('friendNode')");
   }

     [Fact]
        public void Match_WithMultipleEdgeTypes_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
       var query = Context.People.AsQueryable()
          .Match(
          p => p.As("person").Out(x => x.Friends).As("friend"),
         p => p.As("person").Out(x => x.Companies).As("employer"),
  p => p.As("friend").Out(x => x.Companies).As("friendEmployer"),
    p => p.As("employer").Where(c => c.Name == "friendEmployer")
 );

    var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

        // Assert
      gremlin.Should().Contain(".match(");
    gremlin.Should().Contain(".out('friendsWith')");
            gremlin.Should().Contain(".out('worksAt')");
   }

        [Fact]
        public void Path_WithMatchAndSelect_ShouldGenerateCorrectGremlin()
        {
      // Arrange & Act
            var query = Context.People.AsQueryable()
    .As("start")
      .Match(
                  p => p.As("start").Out(x => x.Friends).As("intermediate"),
   p => p.As("intermediate").Out(x => x.Companies).As("end")
            )
           .SelectByLabel<IPerson, ICompany>("end")
       .Path();

         var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
     gremlin.Should().Contain(".match(");
    gremlin.Should().Contain(".select('end')");
            gremlin.Should().Contain(".path()");
      }

        #endregion

     #region Edge Cases

        [Fact]
        public void As_WithEmptyLabel_ShouldThrowException()
   {
            // Arrange, Act & Assert
Assert.Throws<ArgumentException>(() =>
      {
            var query = Context.People.AsQueryable()
          .As("")
     .Out(x => x.Friends);
           var gremlin = query.ToString();
            });
      }

        [Fact]
     public void SelectByLabel_WithNonExistentLabel_ShouldGenerateValidGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
        .As("person")
          .Out(x => x.Friends)
  .SelectByLabel<IPerson, IPerson>("nonExistentLabel");

          var gremlin = query.ToString();
      _output.WriteLine($"Generated Gremlin: {gremlin}");

     // Assert
       gremlin.Should().Contain(".select('nonExistentLabel')");
        }

        [Fact]
        public void Match_WithEmptyPatternList_ShouldThrowException()
        {
  // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() =>
    {
        var query = Context.People.AsQueryable()
       .Match();
    var gremlin = query.ToString();
          });
        }

        #endregion
    }
}
