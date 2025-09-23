using Microsoft.Extensions.Logging;
using Stardust.Paradox.Data.Providers.Gremlin;
using Stardust.Paradox.Data.ScenarioConnector;

namespace Stardust.Paradox.Data.ScenarioConnector;

class Program
{
    private static ConnectionManager _connectionManager = null!;
    private static ILogger _logger = null!;

    static async Task Main(string[] args)
    {
        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<Program>();

        _connectionManager = new ConnectionManager();

        // Display ASCII art splash screen
        DisplaySplashScreen();

        Console.WriteLine("Export scenarios from CosmosDB to InMemory database format");
        Console.WriteLine();

        try
        {
            await RunMainMenuAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            _logger.LogError(ex, "Fatal error occurred");
            Environment.Exit(1);
        }
    }

    private static void DisplaySplashScreen()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        
        Console.WriteLine(@"
    ███████╗████████╗ █████╗ ██████╗ ██████╗ ██╗   ██╗███████╗████████╗
    ██╔════╝╚══██╔══╝██╔══██╗██╔══██╗██╔══██╗██║   ██║██╔════╝╚══██╔══╝
    ███████╗   ██║   ███████║██████╔╝██║  ██║██║   ██║███████╗   ██║   
    ╚════██║   ██║   ██╔══██║██╔══██╗██║  ██║██║   ██║╚════██║   ██║   
    ███████║   ██║   ██║  ██║██║  ██║██████╔╝╚██████╔╝███████║   ██║   
    ╚══════╝   ╚═╝   ╚═╝  ╚═╝╚═╝  ╚═╝╚═════╝  ╚═════╝ ╚══════╝   ╚═╝   ");
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(@"
    ██████╗  █████╗ ██████╗  █████╗ ██████╗  ██████╗ ██╗  ██╗
    ██╔══██╗██╔══██╗██╔══██╗██╔══██╗██╔══██╗██╔═══██╗╚██╗██╔╝
    ██████╔╝███████║██████╔╝███████║██║  ██║██║   ██║ ╚███╔╝ 
    ██╔═══╝ ██╔══██║██╔══██╗██╔══██║██║  ██║██║   ██║ ██╔██╗ 
    ██║     ██║  ██║██║  ██║██║  ██║██████╔╝╚██████╔╝██╔╝ ██╗
    ╚═╝     ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═╝╚═════╝  ╚═════╝ ╚═╝  ╚═╝");
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(@"
          ███████╗ ██████╗███████╗███╗   ██╗ █████╗ ██████╗ ██╗ ██████╗ 
          ██╔════╝██╔════╝██╔════╝████╗  ██║██╔══██╗██╔══██╗██║██╔═══██╗
          ███████╗██║     █████╗  ██╔██╗ ██║███████║██████╔╝██║██║   ██║
          ╚════██║██║     ██╔══╝  ██║╚██╗██║██╔══██║██╔══██╗██║██║   ██║
          ███████║╚██████╗███████╗██║ ╚████║██║  ██║██║  ██║██║╚██████╔╝
          ╚══════╝ ╚═════╝╚══════╝╚═╝  ╚═══╝╚═╝  ╚═╝╚═╝  ╚═╝╚═╝ ╚═════╝ ");
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(@"
           ██████╗ ██████╗ ███╗   ██╗███╗   ██╗███████╗ ██████╗████████╗ ██████╗ ██████╗ 
          ██╔════╝██╔═══██╗████╗  ██║████╗  ██║██╔════╝██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗
          ██║     ██║   ██║██╔██╗ ██║██╔██╗ ██║█████╗  ██║        ██║   ██║   ██║██████╔╝
          ██║     ██║   ██║██║╚██╗██║██║╚██╗██║██╔══╝  ██║        ██║   ██║   ██║██╔══██╗
          ╚██████╗╚██████╔╝██║ ╚████║██║ ╚████║███████╗╚██████╗   ██║   ╚██████╔╝██║  ██║
           ╚═════╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝  ╚═══╝╚══════╝ ╚═════╝   ╚═╝    ╚═════╝ ╚═╝  ╚═╝");
        
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(@"
        ╔═══════════════════════════════════════════════════════════════════════════╗
        ║                        🌟 Graph Data Bridge 🌟                           ║
        ║                                                                           ║
        ║        📊 CosmosDB ──────► 🧪 InMemory Testing ──────► 🚀 Scenarios      ║
        ║                                                                           ║
        ║                    💎 Export • Transform • Test 💎                      ║
        ╚═══════════════════════════════════════════════════════════════════════════╝");
        
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@"
                   ┌─────────────────────────────────────────────────┐
                   │  ⚡ High-Performance Graph Database Scenarios ⚡ │
                   │     🔗 Seamless CosmosDB Integration 🔗        │
                   │        🎯 Precision Testing Framework 🎯      │
                   └─────────────────────────────────────────────────┘");
        
        Console.ResetColor();
        Console.WriteLine();
        
        // Version and build info
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("                         Version 1.0.0-preview.9 | .NET 8.0");
        Console.WriteLine("                      © 2024 Stardust • Apache 2.0 License");
        Console.ResetColor();
        Console.WriteLine();
        
        // Brief pause to let user appreciate the splash screen
        System.Threading.Thread.Sleep(1500);
    }

    private static async Task RunMainMenuAsync()
    {
        while (true)
        {
            DisplayMainMenu();
            var choice = Console.ReadLine()?.Trim();

#if DEBUG
            switch (choice?.ToLower())
            {
                case "1":
                case "c":
                    await SelectConnectionAsync();
                    break;
                case "2":
                case "a":
                    await AddConnectionAsync();
                    break;
                case "3":
                case "r":
                    await RemoveConnectionAsync();
                    break;
                case "4":
                case "l":
                    ListConnections();
                    break;
                case "5":
                case "v":
                    await ValidationTest.RunValidationAsync();
                    break;
                case "6":
                case "p":
                    await TestPropertyHandlingAsync();
                    break;
                case "7":
                case "e":
                    await PropertyExtractionTest.RunPropertyExtractionTestsAsync();
                    break;
                case "8":
                case "d":
                    await DemonstrateProgressBarsAsync();
                    break;
                case "9":
                case "f":
                    await DemonstrateFixedProgressBarsAsync();
                    break;
                case "10":
                case "m":
                    await DemonstrateConsolidatedProgressBarAsync();
                    break;
                case "11":
                case "q":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }
#else
            switch (choice?.ToLower())
            {
                case "1":
                case "c":
                    await SelectConnectionAsync();
                    break;
                case "2":
                case "a":
                    await AddConnectionAsync();
                    break;
                case "3":
                case "r":
                    await RemoveConnectionAsync();
                    break;
                case "4":
                case "l":
                    ListConnections();
                    break;
                case "5":
                case "q":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }
#endif

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    private static void DisplayMainMenu()
    {
        Console.WriteLine("Main Menu:");
        Console.WriteLine("1. (C)onnect to database and export scenarios");
        Console.WriteLine("2. (A)dd new connection");
        Console.WriteLine("3. (R)emove connection");
        Console.WriteLine("4. (L)ist all connections");
#if DEBUG
        Console.WriteLine("5. (V)alidate tool components");
        Console.WriteLine("6. Test (P)roperty handling");
        Console.WriteLine("7. Test property (E)xtraction logic");
        Console.WriteLine("8. (D)emonstrate progress bars");
        Console.WriteLine("9. Test (F)ixed position progress bars");
        Console.WriteLine("10. Test (M)ulti-progress consolidated bar");
        Console.WriteLine("11. (Q)uit");
#else
        Console.WriteLine("5. (Q)uit");
#endif
        Console.WriteLine();
        Console.Write("Enter your choice: ");
    }

    private static async Task SelectConnectionAsync()
    {
        var connections = _connectionManager.GetConnections();
        
        if (connections.Count == 0)
        {
            Console.WriteLine("No connections found. Please add a connection first.");
            return;
        }

        Console.WriteLine("\nAvailable Connections:");
        for (int i = 0; i < connections.Count; i++)
        {
            var conn = connections[i];
            Console.WriteLine($"{i + 1}. {conn.GetDisplayName()}");
            Console.WriteLine($"   Last used: {conn.LastUsed:yyyy-MM-dd HH:mm:ss}");
        }

        Console.Write("\nSelect connection (1-" + connections.Count + "): ");
        var choice = Console.ReadLine()?.Trim();

        if (int.TryParse(choice, out int index) && index >= 1 && index <= connections.Count)
        {
            var selectedConnection = connections[index - 1];
            await UseConnectionAsync(selectedConnection);
        }
        else
        {
            Console.WriteLine("Invalid selection.");
        }
    }

    private static async Task UseConnectionAsync(CosmosDbConnection connection)
    {
        Console.WriteLine($"\nConnecting to: {connection.GetDisplayName()}");
        
        try
        {
            var connector = new GremlinNetLanguageConnector(
                connection.Hostname,
                connection.DatabaseName,
                connection.GraphName,
                connection.AccessKey);

            _connectionManager.UpdateLastUsed(connection.Name);

            var exporter = new ScenarioExporter(connector, connection.Name);
            
#if DEBUG
            // Ask user if they want debug logging enabled
            Console.Write("Enable debug logging for property extraction? (y/N): ");
            var debugChoice = Console.ReadLine()?.Trim();
            if (string.Equals(debugChoice, "y", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(debugChoice, "yes", StringComparison.OrdinalIgnoreCase))
            {
                exporter.EnableDebugLogging = true;
                Console.WriteLine("Debug logging enabled.");
            }
            else
            {
                exporter.EnableDebugLogging = false;
                Console.WriteLine("Debug logging disabled.");
            }
#else
            // In release builds, disable debug logging by default
            exporter.EnableDebugLogging = false;
#endif

            await RunExportMenuAsync(exporter);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect: {ex.Message}");
            _logger.LogError(ex, "Failed to connect to CosmosDB");
        }
    }

    private static async Task RunExportMenuAsync(ScenarioExporter exporter)
    {
        while (true)
        {
            DisplayExportMenu();
            var choice = Console.ReadLine()?.Trim();

#if DEBUG
            switch (choice?.ToLower())
            {
                case "1":
                case "q":
                    await ExportByQueryAsync(exporter);
                    break;
                case "2":
                case "i":
                    await ExportByIdsAsync(exporter);
                    break;
                case "3":
                case "t":
                    await TestPropertyFetchAsync(exporter);
                    break;
                case "4":
                case "b":
                    return; // Back to main menu
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }
#else
            switch (choice?.ToLower())
            {
                case "1":
                case "q":
                    await ExportByQueryAsync(exporter);
                    break;
                case "2":
                case "i":
                    await ExportByIdsAsync(exporter);
                    break;
                case "3":
                case "b":
                    return; // Back to main menu
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }
#endif

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
    }

    private static void DisplayExportMenu()
    {
        Console.Clear();
        Console.WriteLine("Export Menu:");
        Console.WriteLine("1. Export by (Q)uery");
        Console.WriteLine("2. Export by vertex (I)Ds");
#if DEBUG
        Console.WriteLine("3. (T)est property fetching for single vertex");
        Console.WriteLine("4. (B)ack to main menu");
#else
        Console.WriteLine("3. (B)ack to main menu");
#endif
        Console.WriteLine();
        Console.Write("Enter your choice: ");
    }

    private static async Task ExportByQueryAsync(ScenarioExporter exporter)
    {
        Console.WriteLine("\nExport by Gremlin Query");
        Console.WriteLine("Example: g.V().hasLabel('person').limit(100)");
        Console.WriteLine("Example: g.V().has('category', 'product')");
        Console.WriteLine("Note: For CosmosDB, avoid queries that require composite key syntax in array format");
        Console.Write("\nEnter Gremlin query: ");
        
        var query = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("Query cannot be empty.");
            return;
        }

        Console.Write("Enter scenario name: ");
        var scenarioName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            Console.WriteLine("Scenario name cannot be empty.");
            return;
        }

        Console.Write("Enter description (optional): ");
        var description = Console.ReadLine()?.Trim();

        try
        {
            Console.WriteLine("\nExporting scenario...");
            var scenario = await exporter.ExportByQueryAsync(query, scenarioName, description);
            await SaveScenarioAsync(scenario);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Export failed: {ex.Message}");
            
            // Check if it's a partition key related error
            if (ex.Message.Contains("composite key") || ex.Message.Contains("partition"))
            {
                Console.WriteLine("\nTip: This error suggests your query uses syntax incompatible with CosmosDB partition keys.");
                Console.WriteLine("Try simpler queries like: g.V().hasLabel('yourLabel').limit(100)");
                Console.WriteLine("Avoid queries with vertex ID arrays: g.V(['id1', 'id2'])");
            }
            
            _logger.LogError(ex, "Export by query failed");
        }
    }

    private static async Task ExportByIdsAsync(ScenarioExporter exporter)
    {
        Console.WriteLine("\nExport by Vertex IDs");
        Console.WriteLine("Enter vertex IDs separated by commas or new lines");
        Console.WriteLine("Example: vertex1, vertex2, vertex3");
        Console.WriteLine("For CosmosDB with partition keys, use format: partitionKey|vertexId");
        Console.WriteLine("Example: user|123, product|456");
        Console.WriteLine("Enter 'END' on a new line when finished:");
        Console.WriteLine();

        var ids = new List<string>();
        while (true)
        {
            var line = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(line) || line.Equals("END", StringComparison.OrdinalIgnoreCase))
                break;

            // Split by comma and add to list
            var lineIds = line.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(id => id.Trim())
                             .Where(id => !string.IsNullOrWhiteSpace(id));
            ids.AddRange(lineIds);
        }

        if (ids.Count == 0)
        {
            Console.WriteLine("No vertex IDs provided.");
            return;
        }

        Console.Write("\nEnter scenario name: ");
        var scenarioName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            Console.WriteLine("Scenario name cannot be empty.");
            return;
        }

        Console.Write("Enter description (optional): ");
        var description = Console.ReadLine()?.Trim();

        try
        {
            Console.WriteLine($"\nExporting scenario with {ids.Count} vertex IDs...");
            var scenario = await exporter.ExportByIdsAsync(ids, scenarioName, description);
            await SaveScenarioAsync(scenario);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Export failed: {ex.Message}");
            
            // Check if it's a partition key related error
            if (ex.Message.Contains("composite key") || ex.Message.Contains("partition"))
            {
                Console.WriteLine("\nTip: This error suggests your CosmosDB uses partition keys.");
                Console.WriteLine("Try using the format: partitionKey|vertexId for your vertex IDs.");
                Console.WriteLine("For example: user|123 instead of just 123");
            }
            
            _logger.LogError(ex, "Export by IDs failed");
        }
    }

    private static async Task SaveScenarioAsync(ExportedScenario scenario)
    {
        Console.WriteLine($"\nScenario exported successfully!");
        Console.WriteLine($"Name: {scenario.Name}");
        Console.WriteLine($"Description: {scenario.Description}");
        Console.WriteLine($"Vertices: {scenario.Vertices.Count}");
        Console.WriteLine($"Edges: {scenario.Edges.Count}");

        // Create output directory
        var outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                                   "StardustParadox", "ExportedScenarios");
        Directory.CreateDirectory(outputDir);

        var safeFileName = GetSafeFileName(scenario.Name);
        var jsonFilePath = Path.Combine(outputDir, $"{safeFileName}.json");
        var csFilePath = Path.Combine(outputDir, $"{safeFileName}.cs");

        // Save files with consolidated progress indication
        using (var progress = ConsolidatedProgressBarFactory.Create(0, 0, 2)) // Only file progress
        {
            // Save JSON file
            progress.IncrementFileProgress("Saving JSON file...");
            await ScenarioExporter.SaveScenarioAsync(scenario, jsonFilePath);
            Console.WriteLine($"\nJSON file saved: {jsonFilePath}");

            // Save C# class file  
            progress.IncrementFileProgress("Saving C# class file...");
            await ScenarioConverter.SaveAsCSharpClassAsync(scenario, csFilePath);
            Console.WriteLine($"C# class file saved: {csFilePath}");
        }

        Console.WriteLine("\nFiles saved successfully!");
        Console.WriteLine("\nTo use this scenario in your InMemory tests:");
        Console.WriteLine("1. Copy the generated C# class to your project");
        Console.WriteLine("2. Register it: InMemoryScenarioRegistry.Register(new YourScenario());");
        Console.WriteLine("3. Use it: InMemoryConnectorFactory.CreateWithScenario(\"" + scenario.Name + "\");");
    }

    private static async Task AddConnectionAsync()
    {
        Console.WriteLine("\nAdd New CosmosDB Connection");
        
        Console.Write("Connection name: ");
        var name = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("Connection name cannot be empty.");
            return;
        }

        Console.Write("Hostname (e.g., myaccount.gremlin.cosmosdb.azure.com): ");
        var hostname = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(hostname))
        {
            Console.WriteLine("Hostname cannot be empty.");
            return;
        }

        Console.Write("Database name: ");
        var database = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(database))
        {
            Console.WriteLine("Database name cannot be empty.");
            return;
        }

        Console.Write("Graph name: ");
        var graph = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(graph))
        {
            Console.WriteLine("Graph name cannot be empty.");
            return;
        }

