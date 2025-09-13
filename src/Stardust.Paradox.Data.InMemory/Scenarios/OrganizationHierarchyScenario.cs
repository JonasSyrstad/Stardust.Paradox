using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Organization hierarchy scenario with employees, departments, and management structure
/// </summary>
public class OrganizationHierarchyScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "OrganizationHierarchy";
    public override string Description => "Company structure with employees, departments, and management relationships";

    protected override (ScenarioVertexDefinition[] vertices, SenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new ScenarioVertexDefinition("ceo", "employee", Props(
                ("name", "CEO Smith"),
                ("position", "Chief Executive Officer"),
                ("department", "Executive"),
                ("salary", 200000)
            )),
            new ScenarioVertexDefinition("eng_manager", "employee", Props(
                ("name", "Engineering Manager"),
                ("position", "Engineering Manager"),
                ("department", "Engineering"),
                ("salary", 120000)
            )),
            new ScenarioVertexDefinition("dev1", "employee", Props(
                ("name", "Developer One"),
                ("position", "Senior Developer"),
                ("department", "Engineering"),
                ("salary", 95000)
            )),
            new ScenarioVertexDefinition("dev2", "employee", Props(
                ("name", "Developer Two"),
                ("position", "Junior Developer"),
                ("department", "Engineering"),
                ("salary", 70000)
            )),
            new ScenarioVertexDefinition("hr_manager", "employee", Props(
                ("name", "HR Manager"),
                ("position", "HR Manager"),
                ("department", "Human Resources"),
                ("salary", 85000)
            )),
            new ScenarioVertexDefinition("engineering_dept", "department", Props(
                ("name", "Engineering"),
                ("budget", 500000)
            )),
            new ScenarioVertexDefinition("hr_dept", "department", Props(
                ("name", "Human Resources"),
                ("budget", 200000)
            ))
        };

        var edges = new SenarioEdgeDefinition[]
        {
            new SenarioEdgeDefinition("manages", "ceo", "eng_manager"),
            new SenarioEdgeDefinition("manages", "ceo", "hr_manager"),
            new SenarioEdgeDefinition("manages", "eng_manager", "dev1"),
            new SenarioEdgeDefinition("manages", "eng_manager", "dev2"),
            new SenarioEdgeDefinition("works_for", "eng_manager", "engineering_dept"),
            new SenarioEdgeDefinition("works_for", "dev1", "engineering_dept"),
            new SenarioEdgeDefinition("works_for", "dev2", "engineering_dept"),
            new SenarioEdgeDefinition("works_for", "hr_manager", "hr_dept")
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // All direct reports of CEO
        database.RegisterCustomResponse(@"g\.V\('ceo'\)\.out\('manages'\)", (query, parameters) =>
        {
            var engManager = database.GetVertex("eng_manager")?.ToGremlinResponse();
            var hrManager = database.GetVertex("hr_manager")?.ToGremlinResponse();
            return new[] { engManager, hrManager }.Where(x => x != null);
        });

        // High salary employees
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('employee'\)\.has\('salary', gte\(100000\)\)", (query, parameters) =>
        {
            var ceo = database.GetVertex("ceo")?.ToGremlinResponse();
            var engManager = database.GetVertex("eng_manager")?.ToGremlinResponse();
            return new[] { ceo, engManager }.Where(x => x != null);
        });
    }
}