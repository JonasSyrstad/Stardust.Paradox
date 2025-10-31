using Stardust.Paradox.Data.Linq.Tests.Models;
using Stardust.Particles;
using Xunit.Abstractions;

namespace Stardust.Paradox.Data.Linq.Tests;

public class RealLifeTests : LinqTestBase
{
    private readonly ITestOutputHelper _output;

    public RealLifeTests(ITestOutputHelper output)
    {
        _output = output;
    }
    [Fact]
    public async Task LIstAllPeople()
    {
        var people = await (from p in Context.People.AsQueryable() select p).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.NotNull(people);
        Assert.NotEmpty(people);
    }

    [Fact()]
    public async Task LIstAllSkillEdges()
    {
        var people = await (from p in Context.People.AsQueryable() 
                select p.OutE(person=>person.Skills)//fetches the edges as the base interface IEdge
                    .Cast<IUserSkill>()//cast to the specific edge type
            ).ToListAsync();
        var firstSkill=people.First();
        Assert.IsAssignableFrom<IUserSkill>(firstSkill);
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.NotNull(people);
        Assert.NotEmpty(people);
    }

    [Fact]
    public async Task LIstAllSkillEdges2()
    {
        var people = await (from p in Context.People.AsQueryable().OutE<IUserSkill>() select p).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.NotNull(people);
        Assert.NotEmpty(people);
    }

    [Fact()]
    public async Task LIstAllSkillEdges3()
    {
        var people = await (from s in Context.Skills.AsQueryable()
                select s.InE(skill => skill.Practitioners)//fetches the edges as the base interface IEdge
                    .Cast<IUserSkill>()//cast to the specific edge type
            ).ToListAsync();
        var firstSkill = people.First();
        Assert.IsAssignableFrom<IUserSkill>(firstSkill);
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.NotNull(people);
        Assert.NotEmpty(people);
    }

    [Fact]
    public async Task LIstAllSkillEdges4()
    {
        // InE on a vertex finds incoming edges. hasSkill edges go FROM person TO skill
        // So we need to query from Skills to find incoming hasSkill edges
        var people = await (from s in Context.Skills.AsQueryable().InE<IUserSkill>() select s).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.NotNull(people);
        Assert.NotEmpty(people);
    }

    [Fact]
    public async Task ListAllPeopleOver25()
    {
        var people = await (from p in Context.People.AsQueryable()
            where p.Age > 25
            select p).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
            
        Assert.NotNull(people);
        Assert.NotEmpty(people);
        Assert.True(people.All(p => p.Age > 25));
        Assert.NotEmpty(Connector.GetQueryLog().First().Parameters);
    }

    [Fact]
    public async Task ListAllPeopleBetwee20and30()
    {
        var people = await (from p in Context.People.AsQueryable()
            where p.Age > 20 && p.Age < 30
            select new { p.Age,p.Name}).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);

        Assert.NotNull(people);
        Assert.NotEmpty(people);
        Assert.True(people.All(p => p.Age > 20 && p.Age<30));
        Assert.NotEmpty(Connector.GetQueryLog().First().Parameters);
    }

}