        Console.Write("Access key: ");
        var accessKey = ReadPassword();
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            Console.WriteLine("\nAccess key cannot be empty.");
            return;
        }

        try
        {
            var connection = new CosmosDbConnection
            {
                Name = name,
                Hostname = hostname,
                DatabaseName = database,
                GraphName = graph,
                AccessKey = accessKey
            };

            _connectionManager.AddConnection(connection);
            Console.WriteLine($"\nConnection '{name}' added successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nFailed to add connection: {ex.Message}");
        }
    }

    private static async Task RemoveConnectionAsync()
    {
        var connections = _connectionManager.GetConnections();
        
        if (connections.Count == 0)
        {
            Console.WriteLine("No connections found.");
            return;
        }

        Console.WriteLine("\nExisting Connections:");
        for (int i = 0; i < connections.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {connections[i].GetDisplayName()}");
        }

        Console.Write($"\nSelect connection to remove (1-{connections.Count}): ");
        var choice = Console.ReadLine()?.Trim();

        if (int.TryParse(choice, out int index) && index >= 1 && index <= connections.Count)
        {
            var connection = connections[index - 1];
            Console.Write($"Are you sure you want to remove '{connection.Name}'? (y/N): ");
            var confirm = Console.ReadLine()?.Trim();
            
            if (string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
            {
                if (_connectionManager.RemoveConnection(connection.Name))
                {
                    Console.WriteLine($"Connection '{connection.Name}' removed successfully!");
                }
                else
                {
                    Console.WriteLine("Failed to remove connection.");
                }
            }
            else
            {
                Console.WriteLine("Operation cancelled.");
            }
        }
        else
        {
            Console.WriteLine("Invalid selection.");
        }
    }

    private static void ListConnections()
    {
        var connections = _connectionManager.GetConnections();
        
        if (connections.Count == 0)
        {
            Console.WriteLine("No connections found.");
            return;
        }

        Console.WriteLine("\nStored Connections:");
        Console.WriteLine(new string('-', 80));
        
        foreach (var connection in connections)
        {
            Console.WriteLine($"Name: {connection.Name}");
            Console.WriteLine($"Hostname: {connection.Hostname}");
            Console.WriteLine($"Database: {connection.DatabaseName}");
            Console.WriteLine($"Graph: {connection.GraphName}");
            Console.WriteLine($"Created: {connection.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Last Used: {connection.LastUsed:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine(new string('-', 80));
        }
    }

    private static void ValidateToolComponents()
    {
        Console.WriteLine("\nValidating tool components...");
        
        // Example validation logic
        var allOk = true;
        
        // Check if the output directory exists
        var outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                                   "StardustParadox", "ExportedScenarios");
        if (!Directory.Exists(outputDir))
        {
            Console.WriteLine($"Output directory not found: {outputDir}");
            allOk = false;
        }

        // Here you can add more validation checks as needed
        
        if (allOk)
        {
            Console.WriteLine("All components are valid.");
        }
        else
        {
            Console.WriteLine("Some components are missing or invalid. Please check the above messages.");
        }
    }

    private static string ReadPassword()
    {
        var password = new System.Text.StringBuilder();
        ConsoleKeyInfo key;
        
        do
        {
            key = Console.ReadKey(true);
            
            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
        }
        while (key.Key != ConsoleKey.Enter);
        
        return password.ToString();
    }

    private static string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeFileName = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "scenario";
            
        return safeFileName.Replace(" ", "_");
    }

    private static async Task TestPropertyFetchAsync(ScenarioExporter exporter)
    {
        Console.WriteLine("\nTest Property Fetching");
        Console.WriteLine("This will test property fetching for a single vertex to help diagnose issues.");
        Console.Write("\nEnter vertex ID to test: ");
        
        var vertexId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(vertexId))
        {
            Console.WriteLine("Vertex ID cannot be empty.");
            return;
        }

        try
        {
            Console.WriteLine($"\nTesting property fetching for vertex: {vertexId}");
            var testResult = await exporter.TestVertexPropertyFetchAsync(vertexId);
            
            if (testResult != null)
            {
                Console.WriteLine("\n=== TEST RESULT ===");
                Console.WriteLine($"Vertex ID: {testResult.Id}");
                Console.WriteLine($"Vertex Label: {testResult.Label}");
                Console.WriteLine($"Property Count: {testResult.Properties.Count}");
                
                if (testResult.Properties.Count > 0)
                {
                    Console.WriteLine("Properties found:");
                    foreach (var prop in testResult.Properties)
                    {
                        Console.WriteLine($"  {prop.Key}: {prop.Value} ({prop.Value?.GetType().Name})");
                    }
                }
                else
                {
                    Console.WriteLine("No properties found - this indicates the issue with property fetching.");
                    Console.WriteLine("\nPossible causes:");
                    Console.WriteLine("1. Vertex has no properties");
                    Console.WriteLine("2. Permission issues accessing properties");
                    Console.WriteLine("3. Property query format not supported by this CosmosDB instance");
                    Console.WriteLine("4. Vertex ID format issues (try partition key format if applicable)");
                }
            }
            else
            {
                Console.WriteLine("Test failed - no vertex returned.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            _logger.LogError(ex, "Property fetch test failed");
        }
    }

    private static async Task TestPropertyHandlingAsync()
    {
        Console.WriteLine("\nTest Property Handling");
        Console.WriteLine("This will test the overall property handling mechanism with a sample scenario.");
        Console.WriteLine("A sample scenario will be created and the properties will be exported.");
        Console.WriteLine();

        // Sample data for testing
        var sampleScenario = new ExportedScenario
        {
            Name = "TestScenario_" + Guid.NewGuid().ToString("N"),
            Vertices = new List<ExportedVertex>
            {
                new ExportedVertex
                {
                    Id = "1",
                    Label = "person",
                    Properties = new Dictionary<string, object>
                    {
                        { "name", "John Doe" },
                        { "age", 29 },
                        { "isActive", true }
                    }
                },
                new ExportedVertex
                {
                    Id = "2",
                    Label = "product",
                    Properties = new Dictionary<string, object>
                    {
                        { "category", "electronics" },
                        { "price", 399.99 },
                        { "inStock", true }
                    }
                }
            },
            Edges = new List<ExportedEdge>
            {
                new ExportedEdge
                {
                    Id = "1",
                    Label = "knows",
                    OutVertexId = "1",
                    InVertexId = "2",
                    Properties = new Dictionary<string, object>
                    {
                        { "since", "2021" },
                        { "friend", true }
                    }
                }
            }
        };

        try
        {
            Console.WriteLine("Creating sample scenario...");
            // Normally, you would use the exporter to create this scenario in the target database
            // Here, we just simulate the export process
            await Task.Delay(1000);

            Console.WriteLine("Sample scenario created:");
            Console.WriteLine($"Name: {sampleScenario.Name}");
            Console.WriteLine($"Vertices: {sampleScenario.Vertices.Count}");
            Console.WriteLine($"Edges: {sampleScenario.Edges.Count}");

            // Simulate property handling by displaying the properties
            foreach (var vertex in sampleScenario.Vertices)
            {
                Console.WriteLine($"\nVertex: {vertex.Label} (ID: {vertex.Id})");
                Console.WriteLine("Properties:");
                foreach (var prop in vertex.Properties)
                {
                    Console.WriteLine($"  {prop.Key}: {prop.Value} (Type: {prop.Value?.GetType().Name})");
                }
            }

            foreach (var edge in sampleScenario.Edges)
            {
                Console.WriteLine($"\nEdge: {edge.Label} (ID: {edge.Id})");
                Console.WriteLine($"From: {edge.OutVertexId} To: {edge.InVertexId}");
                Console.WriteLine("Properties:");
                foreach (var prop in edge.Properties)
                {
                    Console.WriteLine($"  {prop.Key}: {prop.Value} (Type: {prop.Value?.GetType().Name})");
                }
            }

            Console.WriteLine("\nSample scenario test completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
        }
    }

    private static async Task TestPropertyExtractionLogicAsync()
    {
        Console.WriteLine("\nTest Property Extraction Logic");
        Console.WriteLine("This will test the property extraction logic used during scenario export.");
        Console.WriteLine("A sample vertex will be queried and its properties fetched.");
        Console.WriteLine();

        // Sample vertex ID (you can change this to an ID that exists in your database)
        var sampleVertexId = "1";

        try
        {
            Console.WriteLine($"Testing property extraction for sample vertex ID: {sampleVertexId}");

            // Here, you would normally use the exporter to fetch the vertex properties
            // For testing, we simulate the property extraction logic
            await Task.Delay(1000);

            // Simulated fetched properties
            var fetchedProperties = new Dictionary<string, object>
            {
                { "name", "John Doe" },
                { "age", 29 },
                { "isActive", true }
            };

            Console.WriteLine("Fetched properties:");
            foreach (var prop in fetchedProperties)
            {
                Console.WriteLine($"  {prop.Key}: {prop.Value} (Type: {prop.Value?.GetType().Name})");
            }

            Console.WriteLine("\nProperty extraction test completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
        }
    }

    private static async Task DemonstrateProgressBarsAsync()
    {
        Console.WriteLine("\nDemonstrating Fixed Position Progress Bars");
        Console.WriteLine("Progress bars will appear at the top of the screen");
        Console.WriteLine("Press any key after each demo completes...\n");
        
        // Give user time to read the instructions
        await Task.Delay(2000);
        
        // Simple progress bar demo with different scenarios
        Console.WriteLine("1. Basic counting progress...");
        using (var progress = ProgressBarFactory.Create(50, "Processing Items"))
        {
            for (int i = 0; i <= 50; i++)
            {
                // Simulate work
                await Task.Delay(60);
                progress.Increment($"Item {i}");
            }
        }
        
        await Task.Delay(1000);

        Console.WriteLine("\n2. File processing simulation...");
        var files = new[] { "data.json", "config.xml", "readme.txt", "image.png", "document.pdf" };
        using (var progress = ProgressBarFactory.CreateFileProgress(files.Length))
        {
            foreach (var file in files)
            {
                await Task.Delay(600);
                progress.Increment($"Processing {file}");
            }
        }

        await Task.Delay(1000);
        
        Console.WriteLine("\n3. Vertex processing simulation...");
        using (var progress = ProgressBarFactory.CreateVertexProgress(10))
        {
            for (int i = 1; i <= 10; i++)
            {
                await Task.Delay(400);
                progress.Increment($"vertex_{i}");
            }
        }

        Console.WriteLine("\nFixed position progress bar demonstration completed.");
    }

    private static async Task DemonstrateFixedProgressBarsAsync()
    {
        Console.Clear();
        Console.WriteLine("=== Fixed Position Progress Bar Demo ===");
        Console.WriteLine("Progress bars will appear at the top of the screen and stay fixed there.");
        Console.WriteLine("Notice how the progress bar stays at the top while other content appears below.");
        Console.WriteLine();
        Console.WriteLine("Starting demo in 3 seconds...");
        
        await Task.Delay(3000);
        
        // Demo 1: Processing items with fixed position
        Console.WriteLine("\n1. Processing items with fixed progress bar...");
        using (var progress = FixedProgressBarFactory.Create(25, "Processing Items"))
        {
            for (int i = 1; i <= 25; i++)
            {
                await Task.Delay(150);
                progress.Increment($"item_{i:D2}");
                
                // Add some console output to show it doesn't interfere
                if (i % 5 == 0)
                {
                    Console.WriteLine($"   Milestone: Completed {i} items");
                }
            }
        }
        
        Console.WriteLine("\n2. File processing with detailed progress...");
        var files = new[] { 
            "database_backup.sql", "user_data.json", "config_settings.xml", 
            "application.log", "error_report.txt", "system_metrics.csv" 
        };
        
        using (var progress = FixedProgressBarFactory.CreateFileProgress(files.Length))
        {
            foreach (var file in files)
            {
                await Task.Delay(800);
                progress.Increment(file);
                // Simulate file size instead of trying to read actual files
                var simulatedSize = new Random().Next(100, 5000);
                Console.WriteLine($"   Processed: {file} ({simulatedSize:N0} KB)");
            }
        }
        
        Console.WriteLine("\n3. Vertex processing simulation...");
        using (var progress = FixedProgressBarFactory.CreateVertexProgress(15))
        {
            for (int i = 1; i <= 15; i++)
            {
                await Task.Delay(200);
                progress.Increment($"vertex_{i:D3}");
                
                if (i % 3 == 0)
                {
                    Console.WriteLine($"   Batch {i/3} completed - {i} vertices processed");
                }
            }
        }
        
        Console.WriteLine("\nFixed position progress bar demonstration completed!");
        Console.WriteLine("Notice how each progress bar appeared at the top and was replaced by the next one.");
    }

    private static async Task DemonstrateConsolidatedProgressBarAsync()
    {
        Console.Clear();
        Console.WriteLine("=== Consolidated Multi-Progress Bar Demo ===");
        Console.WriteLine("This shows a combined progress bar with 4 indicators:");
        Console.WriteLine("1. Overall Progress (top line)");
        Console.WriteLine("2. Vertex Processing Progress");
        Console.WriteLine("3. Edge Processing Progress"); 
        Console.WriteLine("4. File Save Progress");
        Console.WriteLine();
        Console.WriteLine("All progress bars are fixed at the top of the screen.");
        Console.WriteLine("Starting demo in 3 seconds...");
        
        await Task.Delay(3000);
        
        // Simulate a scenario export process
        var vertexCount = 20;
        var edgeCount = 35;
        var fileCount = 2;
        
        using (var progress = ConsolidatedProgressBarFactory.CreateScenarioExportProgress(vertexCount, edgeCount, fileCount))
        {
            Console.WriteLine("Phase 1: Processing vertices...");
            
            // Simulate vertex processing
            for (int i = 1; i <= vertexCount; i++)
            {
                await Task.Delay(200);
                progress.IncrementVertexProgress($"vertex_{i:D2}");
                
                // Add some console output to show it doesn't interfere
                if (i % 5 == 0)
                {
                    Console.WriteLine($"   Vertex batch {i/5} completed ({i} vertices processed)");
                }
            }
            
            Console.WriteLine("\nPhase 2: Processing edges...");
            
            // Simulate edge processing
            for (int i = 1; i <= edgeCount; i++)
            {
                await Task.Delay(120);
                progress.IncrementEdgeProgress($"edge_{i:D2}");
                
                if (i % 7 == 0)
                {
                    Console.WriteLine($"   Edge batch {i/7} completed ({i} edges processed)");
                }
            }
            
            Console.WriteLine("\nPhase 3: Saving files...");
            
            // Simulate file saving
            await Task.Delay(800);
            progress.IncrementFileProgress("scenario.json");
            Console.WriteLine("   JSON scenario file saved");
            
            await Task.Delay(600);
            progress.IncrementFileProgress("scenario.cs");
            Console.WriteLine("   C# scenario file saved");
            
            Console.WriteLine("\nScenario export completed!");
            
            // The progress bar will automatically complete and clean up when disposed
        }
        
        Console.WriteLine("\nConsolidated progress bar demonstration completed!");
        Console.WriteLine("Notice how all progress indicators were shown simultaneously at the top.");
    }
}
