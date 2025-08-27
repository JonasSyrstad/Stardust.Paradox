using Stardust.Paradox.Data;
using Stardust.Paradox.Data.MockTester.Models;
using Stardust.Paradox.Data.Mocker;
using Stardust.Paradox.Data.Traversals;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Stardust.Paradox.Data.MockTester
{
    /// <summary>
    /// Interface for shopping cart context
    /// </summary>
    public interface IShoppingCartContext : IGraphContext
    {
        IGraphSet<ICustomer> Customers { get; }
        IGraphSet<IProduct> Products { get; }
        IGraphSet<IShoppingCart> ShoppingCarts { get; }
        IGraphSet<IOrder> Orders { get; }
        IGraphSet<ICategory> Categories { get; }
        
        IEdgeGraphSet<ICartItem> CartItems { get; }
        IEdgeGraphSet<IOrderItem> OrderItems { get; }
        IEdgeGraphSet<IProductCategory> ProductCategories { get; }
        
        double ConsumedRU { get; }
    }

    /// <summary>
    /// Shopping cart test context for mock testing
    /// </summary>
    public class ShoppingCartContext : GraphContextBase, IShoppingCartContext
    {
        private static bool _shoppingCartModelInitialized = false;
        private static readonly object _shoppingCartLockObject = new object();

        public MockGremlinLanguageConnector MockConnector { get; }

        public ShoppingCartContext(MockGremlinLanguageConnector connector) : base(connector, CreateServiceProvider(), null)
        {
            MockConnector = connector;
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            
            // Set up the entity binding for code generation
            services.AddEntityBinding((entity, implementation) => 
            {
                services.AddTransient(entity, implementation);
            });
            
            return services.BuildServiceProvider();
        }

        static ShoppingCartContext()
        {
            PartitionKeyName = "pk";
        }

        protected override void Dispose(bool disposing)
        {
            OnDisposing?.Invoke(this);
            base.Dispose(disposing);
        }

        public Action<ShoppingCartContext> OnDisposing { get; set; }

        protected override bool InitializeModel(IGraphConfiguration configuration)
        {
            lock (_shoppingCartLockObject)
            {
                if (_shoppingCartModelInitialized)
                    return true;

                try
                {
                    // Configure Customer
                    configuration.ConfigureCollection<ICustomer>();

                    // Configure Product
                    configuration.ConfigureCollection<IProduct>();

                    // Configure Shopping Cart
                    configuration.ConfigureCollection<IShoppingCart>();

                    // Configure Order
                    configuration.ConfigureCollection<IOrder>();

                    // Configure Category
                    configuration.ConfigureCollection<ICategory>();

                    // Configure Edge collections
                    configuration.ConfigureCollection<ICartItem>();
                    configuration.ConfigureCollection<IOrderItem>();
                    configuration.ConfigureCollection<IProductCategory>();

                    _shoppingCartModelInitialized = true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Model already initialized by another test context, ignore
                    _shoppingCartModelInitialized = true;
                }
                catch (ArgumentException)
                {
                    // Type already registered, ignore
                    _shoppingCartModelInitialized = true;
                }
                catch (Exception ex)
                {
                    // Log the exception but don't fail - continue with already initialized model
                    System.Diagnostics.Debug.WriteLine($"Shopping cart model initialization warning: {ex.Message}");
                    _shoppingCartModelInitialized = true;
                }
            }

            return true;
        }

        public IGraphSet<ICustomer> Customers => GraphSet<ICustomer>();
        public IGraphSet<IProduct> Products => GraphSet<IProduct>();
        public IGraphSet<IShoppingCart> ShoppingCarts => GraphSet<IShoppingCart>();
        public IGraphSet<IOrder> Orders => GraphSet<IOrder>();
        public IGraphSet<ICategory> Categories => GraphSet<ICategory>();
        
        public IEdgeGraphSet<ICartItem> CartItems => EdgeGraphSet<ICartItem>();
        public IEdgeGraphSet<IOrderItem> OrderItems => EdgeGraphSet<IOrderItem>();
        public IEdgeGraphSet<IProductCategory> ProductCategories => EdgeGraphSet<IProductCategory>();
    }
}