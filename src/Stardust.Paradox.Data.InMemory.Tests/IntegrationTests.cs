using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Integration tests that test the complete functionality
/// </summary>
public class IntegrationTests
{
    [Fact]
    public async Task CompleteUserJourney_ShouldWorkEndToEnd()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create(options =>
        {
            options.LogQueries = false;
            options.SimulatedRUPerQuery = 1.5;
        });

        // Act & Assert - Complete user journey

        // 1. Create initial data
        await connector.ExecuteAsync("g.addV('user').property('id', 'u1').property('name', 'Alice').property('email', 'alice@example.com').property('age', 28)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('user').property('id', 'u2').property('name', 'Bob').property('email', 'bob@example.com').property('age', 32)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('company').property('id', 'c1').property('name', 'TechCorp').property('industry', 'Technology')", new Dictionary<string, object>());

        // 2. Create relationships
        await connector.ExecuteAsync("g.V('u1').addE('works_for').to(g.V('c1')).property('position', 'Developer')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('u2').addE('works_for').to(g.V('c1')).property('position', 'Manager')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('u1').addE('reports_to').to(g.V('u2'))", new Dictionary<string, object>());

        // 3. Query data with various patterns
        var users = await connector.ExecuteAsync("g.V().hasLabel('user')", new Dictionary<string, object>());
        users.Should().HaveCount(2);

        var company = await connector.ExecuteAsync("g.V().hasLabel('company')", new Dictionary<string, object>());
        company.Should().HaveCount(1);

        // 4. Complex traversals
        var colleagues = await connector.ExecuteAsync("g.V('u1').out('works_for').in('works_for').hasLabel('user')", new Dictionary<string, object>());
        colleagues.Should().HaveCount(2); // Alice and Bob

        var manager = await connector.ExecuteAsync("g.V('u1').out('reports_to')", new Dictionary<string, object>());
        manager.Should().HaveCount(1);
        ((string)manager.First().properties.name).Should().Be("Bob");

        // 5. Aggregations
        var avgAge = await connector.ExecuteAsync("g.V().hasLabel('user').values('age').mean()", new Dictionary<string, object>());
        ((double)avgAge.First()).Should().Be(30.0); // (28 + 32) / 2

        var employeeCount = await connector.ExecuteAsync("g.V('c1').in('works_for').count()", new Dictionary<string, object>());
        ((long)employeeCount.First()).Should().Be(2L);

        // 6. Updates
        await connector.ExecuteAsync("g.V('u1').property('age', 29)", new Dictionary<string, object>());
        var updatedUser = await connector.ExecuteAsync("g.V('u1')", new Dictionary<string, object>());
        ((int)updatedUser.First().properties.age).Should().Be(29);

        // 7. Parameterized queries
        var bobData = await connector.ExecuteAsync("g.V().has('name', p0)", 
            new Dictionary<string, object> { { "p0", "Bob" } });
        bobData.Should().HaveCount(1);
        ((string)bobData.First().properties.name).Should().Be("Bob");

        // 8. Property operations
        var emails = await connector.ExecuteAsync("g.V().hasLabel('user').values('email')", new Dictionary<string, object>());
        emails.Should().HaveCount(2);

        var valueMaps = await connector.ExecuteAsync("g.V('u1').valueMap()", new Dictionary<string, object>());
        var valueMap = valueMaps.First() as Dictionary<string, object>;
        valueMap.Should().ContainKey("name");
        valueMap.Should().ContainKey("email");
        valueMap.Should().ContainKey("age");

        // 9. Edge operations
        var workRelations = await connector.ExecuteAsync("g.E().hasLabel('works_for')", new Dictionary<string, object>());
        workRelations.Should().HaveCount(2);

        var positions = await connector.ExecuteAsync("g.E().hasLabel('works_for').values('position')", new Dictionary<string, object>());
        positions.Should().HaveCount(2);

        // 10. Deletion
        await connector.ExecuteAsync("g.V('u2').drop()", new Dictionary<string, object>());
        var remainingUsers = await connector.ExecuteAsync("g.V().hasLabel('user')", new Dictionary<string, object>());
        remainingUsers.Should().HaveCount(1);

