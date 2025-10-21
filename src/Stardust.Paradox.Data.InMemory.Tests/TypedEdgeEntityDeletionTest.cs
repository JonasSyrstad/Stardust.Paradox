using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Stardust.Paradox.Data.InMemory.Tests.CosmosDbMigrated;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Test to verify that typed edge entity deletions work correctly
    /// This tests the change tracking system path, not the collection path
    /// </summary>
    public class TypedEdgeEntityDeletionTest
    {
        private readonly ITestOutputHelper _output;

        public TypedEdgeEntityDeletionTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Delete_TypedEdgeEntity_ShouldBeImmediatelyVisible()
        {
            // Arrange
            var connector = new Stardust.Paradox.Data.InMemory.InMemoryGremlinLanguageConnector();
            using (var tc = new TestContext(connector))
            {
                // Create two profiles
                var alice = tc.CreateEntity<IProfile>("Alice_Test");
                alice.Name = "Alice";
                alice.Pk = "Alice_Test";
                alice.FirstName = "Alice";
                
                var company = tc.CreateEntity<ICompany>("TestCorp");
                company.Name = "TestCorp";
                company.Pk = "TestCorp";
                
                await tc.SaveChangesAsync();
                
                // Create an employment edge
                var employment = tc.Employments.Create(alice, company);
                _output.WriteLine($"edgeId just after create but before save: {employment.Id}");
                // Don't set HiredDate to avoid date format issues
                employment.Manager = "TestManager";
                
                await tc.SaveChangesAsync();
                var edges = connector.Database.GetAllEdges();
                _output.WriteLine($"Found {edges.Count()} edges after save");
                _output.WriteLine($"Found edgeIds {string.Join(", ", edges.Select(e => e.Id))}");
            }
            
            // Verify employment exists
            using (var tc = new TestContext(connector))
            {
                var employments = await tc.Employments.AllAsync();
                var testEmployments = employments.Where(e => e.Manager == "TestManager").ToList();
                
                _output.WriteLine($"Found {testEmployments.Count} test employments before deletion");
                Assert.NotEmpty(testEmployments);
                
                var employmentId = testEmployments.First().Id;
                _output.WriteLine($"EdgeId just before the delete: {employmentId}");
                // Delete the employment
                await tc.Employments.DeleteAsync(employmentId);
                await tc.SaveChangesAsync();
            }
            
            // Assert: Employment should be gone
            using (var tc = new TestContext(connector))
            {
                var employments = await tc.Employments.AllAsync();
                var testEmployments = employments.Where(e => e.Manager == "TestManager").ToList();
                
                _output.WriteLine($"Found {testEmployments.Count} test employments after deletion");
                Assert.Empty(testEmployments);
            }
        }

        [Fact]
        public async Task Delete_TypedEdgeEntity_ViaContextDelete_ShouldBeImmediatelyVisible()
        {
            // Arrange
            var connector = new Stardust.Paradox.Data.InMemory.InMemoryGremlinLanguageConnector();
            using (var tc = new TestContext(connector))
            {
                // Create two profiles
                var bob = tc.CreateEntity<IProfile>("Bob_Test");
                bob.Name = "Bob";
                bob.Pk = "Bob_Test";
                bob.FirstName = "Bob";
                
                var company2 = tc.CreateEntity<ICompany>("TestCorp2");
                company2.Name = "TestCorp2";
                company2.Pk = "TestCorp2";
                
                await tc.SaveChangesAsync();
                
                // Create an employment edge
                var employment = tc.Employments.Create(bob, company2);
                // Don't set HiredDate to avoid date format issues
                employment.Manager = "TestManager2";
                
                await tc.SaveChangesAsync();
            }
            
            // Delete using context.Delete()
            using (var tc = new TestContext(connector))
            {
                var employments = await tc.Employments.AllAsync();
                var testEmployment = employments.FirstOrDefault(e => e.Manager == "TestManager2");
                
                Assert.NotNull(testEmployment);
                
                // Delete via context
                tc.Delete(testEmployment);
                await tc.SaveChangesAsync();
            }
            
            // Assert: Employment should be gone
            using (var tc = new TestContext(connector))
            {
                var employments = await tc.Employments.AllAsync();
                var testEmployments = employments.Where(e => e.Manager == "TestManager2").ToList();
                
                _output.WriteLine($"Found {testEmployments.Count} employments with Manager=TestManager2 after deletion");
                Assert.Empty(testEmployments);
            }
        }
    }
}
