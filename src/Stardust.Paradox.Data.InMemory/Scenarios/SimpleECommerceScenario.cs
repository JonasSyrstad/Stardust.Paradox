using System;
using Stardust.Paradox.Data.InMemory.Core;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Simple e-commerce scenario with products, customers, and orders
/// </summary>
public class SimpleECommerceScenario : InMemoryScenarioProviderBase
{
    public override string ScenarioName => "SimpleECommerce";
    public override string Description => "Basic e-commerce setup with products, customers, and purchase relationships";

    protected override (ScenarioVertexDefinition[] vertices, SenarioEdgeDefinition[] edges) GetScenarioData()
    {
        var vertices = new ScenarioVertexDefinition[]
        {
            new ScenarioVertexDefinition("customer1", "customer", Props(
                ("name", "Customer One"),
                ("email", "customer1@example.com"),
                ("membershipLevel", "Gold")
            )),
            new ScenarioVertexDefinition("customer2", "customer", Props(
                ("name", "Customer Two"),
                ("email", "customer2@example.com"),
                ("membershipLevel", "Silver")
            )),
            new ScenarioVertexDefinition("laptop", "product", Props(
                ("name", "Gaming Laptop"),
                ("price", 1299.99),
                ("category", "Electronics"),
                ("inStock", true)
            )),
            new ScenarioVertexDefinition("mouse", "product", Props(
                ("name", "Wireless Mouse"),
                ("price", 49.99),
                ("category", "Electronics"),
                ("inStock", true)
            )),
            new ScenarioVertexDefinition("book", "product", Props(
                ("name", "Programming Guide"),
                ("price", 29.99),
                ("category", "Books"),
                ("inStock", false)
            )),
            new ScenarioVertexDefinition("order1", "order", Props(
                ("orderNumber", "ORD-001"),
                ("total", 1349.98),
                ("status", "shipped"),
                ("orderDate", DateTime.UtcNow.AddDays(-3))
            ))
        };

        var edges = new SenarioEdgeDefinition[]
        {
            new SenarioEdgeDefinition("purchased", "customer1", "laptop"),
            new SenarioEdgeDefinition("purchased", "customer1", "mouse"),
            new SenarioEdgeDefinition("purchased", "customer2", "book"),
            new SenarioEdgeDefinition("contains", "order1", "laptop"),
            new SenarioEdgeDefinition("contains", "order1", "mouse"),
            new SenarioEdgeDefinition("placed", "customer1", "order1")
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
