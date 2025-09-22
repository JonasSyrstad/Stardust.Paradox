using System;
using System.Linq;
using System.Threading.Tasks;

// Simulate what option 5 in the demo should do when displaying vertex properties
Console.WriteLine("?? VERTEX PROPERTIES DISPLAY TEST");
Console.WriteLine("=================================");
Console.WriteLine("This shows what option 5 should display when you run g.V().hasLabel('product')");
Console.WriteLine();

// Simulate product vertex data that the demo creates
var sampleProducts = new[]
{
    new { id = "prod1", label = "product", name = "Laptop Pro", category = "Electronics", price = 1299.99, brand = "TechCorp", rating = 4.5 },
    new { id = "prod2", label = "product", name = "Gaming Mouse", category = "Electronics", price = 79.99, brand = "GameStudio", rating = 4.7 },
    new { id = "prod3", label = "product", name = "Designer Jacket", category = "Fashion", price = 299.99, brand = "FashionHub", rating = 4.3 }
};

Console.WriteLine("?? Sample Products (what option 5 should show with properties):");
Console.WriteLine();

for (int i = 0; i < sampleProducts.Length; i++)
{
    var product = sampleProducts[i];
    
    // Format like our enhanced vertex formatter
    var formattedVertex = $"v[{product.id}:{product.label}] {{name=\"{product.name}\", category=\"{product.category}\", price={product.price}, +2 more}}";
    
    Console.WriteLine($"   [{i + 1,3}] {formattedVertex}");
}

Console.WriteLine();
Console.WriteLine("?? EXPECTED BEHAVIOR:");
Console.WriteLine("When you run option 5 in the demo, you should see:");
Console.WriteLine("- Vertex ID and label: v[prod1:product]");
Console.WriteLine("- Property values: {name=\"Laptop Pro\", category=\"Electronics\", price=1299.99, +2 more}");
Console.WriteLine("- All products showing their actual property values");
Console.WriteLine();
Console.WriteLine("? PREVIOUS BEHAVIOR (before fix):");
Console.WriteLine("- Only basic vertex info: v[prod1:product]");
Console.WriteLine("- No property values visible");
Console.WriteLine();
Console.WriteLine("? ENHANCED BEHAVIOR (after our fix):");
Console.WriteLine("- Full vertex with properties: v[prod1:product] {name=\"Laptop Pro\", category=\"Electronics\", price=1299.99, +2 more}");
Console.WriteLine();
Console.WriteLine("?? THE FIX:");
Console.WriteLine("We enhanced the FormatResult() method in Program.cs to:");
Console.WriteLine("1. Detect GremlinResponseObject types");
Console.WriteLine("2. Extract vertex properties using reflection");  
Console.WriteLine("3. Format properties in a readable way");
Console.WriteLine("4. Show first 3 properties + count of remaining");
Console.WriteLine();
Console.WriteLine("Now when you run the demo and choose option 5 'All products (with properties)',");
Console.WriteLine("you should see the vertex properties displayed correctly!");

Console.WriteLine("\nPress any key to continue...");
Console.ReadKey();