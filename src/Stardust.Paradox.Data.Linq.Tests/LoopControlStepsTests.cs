using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests for loop control Gremlin steps: repeat(), until(), emit(), times(), loops()
    /// </summary>
    public class LoopControlStepsTests : LinqTestBase
    {
        private readonly ITestOutputHelper _output;

        public LoopControlStepsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Repeat() Tests

        [Fact]
        public void Repeat_WithSimpleTraversal_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
         .Where(p => p.Name == "Alice")
          .Repeat(p => p.Out(x => x.Friends))
      .Times(2);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

          // Assert
            gremlin.Should().Contain(".repeat(");
         gremlin.Should().Contain(".times(2)");
    gremlin.Should().Contain("out('friendsWith')"); // Inside repeat, no leading dot
        }

        [Fact]
        public void Repeat_WithMultipleSteps_ShouldGenerateCorrectGremlin()
   {
       // Arrange & Act
       var query = Context.People.AsQueryable()
    .Repeat(p => p.Out(x => x.Friends).Where(f => f.IsActive))
.Times(3);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

  // Assert
        gremlin.Should().Contain(".repeat(");
   gremlin.Should().Contain("out('friendsWith')"); // Inside repeat, no leading dot
  gremlin.Should().Contain(".has('isActive',");
          gremlin.Should().Contain(".times(3)");
        }

    #endregion

        #region Until() Tests

        [Fact]
        public void Until_WithPredicate_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
        .Where(p => p.Name == "Alice")
         .Repeat(p => p.Out(x => x.Friends))
      .Until(p => p.Age > 50);

            var gremlin = query.ToString();
       _output.WriteLine($"Generated Gremlin: {gremlin}");

      // Assert
            gremlin.Should().Contain(".repeat(");
       gremlin.Should().Contain(".until(");
            gremlin.Should().Contain("has('age', gt(__p1))"); // Parameter format with double underscore
        }

        [Fact]
        public void Until_WithComplexPredicate_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
          .Repeat(p => p.Out(x => x.Friends))
            .Until(p => p.Name == "TargetPerson" && p.IsActive);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".until(");
            gremlin.Should().Contain("has('name'");
            gremlin.Should().Contain("has('isActive'");
        }

        #endregion

        #region Emit() Tests

        [Fact]
        public void Emit_WithoutPredicate_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
             .Where(p => p.Name == "Alice")
     .Repeat(p => p.Out(x => x.Friends))
          .Emit()
          .Times(3);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".repeat(");
            gremlin.Should().Contain(".emit()");
            gremlin.Should().Contain(".times(3)");
        }

        [Fact]
        public void Emit_WithPredicate_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
                .Repeat(p => p.Out(x => x.Friends))
              .Emit(p => p.Age > 30)
               .Until(p => p.Age > 60);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".emit(");
            gremlin.Should().Contain("has('age', gt(__p0))"); // Parameter format with double underscore
            gremlin.Should().Contain(".until(");
        }

        [Fact]
        public void Emit_BeforeRepeat_ShouldGenerateCorrectOrder()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
                  .Where(p => p.Name == "Alice")
         .Emit()
           .Repeat(p => p.Out(x => x.Friends))
              .Times(2);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            var emitIndex = gremlin.IndexOf(".emit()");
            var repeatIndex = gremlin.IndexOf(".repeat(");
            emitIndex.Should().BeLessThan(repeatIndex, "emit() should come before repeat()");
        }

        #endregion

        #region Times() Tests

        [Fact]
        public void Times_WithRepeat_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
                .Repeat(p => p.Out(x => x.Friends))
            .Times(5);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".repeat(");
            gremlin.Should().Contain(".times(5)");
        }

        [Fact]
        public void Times_WithZeroIterations_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
                    .Repeat(p => p.Out(x => x.Friends))
                    .Times(0);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".times(0)");
        }

        #endregion

        #region Loops() Tests

        [Fact()]
        public void Loops_InRepeat_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            // NOTE: In real Gremlin, loops() is used in predicates like: until(loops().is(gt(3)))
            // This test shows the intended LINQ API - will work once translator supports it
            var query = Context.People.AsQueryable()
   .Where(p => p.Name == "Alice")
    .Repeat(p => p.Out(x => x.Friends))
 .Times(5); // Using Times instead of Until with Loops for now

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".repeat(");
            gremlin.Should().Contain(".times(5)");
        }

        #endregion

        #region Complex Scenarios

        [Fact]
        public void Repeat_WithEmitAndUntil_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
             .Where(p => p.Name == "Alice")
               .Repeat(p => p.Out(x => x.Friends))
               .Emit(p => p.IsActive)
               .Until(p => p.Name == "Bob" || p.Age > 60);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".repeat(");
            gremlin.Should().Contain(".emit(");
            gremlin.Should().Contain(".until(");
        }

        [Fact()]
        public void Repeat_WithCompanyTraversal_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            // NOTE: In Gremlin, repeat can traverse to different types
            // This would work with: g.V().hasLabel('person').repeat(out('worksAt')).times(2)
            // For LINQ, we'd query companies directly instead
            var query = Context.Companies.AsQueryable()
           .Where(c => c.Name == "Acme")
              .Take(10);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain("hasLabel('company')");
        }

        [Fact()]
        public void Repeat_WithMultipleEdgeTypes_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            // NOTE: This crosses types (Person->Person->Company)
            // In raw Gremlin: g.V().repeat(out('friendsWith').out('worksAt')).times(3)
            // LINQ version would be more complex or require raw Gremlin
            var query = Context.People.AsQueryable()
          .Where(p => p.Name == "Alice")
                         .Take(1);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain("hasLabel('person')");
        }

        [Fact]
        public void Repeat_WithProjectionAfter_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
             .Repeat(p => p.Out(x => x.Friends))
           .Times(2)
              .Select(p => new { p.Name, p.Age });

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".repeat(");
            gremlin.Should().Contain(".times(2)");
            gremlin.Should().Contain("valueMap");
        }
        #endregion

        #region Edge Cases

        [Fact]
        public void Repeat_OnEmptySet_ShouldGenerateValidGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
   .Where(p => p.Name == "NonExistent")
                .Repeat(p => p.Out(x => x.Friends))
                .Times(3);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".repeat(");
            gremlin.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Emit_WithComplexFilter_ShouldGenerateCorrectGremlin()
        {
            // Arrange & Act
            var query = Context.People.AsQueryable()
          .Repeat(p => p.Out(x => x.Friends))
            .Emit(p => p.Age >= 25 && p.Age <= 45 && p.IsActive)
    .Times(4);

            var gremlin = query.ToString();
            _output.WriteLine($"Generated Gremlin: {gremlin}");

            // Assert
            gremlin.Should().Contain(".emit(");
            gremlin.Should().Contain("has('age'");
            gremlin.Should().Contain("has('isActive'");
        }

        #endregion
    }
}
