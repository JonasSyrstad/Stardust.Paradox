using System;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Simple e-commerce scenario with products, customers, and orders
/// </summary>
public class SimpleECommerceScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "SimpleECommerce";
    public override string Description => "Basic e-commerce setup with products, customers, and purchase relationships";

    protected override (InMemoryVertexDefinition[] vertices, InMemoryEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new InMemoryVertexDefinition[]
        {
            new InMemoryVertexDefinition("customer1", "customer", Props(
                ("name", "Customer One"),
                ("email", "customer1@example.com"),
                ("membershipLevel", "Gold")
            )),
            new InMemoryVertexDefinition("customer2", "customer", Props(
                ("name", "Customer Two"),
                ("email", "customer2@example.com"),
                ("membershipLevel", "Silver")
            )),
            new InMemoryVertexDefinition("laptop", "product", Props(
                ("name", "Gaming Laptop"),
                ("price", 1299.99),
                ("category", "Electronics"),
                ("inStock", true)
            )),
            new InMemoryVertexDefinition("mouse", "product", Props(
                ("name", "Wireless Mouse"),
                ("price", 49.99),
                ("category", "Electronics"),
                ("inStock", true)
            )),
            new InMemoryVertexDefinition("book", "product", Props(
                ("name", "Programming Guide"),
                ("price", 29.99),
                ("category", "Books"),
                ("inStock", false)
            )),
            new InMemoryVertexDefinition("order1", "order", Props(
                ("orderNumber", "ORD-001"),
                ("total", 1349.98),
                ("status", "shipped"),
                ("orderDate", DateTime.UtcNow.AddDays(-3))
            ))
        };

        var edges = new InMemoryEdgeDefinition[]
        {
            new InMemoryEdgeDefinition("purchased", "customer1", "laptop"),
            new InMemoryEdgeDefinition("purchased", "customer1", "mouse"),
            new InMemoryEdgeDefinition("purchased", "customer2", "book"),
            new InMemoryEdgeDefinition("contains", "order1", "laptop"),
            new InMemoryEdgeDefinition("contains", "order1", "mouse"),
            new InMemoryEdgeDefinition("placed", "customer1", "order1")
        };

        return (vertices, edges);
    }

    protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // High-value customers query
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('customer'\)\.has\('membershipLevel', 'Gold'\)", (query, parameters) =>
        {
            return new[] { database.GetVertex("customer1")?.ToGremlinResponse() }.Where(x => x != null);
        });

        // Products in stock
        database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('product'\)\.has\('inStock', true\)", (query, parameters) =>
        {
            var laptop = database.GetVertex("laptop")?.ToGremlinResponse();
            var mouse = database.GetVertex("mouse")?.ToGremlinResponse();
            return new[] { laptop, mouse }.Where(x => x != null);
        });
    }
}