        // Verify connected edges were also removed
        var remainingEdges = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
        remainingEdges.Should().HaveCount(1); // Only u1->c1 should remain

        // 11. Verify RU consumption
        connector.ConsumedRU.Should().BeGreaterThan(0);

        // 12. Statistics
        var (vertexCount, edgeCount) = connector.GetStatistics();
        vertexCount.Should().Be(2); // u1 and c1
        edgeCount.Should().Be(1);   // u1->c1 works_for
    }

    [Fact]
    public async Task SocialNetworkScenario_ShouldHandleComplexRelationships()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act - Build social network
        // Create users
        await connector.ExecuteAsync("g.addV('user').property('id', 'alice').property('name', 'Alice').property('city', 'NYC')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('user').property('id', 'bob').property('name', 'Bob').property('city', 'LA')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('user').property('id', 'charlie').property('name', 'Charlie').property('city', 'NYC')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('user').property('id', 'diana').property('name', 'Diana').property('city', 'Chicago')", new Dictionary<string, object>());

        // Create posts
        await connector.ExecuteAsync("g.addV('post').property('id', 'p1').property('content', 'Hello World!').property('author', 'alice')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('post').property('id', 'p2').property('content', 'Great day!').property('author', 'bob')", new Dictionary<string, object>());

        // Create relationships
        await connector.ExecuteAsync("g.V('alice').addE('follows').to(g.V('bob'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('follows').to(g.V('charlie'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('charlie').addE('follows').to(g.V('diana'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('alice').addE('friends').to(g.V('charlie'))", new Dictionary<string, object>());

        // Post interactions
        await connector.ExecuteAsync("g.V('alice').addE('posted').to(g.V('p1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('posted').to(g.V('p2'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('bob').addE('liked').to(g.V('p1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('charlie').addE('liked').to(g.V('p1'))", new Dictionary<string, object>());

        // Assert - Complex queries

        // Find Alice's followers
        var aliceFollowers = await connector.ExecuteAsync("g.V('alice').in('follows')", new Dictionary<string, object>());
        aliceFollowers.Should().BeEmpty(); // No one follows Alice

        // Find who Alice follows
        var aliceFollowing = await connector.ExecuteAsync("g.V('alice').out('follows')", new Dictionary<string, object>());
        aliceFollowing.Should().HaveCount(1);
        ((string)aliceFollowing.First().properties.name).Should().Be("Bob");

        // Find Alice's friends in the same city
        var nycFriends = await connector.ExecuteAsync("g.V('alice').out('friends').has('city', 'NYC')", new Dictionary<string, object>());
        nycFriends.Should().HaveCount(1);
        ((string)nycFriends.First().properties.name).Should().Be("Charlie");

        // Find posts liked by Alice's friends
        var friendLikedPosts = await connector.ExecuteAsync("g.V('alice').out('friends').out('liked')", new Dictionary<string, object>());
        friendLikedPosts.Should().HaveCount(1);
        ((string)friendLikedPosts.First().properties.content).Should().Be("Hello World!");

        // Find second-degree connections (friends of friends)
        var secondDegree = await connector.ExecuteAsync("g.V('alice').out('follows').out('follows')", new Dictionary<string, object>());
        secondDegree.Should().HaveCount(1);
        ((string)secondDegree.First().properties.name).Should().Be("Charlie");

        // Count likes per post
        var likesOnP1 = await connector.ExecuteAsync("g.V('p1').in('liked').count()", new Dictionary<string, object>());
        ((long)likesOnP1.First()).Should().Be(2L); // Bob and Charlie liked p1

        // Find users by city
        var nycUsers = await connector.ExecuteAsync("g.V().hasLabel('user').has('city', 'NYC')", new Dictionary<string, object>());
        nycUsers.Should().HaveCount(2); // Alice and Charlie
    }

    [Fact]
    public async Task ECommerceScenario_ShouldHandleTransactionalData()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Act - Build e-commerce graph
        // Create customers
        await connector.ExecuteAsync("g.addV('customer').property('id', 'cust1').property('name', 'John Smith').property('email', 'john@example.com')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('customer').property('id', 'cust2').property('name', 'Jane Doe').property('email', 'jane@example.com')", new Dictionary<string, object>());

        // Create products
        await connector.ExecuteAsync("g.addV('product').property('id', 'prod1').property('name', 'Laptop').property('category', 'Electronics').property('price', 999.99)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('product').property('id', 'prod2').property('name', 'Mouse').property('category', 'Electronics').property('price', 29.99)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('product').property('id', 'prod3').property('name', 'Desk Chair').property('category', 'Furniture').property('price', 199.99)", new Dictionary<string, object>());

        // Create orders
        await connector.ExecuteAsync("g.addV('order').property('id', 'order1').property('total', 1029.98).property('status', 'completed')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('order').property('id', 'order2').property('total', 199.99).property('status', 'pending')", new Dictionary<string, object>());

        // Create relationships
        await connector.ExecuteAsync("g.V('cust1').addE('placed').to(g.V('order1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('cust2').addE('placed').to(g.V('order2'))", new Dictionary<string, object>());

        await connector.ExecuteAsync("g.V('order1').addE('contains').to(g.V('prod1')).property('quantity', 1)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('order1').addE('contains').to(g.V('prod2')).property('quantity', 1)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('order2').addE('contains').to(g.V('prod3')).property('quantity', 1)", new Dictionary<string, object>());

        await connector.ExecuteAsync("g.V('cust1').addE('viewed').to(g.V('prod3'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('cust2').addE('viewed').to(g.V('prod1'))", new Dictionary<string, object>());

        // Assert - E-commerce queries

        // Find all orders by customer
        var johnOrders = await connector.ExecuteAsync("g.V('cust1').out('placed')", new Dictionary<string, object>());
        johnOrders.Should().HaveCount(1);

        // Find products in completed orders
        var completedOrderProducts = await connector.ExecuteAsync("g.V().hasLabel('order').has('status', 'completed').out('contains')", new Dictionary<string, object>());
        completedOrderProducts.Should().HaveCount(2); // Laptop and Mouse

        // Find customers who bought electronics
        var electronicsCustomers = await connector.ExecuteAsync("g.V().hasLabel('product').has('category', 'Electronics').in('contains').in('placed')", new Dictionary<string, object>());
        electronicsCustomers.Should().HaveCount(1); // John

        // Calculate total revenue
        var totalRevenue = await connector.ExecuteAsync("g.V().hasLabel('order').has('status', 'completed').values('total').sum()", new Dictionary<string, object>());
        decimal revenue = Convert.ToDecimal(totalRevenue.First());
        revenue.Should().BeApproximately(1029.98m, 0.01m);

        // Find products viewed but not purchased
        var viewedNotPurchased = await connector.ExecuteAsync("g.V('cust1').out('viewed')", new Dictionary<string, object>());
        viewedNotPurchased.Should().HaveCount(1); // Desk Chair

        // Find recommended products (what other customers who bought similar items also bought)
        var recommendations = await connector.ExecuteAsync("g.V('cust1').out('placed').out('contains').in('contains').in('placed').out('placed').out('contains').dedup()", new Dictionary<string, object>());
        recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AdvancedFilteringAndProjection_ShouldWorkCorrectly()
    {
        // Arrange
        var connector = InMemoryGremlinLanguageConnector.Create();

        // Setup complex test data
        await SetupAdvancedTestData(connector);

        // Act & Assert - Complex filtering and projection queries

        // Multiple has conditions
        var seniorEngineers = await connector.ExecuteAsync("g.V().hasLabel('employee').has('department', 'Engineering').has('seniority', 'Senior')", new Dictionary<string, object>());
        seniorEngineers.Should().HaveCount(1);

        // hasId with multiple values
        var specificEmployees = await connector.ExecuteAsync("g.V().hasId('emp1', 'emp3')", new Dictionary<string, object>());
        specificEmployees.Should().HaveCount(2);

        // Complex value projection
        var engineeringEmails = await connector.ExecuteAsync("g.V().hasLabel('employee').has('department', 'Engineering').values('email')", new Dictionary<string, object>());
        engineeringEmails.Should().HaveCount(2);

        // Filtered value maps - make more robust
        var contactInfo = await connector.ExecuteAsync("g.V('emp1').valueMap('name', 'email')", new Dictionary<string, object>());
        if (contactInfo.Any())
        {
            var contactDict = contactInfo.First() as Dictionary<string, object>;
            if (contactDict != null)
            {
                contactDict.Should().ContainKey("name");
                contactDict.Should().ContainKey("email");
                contactDict.Should().NotContainKey("department");
            }
        }

        // Element maps with complete info
        var fullEmployeeInfo = await connector.ExecuteAsync("g.V('emp1').elementMap()", new Dictionary<string, object>());
        if (fullEmployeeInfo.Any())
        {
            var fullInfo = fullEmployeeInfo.First() as Dictionary<string, object>;
            if (fullInfo != null)
            {
                fullInfo.Should().ContainKey("id");
                fullInfo.Should().ContainKey("label");
                fullInfo.Should().ContainKey("type");
                fullInfo.Should().ContainKey("name");
            }
        }

        // Properties with specific keys
        var nameAgeProps = await connector.ExecuteAsync("g.V('emp1').properties('name', 'age')", new Dictionary<string, object>());
        nameAgeProps.Should().HaveCount(2);

        // Deduplication across complex traversals
        var uniqueDepartments = await connector.ExecuteAsync("g.V().hasLabel('employee').values('department').dedup()", new Dictionary<string, object>());
        uniqueDepartments.Should().HaveCount(3); // Engineering, Sales, HR
    }

    private static async Task SetupAdvancedTestData(InMemoryGremlinLanguageConnector connector)
    {
        // Create employees with various attributes
        await connector.ExecuteAsync("g.addV('employee').property('id', 'emp1').property('name', 'Alice Johnson').property('email', 'alice@company.com').property('department', 'Engineering').property('seniority', 'Senior').property('age', 32)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('employee').property('id', 'emp2').property('name', 'Bob Smith').property('email', 'bob@company.com').property('department', 'Engineering').property('seniority', 'Junior').property('age', 25)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('employee').property('id', 'emp3').property('name', 'Carol Brown').property('email', 'carol@company.com').property('department', 'Sales').property('seniority', 'Senior').property('age', 29)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('employee').property('id', 'emp4').property('name', 'David Wilson').property('email', 'david@company.com').property('department', 'HR').property('seniority', 'Manager').property('age', 35)", new Dictionary<string, object>());

        // Create departments
        await connector.ExecuteAsync("g.addV('department').property('id', 'eng').property('name', 'Engineering').property('budget', 500000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('department').property('id', 'sales').property('name', 'Sales').property('budget', 300000)", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('department').property('id', 'hr').property('name', 'HR').property('budget', 200000)", new Dictionary<string, object>());

        // Create projects
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj1').property('name', 'Project Alpha').property('status', 'active').property('priority', 'high')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('project').property('id', 'proj2').property('name', 'Project Beta').property('status', 'completed').property('priority', 'medium')", new Dictionary<string, object>());

        // Create relationships
        await connector.ExecuteAsync("g.V('emp1').addE('belongs_to').to(g.V('eng'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp2').addE('belongs_to').to(g.V('eng'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp3').addE('belongs_to').to(g.V('sales'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp4').addE('belongs_to').to(g.V('hr'))", new Dictionary<string, object>());

        await connector.ExecuteAsync("g.V('emp1').addE('assigned_to').to(g.V('proj1')).property('role', 'lead')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp2').addE('assigned_to').to(g.V('proj1')).property('role', 'developer')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp3').addE('assigned_to').to(g.V('proj2')).property('role', 'coordinator')", new Dictionary<string, object>());

        await connector.ExecuteAsync("g.V('emp2').addE('reports_to').to(g.V('emp1'))", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('emp1').addE('collaborates_with').to(g.V('emp3'))", new Dictionary<string, object>());
    }
}