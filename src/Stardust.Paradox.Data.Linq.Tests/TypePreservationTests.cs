using FluentAssertions;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit;

namespace Stardust.Paradox.Data.Linq.Tests
{
    /// <summary>
    /// Tests to verify that Out, In, OutE, and InE properly capture and preserve output types from predicates
    /// </summary>
    public class TypePreservationTests : LinqTestBase
    {
        [Fact]
        public void Out_PreservesTargetVertexType()
        {
            // Arrange
            var query = Context.People.AsQueryable();

            // Act - Out should infer ICompany from IEdgeCollection<ICompany>
            IQueryable<ICompany> companies = query.Out(p => p.Companies);

            // Assert - Type should be IQueryable<ICompany>
            companies.Should().NotBeNull();
            companies.ElementType.Should().Be(typeof(ICompany));

            // Verify we can access ICompany-specific properties
            var result = companies.Where(c => c.EmployeeCount > 100).ToList();
            result.Should().BeAssignableTo<IEnumerable<ICompany>>();
        }

        [Fact]
        public void Out_WithMultipleTypes_PreservesEachType()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Each Out should preserve its specific target type
            IQueryable<ICompany> companies = people.Out(p => p.Companies);
            IQueryable<IProject> projects = people.Out(p => p.Projects);
            IQueryable<IPerson> friends = people.Out(p => p.Friends);
            IQueryable<ISkill> skills = people.Out(p => p.Skills);

            // Assert
            companies.ElementType.Should().Be(typeof(ICompany));
            projects.ElementType.Should().Be(typeof(IProject));
            friends.ElementType.Should().Be(typeof(IPerson));
            skills.ElementType.Should().Be(typeof(ISkill));
        }

        [Fact]
        public void In_PreservesSourceVertexType()
        {
            // Arrange
            var companies = Context.Companies.AsQueryable();

            // Act - In should infer IPerson from IEdgeCollection<IPerson>
            IQueryable<IPerson> employees = companies.In(c => c.Employees);

            // Assert
            employees.Should().NotBeNull();
            employees.ElementType.Should().Be(typeof(IPerson));

            // Verify we can access IPerson-specific properties
            var result = employees.Where(p => p.Age > 25).ToList();
            result.Should().BeAssignableTo<IEnumerable<IPerson>>();
        }

        [Fact]
        public void OutE_WithThreeTypeParams_PreservesEdgeType()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - OutE should preserve IEmployment edge type
            IQueryable<IEmployment> employmentEdges = people.OutE<IPerson, ICompany, IEmployment>(p => p.Companies);

            // Assert
            employmentEdges.Should().NotBeNull();
            employmentEdges.ElementType.Should().Be(typeof(IEmployment));

