using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.InMemory;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.InMemory.Tests.CosmosDbMigrated
{
    public class TestContext : GraphContextBase
    {
        
        private static bool _modelInitialized = false;
        private static readonly object _lockObject = new object();

        public TestContext(IGremlinLanguageConnector connector) : base(connector, CreateServiceProvider())
        {
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

        static TestContext()
        {
            PartitionKeyName = "pk";
        }

        protected override void Dispose(bool disposing)
        {
            OnDisposing?.Invoke(this);
            base.Dispose(disposing);
        }

        public Action<TestContext> OnDisposing { get; set; }

        protected override bool InitializeModel(IGraphConfiguration configuration)
        {
            lock (_lockObject)
            {
                if (_modelInitialized)
                    return false; // Skip initialization if already done

                try
                {
                    configuration.ConfigureCollection<IProfile>().In(t => t.Parents, "parent").Out(t => t.Children)
                        //.AddInEdge(t => t.Parents, "parent").Out<IProfile>(t => t.Children)
                        .AddQuery(t => t.AllSiblings, g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());
                    configuration.ConfigureCollection<ICompany>().Out(t => t.Employees, "employer").In(t => t.Employers);
                    configuration.ConfigureCollection<IEmployment>();

                    _modelInitialized = true;
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Binding already exists, skip
                    _modelInitialized = true;
                    return false;
                }
                catch (Exception ex)
                {
                    // Other errors, log and continue
                    System.Diagnostics.Debug.WriteLine($"Model initialization warning: {ex.Message}");
                    _modelInitialized = true;
                    return false;
                }
            }
        }

        public IGraphSet<IProfile> Profiles => GraphSet<IProfile>();

        public IGraphSet<ICompany> Companies => GraphSet<ICompany>();

        public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();

        public void NullPkName()
        {
            PartitionKeyName=null;
        }
        public void ResetPkName()
        {
            PartitionKeyName = "pk";
        }
    }

    [EdgeLabel("employer")]
    public interface IEmployment : IEdge<IProfile, ICompany>, IDynamicGraphEntity
    {
        string Id { get; }

        DateTime HiredDate { get; set; }

        string Manager { get; set; }

        [InlineSerialization(SerializationType.ClearText)]
        ICollection<string> TestInline { get; set; }
    }
}