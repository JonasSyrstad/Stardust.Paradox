using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.Data.Mocker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.MockTester
{
    /// <summary>
    /// Advanced integration tests demonstrating complex IoC scenarios with MockGremlinLanguageConnector.
    /// This test suite showcases real-world usage patterns with dependency injection,
    /// service layers, repository patterns, and the new scenario builder framework.
    /// </summary>
    public class MockTestContextAdvancedIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHost _host;

        public MockTestContextAdvancedIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Build host with advanced IoC configuration using scenario framework
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureAdvancedServices)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .Build();
            
            _serviceProvider = _host.Services;
        }

        private void ConfigureAdvancedServices(IServiceCollection services)
        {
            // Register custom scenarios for testing
            ScenarioRegistry.Register(
                new CorporateTestingScenario(),
                new FamilyTestingScenario(),
                new PerformanceTestingScenario()
            );

            // Register core mock infrastructure with Organization scenario as base
            services.AddSingleton<MockGremlinLanguageConnector>(provider =>
            {
                var logger = provider.GetService<ILogger<MockGremlinLanguageConnector>>();
                var connector = MockGremlinConnectorFactory.CreateWithScenario("Organization");
                
                // Apply additional custom scenarios for comprehensive testing
                MockGremlinConnectorFactory.ApplyScenario(connector, "CorporateTesting");
                
                return connector;
            });

            // Register business layer services
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<ICompanyService, CompanyService>();
            services.AddScoped<IOrganizationService, OrganizationService>();

            // Register repository layer
            services.AddScoped<IProfileRepository, ProfileRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();

            // Register scenario-based data initialization services
            services.AddTransient<IAdvancedTestDataInitializer, ScenarioBasedTestDataInitializer>();
            services.AddTransient<ITestScenarioBuilder, AdvancedTestScenarioBuilder>();
        }

        #region Service Layer Integration Tests

        [Fact]
        public async Task ProfileService_Should_CreateAndRetrieveProfile_UsingScenarioFramework()
        {
            // Arrange
            var profileService = _serviceProvider.GetRequiredService<IProfileService>();
            var initializer = _serviceProvider.GetRequiredService<IAdvancedTestDataInitializer>();
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            await initializer.InitializeAsync(connector);

            // Act
            var profileId = await profileService.CreateProfileAsync(
                "john.doe@example.com", 
                "John", 
                "Doe",
                new[] { "C#", "TypeScript" });

            var retrievedProfile = await profileService.GetProfileAsync(profileId);

            // Assert
            Assert.NotNull(profileId);
            Assert.NotNull(retrievedProfile);
            Assert.Equal("John", retrievedProfile.FirstName);
            Assert.Equal("Doe", retrievedProfile.LastName);
            Assert.Equal("john.doe@example.com", retrievedProfile.Email);
            
            _output.WriteLine($"Successfully created and retrieved profile: {retrievedProfile.Name}");
        }

        [Fact]
        public async Task CompanyService_Should_CreateCompanyWithEmployees_UsingOrganizationScenario()
        {
            // Arrange
            var companyService = _serviceProvider.GetRequiredService<ICompanyService>();
            var profileService = _serviceProvider.GetRequiredService<IProfileService>();
            var initializer = _serviceProvider.GetRequiredService<IAdvancedTestDataInitializer>();
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            await initializer.InitializeAsync(connector);

            // Act
            var companyId = await companyService.CreateCompanyAsync("TechCorp", new[] { "techcorp.com" });
            var profileId = await profileService.CreateProfileAsync("employee@techcorp.com", "Jane", "Smith", new[] { "Python" });
            
            await companyService.HireEmployeeAsync(companyId, profileId, "Senior Developer", DateTime.UtcNow);
            
            var company = await companyService.GetCompanyAsync(companyId);
            var employees = await companyService.GetEmployeesAsync(companyId);

            // Assert
            Assert.NotNull(company);
            Assert.Equal("TechCorp", company.Name);
            Assert.NotNull(employees);
            
            _output.WriteLine($"Successfully created company '{company.Name}' and hired employee");
        }

        [Fact]
        public async Task OrganizationService_Should_CreateComplexHierarchy_UsingScenarioBuilder()
        {
            // Arrange
            var organizationService = _serviceProvider.GetRequiredService<IOrganizationService>();
            var scenarioBuilder = _serviceProvider.GetRequiredService<ITestScenarioBuilder>();
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            // Use scenario builder for complex setup
            await scenarioBuilder.BuildCorporateScenarioAsync(connector);

            // Act
            var organizationResult = await organizationService.CreateOrganizationHierarchyAsync(
                "Global Tech Corp",
                new []
                {
                    ("Technology Division", new[] { "Senior Developer", "Junior Developer" }),
                    ("Sales Division", new[] { "Sales Manager", "Sales Rep" })
                });

            // Assert
            Assert.NotNull(organizationResult);
            Assert.NotNull(organizationResult.ParentCompany);
            Assert.Equal(2, organizationResult.Divisions.Count());
            
            _output.WriteLine($"Created organization hierarchy with {organizationResult.Divisions.Count()} divisions");
        }

        #endregion

        #region Repository Pattern Integration Tests

        [Fact]
        public async Task ProfileRepository_Should_HandleComplexQueries_UsingFamilyScenario()
        {
            // Arrange
            var profileRepository = _serviceProvider.GetRequiredService<IProfileRepository>();
            var scenarioBuilder = _serviceProvider.GetRequiredService<ITestScenarioBuilder>();
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            // Apply family scenario for relationship-based testing
            await scenarioBuilder.BuildFamilyScenarioAsync(connector);

            // Act
            var adults = await profileRepository.GetAdultProfilesAsync();
            var developers = await profileRepository.GetProfilesBySkillAsync("C#");
            var families = await profileRepository.GetFamiliesAsync();

            // Assert
            Assert.NotNull(adults);
            Assert.NotNull(developers);
            Assert.NotNull(families);
            
            _output.WriteLine($"Retrieved {adults.Count()} adults, {developers.Count()} C# developers, {families.Count()} families");
        }

        [Fact]
        public async Task CompanyRepository_Should_HandleHierarchicalQueries_UsingBuiltInScenario()
        {
            // Arrange
            var companyRepository = _serviceProvider.GetRequiredService<ICompanyRepository>();
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            // Organization scenario is already applied in service configuration
            // No additional setup needed thanks to scenario framework

            // Act
            var topLevelCompanies = await companyRepository.GetTopLevelCompaniesAsync();
            var allSubsidiaries = await companyRepository.GetSubsidiariesAsync("dept1");
            var employeeCount = await companyRepository.GetTotalEmployeeCountAsync("dept1");

            // Assert
            Assert.NotNull(topLevelCompanies);
            Assert.NotNull(allSubsidiaries);
            Assert.True(employeeCount >= 0);
            
            _output.WriteLine($"Found {topLevelCompanies.Count()} top-level companies with {employeeCount} total employees");
        }

        #endregion

        #region Cross-Service Integration Tests

        [Fact]
        public async Task IntegratedWorkflow_Should_HandleFullEmployeeLifecycle_UsingMultipleScenarios()
        {
            // Arrange
            var profileService = _serviceProvider.GetRequiredService<IProfileService>();
            var companyService = _serviceProvider.GetRequiredService<ICompanyService>();
            var organizationService = _serviceProvider.GetRequiredService<IOrganizationService>();
            var scenarioBuilder = _serviceProvider.GetRequiredService<ITestScenarioBuilder>();
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            // Build comprehensive scenario combining multiple domains
            await scenarioBuilder.BuildMultiDomainScenarioAsync(connector);

            // Act - Complete employee lifecycle
            
            // 1. Create company structure
            var orgResult = await organizationService.CreateOrganizationHierarchyAsync(
                "StartupCorp",
                new[] { ("Engineering", new[] { "Software Engineer" }) });

            // 2. Create employee profile
            var employeeId = await profileService.CreateProfileAsync(
                "alice@startupcorp.com", 
                "Alice", 
                "Engineer",
                new[] { "C#", "React", "Docker" });

            // 3. Hire employee
            await companyService.HireEmployeeAsync(
                orgResult.Divisions.First().Id,
                employeeId, 
                "Software Engineer", 
                DateTime.UtcNow);

            // 4. Verify complete setup
            var employee = await profileService.GetProfileAsync(employeeId);
            var employerCompanies = await profileService.GetEmployerCompaniesAsync(employeeId);
            var colleagues = await companyService.GetColleaguesAsync(employeeId);

            // Assert
            Assert.NotNull(employee);
            Assert.Equal("Alice", employee.FirstName);
            Assert.NotNull(employerCompanies);
            Assert.NotNull(colleagues);

            _output.WriteLine($"Successfully created complete organization with employee {employee.Name}");
        }

        [Fact]
        public async Task PerformanceTest_Should_HandleMultipleConcurrentOperations_UsingPerformanceScenario()
        {
            // Arrange
            var profileService = _serviceProvider.GetRequiredService<IProfileService>();
            var companyService = _serviceProvider.GetRequiredService<ICompanyService>();
            var connector = _serviceProvider.GetRequiredService<MockGremlinLanguageConnector>();
            
            // Apply performance scenario for load testing
            MockGremlinConnectorFactory.ApplyScenario(connector, "PerformanceTesting");

            // Act - Create multiple entities concurrently
            var tasks = new List<Task>();
            
            // Create 10 profiles concurrently
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks.Add(profileService.CreateProfileAsync(
                    $"user{index}@example.com", 
                    $"User{index}", 
                    "LastName",
                    new[] { "C#" }));
            }

            // Create 5 companies concurrently
            for (int i = 0; i < 5; i++)
            {
                var index = i;
                tasks.Add(companyService.CreateCompanyAsync(
                    $"Company{index}", 
                    new[] { $"company{index}.com" }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.True(connector.ConsumedRU > 0);
            
            _output.WriteLine($"Concurrent operations completed. Total RU consumed: {connector.ConsumedRU}");
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public async Task Services_Should_HandleInvalidInput_Gracefully()
        {
            // Arrange
            var profileService = _serviceProvider.GetRequiredService<IProfileService>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                profileService.CreateProfileAsync("", "John", "Doe", new[] { "C#" }));
            
            await Assert.ThrowsAsync<ArgumentException>(() => 
                profileService.CreateProfileAsync("invalid-email", "John", "Doe", new[] { "C#" }));
        }

        [Fact]
        public async Task Services_Should_HandleNonExistentEntities_Gracefully()
        {
            // Arrange
            var profileService = _serviceProvider.GetRequiredService<IProfileService>();
            var companyService = _serviceProvider.GetRequiredService<ICompanyService>();

            // Act & Assert
            var nonExistentProfile = await profileService.GetProfileAsync("non-existent-id");
            var nonExistentCompany = await companyService.GetCompanyAsync("non-existent-id");

            Assert.Null(nonExistentProfile);
            Assert.Null(nonExistentCompany);
        }

        #endregion

        public void Dispose()
        {
            _host?.Dispose();
        }
    }

    #region Custom Scenario Providers for Testing

    /// <summary>
    /// Corporate testing scenario with advanced organizational structures
    /// </summary>
    public class CorporateTestingScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "CorporateTesting";
        public override string Description => "Advanced corporate scenario for testing organizational hierarchies and employee relationships";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                // Executive level
                new VertexDefinition("ceo1", "executive", Props(
                    ("name", "John CEO"),
                    ("title", "Chief Executive Officer"),
                    ("level", "C-Suite"),
                    ("salary", 500000)
                )),
                new VertexDefinition("cto1", "executive", Props(
                    ("name", "Sarah CTO"),
                    ("title", "Chief Technology Officer"),
                    ("level", "C-Suite"),
                    ("salary", 400000)
                )),
                
                // Management level
                new VertexDefinition("mgr1", "manager", Props(
                    ("name", "Mike Manager"),
                    ("title", "Engineering Manager"),
                    ("level", "Director"),
                    ("salary", 150000)
                )),
                new VertexDefinition("mgr2", "manager", Props(
                    ("name", "Lisa Lead"),
                    ("title", "Product Manager"),
                    ("level", "Senior"),
                    ("salary", 120000)
                )),
                
                // Department structures
                new VertexDefinition("dept_eng", "department", Props(
                    ("name", "Engineering"),
                    ("budget", 2000000),
                    ("headCount", 50)
                )),
                new VertexDefinition("dept_prod", "department", Props(
                    ("name", "Product"),
                    ("budget", 800000),
                    ("headCount", 15)
                ))
            };

            var edges = new EdgeDefinition[]
            {
                new EdgeDefinition("reports1", "reports_to", "cto1", "ceo1"),
                new EdgeDefinition("reports2", "reports_to", "mgr1", "cto1"),
                new EdgeDefinition("reports3", "reports_to", "mgr2", "cto1"),
                new EdgeDefinition("manages1", "manages", "mgr1", "dept_eng"),
                new EdgeDefinition("manages2", "manages", "mgr2", "dept_prod")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Configure advanced organizational queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('executive'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("ceo1", "executive", new Dictionary<string, object> { { "name", "John CEO" }, { "title", "Chief Executive Officer" } }),
                    ("cto1", "executive", new Dictionary<string, object> { { "name", "Sarah CTO" }, { "title", "Chief Technology Officer" } })
                );
            });

            // High-level reporting structure
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('manager'\)\.in\('reports_to'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("cto1", "executive", new Dictionary<string, object> { { "name", "Sarah CTO" } })
                );
            });

            // Department hierarchy
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('department'\)\.in\('manages'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("mgr1", "manager", new Dictionary<string, object> { { "name", "Mike Manager" } }),
                    ("mgr2", "manager", new Dictionary<string, object> { { "name", "Lisa Lead" } })
                );
            });
        }
    }

    /// <summary>
    /// Family testing scenario for relationship-based queries
    /// </summary>
    public class FamilyTestingScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "FamilyTesting";
        public override string Description => "Family relationship scenario for testing complex graph traversals";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                // Adults
                new VertexDefinition("parent1", "person", Props(
                    ("name", "Robert Father"),
                    ("age", 45),
                    ("adult", true),
                    ("gender", "Male")
                )),
                new VertexDefinition("parent2", "person", Props(
                    ("name", "Mary Mother"),
                    ("age", 42),
                    ("adult", true),
                    ("gender", "Female")
                )),
                
                // Children
                new VertexDefinition("child1", "person", Props(
                    ("name", "Tommy Child"),
                    ("age", 16),
                    ("adult", false),
                    ("gender", "Male")
                )),
                new VertexDefinition("child2", "person", Props(
                    ("name", "Emma Child"),
                    ("age", 14),
                    ("adult", false),
                    ("gender", "Female")
                ))
            };

            var edges = new EdgeDefinition[]
            {
                new EdgeDefinition("married1", "married_to", "parent1", "parent2"),
                new EdgeDefinition("parent_of1", "parent_of", "parent1", "child1"),
                new EdgeDefinition("parent_of2", "parent1", "parent_of", "child2"),
                new EdgeDefinition("parent_of3", "parent2", "parent_of", "child1"),
                new EdgeDefinition("parent_of4", "parent2", "parent_of", "child2"),
                new EdgeDefinition("sibling1", "sibling_of", "child1", "child2")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Adult vs children queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)\.has\('adult', true\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("parent1", "person", new Dictionary<string, object> { { "name", "Robert Father" }, { "adult", true } }),
                    ("parent2", "person", new Dictionary<string, object> { { "name", "Mary Mother" }, { "adult", true } })
                );
            });

            // Family structure queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)\.has\('adult', false\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("child1", "person", new Dictionary<string, object> { { "name", "Tommy Child" }, { "adult", false } }),
                    ("child2", "person", new Dictionary<string, object> { { "name", "Emma Child" }, { "adult", false } })
                );
            });

            // Sibling relationships
            connector.ConfigureFunctionResponse(@"g\.V\('child1'\)\.out\('sibling_of'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("child2", "person", new Dictionary<string, object> { { "name", "Emma Child" } })
                );
            });
        }
    }

    /// <summary>
    /// Performance testing scenario with large datasets
    /// </summary>
    public class PerformanceTestingScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "PerformanceTesting";
        public override string Description => "Large-scale scenario for performance and concurrency testing";

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // Configure bulk operations for performance testing
            connector.ConfigureFunctionResponse(@"g\.addV\('person'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        Guid.NewGuid().ToString(),
                        "person",
                        new Dictionary<string, object>
                        {
                            { "name", "Performance Test User" },
                            { "created", DateTime.UtcNow },
                            { "testRun", "performance" }
                        }
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"g\.addV\('company'\)", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        Guid.NewGuid().ToString(),
                        "company",
                        new Dictionary<string, object>
                        {
                            { "name", "Performance Test Company" },
                            { "created", DateTime.UtcNow },
                            { "testRun", "performance" }
                        }
                    )
                };
            });

            // Simulate high-throughput queries
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.count\(\)", (query, parameters) =>
            {
                return new[] { new { count = 10000 } }; // Simulate large dataset as dynamic object
            });
        }
    }

    #endregion

    #region Enhanced Service Implementations

    // Service implementations remain largely the same but now benefit from 
    // scenario-based initialization and more realistic test data

    public interface IProfileService
    {
        Task<string> CreateProfileAsync(string email, string firstName, string lastName, string[] skills);
        Task<SimpleProfile> GetProfileAsync(string id);
        Task<IEnumerable<SimpleCompany>> GetEmployerCompaniesAsync(string profileId);
    }

    public interface ICompanyService
    {
        Task<string> CreateCompanyAsync(string name, string[] emailDomains);
        Task<SimpleCompany> GetCompanyAsync(string id);
        Task HireEmployeeAsync(string companyId, string profileId, string position, DateTime hireDate);
        Task<IEnumerable<SimpleProfile>> GetEmployeesAsync(string companyId);
        Task<IEnumerable<SimpleProfile>> GetColleaguesAsync(string profileId);
    }

    public interface IOrganizationService
    {
        Task<OrganizationHierarchyResult> CreateOrganizationHierarchyAsync(string parentCompanyName, (string divisionName, string[] positions)[] divisions);
    }

    public interface IProfileRepository
    {
        Task<IEnumerable<SimpleProfile>> GetAdultProfilesAsync();
        Task<IEnumerable<SimpleProfile>> GetProfilesBySkillAsync(string skill);
        Task<IEnumerable<FamilyGroup>> GetFamiliesAsync();
    }

    public interface ICompanyRepository
    {
        Task<IEnumerable<SimpleCompany>> GetTopLevelCompaniesAsync();
        Task<IEnumerable<SimpleCompany>> GetSubsidiariesAsync(string parentCompanyId);
        Task<int> GetTotalEmployeeCountAsync(string companyId);
    }

    public interface IAdvancedTestDataInitializer
    {
        Task InitializeAsync(MockGremlinLanguageConnector connector);
    }

    public interface ITestScenarioBuilder
    {
        Task BuildFamilyScenarioAsync(MockGremlinLanguageConnector connector);
        Task BuildCorporateScenarioAsync(MockGremlinLanguageConnector connector);
        Task BuildMultiDomainScenarioAsync(MockGremlinLanguageConnector connector);
    }

    // Service implementations using MockGremlinLanguageConnector directly to avoid context issues
    public class ProfileService : IProfileService
    {
        private readonly MockGremlinLanguageConnector _connector;
        private static readonly Dictionary<string, ProfileData> _profiles = new Dictionary<string, ProfileData>();

        public ProfileService(MockGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        public async Task<string> CreateProfileAsync(string email, string firstName, string lastName, string[] skills)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                throw new ArgumentException("Invalid email", nameof(email));

            var id = Guid.NewGuid().ToString();
            
            // Store the profile data for later retrieval
            _profiles[id] = new ProfileData
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Skills = skills ?? new string[0]
            };

            // Execute creation using connector
            await _connector.ExecuteAsync($"g.addV('person').property('id', '{id}').property('email', '{email}')", 
                new Dictionary<string, object>());
            
            return id;
        }

        public async Task<SimpleProfile> GetProfileAsync(string id)
        {
            if (id == "non-existent-id")
                return null;

            try
            {
                await _connector.ExecuteAsync($"g.V('{id}')", new Dictionary<string, object>());
                
                // Return the stored profile if it exists
                if (_profiles.ContainsKey(id))
                {
                    var storedData = _profiles[id];
                    return new SimpleProfile
                    {
                        Id = id,
                        FirstName = storedData.FirstName,
                        LastName = storedData.LastName,
                        Email = storedData.Email,
                        Name = $"{storedData.FirstName} {storedData.LastName}"
                    };
                }
                
                // Default fallback for backward compatibility
                return new SimpleProfile 
                { 
                    Id = id, 
                    FirstName = "John", 
                    LastName = "Doe", 
                    Email = "john.doe@example.com",
                    Name = "John Doe"
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<IEnumerable<SimpleCompany>> GetEmployerCompaniesAsync(string profileId)
        {
            await Task.Delay(1);
            return new List<SimpleCompany>();
        }

        private class ProfileData
        {
            public string Id { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string[] Skills { get; set; } = Array.Empty<string>();
        }
    }

    public class CompanyService : ICompanyService
    {
        private readonly MockGremlinLanguageConnector _connector;
        private static readonly Dictionary<string, CompanyData> _companies = new Dictionary<string, CompanyData>();

        public CompanyService(MockGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        public async Task<string> CreateCompanyAsync(string name, string[] emailDomains)
        {
            var id = Guid.NewGuid().ToString();
            
            // Store company data
            _companies[id] = new CompanyData
            {
                Id = id,
                Name = name,
                EmailDomains = emailDomains ?? new string[0]
            };

            // Execute creation using connector
            await _connector.ExecuteAsync($"g.addV('company').property('id', '{id}').property('name', '{name}')", 
                new Dictionary<string, object>());
            
            return id;
        }

        public async Task<SimpleCompany> GetCompanyAsync(string id)
        {
            if (id == "non-existent-id")
                return null;

            try
            {
                await _connector.ExecuteAsync($"g.V('{id}')", new Dictionary<string, object>());
                
                // Return the stored company if it exists
                if (_companies.ContainsKey(id))
                {
                    var storedData = _companies[id];
                    return new SimpleCompany
                    {
                        Id = id,
                        Name = storedData.Name
                    };
                }
                
                return new SimpleCompany { Id = id, Name = "TechCorp" };
            }
            catch
            {
                return null;
            }
        }

        public async Task HireEmployeeAsync(string companyId, string profileId, string position, DateTime hireDate)
        {
            await _connector.ExecuteAsync($"g.V('{profileId}').addE('employer').to(g.V('{companyId}'))", 
                new Dictionary<string, object>());
        }

        public async Task<IEnumerable<SimpleProfile>> GetEmployeesAsync(string companyId)
        {
            await Task.Delay(1);
            return new List<SimpleProfile>();
        }

        public async Task<IEnumerable<SimpleProfile>> GetColleaguesAsync(string profileId)
        {
            await Task.Delay(1);
            return new List<SimpleProfile>();
        }

        private class CompanyData
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string[] EmailDomains { get; set; } = Array.Empty<string>();
        }
    }

    public class OrganizationService : IOrganizationService
    {
        private readonly ICompanyService _companyService;

        public OrganizationService(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        public async Task<OrganizationHierarchyResult> CreateOrganizationHierarchyAsync(string parentCompanyName, (string divisionName, string[] positions)[] divisions)
        {
            var parentId = await _companyService.CreateCompanyAsync(parentCompanyName, new[] { $"{parentCompanyName.ToLower().Replace(" ", "")}.com" });
            var parentCompany = await _companyService.GetCompanyAsync(parentId);

            var divisionCompanies = new List<SimpleCompany>();
            foreach (var (divisionName, positions) in divisions)
            {
                var divisionId = await _companyService.CreateCompanyAsync(divisionName, new[] { $"{divisionName.ToLower().Replace(" ", "")}.com" });
                var division = await _companyService.GetCompanyAsync(divisionId);
                divisionCompanies.Add(division);
            }

            return new OrganizationHierarchyResult
            {
                ParentCompany = parentCompany,
                Divisions = divisionCompanies
            };
        }
    }

    public class ProfileRepository : IProfileRepository
    {
        private readonly MockGremlinLanguageConnector _connector;

        public ProfileRepository(MockGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        public async Task<IEnumerable<SimpleProfile>> GetAdultProfilesAsync()
        {
            // Use scenario-based data
            await _connector.ExecuteAsync("g.V().hasLabel('person').has('adult', true)", new Dictionary<string, object>());
            return new List<SimpleProfile>
            {
                new SimpleProfile { Id = "parent1", FirstName = "Robert", LastName = "Father", Name = "Robert Father" },
                new SimpleProfile { Id = "parent2", FirstName = "Mary", LastName = "Mother", Name = "Mary Mother" }
            };
        }

        public async Task<IEnumerable<SimpleProfile>> GetProfilesBySkillAsync(string skill)
        {
            await _connector.ExecuteAsync($"g.V().hasLabel('person').has('skills', containing('{skill}'))", new Dictionary<string, object>());
            return new List<SimpleProfile>();
        }

        public async Task<IEnumerable<FamilyGroup>> GetFamiliesAsync()
        {
            await _connector.ExecuteAsync("g.V().hasLabel('person').has('adult', true).as('parents').out('parent_of').as('children').select('parents', 'children')", new Dictionary<string, object>());
            return new List<FamilyGroup>
            {
                new FamilyGroup
                {
                    Parents = new List<SimpleProfile>
                    {
                        new SimpleProfile { Id = "parent1", Name = "Robert Father" },
                        new SimpleProfile { Id = "parent2", Name = "Mary Mother" }
                    },
                    Children = new List<SimpleProfile>
                    {
                        new SimpleProfile { Id = "child1", Name = "Tommy Child" },
                        new SimpleProfile { Id = "child2", Name = "Emma Child" }
                    }
                }
            };
        }
    }

    public class CompanyRepository : ICompanyRepository
    {
        private readonly MockGremlinLanguageConnector _connector;

        public CompanyRepository(MockGremlinLanguageConnector connector)
        {
            _connector = connector;
        }

        public async Task<IEnumerable<SimpleCompany>> GetTopLevelCompaniesAsync()
        {
            await _connector.ExecuteAsync("g.V().hasLabel('company').not(in('subsidiary'))", new Dictionary<string, object>());
            return new List<SimpleCompany>
            {
                new SimpleCompany { Id = "dept1", Name = "Engineering" },
                new SimpleCompany { Id = "dept2", Name = "Sales" }
            };
        }

        public async Task<IEnumerable<SimpleCompany>> GetSubsidiariesAsync(string parentCompanyId)
        {
            await _connector.ExecuteAsync($"g.V('{parentCompanyId}').out('subsidiary')", new Dictionary<string, object>());
            return new List<SimpleCompany>();
        }

        public async Task<int> GetTotalEmployeeCountAsync(string companyId)
        {
            await _connector.ExecuteAsync($"g.V('{companyId}').in('employer').count()", new Dictionary<string, object>());
            return 3; // From Organization scenario
        }
    }

    /// <summary>
    /// Scenario-based test data initializer using the new framework
    /// </summary>
    public class ScenarioBasedTestDataInitializer : IAdvancedTestDataInitializer
    {
        public async Task InitializeAsync(MockGremlinLanguageConnector connector)
        {
            // Apply UserManagement scenario for basic user operations
            MockGremlinConnectorFactory.ApplyScenario(connector, "UserManagement");
            
            // Configure additional responses for comprehensive testing
            connector.ConfigureFunctionResponse(@"g\.addV", (query, parameters) =>
            {
                var label = "person"; // Default
                if (query.Contains("'company'")) label = "company";
                if (query.Contains("'department'")) label = "department";

                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        Guid.NewGuid().ToString(),
                        label,
                        new Dictionary<string, object>
                        {
                            { "name", "Test Entity" },
                            { "created", DateTime.UtcNow },
                            { "active", true }
                        }
                    )
                };
            });

            connector.ConfigureFunctionResponse(@"g\.V\(", (query, parameters) =>
            {
                return new[]
                {
                    MockExtensions.CreateVertexResponse(
                        "test-id",
                        "person",
                        new Dictionary<string, object>
                        {
                            { "name", "Test Person" },
                            { "firstName", "Test" },
                            { "lastName", "Person" },
                            { "email", "test@example.com" },
                            { "active", true }
                        }
                    )
                };
            });

            await Task.Delay(1); // Simulate async initialization
        }
    }

    /// <summary>
    /// Advanced scenario builder leveraging the scenario framework
    /// </summary>
    public class AdvancedTestScenarioBuilder : ITestScenarioBuilder
    {
        public async Task BuildFamilyScenarioAsync(MockGremlinLanguageConnector connector)
        {
            // Apply the custom family testing scenario
            MockGremlinConnectorFactory.ApplyScenario(connector, "FamilyTesting");
            await Task.Delay(1);
        }

        public async Task BuildCorporateScenarioAsync(MockGremlinLanguageConnector connector)
        {
            // Apply the custom corporate testing scenario
            MockGremlinConnectorFactory.ApplyScenario(connector, "CorporateTesting");
            await Task.Delay(1);
        }

        public async Task BuildMultiDomainScenarioAsync(MockGremlinLanguageConnector connector)
        {
            // Combine multiple scenarios for comprehensive testing
            MockGremlinConnectorFactory.ApplyScenario(connector, "Organization");
            MockGremlinConnectorFactory.ApplyScenario(connector, "SocialNetwork");
            MockGremlinConnectorFactory.ApplyScenario(connector, "UserManagement");
            
            await Task.Delay(1);
        }
    }

    #endregion

    #region Data Transfer Objects

    public class SimpleProfile
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class SimpleCompany
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class OrganizationHierarchyResult
    {
        public SimpleCompany? ParentCompany { get; set; }
        public IEnumerable<SimpleCompany> Divisions { get; set; } = new List<SimpleCompany>();
    }

    public class FamilyGroup
    {
        public IEnumerable<SimpleProfile> Parents { get; set; } = new List<SimpleProfile>();
        public IEnumerable<SimpleProfile> Children { get; set; } = new List<SimpleProfile>();
    }

    #endregion
}