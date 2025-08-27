using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using Stardust.Paradox.Data.MockTester.Models;
using Stardust.Paradox.Data.Mocker;
using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.MockTester
{
    public interface IMockTestContext: IGraphContext
    {
        IGraphSet<IProfile> Profiles { get; }
        IGraphSet<ICompany> Companies { get; }
        IEdgeGraphSet<IEmployment> Employments { get; }
        double ConsumedRU { get; }
    }

    public class MockTestContext : GraphContextBase, IMockTestContext
    {
        private static bool _modelInitialized = false;
        private static readonly object _lockObject = new object();

        public MockGremlinLanguageConnector MockConnector { get; }

        public MockTestContext(MockGremlinLanguageConnector connector) : base(connector, null, null)
        {
            MockConnector = connector;
        }

        static MockTestContext()
        {
            PartitionKeyName = "pk";
        }

        protected override void Dispose(bool disposing)
        {
            OnDisposing?.Invoke(this);
            base.Dispose(disposing);
        }

        public Action<MockTestContext> OnDisposing { get; set; }

        protected override bool InitializeModel(IGraphConfiguration configuration)
        {
            lock (_lockObject)
            {
                if (_modelInitialized)
                    return true;

                try
                {
                    configuration.ConfigureCollection<IProfile>()
                        .In(t => t.Parents, "parent").Out(t => t.Children)
                        .AddQuery(t => t.AllSiblings, g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());
                        
                    configuration.ConfigureCollection<ICompany>()
                        .Out(t => t.Employees, "employer").In(t => t.Employers);
                        
                    configuration.ConfigureCollection<IEmployment>();

                    _modelInitialized = true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Model already initialized by another test context, ignore
                    _modelInitialized = true;
                }
                catch (ArgumentException)
                {
                    // Type already registered, ignore
                    _modelInitialized = true;
                }
                catch (Exception ex)
                {
                    // Log the exception but don't fail - continue with already initialized model
                    System.Diagnostics.Debug.WriteLine($"Model initialization warning: {ex.Message}");
                    _modelInitialized = true;
                }
            }

            return true;
        }

        public IGraphSet<IProfile> Profiles => GraphSet<IProfile>();

        public IGraphSet<ICompany> Companies => GraphSet<ICompany>();

        public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
    }
}