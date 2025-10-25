using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.Linq.Tests.Scenarios
{
    /// <summary>
    /// Test scenario for LINQ query testing with a diverse set of data
    /// </summary>
    public class LinqTestScenario : InMemoryScenarioProviderBase
    {
        public override string ScenarioName => "LinqTest";
        public override string Description => "Comprehensive test data for LINQ query operations";

        protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new ScenarioVertexDefinition[]
            {
                // Users
                new ScenarioVertexDefinition("user1", "person", Props(
                    ("pk", "test-partition"),
                    ("name", "Alice Johnson"),
                    ("email", "alice@example.com"),
                    ("age", 30),
                    ("isActive", true),
                    ("score", 95.5m),
                    ("city", "Seattle")
                )),
                new ScenarioVertexDefinition("user2", "person", Props(
                    ("pk", "test-partition"),
                    ("name", "Bob Smith"),
                    ("email", "bob@example.com"),
                    ("age", 25),
                    ("isActive", true),
                    ("score", 88.0m),
                    ("city", "Portland")
                )),
                new ScenarioVertexDefinition("user3", "person", Props(
                    ("pk", "test-partition"),
                    ("name", "Charlie Brown"),
                    ("email", "charlie@example.com"),
                    ("age", 35),
                    ("isActive", false),
                    ("score", 92.3m),
                    ("city", "Seattle")
                )),
                new ScenarioVertexDefinition("user4", "person", Props(
                    ("pk", "test-partition"),
                    ("name", "Diana Prince"),
                    ("email", "diana@example.com"),
                    ("age", 28),
                    ("isActive", true),
                    ("score", 98.7m),
                    ("city", "Boston")
                )),
                new ScenarioVertexDefinition("user5", "person", Props(
                    ("pk", "test-partition"),
                    ("name", "Eve Adams"),
                    ("email", "eve@example.com"),
                    ("age", 42),
                    ("isActive", false),
                    ("score", 85.2m),
                    ("city", "Seattle")
                )),
                
                // Companies
                new ScenarioVertexDefinition("company1", "company", Props(
                    ("pk", "test-partition"),
                    ("name", "TechCorp"),
                    ("industry", "Technology"),
                    ("employeeCount", 500),
                    ("founded", 2010)
                )),
                new ScenarioVertexDefinition("company2", "company", Props(
                    ("pk", "test-partition"),
                    ("name", "DataSystems"),
                    ("industry", "Technology"),
                    ("employeeCount", 250),
                    ("founded", 2015)
                )),
                new ScenarioVertexDefinition("company3", "company", Props(
                    ("pk", "test-partition"),
                    ("name", "CloudServices"),
                    ("industry", "Cloud Computing"),
                    ("employeeCount", 1000),
                    ("founded", 2008)
                )),
                
                // Projects
                new ScenarioVertexDefinition("project1", "project", Props(
                    ("pk", "test-partition"),
                    ("name", "Project Alpha"),
                    ("status", "Active"),
                    ("budget", 100000),
                    ("priority", 1)
                )),
                new ScenarioVertexDefinition("project2", "project", Props(
                    ("pk", "test-partition"),
                    ("name", "Project Beta"),
                    ("status", "Completed"),
                    ("budget", 75000),
                    ("priority", 2)
                )),
                new ScenarioVertexDefinition("project3", "project", Props(
                    ("pk", "test-partition"),
                    ("name", "Project Gamma"),
                    ("status", "Active"),
                    ("budget", 150000),
                    ("priority", 1)
                )),
                
                // Skills
                new ScenarioVertexDefinition("skill1", "skill", Props(
                    ("pk", "test-partition"),
                    ("name", "C#"),
                    ("category", "Programming"),
                    ("level", "Advanced")
                )),
                new ScenarioVertexDefinition("skill2", "skill", Props(
                    ("pk", "test-partition"),
                    ("name", "Python"),
                    ("category", "Programming"),
                    ("level", "Intermediate")
                )),
                new ScenarioVertexDefinition("skill3", "skill", Props(
                    ("pk", "test-partition"),
                    ("name", "Azure"),
                    ("category", "Cloud"),
                    ("level", "Advanced")
                ))
            };

            var edges = new ScenarioEdgeDefinition[]
            {
                // Employment relationships
                new ScenarioEdgeDefinition("worksAt", "user1", "company1", Props(
                    ("role", "Senior Developer"),
                    ("startDate", "2020-01-15"),
                    ("salary", 120000)
                )),
                new ScenarioEdgeDefinition("worksAt", "user2", "company1", Props(
                    ("role", "Developer"),
                    ("startDate", "2021-06-01"),
                    ("salary", 95000)
                )),
                new ScenarioEdgeDefinition("worksAt", "user3", "company2", Props(
                    ("role", "Lead Developer"),
                    ("startDate", "2019-03-10"),
                    ("salary", 135000)
                )),
                new ScenarioEdgeDefinition("worksAt", "user4", "company3", Props(
                    ("role", "Architect"),
                    ("startDate", "2018-09-01"),
                    ("salary", 150000)
                )),
                
                // Friendships
                new ScenarioEdgeDefinition("friendsWith", "user1", "user2", Props(
                    ("since", "2020-05-10"),
                    ("strength", "strong")
                )),
                new ScenarioEdgeDefinition("friendsWith", "user1", "user4", Props(
                    ("since", "2019-11-20"),
                    ("strength", "medium")
                )),
                new ScenarioEdgeDefinition("friendsWith", "user2", "user3", Props(
                    ("since", "2021-02-14"),
                    ("strength", "strong")
                )),
                new ScenarioEdgeDefinition("friendsWith", "user4", "user5", Props(
                    ("since", "2018-07-04"),
                    ("strength", "weak")
                )),
                
                // Project assignments
                new ScenarioEdgeDefinition("assignedTo", "user1", "project1", Props(
                    ("role", "Lead"),
                    ("hoursPerWeek", 40)
                )),
                new ScenarioEdgeDefinition("assignedTo", "user2", "project1", Props(
                    ("role", "Developer"),
                    ("hoursPerWeek", 30)
                )),
                new ScenarioEdgeDefinition("assignedTo", "user1", "project3", Props(
                    ("role", "Consultant"),
                    ("hoursPerWeek", 10)
                )),
                new ScenarioEdgeDefinition("assignedTo", "user4", "project2", Props(
                    ("role", "Architect"),
                    ("hoursPerWeek", 20)
                )),
                
                // Skills
                new ScenarioEdgeDefinition("hasSkill", "user1", "skill1", Props(
                    ("yearsExperience", 8),
                    ("proficiency", 9)
                )),
                new ScenarioEdgeDefinition("hasSkill", "user1", "skill3", Props(
                    ("yearsExperience", 5),
                    ("proficiency", 8)
                )),
                new ScenarioEdgeDefinition("hasSkill", "user2", "skill2", Props(
                    ("yearsExperience", 3),
                    ("proficiency", 7)
                )),
                new ScenarioEdgeDefinition("hasSkill", "user3", "skill1", Props(
                    ("yearsExperience", 10),
                    ("proficiency", 10)
                )),
                new ScenarioEdgeDefinition("hasSkill", "user4", "skill1", Props(
                    ("yearsExperience", 12),
                    ("proficiency", 10)
                )),
                new ScenarioEdgeDefinition("hasSkill", "user4", "skill3", Props(
                    ("yearsExperience", 7),
                    ("proficiency", 9)
                ))
            };

            return (vertices, edges);
        }
    }
}
