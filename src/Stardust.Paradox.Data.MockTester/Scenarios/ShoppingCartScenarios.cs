using Stardust.Paradox.Data.Mocker;
using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.MockTester.Scenarios
{
    /// <summary>
    /// Empty shopping cart scenario - new customer with no items
    /// </summary>
    public class EmptyCartScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "EmptyCart";
        public override string Description => "New customer with empty shopping cart and available products";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                // Customer
                new VertexDefinition("cust1", "customer", Props(
                    ("name", "John Doe"),
                    ("email", "john.doe@email.com"),
                    ("createdAt", DateTime.UtcNow.AddDays(-1))
                )),

                // Empty cart
                new VertexDefinition("cart1", "cart", Props(
                    ("createdAt", DateTime.UtcNow.AddHours(-2)),
                    ("lastModified", DateTime.UtcNow.AddHours(-2)),
                    ("status", "active")
                )),

                // Available products
                new VertexDefinition("prod1", "product", Props(
                    ("name", "Laptop"),
                    ("description", "High-performance laptop"),
                    ("price", 999.99m),
                    ("stockQuantity", 10),
                    ("category", "Electronics")
                )),
                new VertexDefinition("prod2", "product", Props(
                    ("name", "Mouse"),
                    ("description", "Wireless optical mouse"),
                    ("price", 29.99m),
                    ("stockQuantity", 50),
                    ("category", "Electronics")
                )),
                new VertexDefinition("prod3", "product", Props(
                    ("name", "Book"),
                    ("description", "Programming guide"),
                    ("price", 39.99m),
                    ("stockQuantity", 25),
                    ("category", "Books")
                )),

                // Categories
                new VertexDefinition("cat1", "category", Props(
                    ("name", "Electronics"),
                    ("description", "Electronic devices and accessories")
                )),
                new VertexDefinition("cat2", "category", Props(
                    ("name", "Books"),
                    ("description", "Books and educational materials")
                ))
            };

            var edges = new EdgeDefinition[]
            {
                // Customer owns cart
                new EdgeDefinition("owner1", "owner", "cust1", "cart1"),
                
                // Product categories
                new EdgeDefinition("categ1", "categorized_as", "prod1", "cat1"),
                new EdgeDefinition("categ2", "categorized_as", "prod2", "cat1"),
                new EdgeDefinition("categ3", "categorized_as", "prod3", "cat2")
            };

            return (vertices, edges);
        }
    }

    /// <summary>
    /// Active shopping cart scenario - cart with multiple items
    /// </summary>
    public class ActiveCartScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "ActiveCart";
        public override string Description => "Customer with active shopping cart containing multiple items";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var baseTime = DateTime.UtcNow;
            
            var vertices = new VertexDefinition[]
            {
                // Customer
                new VertexDefinition("cust1", "customer", Props(
                    ("name", "Sarah Smith"),
                    ("email", "sarah.smith@email.com"),
                    ("createdAt", baseTime.AddDays(-30))
                )),

                // Active cart with items
                new VertexDefinition("cart1", "cart", Props(
                    ("createdAt", baseTime.AddHours(-3)),
                    ("lastModified", baseTime.AddMinutes(-15)),
                    ("status", "active")
                )),

                // Products in cart
                new VertexDefinition("prod1", "product", Props(
                    ("name", "Gaming Laptop"),
                    ("description", "High-end gaming laptop"),
                    ("price", 1299.99m),
                    ("stockQuantity", 5),
                    ("category", "Electronics")
                )),
                new VertexDefinition("prod2", "product", Props(
                    ("name", "Gaming Mouse"),
                    ("description", "RGB gaming mouse"),
                    ("price", 79.99m),
                    ("stockQuantity", 20),
                    ("category", "Electronics")
                )),
                new VertexDefinition("prod3", "product", Props(
                    ("name", "Mousepad"),
                    ("description", "Large gaming mousepad"),
                    ("price", 24.99m),
                    ("stockQuantity", 30),
                    ("category", "Electronics")
                )),

                // Category
                new VertexDefinition("cat1", "category", Props(
                    ("name", "Electronics"),
                    ("description", "Electronic devices and accessories")
                ))
            };

            var edges = new EdgeDefinition[]
            {
                // Customer owns cart
                new EdgeDefinition("owner1", "owner", "cust1", "cart1"),
                
                // Cart items
                new EdgeDefinition("item1", "contains", "cart1", "prod1"),
                new EdgeDefinition("item2", "contains", "cart1", "prod2"),
                new EdgeDefinition("item3", "contains", "cart1", "prod3"),
                
                // Product categories
                new EdgeDefinition("categ1", "categorized_as", "prod1", "cat1"),
                new EdgeDefinition("categ2", "categorized_as", "prod2", "cat1"),
                new EdgeDefinition("categ3", "categorized_as", "prod3", "cat1")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Cart total calculation
            connector.ConfigureFunctionResponse(@"g\.V\('cart1'\)\.out\('contains'\)\.values\('priceAtTime'\)\.sum\(\)", (query, parameters) =>
            {
                return new[] { new { sum = 1429.96m } }; // 1299.99 + 79.99 + (24.99 * 2)
            });

            // Cart item count
            connector.ConfigureFunctionResponse(@"g\.V\('cart1'\)\.outE\('contains'\)\.values\('quantity'\)\.sum\(\)", (query, parameters) =>
            {
                return new[] { new { sum = 4 } }; // 1 + 1 + 2
            });
        }
    }

    /// <summary>
    /// Abandoned cart scenario - cart that hasn't been modified recently
    /// </summary>
    public class AbandonedCartScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "AbandonedCart";
        public override string Description => "Customer with abandoned shopping cart (items but no recent activity)";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var baseTime = DateTime.UtcNow;
            
            var vertices = new VertexDefinition[]
            {
                // Customer
                new VertexDefinition("cust1", "customer", Props(
                    ("name", "Mike Johnson"),
                    ("email", "mike.johnson@email.com"),
                    ("createdAt", baseTime.AddMonths(-3))
                )),

                // Abandoned cart
                new VertexDefinition("cart1", "cart", Props(
                    ("createdAt", baseTime.AddDays(-7)),
                    ("lastModified", baseTime.AddDays(-3)),
                    ("status", "abandoned")
                )),

                // Products in abandoned cart
                new VertexDefinition("prod1", "product", Props(
                    ("name", "Smartphone"),
                    ("description", "Latest model smartphone"),
                    ("price", 799.99m),
                    ("stockQuantity", 8),
                    ("category", "Electronics")
                )),
                new VertexDefinition("prod2", "product", Props(
                    ("name", "Phone Case"),
                    ("description", "Protective phone case"),
                    ("price", 19.99m),
                    ("stockQuantity", 100),
                    ("category", "Accessories")
                ))
            };

            var edges = new EdgeDefinition[]
            {
                // Customer owns cart
                new EdgeDefinition("owner1", "owner", "cust1", "cart1"),
                
                // Cart items
                new EdgeDefinition("item1", "contains", "cart1", "prod1"),
                new EdgeDefinition("item2", "contains", "cart1", "prod2")
            };

            return (vertices, edges);
        }
    }

    /// <summary>
    /// Customer with order history scenario
    /// </summary>
    public class CustomerWithOrdersScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "CustomerWithOrders";
        public override string Description => "Returning customer with order history and current active cart";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var baseTime = DateTime.UtcNow;
            
            var vertices = new VertexDefinition[]
            {
                // Returning customer
                new VertexDefinition("cust1", "customer", Props(
                    ("name", "Alice Brown"),
                    ("email", "alice.brown@email.com"),
                    ("createdAt", baseTime.AddMonths(-6))
                )),

                // Current active cart
                new VertexDefinition("cart1", "cart", Props(
                    ("createdAt", baseTime.AddHours(-1)),
                    ("lastModified", baseTime.AddMinutes(-10)),
                    ("status", "active")
                )),

                // Previous orders
                new VertexDefinition("order1", "order", Props(
                    ("orderNumber", "ORD-001"),
                    ("orderDate", baseTime.AddMonths(-2)),
                    ("totalAmount", 159.98m),
                    ("status", "delivered"),
                    ("shippingAddress", "123 Main St, City, State")
                )),
                new VertexDefinition("order2", "order", Props(
                    ("orderNumber", "ORD-002"),
                    ("orderDate", baseTime.AddDays(-14)), // Fixed: AddWeeks(-2) becomes AddDays(-14)
                    ("totalAmount", 89.99m),
                    ("status", "delivered"),
                    ("shippingAddress", "123 Main St, City, State")
                )),

                // Products
                new VertexDefinition("prod1", "product", Props(
                    ("name", "Wireless Headphones"),
                    ("description", "Noise-cancelling headphones"),
                    ("price", 149.99m),
                    ("stockQuantity", 15),
                    ("category", "Electronics")
                )),
                new VertexDefinition("prod2", "product", Props(
                    ("name", "USB Cable"),
                    ("description", "USB-C charging cable"),
                    ("price", 9.99m),
                    ("stockQuantity", 200),
                    ("category", "Electronics")
                )),
                new VertexDefinition("prod3", "product", Props(
                    ("name", "Tablet"),
                    ("description", "10-inch tablet"),
                    ("price", 299.99m),
                    ("stockQuantity", 12),
                    ("category", "Electronics")
                ))
            };

            var edges = new EdgeDefinition[]
            {
                // Customer relationships
                new EdgeDefinition("owner1", "owner", "cust1", "cart1"),
                new EdgeDefinition("purchaser1", "purchaser", "cust1", "order1"),
                new EdgeDefinition("purchaser2", "purchaser", "cust1", "order2"),
                
                // Current cart item
                new EdgeDefinition("cartitem1", "contains", "cart1", "prod3"),
                
                // Order 1 items
                new EdgeDefinition("orderitem1", "ordered", "order1", "prod1"),
                new EdgeDefinition("orderitem2", "ordered", "order1", "prod2"),
                
                // Order 2 items
                new EdgeDefinition("orderitem3", "ordered", "order2", "prod2")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Customer lifetime value
            connector.ConfigureFunctionResponse(@"g\.V\('cust1'\)\.out\('purchaser'\)\.values\('totalAmount'\)\.sum\(\)", (query, parameters) =>
            {
                return new[] { new { sum = 249.97m } }; // 159.98 + 89.99
            });

            // Customer order count
            connector.ConfigureFunctionResponse(@"g\.V\('cust1'\)\.out\('purchaser'\)\.count\(\)", (query, parameters) =>
            {
                return new[] { new { count = 2L } };
            });
        }
    }

    /// <summary>
    /// Multiple customers scenario for testing bulk operations
    /// </summary>
    public class MultipleCustomersScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "MultipleCustomers";
        public override string Description => "Multiple customers with varying cart states for bulk testing";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var baseTime = DateTime.UtcNow;
            
            var vertices = new VertexDefinition[]
            {
                // Customers
                new VertexDefinition("cust1", "customer", Props(
                    ("name", "Customer One"),
                    ("email", "cust1@email.com"),
                    ("createdAt", baseTime.AddMonths(-1))
                )),
                new VertexDefinition("cust2", "customer", Props(
                    ("name", "Customer Two"),
                    ("email", "cust2@email.com"),
                    ("createdAt", baseTime.AddDays(-15))
                )),
                new VertexDefinition("cust3", "customer", Props(
                    ("name", "Customer Three"),
                    ("email", "cust3@email.com"),
                    ("createdAt", baseTime.AddDays(-5))
                )),

                // Shopping carts with different states
                new VertexDefinition("cart1", "cart", Props(
                    ("createdAt", baseTime.AddHours(-2)),
                    ("lastModified", baseTime.AddMinutes(-5)),
                    ("status", "active")
                )),
                new VertexDefinition("cart2", "cart", Props(
                    ("createdAt", baseTime.AddDays(-5)),
                    ("lastModified", baseTime.AddDays(-4)),
                    ("status", "abandoned")
                )),
                new VertexDefinition("cart3", "cart", Props(
                    ("createdAt", baseTime.AddHours(-1)),
                    ("lastModified", baseTime.AddHours(-1)),
                    ("status", "active")
                )),

                // Popular products
                new VertexDefinition("prod1", "product", Props(
                    ("name", "Popular Item"),
                    ("description", "Most popular product"),
                    ("price", 49.99m),
                    ("stockQuantity", 100),
                    ("category", "Trending")
                )),
                new VertexDefinition("prod2", "product", Props(
                    ("name", "Budget Option"),
                    ("description", "Affordable alternative"),
                    ("price", 19.99m),
                    ("stockQuantity", 500),
                    ("category", "Budget")
                ))
            };

            var edges = new EdgeDefinition[]
            {
                // Customer-cart relationships
                new EdgeDefinition("owner1", "owner", "cust1", "cart1"),
                new EdgeDefinition("owner2", "owner", "cust2", "cart2"),
                new EdgeDefinition("owner3", "owner", "cust3", "cart3"),
                
                // Cart items
                new EdgeDefinition("item1", "contains", "cart1", "prod1"),
                new EdgeDefinition("item2", "contains", "cart2", "prod1"),
                new EdgeDefinition("item3", "contains", "cart2", "prod2"),
                new EdgeDefinition("item4", "contains", "cart3", "prod2")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Active carts count
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('cart'\)\.has\('status', 'active'\)\.count\(\)", (query, parameters) =>
            {
                return new[] { new { count = 2L } };
            });

            // Abandoned carts count  
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('cart'\)\.has\('status', 'abandoned'\)\.count\(\)", (query, parameters) =>
            {
                return new[] { new { count = 1L } };
            });

            // Total customers
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('customer'\)\.count\(\)", (query, parameters) =>
            {
                return new[] { new { count = 3L } };
            });
        }
    }
}