using Stardust.Nucleus;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;
using System;
using System.Collections.Generic;

namespace Stardust.Paradox.CosmosDbTest
{
    public class TestContext : GraphContextBase
    {
        public TestContext(IGremlinLanguageConnector connector) : base(connector, Resolver.CreateScopedResolver())
        {
        }
        static TestContext()
        {
            PartitionKeyName = "pk";
        }
        protected override void Dispose(bool disposing)
        {
			OnDisposing?.Invoke(this);
            base.Dispose(disposing);
            ServiceProvider.TryDispose();

        }

	    public Action<TestContext> OnDisposing { get; set; }
	    
    
        protected override bool InitializeModel(IGraphConfiguration configuration)
        {
            configuration.ConfigureCollection<IProfile>()
                .In(t=>t.Parents,"parent").Out(t=>t.Children)
                //.AddInEdge(t => t.Parents, "parent").Out<IProfile>(t => t.Children)
                .AddQuery(t => t.AllSiblings, g => g.V("{id}").As("s").In("parent").Out("parent").Dedup());
            configuration.ConfigureCollection<ICompany>().Out(t => t.Employees, "employer").In(t => t.Employers);//.AddOutEdge(t=>t.Employees, "employer").In<IProfile>(t=>t.Employers);
            configuration.ConfigureCollection<IEmployment>();

            return true;
        }

        public IGraphSet<IProfile> Profiles => GraphSet<IProfile>();

        public IGraphSet<ICompany> Companies => GraphSet<ICompany>();

        public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
    }

    [EdgeLabel("employer")]
    public interface IEmployment : IEdge<IProfile, ICompany>,IDynamicGraphEntity
    {
        string Id { get; }

        DateTime HiredDate { get; set; }

        string Manager { get; set; }

		[InlineSerialization(SerializationType.ClearText)]
		ICollection<string> TestInline { get; set; }
    }
}