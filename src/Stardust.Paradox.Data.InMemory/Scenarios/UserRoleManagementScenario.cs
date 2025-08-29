using System;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// User role management scenario for testing access control patterns
/// </summary>
public class UserRoleManagementScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "UserRoleManagement";
    public override string Description => "User management system with roles, permissions, and access control";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new InMemoryVertexDefinition[]
        {
            new InMemoryVertexDefinition("admin", "user", Props(
                ("username", "admin"),
                ("email", "admin@company.com"),
                ("active", true),
                ("lastLogin", DateTime.UtcNow.AddHours(-1))
            )),
            new InMemoryVertexDefinition("manager", "user", Props(
                ("username", "manager"),
                ("email", "manager@company.com"),
                ("active", true),
                ("lastLogin", DateTime.UtcNow.AddHours(-3))
            )),
            new InMemoryVertexDefinition("developer", "user", Props(
                ("username", "developer"),
                ("email", "dev@company.com"),
                ("active", true),
                ("lastLogin", DateTime.UtcNow.AddMinutes(-30))
            )),
            new InMemoryVertexDefinition("guest", "user", Props(
                ("username", "guest"),
                ("email", "guest@company.com"),
                ("active", false),
                ("lastLogin", DateTime.UtcNow.AddDays(-7))
            )),
            new InMemoryVertexDefinition("admin_role", "role", Props(
                ("name", "Administrator"),
                ("level", 10),
                ("description", "Full system access")
            )),
            new InMemoryVertexDefinition("manager_role", "role", Props(
                ("name", "Manager"),
                ("level", 7),
                ("description", "Management access")
            )),
            new InMemoryVertexDefinition("dev_role", "role", Props(
                ("name", "Developer"),
                ("level", 5),
                ("description", "Development access")
            )),
            new InMemoryVertexDefinition("guest_role", "role", Props(
                ("name", "Guest"),
                ("level", 1),
                ("description", "Read-only access")
            )),
            new InMemoryVertexDefinition("create_perm", "permission", Props(
                ("name", "CREATE"),
                ("resource", "users")
            )),
            new InMemoryVertexDefinition("read_perm", "permission", Props(
                ("name", "READ"),
                ("resource", "users")
            )),
            new InMemoryVertexDefinition("update_perm", "permission", Props(
                ("name", "UPDATE"),
                ("resource", "users")
            )),
            new InMemoryVertexDefinition("delete_perm", "permission", Props(
                ("name", "DELETE"),
                ("resource", "users")
            ))
        };

        var edges = new InMemoryEdgeDefinition[]
        {
            // User-Role assignments
            new InMemoryEdgeDefinition("has_role", "admin", "admin_role"),
            new InMemoryEdgeDefinition("has_role", "manager", "manager_role"),
            new InMemoryEdgeDefinition("has_role", "developer", "dev_role"),
            new InMemoryEdgeDefinition("has_role", "guest", "guest_role"),
                
            // Role-Permission assignments
            new InMemoryEdgeDefinition("has_permission", "admin_role", "create_perm"),
            new InMemoryEdgeDefinition("has_permission", "admin_role", "read_perm"),
            new InMemoryEdgeDefinition("has_permission", "admin_role", "update_perm"),
            new InMemoryEdgeDefinition("has_permission", "admin_role", "delete_perm"),
            new InMemoryEdgeDefinition("has_permission", "manager_role", "read_perm"),
            new InMemoryEdgeDefinition("has_permission", "manager_role", "update_perm"),
            new InMemoryEdgeDefinition("has_permission", "dev_role", "read_perm"),
            new InMemoryEdgeDefinition("has_permission", "guest_role", "read_perm")
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Active users
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('user'\)\.has\('active', true\)", (query, parameters) =>
        {
            var admin = database.GetVertex("admin")?.ToGremlinResponse();
            var manager = database.GetVertex("manager")?.ToGremlinResponse();
            var developer = database.GetVertex("developer")?.ToGremlinResponse();
            return new[] { admin, manager, developer }.Where(x => x != null);
        });

        // User permissions
        database.RegisterCustomResponse(@"g\.V\('admin'\)\.out\('has_role'\)\.out\('has_permission'\)", (query, parameters) =>
        {
            var create = database.GetVertex("create_perm")?.ToGremlinResponse();
            var read = database.GetVertex("read_perm")?.ToGremlinResponse();
            var update = database.GetVertex("update_perm")?.ToGremlinResponse();
            var delete = database.GetVertex("delete_perm")?.ToGremlinResponse();
            return new[] { create, read, update, delete }.Where(x => x != null);
        });
    }
}