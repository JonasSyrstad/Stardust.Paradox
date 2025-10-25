using FluentAssertions.Common;
using Microsoft.Extensions.DependencyInjection;
using Stardust.Paradox.Data.Linq.Tests.Models;
using Xunit.Sdk;

namespace Stardust.Paradox.Data.Linq.Tests
{
    public class LinqTestContext : GraphContextBase
    {
        public LinqTestContext(IGremlinLanguageConnector connector) : base(connector, CreateServiceProvider())
        {
        }

        static LinqTestContext()
        {
            PartitionKeyName = "pk";
        }
        private static bool _isInitialized = false;
        private static object _lock = new object();

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
        protected override bool InitializeModel(IGraphConfiguration configuration)
        {
            if (_isInitialized) return true;
            // Configure all entities
            lock (_lock)
            {
                
                if(_isInitialized) return true;
                configuration.ConfigureCollection<IPerson>();
                configuration.ConfigureCollection<ICompany>();
                configuration.ConfigureCollection<IProject>();
                configuration.ConfigureCollection<ISkill>();
                configuration.ConfigureCollection<IEmployment>();
                configuration.ConfigureCollection<IFriendship>();
                configuration.ConfigureCollection<IAssignment>();
                configuration.ConfigureCollection<IUserSkill>();
                
                _isInitialized = true;
            }


            return true;
        }

        public IGraphSet<IPerson> People => GraphSet<IPerson>();
        public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
        public IGraphSet<IProject> Projects => GraphSet<IProject>();
        public IGraphSet<ISkill> Skills => GraphSet<ISkill>();

        public IEdgeGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();
        public IEdgeGraphSet<IFriendship> Friendships => EdgeGraphSet<IFriendship>();
        public IEdgeGraphSet<IAssignment> Assignments => EdgeGraphSet<IAssignment>();
        public IEdgeGraphSet<IUserSkill> UserSkills => EdgeGraphSet<IUserSkill>();
    }
}
