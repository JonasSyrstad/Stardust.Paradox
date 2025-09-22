using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.InMemory;

namespace Stardust.Paradox.Data.InMemory.Demo
{
    /// <summary>
    /// Complex scenario provider that creates a realistic social network with e-commerce elements
    /// This scenario includes users, products, companies, orders, friendships, and reviews
    /// </summary>
    public class SocialCommerceScenario
    {
        private readonly InMemoryGremlinLanguageConnector _connector;
        private readonly Dictionary<string, string> _userIds = new();
        private readonly Dictionary<string, string> _productIds = new();
        private readonly Dictionary<string, string> _companyIds = new();
        private readonly Dictionary<string, string> _orderIds = new();
        private readonly Dictionary<string, string> _reviewIds = new();

        public SocialCommerceScenario(InMemoryGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        /// <summary>
        /// Load the complete social commerce scenario
        /// </summary>
        public async Task LoadScenarioAsync()
        {
            Console.WriteLine("???  Loading Social Commerce Scenario...");
            
            await CreateUsersAsync();
            await CreateCompaniesAsync();
            await CreateProductsAsync();
            await CreateFriendshipsAsync();
            await CreateOrdersAsync();
            await CreateReviewsAsync();
            await CreateEmploymentRelationshipsAsync();
            
            Console.WriteLine("? Scenario loaded successfully!");
            await PrintScenarioSummaryAsync();
        }

        private async Task CreateUsersAsync()
        {
            Console.WriteLine("?? Creating users...");
            
            var users = new[]
            {
                new { Name = "Alice Johnson", Age = 28, City = "Seattle", Email = "alice@example.com", Interests = new[] { "Technology", "Reading", "Travel" } },
                new { Name = "Bob Smith", Age = 34, City = "San Francisco", Email = "bob@example.com", Interests = new[] { "Sports", "Gaming", "Music" } },
                new { Name = "Carol Davis", Age = 25, City = "New York", Email = "carol@example.com", Interests = new[] { "Fashion", "Art", "Photography" } },
                new { Name = "David Wilson", Age = 42, City = "Austin", Email = "david@example.com", Interests = new[] { "Business", "Technology", "Cooking" } },
                new { Name = "Eva Brown", Age = 31, City = "Boston", Email = "eva@example.com", Interests = new[] { "Health", "Fitness", "Travel" } },
                new { Name = "Frank Miller", Age = 29, City = "Chicago", Email = "frank@example.com", Interests = new[] { "Music", "Art", "Technology" } },
                new { Name = "Grace Lee", Age = 26, City = "Los Angeles", Email = "grace@example.com", Interests = new[] { "Entertainment", "Fashion", "Travel" } },
                new { Name = "Henry Taylor", Age = 38, City = "Denver", Email = "henry@example.com", Interests = new[] { "Outdoor", "Sports", "Photography" } }
            };

            foreach (var user in users)
            {
                var query = $"g.addV('user').property('name', '{user.Name}').property('age', {user.Age}).property('city', '{user.City}').property('email', '{user.Email}').property('interests', '{string.Join(",", user.Interests)}')";
                var result = await _connector.ExecuteAsync(query, null);
                var vertex = result.FirstOrDefault();
                if (vertex != null)
                {
                    _userIds[user.Name] = vertex.id.ToString();
                }
            }
        }

        private async Task CreateCompaniesAsync()
        {
            Console.WriteLine("?? Creating companies...");
            
            var companies = new[]
            {
                new { Name = "TechCorp", Industry = "Technology", Founded = 2010, City = "Seattle", Employees = 5000 },
                new { Name = "GameStudio", Industry = "Gaming", Founded = 2015, City = "San Francisco", Employees = 200 },
                new { Name = "FashionHub", Industry = "Fashion", Founded = 2018, City = "New York", Employees = 150 },
                new { Name = "FoodieWorld", Industry = "Food", Founded = 2020, City = "Austin", Employees = 75 },
                new { Name = "HealthTech", Industry = "Healthcare", Founded = 2012, City = "Boston", Employees = 800 }
            };

            foreach (var company in companies)
            {
                var query = $"g.addV('company').property('name', '{company.Name}').property('industry', '{company.Industry}').property('founded', {company.Founded}).property('city', '{company.City}').property('employees', {company.Employees})";
                var result = await _connector.ExecuteAsync(query, null);
                var vertex = result.FirstOrDefault();
                if (vertex != null)
                {
                    _companyIds[company.Name] = vertex.id.ToString();
                }
            }
        }

        private async Task CreateProductsAsync()
        {
            Console.WriteLine("?? Creating products...");
            
            var products = new[]
            {
                new { Name = "Laptop Pro", Category = "Electronics", Price = 1299.99, Brand = "TechCorp", Rating = 4.5 },
                new { Name = "Gaming Mouse", Category = "Electronics", Price = 79.99, Brand = "GameStudio", Rating = 4.7 },
                new { Name = "Designer Jacket", Category = "Fashion", Price = 299.99, Brand = "FashionHub", Rating = 4.3 },
                new { Name = "Organic Coffee", Category = "Food", Price = 24.99, Brand = "FoodieWorld", Rating = 4.8 },
                new { Name = "Fitness Tracker", Category = "Health", Price = 199.99, Brand = "HealthTech", Rating = 4.4 },
                new { Name = "Wireless Headphones", Category = "Electronics", Price = 149.99, Brand = "TechCorp", Rating = 4.6 },
                new { Name = "Running Shoes", Category = "Fashion", Price = 129.99, Brand = "FashionHub", Rating = 4.2 },
                new { Name = "Protein Powder", Category = "Health", Price = 49.99, Brand = "HealthTech", Rating = 4.5 },
                new { Name = "Gaming Keyboard", Category = "Electronics", Price = 129.99, Brand = "GameStudio", Rating = 4.8 },
                new { Name = "Artisan Tea", Category = "Food", Price = 19.99, Brand = "FoodieWorld", Rating = 4.7 }
            };

            foreach (var product in products)
            {
                var query = $"g.addV('product').property('name', '{product.Name}').property('category', '{product.Category}').property('price', {product.Price}).property('brand', '{product.Brand}').property('rating', {product.Rating})";
                var result = await _connector.ExecuteAsync(query, null);
                var vertex = result.FirstOrDefault();
                if (vertex != null)
                {
                    _productIds[product.Name] = vertex.id.ToString();
                }
            }
        }

        private async Task CreateFriendshipsAsync()
        {
            Console.WriteLine("?? Creating friendships...");
            
            var friendships = new[]
            {
                ("Alice Johnson", "Bob Smith", "2020-01-15"),
                ("Alice Johnson", "Carol Davis", "2019-06-20"),
                ("Bob Smith", "David Wilson", "2021-03-10"),
                ("Carol Davis", "Eva Brown", "2020-08-05"),
                ("David Wilson", "Frank Miller", "2019-11-12"),
                ("Eva Brown", "Grace Lee", "2021-01-25"),
                ("Frank Miller", "Henry Taylor", "2020-05-18"),
                ("Alice Johnson", "Eva Brown", "2021-07-30"),
                ("Bob Smith", "Frank Miller", "2020-12-03"),
                ("Grace Lee", "Henry Taylor", "2021-09-14")
            };

            foreach (var (user1, user2, since) in friendships)
            {
                if (_userIds.TryGetValue(user1, out var user1Id) && _userIds.TryGetValue(user2, out var user2Id))
                {
                    var query = $"g.V('{user1Id}').addE('friends').to(g.V('{user2Id}')).property('since', '{since}').property('strength', {new Random().NextDouble() * 0.5 + 0.5:F2})";
                    await _connector.ExecuteAsync(query, null);
                }
            }
        }

        private async Task CreateOrdersAsync()
        {
            Console.WriteLine("?? Creating orders...");
            
            var orders = new[]
            {
                ("Alice Johnson", "Laptop Pro", "2023-01-15", 1, 1299.99),
                ("Alice Johnson", "Wireless Headphones", "2023-01-15", 1, 149.99),
                ("Bob Smith", "Gaming Mouse", "2023-02-20", 1, 79.99),
                ("Bob Smith", "Gaming Keyboard", "2023-02-20", 1, 129.99),
                ("Carol Davis", "Designer Jacket", "2023-03-10", 1, 299.99),
                ("David Wilson", "Organic Coffee", "2023-01-25", 3, 74.97),
                ("Eva Brown", "Fitness Tracker", "2023-02-14", 1, 199.99),
                ("Eva Brown", "Running Shoes", "2023-02-14", 1, 129.99),
                ("Frank Miller", "Wireless Headphones", "2023-03-05", 1, 149.99),
                ("Grace Lee", "Designer Jacket", "2023-03-18", 1, 299.99),
                ("Henry Taylor", "Protein Powder", "2023-01-30", 2, 99.98)
            };

            foreach (var (userName, productName, date, quantity, total) in orders)
            {
                if (_userIds.TryGetValue(userName, out var userId) && _productIds.TryGetValue(productName, out var productId))
                {
                    var orderQuery = $"g.addV('order').property('date', '{date}').property('total', {total}).property('status', 'completed')";
                    var orderResult = await _connector.ExecuteAsync(orderQuery, null);
                    var orderVertex = orderResult.FirstOrDefault();
                    
                    if (orderVertex != null)
                    {
                        var orderId = orderVertex.id.ToString();
                        _orderIds[$"{userName}-{productName}-{date}"] = orderId;
                        
                        // User placed order
                        await _connector.ExecuteAsync($"g.V('{userId}').addE('placed').to(g.V('{orderId}')).property('timestamp', '{date}')", null);
                        
                        // Order contains product
                        await _connector.ExecuteAsync($"g.V('{orderId}').addE('contains').to(g.V('{productId}')).property('quantity', {quantity})", null);
                    }
                }
            }
        }

        private async Task CreateReviewsAsync()
        {
            Console.WriteLine("? Creating reviews...");
            
            var reviews = new[]
            {
                ("Alice Johnson", "Laptop Pro", 5, "Excellent performance and build quality!", "2023-01-20"),
                ("Bob Smith", "Gaming Mouse", 5, "Perfect for gaming, very responsive.", "2023-02-25"),
                ("Carol Davis", "Designer Jacket", 4, "Beautiful design, good quality fabric.", "2023-03-15"),
                ("David Wilson", "Organic Coffee", 5, "Best coffee I've ever tasted!", "2023-02-01"),
                ("Eva Brown", "Fitness Tracker", 4, "Great features, battery could be better.", "2023-02-20"),
                ("Frank Miller", "Wireless Headphones", 5, "Amazing sound quality and comfort.", "2023-03-10"),
                ("Grace Lee", "Designer Jacket", 4, "Love the style, fits perfectly.", "2023-03-25"),
                ("Henry Taylor", "Protein Powder", 5, "Great taste and results!", "2023-02-05")
            };

            foreach (var (userName, productName, rating, comment, date) in reviews)
            {
                if (_userIds.TryGetValue(userName, out var userId) && _productIds.TryGetValue(productName, out var productId))
                {
                    var reviewQuery = $"g.addV('review').property('rating', {rating}).property('comment', '{comment}').property('date', '{date}')";
                    var reviewResult = await _connector.ExecuteAsync(reviewQuery, null);
                    var reviewVertex = reviewResult.FirstOrDefault();
                    
                    if (reviewVertex != null)
                    {
                        var reviewId = reviewVertex.id.ToString();
                        _reviewIds[$"{userName}-{productName}"] = reviewId;
                        
                        // User wrote review
                        await _connector.ExecuteAsync($"g.V('{userId}').addE('wrote').to(g.V('{reviewId}')).property('timestamp', '{date}')", null);
                        
                        // Review is about product
                        await _connector.ExecuteAsync($"g.V('{reviewId}').addE('about').to(g.V('{productId}'))", null);
                    }
                }
            }
        }

        private async Task CreateEmploymentRelationshipsAsync()
        {
            Console.WriteLine("?? Creating employment relationships...");
            
            var employments = new[]
            {
                ("Alice Johnson", "TechCorp", "Software Engineer", "2021-01-15"),
                ("Bob Smith", "GameStudio", "Game Developer", "2020-06-01"),
                ("Carol Davis", "FashionHub", "Designer", "2022-03-01"),
                ("David Wilson", "FoodieWorld", "Product Manager", "2020-08-15"),
                ("Eva Brown", "HealthTech", "Data Scientist", "2019-11-01"),
                ("Frank Miller", "TechCorp", "UX Designer", "2021-09-01")
            };

            foreach (var (userName, companyName, position, startDate) in employments)
            {
                if (_userIds.TryGetValue(userName, out var userId) && _companyIds.TryGetValue(companyName, out var companyId))
                {
                    await _connector.ExecuteAsync($"g.V('{userId}').addE('works_for').to(g.V('{companyId}')).property('position', '{position}').property('start_date', '{startDate}')", null);
                }
            }
        }

        private async Task PrintScenarioSummaryAsync()
        {
            var stats = await GetScenarioStatsAsync();
            
            Console.WriteLine("\n?? Scenario Summary:");
            Console.WriteLine($"   ?? Users: {stats["users"]}");
            Console.WriteLine($"   ?? Companies: {stats["companies"]}");
            Console.WriteLine($"   ?? Products: {stats["products"]}");
            Console.WriteLine($"   ?? Orders: {stats["orders"]}");
            Console.WriteLine($"   ? Reviews: {stats["reviews"]}");
            Console.WriteLine($"   ?? Friendships: {stats["friendships"]}");
            Console.WriteLine($"   ?? Employment relationships: {stats["employments"]}");
            Console.WriteLine($"   ?? Total vertices: {stats["totalVertices"]}");
            Console.WriteLine($"   ?? Total edges: {stats["totalEdges"]}");
        }

        private async Task<Dictionary<string, int>> GetScenarioStatsAsync()
        {
            var stats = new Dictionary<string, int>();
            
            var userCount = await _connector.ExecuteAsync("g.V().hasLabel('user').count()", null);
            stats["users"] = Convert.ToInt32(userCount.FirstOrDefault() ?? 0);
            
            var companyCount = await _connector.ExecuteAsync("g.V().hasLabel('company').count()", null);
            stats["companies"] = Convert.ToInt32(companyCount.FirstOrDefault() ?? 0);
            
            var productCount = await _connector.ExecuteAsync("g.V().hasLabel('product').count()", null);
            stats["products"] = Convert.ToInt32(productCount.FirstOrDefault() ?? 0);
            
            var orderCount = await _connector.ExecuteAsync("g.V().hasLabel('order').count()", null);
            stats["orders"] = Convert.ToInt32(orderCount.FirstOrDefault() ?? 0);
            
            var reviewCount = await _connector.ExecuteAsync("g.V().hasLabel('review').count()", null);
            stats["reviews"] = Convert.ToInt32(reviewCount.FirstOrDefault() ?? 0);
            
            var friendshipCount = await _connector.ExecuteAsync("g.E().hasLabel('friends').count()", null);
            stats["friendships"] = Convert.ToInt32(friendshipCount.FirstOrDefault() ?? 0);
            
            var employmentCount = await _connector.ExecuteAsync("g.E().hasLabel('works_for').count()", null);
            stats["employments"] = Convert.ToInt32(employmentCount.FirstOrDefault() ?? 0);
            
            var totalVertices = await _connector.ExecuteAsync("g.V().count()", null);
            stats["totalVertices"] = Convert.ToInt32(totalVertices.FirstOrDefault() ?? 0);
            
            var totalEdges = await _connector.ExecuteAsync("g.E().count()", null);
            stats["totalEdges"] = Convert.ToInt32(totalEdges.FirstOrDefault() ?? 0);
            
            return stats;
        }

        /// <summary>
        /// Get predefined example queries for exploration
        /// </summary>
        public Dictionary<string, string> GetExampleQueries()
        {
            return new Dictionary<string, string>
            {
                ["1. List all users (with properties)"] = "g.V().hasLabel('user')",
                ["2. Find Alice's friends (with details)"] = "g.V().has('user', 'name', 'Alice Johnson').out('friends')",
                ["3. Users who bought electronics"] = "g.V().hasLabel('user').where(out('placed').out('contains').has('product', 'category', 'Electronics')).values('name').dedup()",
                ["4. Average product ratings"] = "g.V().hasLabel('product').as('p').in('about').values('rating').mean().as('avg').select('p').by('name').as('product').select('avg', 'product')",
                ["5. All products (with properties)"] = "g.V().hasLabel('product')",
                ["6. Popular products (>1 review)"] = "g.V().hasLabel('product').where(in('about').count().is(gt(1))).values('name')",
                ["7. Users in same city as TechCorp"] = "g.V().has('company', 'name', 'TechCorp').values('city').as('city').V().hasLabel('user').where(values('city').where(eq('city'))).values('name')",
                ["8. Most expensive order"] = "g.V().hasLabel('order').order().by('total', desc).limit(1).values('total')",
                ["9. Products bought by Alice's friends"] = "g.V().has('user', 'name', 'Alice Johnson').out('friends').out('placed').out('contains').values('name').dedup()",
                ["10. Companies (with details)"] = "g.V().hasLabel('company')",
                ["11. User purchase patterns"] = "g.V().hasLabel('user').project('user', 'totalSpent', 'orderCount').by('name').by(out('placed').values('total').sum()).by(out('placed').count())",
                ["12. Product categories by popularity"] = "g.V().hasLabel('product').group().by('category').by(in('about').count()).unfold().order().by(values, desc)"
            };
        }
    }
}