using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Xunit;
using static Xunit.Assert; // Importing static members of Assert class

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests for complex parameterized queries like g.V().has(__p0,within(__p1,__p2,__p3,__p4)).has(__p5,__p6).has(__p7,__p8).outE(__p9).range(__p10,__p11)
    /// Ensures the InMemory connector can handle complex TinkerPop-style parameterized queries correctly
    /// </summary>
    public class ComplexParameterizedQueryTests
    {
        [Fact]
        public async Task ExecuteAsync_WithinPredicateWithMultipleParameters_ShouldFilterCorrectly()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test within() predicate with multiple parameter values
            var query = "g.V().has(__p0, within(__p1, __p2, __p3, __p4))";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" },
                { "__p1", "Electronics" },
                { "__p2", "Books" },
                { "__p3", "Clothing" },
                { "__p4", "Sports" }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert - Check actual test data setup: Electronics (2), Books (1), Clothing (1), Sports (1) = 5 total
            result.Should().HaveCount(5, "should find 5 vertices with categories in the within list");
            
            var categories = result.Select(v => ExtractPropertyValue(v, "category")).Where(c => c != null).ToList();
            categories.Should().Contain("Electronics");
            categories.Should().Contain("Books");  
            categories.Should().Contain("Clothing");
            categories.Should().Contain("Sports");
        }

        [Fact]
        public async Task ExecuteAsync_ChainedHasFiltersWithParameters_ShouldApplyAllFilters()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test chained has() filters with parameters
            var query = "g.V().has(__p0, __p1).has(__p2, __p3).has(__p4, __p5)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "label" },
                { "__p1", "product" },
                { "__p2", "category" },
                { "__p3", "Electronics" },
                { "__p4", "price" },
                { "__p5", 999.99 }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(1, "should find exactly 1 product matching all criteria");
            var product = result.First();
            
            // Extract properties correctly
            string category = ExtractPropertyValue(product, "category");
            string priceStr = ExtractPropertyValue(product, "price");
            
            category.Should().Be("Electronics");
            if (double.TryParse(priceStr, out double priceValue))
            {
                priceValue.Should().Be(999.99);
            }
            else
            {
                priceStr.Should().Be("999.99");
            }
        }

        [Fact]
        public async Task ExecuteAsync_ComplexTraversalWithOutEAndRange_ShouldWorkCorrectly()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestDataWithEdges(connector);

            // Act - Test the exact pattern from the user's request
            var query = "g.V().has(__p0, within(__p1, __p2, __p3, __p4)).has(__p5, __p6).has(__p7, __p8).outE(__p9).range(__p10, __p11)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" },
                { "__p1", "Electronics" },
                { "__p2", "Books" },
                { "__p3", "Clothing" },
                { "__p4", "Sports" },
                { "__p5", "status" },
                { "__p6", "active" },
                { "__p7", "featured" },
                { "__p8", true },
                { "__p9", "relatedTo" },
                { "__p10", 0 },
                { "__p11", 2 }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(2, "should return the first 2 edges from the range");
            
            // Verify all results are edges
            foreach (var edge in result)
            {
                string edgeType = ExtractPropertyValue(edge, "type") ?? ExtractType(edge);
                string edgeLabel = ExtractPropertyValue(edge, "label") ?? ExtractLabel(edge);
                
                edgeType.Should().Be("edge");
                edgeLabel.Should().Be("relatedTo");
            }
        }

        // Helper methods to extract type and label
        private static string ExtractType(dynamic item)
        {
            try
            {
                if (item is IDictionary<string, object> dict && dict.TryGetValue("type", out var type))
                {
                    return type?.ToString();
                }
                
                try
                {
                    return item.type?.ToString();
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractLabel(dynamic item)
        {
            try
            {
                if (item is IDictionary<string, object> dict && dict.TryGetValue("label", out var label))
                {
                    return label?.ToString();
                }
                
                try
                {
                    return item.label?.ToString();
                }
                catch
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        [Fact]
        public async Task ExecuteAsync_WithinPredicateEmptyValues_ShouldReturnEmptyResult()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test within() with no matching values
            var query = "g.V().has(__p0, within(__p1, __p2))";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" },
                { "__p1", "NonExistentCategory1" },
                { "__p2", "NonExistentCategory2" }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().BeEmpty("should return no results when within values don't match");
        }

        [Fact]
        public async Task ExecuteAsync_WithinPredicateSingleValue_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test within() with single value (edge case)
            var query = "g.V().has(__p0, within(__p1))";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" },
                { "__p1", "Electronics" }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(2, "should find 2 electronics products (Laptop and Old Phone)");
            
            // Validate categories without using dynamic variable type inference
            var electronicsCount = 0;
            foreach (var item in result)
            {
                var categoryValue = ExtractPropertyValue(item, "category");
                if (categoryValue == "Electronics")
                {
                    electronicsCount++;
                }
            }
            
            electronicsCount.Should().Be(2, "all items should have Electronics category");
        }

        // Helper method to extract property values correctly
        private static string ExtractPropertyValue(dynamic item, string propertyName)
        {
            try
            {
                // Handle GremlinResponseObject format
                if (item is IDictionary<string, object> dict && dict.TryGetValue("properties", out var propsObj))
                {
                    if (propsObj is IDictionary<string, object> properties && properties.TryGetValue(propertyName, out var propValue))
                    {
                        // Handle CosmosDB format: [{"value": actualValue}]
                        if (propValue is IEnumerable<dynamic> propList)
                        {
                            var first = propList.FirstOrDefault();
                            if (first is IDictionary<string, object> firstDict && firstDict.TryGetValue("value", out var val))
                            {
                                return val?.ToString();
                            }
                        }
                        return propValue?.ToString();
                    }
                }
                
                // Try dynamic access
                try
                {
                    var dynamicProps = item.properties;
                    if (dynamicProps != null)
                    {
                        var prop = dynamicProps[propertyName];
                        if (prop != null)
                        {
                            return prop.ToString();
                        }
                    }
                }
                catch
                {
                    // Ignore dynamic access errors
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        [Fact]
        public async Task ExecuteAsync_ParameterizedQueryWithNumericValues_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test parameters with different numeric types
            var query = "g.V().has(__p0, __p1).has(__p2, __p3).has(__p4, __p5)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "price" },
                { "__p1", 29.99 }, // double
                { "__p2", "stock" },
                { "__p3", 50 }, // int
                { "__p4", "rating" },
                { "__p5", 4.5f } // float
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(1, "should find product matching all numeric criteria");
        }

        [Fact]
        public async Task ExecuteAsync_ParameterizedQueryWithBooleanValues_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test boolean parameters
            var query = "g.V().has(__p0, __p1).has(__p2, __p3)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "featured" },
                { "__p1", true },
                { "__p2", "discontinued" },
                { "__p3", false }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(2, "should find 2 featured, non-discontinued products");
        }

        [Fact]
        public async Task ExecuteAsync_ComplexQueryWithAggregation_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test complex query ending with aggregation
            var query = "g.V().has(__p0, within(__p1, __p2)).has(__p3, __p4).values(__p5).sum()";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" },
                { "__p1", "Electronics" },
                { "__p2", "Books" },
                { "__p3", "featured" },
                { "__p4", true },
                { "__p5", "price" }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(1, "should return single aggregated value");
            ((double)result.First()).Should().BeGreaterThan(0, "sum should be positive");
        }

        [Fact]
        public async Task ExecuteAsync_RangeWithZeroStart_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestDataWithEdges(connector);

            // Act - Test range starting from 0
            var query = "g.V().has(__p0, __p1).outE().range(__p2, __p3)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" },
                { "__p1", "Electronics" },
                { "__p2", 0 },
                { "__p3", 1 }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(1, "should return exactly 1 edge from range [0,1)");
        }

        [Fact]
        public async Task ExecuteAsync_RangeWithNonZeroStart_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestDataWithEdges(connector);

            // Act - Test range with non-zero start
            var query = "g.V().has(__p0, __p1).outE().range(__p2, __p3)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" },
                { "__p1", "Electronics" },
                { "__p2", 1 },
                { "__p3", 3 }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCountLessOrEqualTo(2, "should return at most 2 edges from range [1,3)");
        }

        [Fact]
        public async Task ExecuteAsync_ParameterSubstitutionWithSpecialCharacters_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Create test data with special characters
            await connector.ExecuteAsync("g.addV('product').property('name', 'Test Product').property('sku', 'ABC-123_DEF.456')", new Dictionary<string, object>());

            // Act - Test parameter with special characters
            var query = "g.V().has(__p0, __p1)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "sku" },
                { "__p1", "ABC-123_DEF.456" }
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(1, "should find product with special character SKU");
            
            var skuValue = ExtractPropertyValue(result.First(), "sku");
            var expectedSku = "ABC-123_DEF.456";
            
            // Direct comparison to avoid dynamic type issues
            Assert.Equal(expectedSku, skuValue);
        }

        [Fact]
        public async Task ExecuteAsync_MixedParameterTypes_ShouldWork()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupTestData(connector);

            // Act - Test query with mixed parameter types (string, int, double, bool)
            var query = "g.V().has(__p0, __p1).has(__p2, __p3).has(__p4, __p5).has(__p6, __p7)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "category" }, // string key
                { "__p1", "Electronics" }, // string value
                { "__p2", "stock" }, // string key
                { "__p3", 100 }, // int value
                { "__p4", "price" }, // string key
                { "__p5", 999.99 }, // double value
                { "__p6", "featured" }, // string key
                { "__p7", true } // bool value
            };

            var result = await connector.ExecuteAsync(query, parameters);

            // Assert
            result.Should().HaveCount(1, "should find exactly 1 product matching all mixed-type criteria");
        }

        [Fact]
        public async Task ExecuteAsync_ParameterizedQueryPerformance_ShouldBeEfficient()
        {
            // Arrange
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupLargeTestDataset(connector);

            // Act - Test performance with large dataset
            var startTime = DateTime.UtcNow;
            
            var query = "g.V().has(__p0, within(__p1, __p2, __p3)).has(__p4, __p5).outE(__p6).range(__p7, __p8)";
            var parameters = new Dictionary<string, object>
            {
                { "__p0", "type" },
                { "__p1", "user" },
                { "__p2", "product" },
                { "__p3", "order" },
                { "__p4", "active" },
                { "__p5", true },
                { "__p6", "related" },
                { "__p7", 0 },
                { "__p8", 10 }
            };

            var result = await connector.ExecuteAsync(query, parameters);
            var endTime = DateTime.UtcNow;
            var executionTime = endTime - startTime;

            // Assert
            result.Should().NotBeNull("should return valid results");
            executionTime.TotalMilliseconds.Should().BeLessThan(1000, "query should execute in reasonable time");
        }

        private async Task SetupTestData(InMemoryGremlinLanguageConnector connector)
        {
            // Create products with various categories and properties
            await connector.ExecuteAsync("g.addV('product').property('id', 'p1').property('name', 'Laptop').property('category', 'Electronics').property('price', 999.99).property('stock', 100).property('featured', true).property('discontinued', false)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('product').property('id', 'p2').property('name', 'Book').property('category', 'Books').property('price', 29.99).property('stock', 50).property('rating', 4.5).property('featured', true).property('discontinued', false)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('product').property('id', 'p3').property('name', 'T-Shirt').property('category', 'Clothing').property('price', 19.99).property('stock', 200).property('featured', false).property('discontinued', false)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('product').property('id', 'p4').property('name', 'Soccer Ball').property('category', 'Sports').property('price', 39.99).property('stock', 75).property('featured', false).property('discontinued', false)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('product').property('id', 'p5').property('name', 'Old Phone').property('category', 'Electronics').property('price', 199.99).property('stock', 0).property('featured', false).property('discontinued', true)", new Dictionary<string, object>());
        }

        private async Task SetupTestDataWithEdges(InMemoryGremlinLanguageConnector connector)
        {
            // First create the vertices
            await SetupTestData(connector);

            // Add status and featured properties for filtering
            await connector.ExecuteAsync("g.V('p1').property('status', 'active').property('featured', true)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p2').property('status', 'active').property('featured', true)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p3').property('status', 'inactive')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p4').property('status', 'active').property('featured', false)", new Dictionary<string, object>());

            // Create edges between products
            await connector.ExecuteAsync("g.V('p1').addE('relatedTo').to(g.V('p2'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p1').addE('relatedTo').to(g.V('p3'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p1').addE('relatedTo').to(g.V('p4'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p2').addE('relatedTo').to(g.V('p3'))", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p2').addE('relatedTo').to(g.V('p4'))", new Dictionary<string, object>());
        }

        private async Task SetupLargeTestDataset(InMemoryGremlinLanguageConnector connector)
        {
            // Create a larger dataset for performance testing
            var types = new[] { "user", "product", "order", "category" };
            
            for (int i = 0; i < 100; i++)
            {
                var type = types[i % types.Length];
                await connector.ExecuteAsync($"g.addV('{type}').property('id', 'item{i}').property('type', '{type}').property('active', {(i % 2 == 0).ToString().ToLower()})", new Dictionary<string, object>());
            }

            // Create edges
            for (int i = 0; i < 50; i++)
            {
                await connector.ExecuteAsync($"g.V('item{i}').addE('related').to(g.V('item{i + 50}'))", new Dictionary<string, object>());
            }
        }
    }
}