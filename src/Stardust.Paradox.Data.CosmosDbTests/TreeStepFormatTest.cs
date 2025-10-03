using Newtonsoft.Json;
using Stardust.Nucleus;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Providers.Gremlin;
using Stardust.Paradox.Data.Traversals;
using Stardust.Paradox.Data.Traversals.Helpers;
using Stardust.Particles;
using Stardust.Particles.Collection.Arrays;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations.DataTypes;
using Stardust.Paradox.Data.Internals;
using Xunit;
using Xunit.Abstractions;
using static Stardust.Paradox.Data.Traversals.GremlinFactory;

namespace Stardust.Paradox.CosmosDbTest
{
    /// <summary>
    /// Test to capture the exact format of tree step output from CosmosDB
    /// This will be used as reference for InMemory implementation
    /// </summary>
    public class TreeStepFormatTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IDependencyResolver _scope;

        public TreeStepFormatTest(ITestOutputHelper output)
        {
            _output = output;
            _scope = Resolver.CreateScopedResolver();
        }

        static TreeStepFormatTest()
        {
            ConfigurationManagerHelper.SetManager(new NullManager());
            Resolver.LoadModuleConfiguration<TestBp>();
        }

        [Fact]
        public async Task CaptureTreeStepFormat()
        {
            // Clear any existing data
            await G.V().Drop().ExecuteAsync();
            
            using (var tc = TestContext())
            {
                // Create a simple tree structure for testing
                var root = CreateItem(tc, "root", "Root Node", true);
                var child1 = CreateItem(tc, "child1", "Child 1", true);
                var child2 = CreateItem(tc, "child2", "Child 2", true);
                var grandchild1 = CreateItem(tc, "grandchild1", "Grandchild 1", true);
                var grandchild2 = CreateItem(tc, "grandchild2", "Grandchild 2", true);

                await tc.SaveChangesAsync();

                // Create parent-child relationships
                root.Children.Add(child1);
                root.Children.Add(child2);
                child1.Children.Add(grandchild1);
                child1.Children.Add(grandchild2);

                await tc.SaveChangesAsync();

                // Test different tree queries to capture the format
                await TestBasicTreeQuery();
                await TestTreeWithRepeat();
                await TestTreeWithUntil();
            }
        }

        private async Task TestBasicTreeQuery()
        {
            _output.WriteLine("=== Basic Tree Query ===");
            
            try
            {
                // Simple tree query
                var query = G.V("root").Out("parent").Tree();
                var result = await query.ExecuteAsync();
                
                _output.WriteLine($"Query: {query}");
                _output.WriteLine($"Result Type: {result?.GetType()?.FullName}");
                _output.WriteLine($"Result Count: {result?.Count()}");
                _output.WriteLine("Raw Result:");
                _output.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
                
                if (result?.Any() == true)
                {
                    var firstResult = result.First();
                    _output.WriteLine($"First Result Type: {firstResult?.GetType()?.FullName}");
                    _output.WriteLine("First Result:");
                    _output.WriteLine(JsonConvert.SerializeObject(firstResult, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error in basic tree query: {ex.Message}");
            }
        }

        private async Task TestTreeWithRepeat()
        {
            _output.WriteLine("\n=== Tree with Repeat Query ===");
            
            try
            {
                // Tree with repeat pattern (like GraphContextBase.GetTreeAsync uses)
                var query = G.V("root").Repeat(p => p.Out("parent")).Until(p => p.OutE("parent").Count().Is(0)).Tree();
                var result = await query.ExecuteAsync();
                
                _output.WriteLine($"Query: {query}");
                _output.WriteLine($"Result Type: {result?.GetType()?.FullName}");
                _output.WriteLine($"Result Count: {result?.Count()}");
                _output.WriteLine("Raw Result:");
                _output.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
                
                if (result?.Any() == true)
                {
                    var firstResult = result.First();
                    _output.WriteLine($"First Result Type: {firstResult?.GetType()?.FullName}");
                    _output.WriteLine("First Result:");
                    _output.WriteLine(JsonConvert.SerializeObject(firstResult, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error in tree with repeat query: {ex.Message}");
            }
        }

        private async Task TestTreeWithUntil()
        {
            _output.WriteLine("\n=== Tree with Until Query ===");
            
            try
            {
                // Alternative tree pattern
                var query = G.V("root").Until(p => p.OutE("parent").Count().Is(0)).Repeat(p => p.Out("parent")).Tree();
                var result = await query.ExecuteAsync();
                
                _output.WriteLine($"Query: {query}");
                _output.WriteLine($"Result Type: {result?.GetType()?.FullName}");
                _output.WriteLine($"Result Count: {result?.Count()}");
                _output.WriteLine("Raw Result:");
                _output.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
                
                if (result?.Any() == true)
                {
                    var firstResult = result.First();
                    _output.WriteLine($"First Result Type: {firstResult?.GetType()?.FullName}");
                    _output.WriteLine("First Result:");
                    _output.WriteLine(JsonConvert.SerializeObject(firstResult, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error in tree with until query: {ex.Message}");
            }
        }

        private TestContext TestContext()
        {
            JsonConvert.DefaultSettings = () =>
            {
                return new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Error = (sender, args) =>
                    {
                        Logging.DebugMessage("");
                    },

                }.RegisterGraphSerializer();
            };
            
            var tc = new TestContext(new GremlinNetLanguageConnector($"jonas-playground.gremlin.cosmos.azure.com", "graphTest", "graphTest", ConfigurationManagerHelper.GetValueOnKey("cosmosDbKey")));
            tc.OnDisposing = c =>
            {
                c.SaveChangesError -= OnTcOnSaveChangesError;
                c.SavingChanges -= OnTcOnSavingChanges;
                c.ChangesSaved -= OnTcOnChangesSaved;
            };
            tc.Disposing += Tc_Disposing;
            tc.SavingChanges += OnTcOnSavingChanges;
            tc.ChangesSaved += OnTcOnChangesSaved;
            tc.SaveChangesError += OnTcOnSaveChangesError;
            return tc;
        }

        private void Tc_Disposing(GraphContextBase sender)
        {
            _output.WriteLine($"Total RU Consumption {sender.ConsumedRU}");
        }

        private void OnTcOnSavingChanges(object sender, SaveEventArgs args)
        {
            _output.WriteLine($"saving changes with {args.TrackedItems.Count()} tracked items");
        }

        private void OnTcOnChangesSaved(object sender, SaveEventArgs args)
        {
            _output.WriteLine($"saved changes with {args.TrackedItems.Count()} tracked items");
        }

        private void OnTcOnSaveChangesError(object sender, SaveEventArgs args)
        {
            _output.WriteLine($"failed statement: {args.FailedUpdateStatement}");
            _output.WriteLine($"saving changes failed with message: {args.Error.Message}");
            _output.WriteLine(args.Error.StackTrace);
        }

        private static IProfile CreateItem(TestContext tc, string id, string occupation = null, bool isAdult = false)
        {
            var item = tc.CreateEntity<IProfile>(id);
            item.Name = id;
            item.Pk = id;
            item.FirstName = id;
            item.Ocupation = occupation;
            item.Adult = isAdult;
            return item;
        }

        public void Dispose()
        {
            _scope.TryDispose();
        }
    }
}