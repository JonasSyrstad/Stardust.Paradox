using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.Mocker.Scenarios
{
    /// <summary>
    /// Social network scenario with users, posts, friendships, and interactions
    /// </summary>
    public class SocialNetworkScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "SocialNetwork";
        public override string Description => "Pre-configured social network with users, posts, friendships, and likes";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                new VertexDefinition("user1", "user", Props(
                    ("name", "John Doe"),
                    ("email", "john@example.com"),
                    ("age", 30),
                    ("followers", 150),
                    ("verified", true)
                )),
                new VertexDefinition("user2", "user", Props(
                    ("name", "Jane Smith"),
                    ("email", "jane@example.com"),
                    ("age", 28),
                    ("followers", 340),
                    ("verified", false)
                )),
                new VertexDefinition("user3", "user", Props(
                    ("name", "Bob Johnson"),
                    ("email", "bob@example.com"),
                    ("age", 35),
                    ("followers", 89),
                    ("verified", false)
                )),
                new VertexDefinition("user4", "user", Props(
                    ("name", "Alice Brown"),
                    ("email", "alice@example.com"),
                    ("age", 25),
                    ("followers", 567),
                    ("verified", true)
                )),
                new VertexDefinition("user5", "user", Props(
                    ("name", "Charlie Wilson"),
                    ("email", "charlie@example.com"),
                    ("age", 32),
                    ("followers", 223),
                    ("verified", false)
                )),
                new VertexDefinition("post1", "post", Props(
                    ("title", "Hello World"),
                    ("content", "My first post on this platform!"),
                    ("authorId", "user1"),
                    ("likes", 23),
                    ("timestamp", DateTime.UtcNow.AddHours(-5))
                )),
                new VertexDefinition("post2", "post", Props(
                    ("title", "Tech Trends 2024"),
                    ("content", "Latest developments in technology..."),
                    ("authorId", "user2"),
                    ("likes", 67),
                    ("timestamp", DateTime.UtcNow.AddHours(-2))
                )),
                new VertexDefinition("post3", "post", Props(
                    ("title", "Cooking Tips"),
                    ("content", "Here are some great cooking tips..."),
                    ("authorId", "user4"),
                    ("likes", 45),
                    ("timestamp", DateTime.UtcNow.AddHours(-8))
                ))
            };

            var edges = new EdgeDefinition[]
            {
                new EdgeDefinition("f1", "friends", "user1", "user2"),
                new EdgeDefinition("f2", "friends", "user2", "user3"),
                new EdgeDefinition("f3", "friends", "user1", "user4"),
                new EdgeDefinition("f4", "friends", "user3", "user5"),
                new EdgeDefinition("f5", "friends", "user4", "user5"),
                new EdgeDefinition("a1", "authored", "user1", "post1"),
                new EdgeDefinition("a2", "authored", "user2", "post2"),
                new EdgeDefinition("a3", "authored", "user4", "post3"),
                new EdgeDefinition("l1", "likes", "user2", "post1"),
                new EdgeDefinition("l2", "likes", "user3", "post1"),
                new EdgeDefinition("l3", "likes", "user1", "post2"),
                new EdgeDefinition("l4", "likes", "user4", "post2"),
                new EdgeDefinition("l5", "likes", "user5", "post2"),
                new EdgeDefinition("l6", "likes", "user1", "post3"),
                new EdgeDefinition("l7", "likes", "user2", "post3")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Configure advanced social network queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('user'\)\.has\('verified', true\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("user1", "user", new Dictionary<string, object> { { "name", "John Doe" }, { "verified", true } }),
                    ("user4", "user", new Dictionary<string, object> { { "name", "Alice Brown" }, { "verified", true } })
                );
            });

            // Popular posts query
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('post'\)\.order\(\)\.by\('likes', desc\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("post2", "post", new Dictionary<string, object> { { "title", "Tech Trends 2024" }, { "likes", 67 } }),
                    ("post3", "post", new Dictionary<string, object> { { "title", "Cooking Tips" }, { "likes", 45 } }),
                    ("post1", "post", new Dictionary<string, object> { { "title", "Hello World" }, { "likes", 23 } })
                );
            });

            // Friends of friends query
            connector.ConfigureFunctionResponse(@"g\.V\('user1'\)\.out\('friends'\)\.out\('friends'\)\.dedup\(\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("user3", "user", new Dictionary<string, object> { { "name", "Bob Johnson" } }),
                    ("user5", "user", new Dictionary<string, object> { { "name", "Charlie Wilson" } })
                );
            });
        }
    }

    /// <summary>
    /// Organization scenario with employees, departments, projects, and hierarchies
    /// </summary>
    public class OrganizationScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "Organization";
        public override string Description => "Enterprise organization with employees, departments, projects, and management hierarchy";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                new VertexDefinition("emp1", "employee", Props(
                    ("name", "Alice Johnson"),
                    ("email", "alice.johnson@company.com"),
                    ("department", "Engineering"),
                    ("position", "Senior Developer"),
                    ("salary", 95000),
                    ("hireDate", DateTime.UtcNow.AddYears(-3))
                )),
                new VertexDefinition("emp2", "employee", Props(
                    ("name", "Bob Smith"),
                    ("email", "bob.smith@company.com"),
                    ("department", "Engineering"),
                    ("position", "Junior Developer"),
                    ("salary", 70000),
                    ("hireDate", DateTime.UtcNow.AddYears(-1))
                )),
                new VertexDefinition("emp3", "employee", Props(
                    ("name", "Carol Davis"),
                    ("email", "carol.davis@company.com"),
                    ("department", "Sales"),
                    ("position", "Sales Manager"),
                    ("salary", 85000),
                    ("hireDate", DateTime.UtcNow.AddYears(-2))
                )),
                new VertexDefinition("emp4", "employee", Props(
                    ("name", "David Wilson"),
                    ("email", "david.wilson@company.com"),
                    ("department", "HR"),
                    ("position", "HR Specialist"),
                    ("salary", 65000),
                    ("hireDate", DateTime.UtcNow.AddMonths(-8))
                )),
                new VertexDefinition("emp5", "employee", Props(
                    ("name", "Eva Martinez"),
                    ("email", "eva.martinez@company.com"),
                    ("department", "Engineering"),
                    ("position", "Engineering Manager"),
                    ("salary", 110000),
                    ("hireDate", DateTime.UtcNow.AddYears(-5))
                )),
                new VertexDefinition("dept1", "department", Props(
                    ("name", "Engineering"),
                    ("budget", 500000),
                    ("headCount", 15),
                    ("location", "Building A")
                )),
                new VertexDefinition("dept2", "department", Props(
                    ("name", "Sales"),
                    ("budget", 300000),
                    ("headCount", 8),
                    ("location", "Building B")
                )),
                new VertexDefinition("dept3", "department", Props(
                    ("name", "HR"),
                    ("budget", 200000),
                    ("headCount", 5),
                    ("location", "Building A")
                )),
                new VertexDefinition("proj1", "project", Props(
                    ("name", "Project Alpha"),
                    ("status", "Active"),
                    ("budget", 150000),
                    ("deadline", DateTime.UtcNow.AddMonths(6))
                )),
                new VertexDefinition("proj2", "project", Props(
                    ("name", "Project Beta"),
                    ("status", "Planning"),
                    ("budget", 200000),
                    ("deadline", DateTime.UtcNow.AddMonths(9))
                ))
            };

            var edges = new EdgeDefinition[]
            {
                new EdgeDefinition("w1", "works_in", "emp1", "dept1"),
                new EdgeDefinition("w2", "works_in", "emp2", "dept1"),
                new EdgeDefinition("w3", "works_in", "emp3", "dept2"),
                new EdgeDefinition("w4", "works_in", "emp4", "dept3"),
                new EdgeDefinition("w5", "works_in", "emp5", "dept1"),
                new EdgeDefinition("m1", "manages", "emp5", "emp1"),
                new EdgeDefinition("m2", "manages", "emp5", "emp2"),
                new EdgeDefinition("m3", "manages", "emp1", "emp2"),
                new EdgeDefinition("a1", "assigned_to", "emp1", "proj1"),
                new EdgeDefinition("a2", "assigned_to", "emp2", "proj1"),
                new EdgeDefinition("a3", "assigned_to", "emp5", "proj2"),
                new EdgeDefinition("a4", "assigned_to", "emp3", "proj2")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Department hierarchy queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('department'\)\.in\('works_in'\)\.groupCount\(\)", (query, parameters) =>
            {
                return new[]
                {
                    new { dept1 = 3, dept2 = 1, dept3 = 1 }
                };
            });

            // High-salary employees
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('employee'\)\.has\('salary', gte\(90000\)\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("emp1", "employee", new Dictionary<string, object> { { "name", "Alice Johnson" }, { "salary", 95000 } }),
                    ("emp5", "employee", new Dictionary<string, object> { { "name", "Eva Martinez" }, { "salary", 110000 } })
                );
            });

            // Project team members
            connector.ConfigureFunctionResponse(@"g\.V\('proj1'\)\.in\('assigned_to'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("emp1", "employee", new Dictionary<string, object> { { "name", "Alice Johnson" } }),
                    ("emp2", "employee", new Dictionary<string, object> { { "name", "Bob Smith" } })
                );
            });
        }
    }

    /// <summary>
    /// E-commerce scenario with customers, products, orders, and categories
    /// </summary>
    public class ECommerceScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "ECommerce";
        public override string Description => "E-commerce platform with customers, products, orders, categories, and purchase history";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                new VertexDefinition("cust1", "customer", Props(
                    ("name", "John Buyer"),
                    ("email", "john.buyer@example.com"),
                    ("memberLevel", "Gold"),
                    ("joinDate", DateTime.UtcNow.AddYears(-2)),
                    ("totalSpent", 2500.00)
                )),
                new VertexDefinition("cust2", "customer", Props(
                    ("name", "Jane Shopper"),
                    ("email", "jane.shopper@example.com"),
                    ("memberLevel", "Silver"),
                    ("joinDate", DateTime.UtcNow.AddMonths(-8)),
                    ("totalSpent", 890.50)
                )),
                new VertexDefinition("cust3", "customer", Props(
                    ("name", "Bob Consumer"),
                    ("email", "bob.consumer@example.com"),
                    ("memberLevel", "Bronze"),
                    ("joinDate", DateTime.UtcNow.AddMonths(-3)),
                    ("totalSpent", 156.99)
                )),
                new VertexDefinition("prod1", "product", Props(
                    ("name", "Gaming Laptop"),
                    ("price", 1299.99),
                    ("category", "Electronics"),
                    ("brand", "TechCorp"),
                    ("inStock", true),
                    ("rating", 4.5)
                )),
                new VertexDefinition("prod2", "product", Props(
                    ("name", "Wireless Mouse"),
                    ("price", 49.99),
                    ("category", "Electronics"),
                    ("brand", "TechCorp"),
                    ("inStock", true),
                    ("rating", 4.2)
                )),
                new VertexDefinition("prod3", "product", Props(
                    ("name", "Programming Book"),
                    ("price", 35.99),
                    ("category", "Books"),
                    ("brand", "TechBooks"),
                    ("inStock", true),
                    ("rating", 4.8)
                )),
                new VertexDefinition("prod4", "product", Props(
                    ("name", "Coffee Mug"),
                    ("price", 12.99),
                    ("category", "Home"),
                    ("brand", "CoffeeCorp"),
                    ("inStock", false),
                    ("rating", 4.0)
                )),
                new VertexDefinition("cat1", "category", Props(
                    ("name", "Electronics"),
                    ("description", "Electronic devices and accessories")
                )),
                new VertexDefinition("cat2", "category", Props(
                    ("name", "Books"),
                    ("description", "Books and educational materials")
                )),
                new VertexDefinition("cat3", "category", Props(
                    ("name", "Home"),
                    ("description", "Home and kitchen items")
                )),
                new VertexDefinition("order1", "order", Props(
                    ("orderNumber", "ORD-2024-001"),
                    ("total", 1349.98),
                    ("status", "Shipped"),
                    ("orderDate", DateTime.UtcNow.AddDays(-5))
                )),
                new VertexDefinition("order2", "order", Props(
                    ("orderNumber", "ORD-2024-002"),
                    ("total", 35.99),
                    ("status", "Delivered"),
                    ("orderDate", DateTime.UtcNow.AddDays(-10))
                ))
            };

            var edges = new EdgeDefinition[]
            {
                new EdgeDefinition("p1", "purchased", "cust1", "prod1"),
                new EdgeDefinition("p2", "purchased", "cust1", "prod2"),
                new EdgeDefinition("p3", "purchased", "cust2", "prod3"),
                new EdgeDefinition("p4", "purchased", "cust3", "prod4"),
                new EdgeDefinition("c1", "belongs_to", "prod1", "cat1"),
                new EdgeDefinition("c2", "belongs_to", "prod2", "cat1"),
                new EdgeDefinition("c3", "belongs_to", "prod3", "cat2"),
                new EdgeDefinition("c4", "belongs_to", "prod4", "cat3"),
                new EdgeDefinition("o1", "contains", "order1", "prod1"),
                new EdgeDefinition("o2", "contains", "order1", "prod2"),
                new EdgeDefinition("o3", "contains", "order2", "prod3"),
                new EdgeDefinition("placed1", "placed_by", "order1", "cust1"),
                new EdgeDefinition("placed2", "placed_by", "order2", "cust2"),
                new EdgeDefinition("r1", "reviewed", "cust1", "prod1"),
                new EdgeDefinition("r2", "reviewed", "cust2", "prod3")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Product recommendations based on purchase history
            connector.ConfigureFunctionResponse(@"g\.V\('cust1'\)\.out\('purchased'\)\.in\('purchased'\)\.out\('purchased'\)\.dedup\(\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("prod3", "product", new Dictionary<string, object> { { "name", "Programming Book" }, { "price", 35.99 } }),
                    ("prod4", "product", new Dictionary<string, object> { { "name", "Coffee Mug" }, { "price", 12.99 } })
                );
            });

            // Top-rated products
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('product'\)\.order\(\)\.by\('rating', desc\)\.limit\(3\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("prod3", "product", new Dictionary<string, object> { { "name", "Programming Book" }, { "rating", 4.8 } }),
                    ("prod1", "product", new Dictionary<string, object> { { "name", "Gaming Laptop" }, { "rating", 4.5 } }),
                    ("prod2", "product", new Dictionary<string, object> { { "name", "Wireless Mouse" }, { "rating", 4.2 } })
                );
            });

            // Customer order history
            connector.ConfigureFunctionResponse(@"g\.V\('cust1'\)\.in\('placed_by'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("order1", "order", new Dictionary<string, object> { { "orderNumber", "ORD-2024-001" }, { "total", 1349.98 } })
                );
            });
        }
    }

    /// <summary>
    /// User management scenario with users, roles, permissions, and access control
    /// </summary>
    public class UserManagementScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "UserManagement";
        public override string Description => "User management system with users, roles, permissions, and role-based access control";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                new VertexDefinition("user1", "user", Props(
                    ("username", "admin"),
                    ("email", "admin@example.com"),
                    ("active", true),
                    ("lastLogin", DateTime.UtcNow.AddHours(-2)),
                    ("createdAt", DateTime.UtcNow.AddMonths(-12))
                )),
                new VertexDefinition("user2", "user", Props(
                    ("username", "manager"),
                    ("email", "manager@example.com"),
                    ("active", true),
                    ("lastLogin", DateTime.UtcNow.AddHours(-4)),
                    ("createdAt", DateTime.UtcNow.AddMonths(-8))
                )),
                new VertexDefinition("user3", "user", Props(
                    ("username", "developer"),
                    ("email", "dev@example.com"),
                    ("active", true),
                    ("lastLogin", DateTime.UtcNow.AddMinutes(-30)),
                    ("createdAt", DateTime.UtcNow.AddMonths(-6))
                )),
                new VertexDefinition("user4", "user", Props(
                    ("username", "guest"),
                    ("email", "guest@example.com"),
                    ("active", false),
                    ("lastLogin", DateTime.UtcNow.AddDays(-30)),
                    ("createdAt", DateTime.UtcNow.AddMonths(-1))
                )),
                new VertexDefinition("role1", "role", Props(
                    ("name", "Administrator"),
                    ("description", "Full system access"),
                    ("level", 10)
                )),
                new VertexDefinition("role2", "role", Props(
                    ("name", "Manager"),
                    ("description", "Management access"),
                    ("level", 7)
                )),
                new VertexDefinition("role3", "role", Props(
                    ("name", "Developer"),
                    ("description", "Development access"),
                    ("level", 5)
                )),
                new VertexDefinition("role4", "role", Props(
                    ("name", "Guest"),
                    ("description", "Read-only access"),
                    ("level", 1)
                )),
                new VertexDefinition("perm1", "permission", Props(
                    ("name", "CREATE_USER"),
                    ("resource", "users"),
                    ("action", "create")
                )),
                new VertexDefinition("perm2", "permission", Props(
                    ("name", "DELETE_USER"),
                    ("resource", "users"),
                    ("action", "delete")
                )),
                new VertexDefinition("perm3", "permission", Props(
                    ("name", "READ_USER"),
                    ("resource", "users"),
                    ("action", "read")
                )),
                new VertexDefinition("perm4", "permission", Props(
                    ("name", "UPDATE_USER"),
                    ("resource", "users"),
                    ("action", "update")
                )),
                new VertexDefinition("perm5", "permission", Props(
                    ("name", "MANAGE_ROLES"),
                    ("resource", "roles"),
                    ("action", "manage")
                ))
            };

            var edges = new EdgeDefinition[]
            {
                new EdgeDefinition("ur1", "has_role", "user1", "role1"),
                new EdgeDefinition("ur2", "has_role", "user2", "role2"),
                new EdgeDefinition("ur3", "has_role", "user3", "role3"),
                new EdgeDefinition("ur4", "has_role", "user4", "role4"),
                new EdgeDefinition("rp1", "has_permission", "role1", "perm1"),
                new EdgeDefinition("rp2", "has_permission", "role1", "perm2"),
                new EdgeDefinition("rp3", "has_permission", "role1", "perm3"),
                new EdgeDefinition("rp4", "has_permission", "role1", "perm4"),
                new EdgeDefinition("rp5", "has_permission", "role1", "perm5"),
                new EdgeDefinition("rp6", "has_permission", "role2", "perm3"),
                new EdgeDefinition("rp7", "has_permission", "role2", "perm4"),
                new EdgeDefinition("rp8", "has_permission", "role3", "perm3"),
                new EdgeDefinition("rp9", "has_permission", "role4", "perm3")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Active users query
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('user'\)\.has\('active', true\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("user1", "user", new Dictionary<string, object> { { "username", "admin" }, { "active", true } }),
                    ("user2", "user", new Dictionary<string, object> { { "username", "manager" }, { "active", true } }),
                    ("user3", "user", new Dictionary<string, object> { { "username", "developer" }, { "active", true } })
                );
            });

            // User permissions query
            connector.ConfigureFunctionResponse(@"g\.V\('user1'\)\.out\('has_role'\)\.out\('has_permission'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("perm1", "permission", new Dictionary<string, object> { { "name", "CREATE_USER" } }),
                    ("perm2", "permission", new Dictionary<string, object> { { "name", "DELETE_USER" } }),
                    ("perm3", "permission", new Dictionary<string, object> { { "name", "READ_USER" } }),
                    ("perm4", "permission", new Dictionary<string, object> { { "name", "UPDATE_USER" } }),
                    ("perm5", "permission", new Dictionary<string, object> { { "name", "MANAGE_ROLES" } })
                );
            });

            // Role hierarchy query
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('role'\)\.order\(\)\.by\('level', desc\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("role1", "role", new Dictionary<string, object> { { "name", "Administrator" }, { "level", 10 } }),
                    ("role2", "role", new Dictionary<string, object> { { "name", "Manager" }, { "level", 7 } }),
                    ("role3", "role", new Dictionary<string, object> { { "name", "Developer" }, { "level", 5 } }),
                    ("role4", "role", new Dictionary<string, object> { { "name", "Guest" }, { "level", 1 } })
                );
            });
        }
    }
}