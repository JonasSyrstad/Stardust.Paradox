# Stardust.Paradox.Data.Mocker

A comprehensive mock framework for testing applications that use the Stardust.Paradox.Data Gremlin providers. This framework allows you to simulate Gremlin query responses and graph operations without connecting to an actual database, with a special focus on **IoC-based testing** using the `IMockTestContext` interface.

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [IoC-Based Testing (Recommended)](#ioc-based-testing-recommended)
- [Context-Based Testing Patterns](#context-based-testing-patterns)
- [Service Layer Testing](#service-layer-testing)
- [Core Concepts](#core-concepts)
- [API Reference](#api-reference)
- [Configuration Options](#configuration-options)
- [Response Providers](#response-providers)
- [Mock Response Creation](#mock-response-creation)
- [Testing Patterns](#testing-patterns)
- [Advanced Scenarios](#advanced-scenarios)
- [AI Agent Guidelines](#ai-agent-guidelines)
- [Best Practices](#best-practices)
- [Common Examples](#common-examples)
- [Troubleshooting](#troubleshooting)

## Overview

The Stardust.Paradox.Data.Mocker framework provides:

- **IoC Container Integration**: Full support for dependency injection with `IMockTestContext`
- **Service Layer Testing**: Test your business logic with proper separation of concerns
- **Repository Pattern Support**: Mock graph operations through familiar patterns
- **Multiple Response Types**: Configure responses using JSON strings, JSON files, or custom functions
- **Operation Detection**: Automatically detects and simulates CREATE, UPDATE, DELETE, and QUERY operations
- **In-Memory Graph Store**: Maintains graph state across operations for realistic testing
- **RU Tracking**: Simulates Request Unit consumption for Cosmos DB scenarios
- **Parameterized Queries**: Full support for parameterized Gremlin queries
- **Fluent Configuration**: Easy-to-use builder pattern for setting up mock scenarios
- **Regex Pattern Matching**: Flexible query pattern matching for different scenarios
- **Pre-built Scenarios**: Common test patterns ready to use
- **.NET Standard 2.0 Compatible**: Works with older .NET Framework and modern .NET versions

## Installation

Add the package reference to your test project:

```xml
<PackageReference Include="Stardust.Paradox.Data.Mocker" Version="[version]" />
```

## Quick Start

### Ultra-Simple Setup for Basic Testing

```csharp
// One-liner for basic graph testing with automatic operation simulation
var connector = MockGremlinConnectorFactory.CreateForTesting();

// Immediately test CRUD operations - no additional setup needed!
await connector.ExecuteAsync("g.addV('person').property('name', 'John')", new Dictionary<string, object>());
var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
Assert.Single(result);
```

## IoC-Based Testing (Recommended)

### ??? Setting Up IoC Container with MockTestContext

The recommended approach for testing business logic is to use dependency injection with the `IMockTestContext` interface:

```csharp
public class BusinessServiceTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHost _host;

    public BusinessServiceTests()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
        
        _serviceProvider = _host.Services;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register the mock connector
        services.AddSingleton<MockGremlinLanguageConnector>(provider =>
        {
            return MockGremlinConnectorFactory.Create(options =>
            {
                options.LogQueries = true;
                options.EnableOperationSimulation = true;
                options.MaintainGraphState = true;
                options.SimulatedRUPerQuery = 1.5;
            });
        });

        // Register MockTestContext as IMockTestContext - This is the key!
        services.AddScoped<IMockTestContext>(provider =>
        {
            var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
            return new MockTestContext(connector);
        });

        // Register your business services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IUserRepository, UserRepository>();
    }

    [Fact]
    public async Task Should_CreateUser_UsingIoC()
    {
        // Arrange
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        
        // Act
        var userId = await userService.CreateUserAsync("john@example.com", "John", "Doe");
        var user = await userService.GetUserAsync(userId);

        // Assert
        Assert.NotNull(userId);
        Assert.NotNull(user);
        Assert.Equal("John", user.FirstName);
    }

    public void Dispose() => _host?.Dispose();
}
```

### ?? Service Implementation Using IMockTestContext

```csharp
public interface IUserService
{
    Task<string> CreateUserAsync(string email, string firstName, string lastName);
    Task<IProfile> GetUserAsync(string id);
}

public class UserService : IUserService
{
    private readonly IMockTestContext _context;

    public UserService(IMockTestContext context)
    {
        _context = context;
    }

    public async Task<string> CreateUserAsync(string email, string firstName, string lastName)
    {
        var id = Guid.NewGuid().ToString();
        
        // Use the graph context to create entities
        var user = _context.CreateEntity<IProfile>(id);
        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;
        user.Name = $"{firstName} {lastName}";
        
        // Save to context
        await _context.SaveChangesAsync();
        
        return id;
    }

    public async Task<IProfile> GetUserAsync(string id)
    {
        return await _context.VAsync<IProfile>(id);
    }
}
```

### ?? Repository Pattern with IoC

```csharp
public interface IUserRepository
{
    Task<IEnumerable<IProfile>> GetActiveUsersAsync();
    Task<IEnumerable<IProfile>> GetUsersByCompanyAsync(string companyId);
}

public class UserRepository : IUserRepository
{
    private readonly IMockTestContext _context;

    public UserRepository(IMockTestContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<IProfile>> GetActiveUsersAsync()
    {
        return await _context.VAsync<IProfile>(g => 
            g.V().HasLabel("person").Has("active", true));
    }

    public async Task<IEnumerable<IProfile>> GetUsersByCompanyAsync(string companyId)
    {
        return await _context.VAsync<IProfile>(g => 
            g.V(companyId).In("employer"));
    }
}
```

## Context-Based Testing Patterns

### ?? Multi-Service Integration Tests

```csharp
[Fact]
public async Task Should_HandleCompleteUserWorkflow_WithIoC()
{
    // Arrange
    var userService = _serviceProvider.GetRequiredService<IUserService>();
    var companyService = _serviceProvider.GetRequiredService<ICompanyService>();
    var userRepository = _serviceProvider.GetRequiredService<IUserRepository>();

    // Act - Create complete scenario
    var companyId = await companyService.CreateCompanyAsync("TechCorp");
    var userId = await userService.CreateUserAsync("alice@techcorp.com", "Alice", "Smith");
    await companyService.HireUserAsync(companyId, userId, "Developer");
    
    // Query through repository
    var companyUsers = await userRepository.GetUsersByCompanyAsync(companyId);
    var user = await userService.GetUserAsync(userId);

    // Assert
    Assert.Single(companyUsers);
    Assert.NotNull(user);
    Assert.Equal("Alice", user.FirstName);
}
```

### ?? Advanced Test Data Initialization

```csharp
public interface ITestDataInitializer
{
    Task InitializeAsync(IMockTestContext context);
}

public class TestDataInitializer : ITestDataInitializer
{
    public async Task InitializeAsync(IMockTestContext context)
    {
        // Access the underlying connector for mock configuration
        if (context is MockTestContext mockContext)
        {
            var connector = mockContext.MockConnector;
            
            // Configure sophisticated mock responses
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('person'\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("user1", "person", new Dictionary<string, object> 
                    { 
                        { "firstName", "John" }, 
                        { "lastName", "Doe" },
                        { "email", "john@example.com" }
                    }),
                    ("user2", "person", new Dictionary<string, object> 
                    { 
                        { "firstName", "Jane" }, 
                        { "lastName", "Smith" },
                        { "email", "jane@example.com" }
                    })
                );
            });
        }
    }
}

// Usage in test
[Fact]
public async Task Should_UseTestDataInitializer()
{
    // Arrange
    var initializer = _serviceProvider.GetRequiredService<ITestDataInitializer>();
    var context = _serviceProvider.GetRequiredService<IMockTestContext>();
    var userRepository = _serviceProvider.GetRequiredService<IUserRepository>();
    
    await initializer.InitializeAsync(context);

    // Act
    var users = await userRepository.GetActiveUsersAsync();

    // Assert
    Assert.Equal(2, users.Count());
}
```

## AI Agent Guidelines

### ?? For AI Assistants Setting Up Context-Based Tests with IoC

When helping developers create IoC-based unit tests with this framework, prioritize **context-based testing with `IMockTestContext`** over direct connector usage. This provides better separation of concerns and more realistic testing scenarios.

#### ? Quick Decision Tree for AI Agents

```
What type of testing does the user need?
?? Service Layer Testing ? Use IMockTestContext + IoC (90% of cases)
?? Repository Testing ? Use IMockTestContext + IoC + Repository Pattern
?? Multi-Service Integration ? Use IMockTestContext + IoC + Multiple Services
?? Complex Business Workflows ? Use IMockTestContext + IoC + Scenario Builders
?? Direct Query Testing ? Use MockGremlinLanguageConnector (rare)
?? Quick Prototyping ? Use CreateForTesting() (development only)
```

#### ?? Context-Based IoC Setup Patterns

```csharp
// Pattern 1: Standard Enterprise IoC Setup (80% of cases)
// Use this for most business applications
private void ConfigureServices(IServiceCollection services)
{
    // Core mock infrastructure
    services.AddSingleton<MockGremlinLanguageConnector>(provider =>
        MockGremlinConnectorFactory.CreateForTesting());

    // Context as the primary abstraction
    services.AddScoped<IMockTestContext>(provider =>
    {
        var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
        return new MockTestContext(connector);
    });

    // Business services - ALWAYS inject IMockTestContext
    services.AddScoped<IUserService, UserService>();
    services.AddScoped<ICompanyService, CompanyService>();
    services.AddScoped<IOrderService, OrderService>();

    // Repository layer
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<ICompanyRepository, CompanyRepository>();

    // Test infrastructure
    services.AddTransient<ITestDataInitializer, TestDataInitializer>();
    services.AddTransient<ITestScenarioBuilder, TestScenarioBuilder>();
}

// Pattern 2: Custom Configuration Setup (15% of cases)
// Use when specific mock behavior is needed
private void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<MockGremlinLanguageConnector>(provider =>
        MockGremlinConnectorFactory.Create(options =>
        {
            options.LogQueries = true;              // Debug mode
            options.MaintainGraphState = true;      // Stateful testing
            options.SimulatedRUPerQuery = 2.5;     // Cost simulation
            options.EnableOperationSimulation = true; // Auto CRUD
        }));

    services.AddScoped<IMockTestContext>(provider =>
    {
        var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
        return new MockTestContext(connector);
    });

    // Register domain-specific services...
}

// Pattern 3: Template-Based Setup (5% of cases)
// Use for specific domain scenarios
private void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<MockGremlinLanguageConnector>(provider =>
        MockGremlinConnectorFactory.CreateOrganizationScenario()); // Pre-built scenarios

    services.AddScoped<IMockTestContext>(provider =>
    {
        var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
        return new MockTestContext(connector);
    });

    // Domain services for organization testing...
}
```

#### ??? Advanced IoC Patterns for Complex Scenarios

```csharp
// Pattern: Domain-Specific Service Collection Extensions with Scenario Providers
public static class TestServiceCollectionExtensions
{
    public static IServiceCollection AddUserManagementTesting(this IServiceCollection services)
    {
        // Register custom scenarios
        ScenarioRegistry.Register(new UserManagementScenario());
        
        // Core mock infrastructure with scenario
        services.AddSingleton<MockGremlinLanguageConnector>(provider =>
            MockGremlinConnectorFactory.CreateWithScenario("UserManagement"));

        services.AddScoped<IMockTestContext>(provider =>
        {
            var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
            return new MockTestContext(connector);
        });

        // Domain services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IHRService, HRService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        // Repository layer
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IHRRepository, HRRepository>();

        // Test infrastructure
        services.AddTransient<ITestDataInitializer, UserManagementTestDataInitializer>();
        services.AddTransient<ITestScenarioBuilder, UserManagementScenarioBuilder>();

        return services;
    }

    public static IServiceCollection AddSocialNetworkTesting(this IServiceCollection services)
    {
        // Use built-in social network scenario
        services.AddSingleton<MockGremlinLanguageConnector>(provider =>
            MockGremlinConnectorFactory.CreateWithScenario("SocialNetwork"));

        services.AddScoped<IMockTestContext>(provider =>
        {
            var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
            return new MockTestContext(connector);
        });

        // Social network specific services...
        services.AddScoped<ISocialNetworkService, SocialNetworkService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IFriendshipService, FriendshipService>();

        return services;
    }

    public static IServiceCollection AddECommerceTesting(this IServiceCollection services)
    {
        // Combine multiple scenarios
        services.AddSingleton<MockGremlinLanguageConnector>(provider =>
            MockGremlinConnectorFactory.CreateWithScenarios("ECommerce", "UserManagement"));

        services.AddScoped<IMockTestContext>(provider =>
        {
            var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
            return new MockTestContext(connector);
        });

        // E-commerce services...
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }

    public static IServiceCollection AddCustomBusinessTesting(this IServiceCollection services, IScenarioProvider customScenario = null)
    {
        // Support for custom scenarios
        if (customScenario != null)
        {
            ScenarioRegistry.Register(customScenario);
        }

        services.AddSingleton<MockGremlinLanguageConnector>(provider =>
        {
            var connector = MockGremlinConnectorFactory.CreateForTesting();
            
            // Apply custom scenario if provided
            if (customScenario != null)
            {
                MockGremlinConnectorFactory.ApplyScenario(connector, customScenario);
            }
            
            return connector;
        });

        services.AddScoped<IMockTestContext>(provider =>
        {
            var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
            return new MockTestContext(connector);
        });

        return services;
    }

    public static IServiceCollection AddPerformanceTesting(this IServiceCollection services, int userCount = 1000)
    {
        // Dynamic scenario creation for performance testing
        services.AddSingleton<MockGremlinLanguageConnector>(provider =>
        {
            var performanceScenario = new PerformanceTestScenario(userCount);
            ScenarioRegistry.Register(performanceScenario);
            return MockGremlinConnectorFactory.CreateWithScenario(performanceScenario.ScenarioName);
        });

        services.AddScoped<IMockTestContext>(provider =>
        {
            var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
            return new MockTestContext(connector);
        });

        // Performance-specific services
        services.AddScoped<IPerformanceTestService, PerformanceTestService>();

        return services;
    }
}

// Usage in test classes with custom scenarios
public class UserManagementTests : IDisposable
{
    private readonly IHost _host;
    private readonly IServiceProvider _serviceProvider;

    public UserManagementTests()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddUserManagementTesting()) // ? Clean setup with scenario
            .Build();
        
        _serviceProvider = _host.Services;
    }
}

public class SocialNetworkTests : IDisposable
{
    private readonly IHost _host;
    private readonly IServiceProvider _serviceProvider;

    public SocialNetworkTests()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSocialNetworkTesting()) // ? Domain-specific with built-in scenario
            .Build();
        
        _serviceProvider = _host.Services;
    }
}

public class CustomBusinessTests : IDisposable
{
    private readonly IHost _host;
    private readonly IServiceProvider _serviceProvider;

    public CustomBusinessTests()
    {
        // Create custom scenario for specific business domain
        var customScenario = new FinancialServicesScenario();
        
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddCustomBusinessTesting(customScenario)) // ? Custom scenario
            .Build();
        
        _serviceProvider = _host.Services;
    }
}

// Advanced: Multi-tenant testing with different scenarios per tenant
public class MultiTenantTests : IDisposable
{
    private readonly Dictionary<string, IHost> _tenantHosts = new Dictionary<string, IHost>();

    [Fact]
    public async Task Should_SupportMultipleTenantScenarios()
    {
        // Tenant A: E-commerce
        _tenantHosts["TenantA"] = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddECommerceTesting())
            .Build();

        // Tenant B: Social Network
        _tenantHosts["TenantB"] = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSocialNetworkTesting())
            .Build();

        // Test tenant A
        var tenantAServices = _tenantHosts["TenantA"].Services;
        var orderService = tenantAServices.GetRequiredService<IOrderService>();
        var order = await orderService.CreateOrderAsync("customer1", new[] { "product1" });
        Assert.NotNull(order);

        // Test tenant B
        var tenantBServices = _tenantHosts["TenantB"].Services;
        var socialService = tenantBServices.GetRequiredService<ISocialNetworkService>();
        var friends = await socialService.GetFriendsAsync("user1");
        Assert.NotEmpty(friends);
    }

    public void Dispose()
    {
        foreach (var host in _tenantHosts.Values)
        {
            host?.Dispose();
        }
    }
}

// Pattern: Scenario-aware test base class
public abstract class ScenarioBasedTestBase<TScenario> : IDisposable 
    where TScenario : IScenarioProvider, new()
{
    protected readonly IHost Host;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IMockTestContext Context;

    protected ScenarioBasedTestBase()
    {
        var scenario = new TScenario();
        ScenarioRegistry.Register(scenario);

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<MockGremlinLanguageConnector>(provider =>
                    MockGremlinConnectorFactory.CreateWithScenario(scenario.ScenarioName));

                services.AddScoped<IMockTestContext>(provider =>
                {
                    var connector = provider.GetRequiredService<MockGremlinLanguageConnector>();
                    return new MockTestContext(connector);
                });

                ConfigureAdditionalServices(services);
            })
            .Build();

        ServiceProvider = Host.Services;
        Context = ServiceProvider.GetRequiredService<IMockTestContext>();
    }

    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Override in derived classes to add domain-specific services
    }

    public void Dispose()
    {
        Host?.Dispose();
    }
}

// Usage of scenario-aware base class
public class ECommerceTests : ScenarioBasedTestBase<ECommerceScenario>
{
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
    }

    [Fact]
    public async Task Should_ProcessOrder_WithECommerceScenario()
    {
        // Arrange
        var orderService = ServiceProvider.GetRequiredService<IOrderService>();

        // Act - scenario data is automatically available
        var order = await orderService.CreateOrderAsync("cust1", new[] { "prod1", "prod2" });

        // Assert
        Assert.NotNull(order);
    }
}

public class SocialNetworkTests : ScenarioBasedTestBase<SocialNetworkScenario>
{
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddScoped<ISocialNetworkService, SocialNetworkService>();
        services.AddScoped<IPostService, PostService>();
    }

    [Fact]
    public async Task Should_GetFriends_WithSocialNetworkScenario()
    {
        // Arrange
        var socialService = ServiceProvider.GetRequiredService<ISocialNetworkService>();

        // Act - scenario data is automatically available
        var friends = await socialService.GetFriendsAsync("user1");

        // Assert
        Assert.NotEmpty(friends);
    }
}