            // Verify we can access IEmployment-specific properties
            var result = employmentEdges.Where(e => e.Salary > 100000).ToList();
            result.Should().BeAssignableTo<IEnumerable<IEmployment>>();
        }

        [Fact]
        public void OutE_PreservesDifferentEdgeTypes()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Each OutE should preserve its specific edge type
            IQueryable<IEmployment> employmentEdges = people.OutE<IPerson, ICompany, IEmployment>(p => p.Companies);
            IQueryable<IFriendship> friendshipEdges = people.OutE<IPerson, IPerson, IFriendship>(p => p.Friends);
            IQueryable<IAssignment> assignmentEdges = people.OutE<IPerson, IProject, IAssignment>(p => p.Projects);
            IQueryable<IUserSkill> skillEdges = people.OutE<IPerson, ISkill, IUserSkill>(p => p.Skills);

            // Assert
            employmentEdges.ElementType.Should().Be(typeof(IEmployment));
            friendshipEdges.ElementType.Should().Be(typeof(IFriendship));
            assignmentEdges.ElementType.Should().Be(typeof(IAssignment));
            skillEdges.ElementType.Should().Be(typeof(IUserSkill));
        }

        [Fact]
        public void InE_WithThreeTypeParams_PreservesEdgeType()
        {
            // Arrange
            var companies = Context.Companies.AsQueryable();

            // Act - InE should preserve IEmployment edge type
            IQueryable<IEmployment> employmentEdges = companies.InE<ICompany, IPerson, IEmployment>(c => c.Employees);

            // Assert
            employmentEdges.Should().NotBeNull();
            employmentEdges.ElementType.Should().Be(typeof(IEmployment));

            // Verify we can access IEmployment-specific properties
            var result = employmentEdges.Where(e => e.Role == "Developer").ToList();
            result.Should().BeAssignableTo<IEnumerable<IEmployment>>();
        }

        [Fact]
        public void BothE_PreservesEdgeType()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - BothE should preserve IFriendship edge type for bidirectional friendships
            IQueryable<IFriendship> friendships = people.BothE<IPerson, IPerson, IFriendship>(p => p.Friends);

            // Assert
            friendships.Should().NotBeNull();
            friendships.ElementType.Should().Be(typeof(IFriendship));

            // Verify we can access IFriendship-specific properties
            var result = friendships.Where(f => f.Strength == "strong").ToList();
            result.Should().BeAssignableTo<IEnumerable<IFriendship>>();
        }

        [Fact]
        public void OutV_PreservesTargetVertexType()
        {
            // Arrange
            var people = Context.People.AsQueryable();
            var employmentEdges = people.OutE<IPerson, ICompany, IEmployment>(p => p.Companies);

            // Act - OutV should preserve ICompany type
            IQueryable<ICompany> companies = employmentEdges.OutV<IEmployment, ICompany>();

            // Assert
            companies.Should().NotBeNull();
            companies.ElementType.Should().Be(typeof(ICompany));

            // Verify we can access ICompany-specific properties
            var result = companies.Where(c => c.Industry == "Technology").ToList();
            result.Should().BeAssignableTo<IEnumerable<ICompany>>();
        }

        [Fact]
        public void InV_PreservesSourceVertexType()
        {
            // Arrange
            var companies = Context.Companies.AsQueryable();
            var employmentEdges = companies.InE<ICompany, IPerson, IEmployment>(c => c.Employees);

            // Act - InV should preserve IPerson type
            IQueryable<IPerson> people = employmentEdges.InV<IEmployment, IPerson>();

            // Assert
            people.Should().NotBeNull();
            people.ElementType.Should().Be(typeof(IPerson));

            // Verify we can access IPerson-specific properties
            var result = people.Where(p => p.IsActive).ToList();
            result.Should().BeAssignableTo<IEnumerable<IPerson>>();
        }

        [Fact]
        public void ChainedTraversals_PreserveTypeThroughout()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Chain multiple traversals, each should preserve types
            var result = people
           .Where(p => p.IsActive)  // IQueryable<IPerson>
               .Out(p => p.Companies)   // IQueryable<ICompany>
           .Where(c => c.EmployeeCount > 100)                   // IQueryable<ICompany>
                .In(c => c.Employees)         // IQueryable<IPerson>
             .Where(p => p.Age > 25)      // IQueryable<IPerson>
           .Out(p => p.Projects)              // IQueryable<IProject>
                .Where(pr => pr.Status == "Active")// IQueryable<IProject>
             .ToList();

            // Assert
            result.Should().BeAssignableTo<IEnumerable<IProject>>();
        }

        [Fact]
        public void EdgeTraversal_PreservesTypesThroughComplexChain()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Chain edge and vertex traversals
            var result = people
         .Where(p => p.Name == "Alice Johnson")      // IQueryable<IPerson>
                .OutE<IPerson, ICompany, IEmployment>(p => p.Companies)   // IQueryable<IEmployment>
  .Where(e => e.Salary > 80000)      // IQueryable<IEmployment>
                .OutV<IEmployment, ICompany>()         // IQueryable<ICompany>
                .Where(c => c.Industry == "Technology")// IQueryable<ICompany>
   .ToList();

            // Assert
            result.Should().BeAssignableTo<IEnumerable<ICompany>>();
        }

        [Fact]
        public void BidirectionalTraversal_PreservesTypes()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Use Both to traverse in both directions
            IQueryable<IPerson> allFriends = people
      .Where(p => p.Name == "Alice Johnson")
         .Both(p => p.Friends)
        .Where(p => p.IsActive);

            // Assert
            allFriends.Should().NotBeNull();
            allFriends.ElementType.Should().Be(typeof(IPerson));
        }

        [Fact]
        public void BothV_PreservesVertexType()
        {
            // Arrange
            var people = Context.People.AsQueryable();
            var friendshipEdges = people.BothE<IPerson, IPerson, IFriendship>(p => p.Friends);

            // Act - BothV should get both ends of friendship edges
            IQueryable<IPerson> allPeopleInFriendships = friendshipEdges.BothV<IFriendship, IPerson>();

            // Assert
            allPeopleInFriendships.Should().NotBeNull();
            allPeopleInFriendships.ElementType.Should().Be(typeof(IPerson));
        }

        [Fact]
        public void OtherV_PreservesVertexType()
        {
            // Arrange
            var people = Context.People.AsQueryable();
            var friendshipEdges = people.OutE<IPerson, IPerson, IFriendship>(p => p.Friends);

            // Act - OtherV should get the other end of edges
            IQueryable<IPerson> otherPeople = friendshipEdges.OtherV<IPerson>();

            // Assert
            otherPeople.Should().NotBeNull();
            otherPeople.ElementType.Should().Be(typeof(IPerson));
        }

        [Fact]
        public void TypeInference_WorksWithoutExplicitTypeParameters()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Type parameters should be inferred from lambda expression
            var companies = people.Out(p => p.Companies);  // TTarget inferred as ICompany
            var friends = people.Out(p => p.Friends);      // TTarget inferred as IPerson
            var projects = people.Out(p => p.Projects);    // TTarget inferred as IProject

            // Assert - All should have correct inferred types
            companies.Should().BeAssignableTo<IQueryable<ICompany>>();
            friends.Should().BeAssignableTo<IQueryable<IPerson>>();
            projects.Should().BeAssignableTo<IQueryable<IProject>>();
        }

        [Fact()]
        public void ProjectionAfterTraversal_PreservesTypeInformation()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Project after type-preserving traversal
            var companyInfo = people
             .Out(p => p.Companies)
           .Select(c => new { c.Name, c.Industry, c.EmployeeCount })
           .ToList();

            // Assert
            companyInfo.Should().NotBeEmpty();
            companyInfo.Should().AllSatisfy(info =>
         {
             info.Name.Should().NotBeNullOrEmpty();
             info.Industry.Should().NotBeNullOrEmpty();
             info.EmployeeCount.Should().BeGreaterThan(0);
         });
        }

        [Fact]
        public void ComplexTypeChain_MaintainsTypeIntegrity()
        {
            // Arrange
            var people = Context.People.AsQueryable();

            // Act - Complex chain with multiple type transitions
            var result = people
          .Where(p => p.IsActive)            // IPerson
        .OutE<IPerson, ICompany, IEmployment>(p => p.Companies)   // IEmployment
        .Where(e => e.Salary > 50000)    // IEmployment  
                      .InV<IEmployment, IPerson>()        // IPerson (back to source)
                 .OutE<IPerson, IProject, IAssignment>(p => p.Projects)    // IAssignment
       .Where(a => a.HoursPerWeek > 20)  // IAssignment
           .OutV<IAssignment, IProject>()                // IProject
         .Where(pr => pr.Status == "Active")    // IProject
              .ToList();

            // Assert - Final type should be IProject
            result.Should().BeAssignableTo<IEnumerable<IProject>>();
        }
    }
}
