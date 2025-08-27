using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.MockTester.Models
{
    /// <summary>
    /// Customer in the shopping system
    /// </summary>
    [VertexLabel("customer")]
    public interface ICustomer : IVertex
    {
        string Name { get; set; }
        string Email { get; set; }
        DateTime CreatedAt { get; set; }
        
        [ReverseEdgeLabel("owner")]
        IEdgeCollection<IShoppingCart> ShoppingCarts { get; }
        
        [ReverseEdgeLabel("purchaser")]
        IEdgeCollection<IOrder> Orders { get; }
    }

    /// <summary>
    /// Product in the catalog
    /// </summary>
    [VertexLabel("product")]
    public interface IProduct : IVertex
    {
        string Name { get; set; }
        string Description { get; set; }
        decimal Price { get; set; }
        int StockQuantity { get; set; }
        string Category { get; set; }
        
        [ReverseEdgeLabel("contains")]
        IEdgeCollection<IShoppingCart> InCarts { get; }
        
        [ReverseEdgeLabel("ordered")]
        IEdgeCollection<IOrder> InOrders { get; }
    }

    /// <summary>
    /// Shopping cart for a customer
    /// </summary>
    [VertexLabel("cart")]
    public interface IShoppingCart : IVertex
    {
        DateTime CreatedAt { get; set; }
        DateTime LastModified { get; set; }
        string Status { get; set; } // "active", "abandoned", "converted"
        
        [EdgeLabel("owner")]
        IEdgeReference<ICustomer> Customer { get; set; }
        
        [EdgeLabel("contains")]
        IEdgeCollection<IProduct> Items { get; }
    }

    /// <summary>
    /// Item in a shopping cart
    /// </summary>
    [EdgeLabel("contains")]
    public interface ICartItem : IEdge<IShoppingCart, IProduct>
    {
        int Quantity { get; set; }
        DateTime AddedAt { get; set; }
        decimal PriceAtTime { get; set; } // Price when added to cart
    }

    /// <summary>
    /// Completed order
    /// </summary>
    [VertexLabel("order")]
    public interface IOrder : IVertex
    {
        string OrderNumber { get; set; }
        DateTime OrderDate { get; set; }
        decimal TotalAmount { get; set; }
        string Status { get; set; } // "pending", "confirmed", "shipped", "delivered", "cancelled"
        string ShippingAddress { get; set; }
        
        [EdgeLabel("purchaser")]
        IEdgeReference<ICustomer> Customer { get; set; }
        
        [EdgeLabel("ordered")]
        IEdgeCollection<IProduct> Items { get; }
    }

    /// <summary>
    /// Item in an order
    /// </summary>
    [EdgeLabel("ordered")]
    public interface IOrderItem : IEdge<IOrder, IProduct>
    {
        int Quantity { get; set; }
        decimal PurchasePrice { get; set; }
    }

    /// <summary>
    /// Product category for organization
    /// </summary>
    [VertexLabel("category")]
    public interface ICategory : IVertex
    {
        string Name { get; set; }
        string Description { get; set; }
        
        [ReverseEdgeLabel("categorized_as")]
        IEdgeCollection<IProduct> Products { get; }
    }

    /// <summary>
    /// Product categorization edge
    /// </summary>
    [EdgeLabel("categorized_as")]
    public interface IProductCategory : IEdge<IProduct, ICategory>
    {
        DateTime AssignedAt { get; set; }
    }
}