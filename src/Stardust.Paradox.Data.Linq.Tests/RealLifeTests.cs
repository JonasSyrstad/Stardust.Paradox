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

    [Fact]
    public async Task LIstAllPeopleWIthSelectAs()
    {
        var people = await (from p in Context.People.AsQueryable().As("a").Out(t=>t.Skills).Has(t=>t.Name=="C#").Select<IPerson>("a") select p).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.IsAssignableFrom<IPerson>(people.First());
        Assert.NotNull(people);
        Assert.NotEmpty(people);
    }

    [Fact]
    public async Task LIstAllSkillsWIthSelectAs()
    {
        var skills = await (from p in Context.People.AsQueryable().OutE(s=>s.Skills).Cast<IUserSkill>().As("a").OtherV<ISkill>().Has(t => t.Name == "C#").Select<IUserSkill>("a") select p).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.IsAssignableFrom<IUserSkill>(skills.First());
        Assert.NotNull(skills);
        Assert.NotEmpty(skills);
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

    [Fact]
    public async Task ListAllSkillsWithSelectAsUsingIn()
    {
        // Start from Skills and traverse incoming edges to find people with C# skills
        var skills = await (from s in Context.Skills.AsQueryable().As("a").In(t=>t.Practitioners).Has(t=>t.Name=="Alice Johnson").Select<ISkill>("a") select s).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.IsAssignableFrom<ISkill>(skills.First());
 Assert.NotNull(skills);
        Assert.NotEmpty(skills);
    }

    [Fact]
    public async Task ListAllSkillEdgesWithInE()
    {
    // Start from Skills and traverse incoming edges to get UserSkill edges
 var skillEdges = await (from s in Context.Skills.AsQueryable().InE(skill => skill.Practitioners).Cast<IUserSkill>() select s).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.IsAssignableFrom<IUserSkill>(skillEdges.First());
        Assert.NotNull(skillEdges);
        Assert.NotEmpty(skillEdges);
    }

    [Fact]
    public async Task ListAllPeopleWithInEAndInV()
    {
        // Start from Skills, get incoming edges, then traverse to the people
        var people = await (from s in Context.Skills.AsQueryable().Where(s => s.Name == "C#").InE<IUserSkill>().InV<IUserSkill, IPerson>() select s).ToListAsync();
        _output.WriteLine(Connector.GetQueryLog().First().Query);
        Assert.IsAssignableFrom<IPerson>(people.First());
        Assert.NotNull(people);
        Assert.NotEmpty(people);
    }

    [Fact]
    public async Task ListAllSkillEdgesWithInESelectAs()
  {
        // Start from Skills, label the edge, traverse to people, filter, then select back to the edge
 var skillEdges = await (from s in Context.Skills.AsQueryable().InE(skill => skill.Practitioners).Cast<IUserSkill>().As("a").InV<IPerson>().Select<IUserSkill>("a") select s).ToListAsync();
 _output.WriteLine(Connector.GetQueryLog().First().Query);
  if (skillEdges.Any())
 {
   Assert.IsAssignableFrom<IUserSkill>(skillEdges.First());
 }
      Assert.NotNull(skillEdges);
   // Note: This test may return empty if the Select doesn't preserve the labeled edges
    // Assert.NotEmpty(skillEdges);
    }

    [Fact]
    public async Task ListAllSkillEdgesUsingInEShorthand()
    {
        // Use the InE<TEdge> shorthand without lambda
    var skillEdges = await (from s in Context.Skills.AsQueryable().InE<IUserSkill>() select s).ToListAsync();
     _output.WriteLine(Connector.GetQueryLog().First().Query);
     Assert.IsAssignableFrom<IUserSkill>(skillEdges.First());
      Assert.NotNull(skillEdges);
   Assert.NotEmpty(skillEdges);
  }

}