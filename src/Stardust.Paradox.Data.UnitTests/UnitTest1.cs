using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Internals;
using System;
using Xunit;

namespace Stardust.Paradox.Data.UnitTests
{
    public class UnitTest1
    {
        [Fact]
        public void CreateInstance()
        {
            var entityType = CodeGenerator.MakeEdgeDataEntity(typeof(ITestEdge), "test");
            Assert.NotNull(entityType);
            var i = Activator.CreateInstance(entityType) as ITestEdge;
            (i as EdgeDataEntity<IIn, IOut>).Reset(true);
            i.Email = "test@something.com";
            i.Name = "Someone";

            Assert.NotNull(i);
        }
    }

    public interface ITestEdge : IEdge<IIn, IOut>
    {
        string Id { get; }

        string Name { get; set; }

        string Email { get; set; }
    }

    public interface IIn : IVertex
    {
    }

    public interface IOut : IVertex
    {
    }
}
