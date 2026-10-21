using Stardust.Paradox.GremlinStudio.Core.Completion;

namespace Stardust.Paradox.GremlinStudioTests;

/// <summary>
/// Schema: (person)-[knows]->(person), (person)-[created]->(software), (company)-[employs]->(person).
/// </summary>
public class GremlinCompletionAnalyzerTests
{
    private static readonly GremlinCompletionSchema Schema = new(
        [("person", 4), ("software", 2), ("company", 1)],
        [("knows", 2), ("created", 4), ("employs", 1)],
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["person"] = ["name", "age"],
            ["software"] = ["name", "lang"],
            ["company"] = ["companyName"]
        },
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["knows"] = ["weight"],
            ["created"] = ["since"],
            ["employs"] = ["role"]
        },
        [
            new GremlinEdgeConnection("knows", "person", "person"),
            new GremlinEdgeConnection("created", "person", "software"),
            new GremlinEdgeConnection("employs", "company", "person")
        ]);

    private static GremlinArgumentCompletion? Analyze(string text) => GremlinCompletionAnalyzer.Analyze(text, Schema);

    [Theory]
    [InlineData("g.V().hasLabel('person').has('", "age,id,label,name")]
    [InlineData("g.V().hasLabel('software').has(", "id,label,lang,name")]
    [InlineData("g.V().hasLabel('person','company').hasNot('", "age,companyName,id,label,name")]
    [InlineData("g.V().has('software', '", "id,label,lang,name")]
    [InlineData("g.V().hasLabel('person').out('created').values('", "lang,name")]
    [InlineData("g.V().hasLabel('person').in('employs').values('", "companyName")]
    [InlineData("g.V().hasLabel('person').outE('created').has('", "id,label,since")]
    [InlineData("g.V().hasLabel('person').inE().valueMap('", "role,weight")]
    [InlineData("g.V().hasLabel('company').outE('employs').inV().properties('", "age,name")]
    [InlineData("g.V().hasLabel('company').outE().otherV().values('", "age,name")]
    [InlineData("g.E().hasLabel('created').outV().elementMap('", "age,name")]
    [InlineData("g.V().hasLabel('person').order().by('", "age,name")]
    [InlineData("g.V().hasLabel('software').as('s').in('created').select('s').values('", "lang,name")]
    [InlineData("g.V().hasLabel('person').where(out('knows')).values('", "age,name")]
    [InlineData("g.V().hasLabel('person').property(single, '", "age,name")]
    [InlineData("g.V().hasLabel('software').properties().hasKey('", "lang,name")]
    public void PropertyKeysFollowTraversalState(string text, string expected)
    {
        var result = Analyze(text);

        Assert.Equal(expected, string.Join(",", result!.PropertyKeys));
    }

    [Theory]
    [InlineData("g.V().has('name', '")]
    [InlineData("g.V().has('name', ")]
    [InlineData("g.V().has('person', 'name', '")]
    [InlineData("g.V().property('name', '")]
    [InlineData("g.V().hasNot('name', ")]
    [InlineData("g.V().values('name').is('")]
    [InlineData("g.V().as('")]
    [InlineData("g.V().has(name")]
    public void ValueArgumentsAreNotCompleted(string text)
    {
        var result = Analyze(text);

        Assert.Null(result);
    }

    [Fact]
    public void ValuesStepProducesNoKeysForValueTraversers()
    {
        var result = Analyze("g.V().values('name').values('");

        Assert.Empty(result!.PropertyKeys);
    }

    [Theory]
    [InlineData("g.V().hasLabel('person').out('", "created,knows")]
    [InlineData("g.V().hasLabel('person').in('", "employs,knows")]
    [InlineData("g.V().hasLabel('software').in('", "created")]
    [InlineData("g.V().hasLabel('software').out('", "")]
    [InlineData("g.V().hasLabel('person','rare').out('", "created,employs,knows")]
    [InlineData("g.V().hasLabel('company').bothE('", "employs")]
    [InlineData("g.V().hasLabel('person').out('knows').outE('", "created,knows")]
    [InlineData("g.addE('", "created,employs,knows")]
    public void EdgeLabelsFollowDirection(string text, string expected)
    {
        var result = Analyze(text);

        Assert.Equal(expected, string.Join(",", result!.EdgeLabels));
    }

    [Theory]
    [InlineData("g.V().hasLabel('", "company,person,software", "")]
    [InlineData("g.E().hasLabel('", "", "created,employs,knows")]
    [InlineData("g.V().hasLabel('person').outE().hasLabel('", "", "created,knows")]
    [InlineData("g.V().hasLabel('person').out('created').hasLabel('", "software", "")]
    [InlineData("g.addV('", "company,person,software", "")]
    public void LabelsMatchElementKind(string text, string expectedVertices, string expectedEdges)
    {
        var result = Analyze(text);

        Assert.Equal((expectedVertices, expectedEdges), (string.Join(",", result!.VertexLabels), string.Join(",", result.EdgeLabels)));
    }

    [Fact]
    public void OpenQuotePrefixIsReported()
    {
        var result = Analyze("g.V().has('na");

        Assert.Equal((true, "na"), (result!.HasOpenQuote, result.Prefix));
    }

    [Fact]
    public void UnsampledLabelFallsBackToAllVertexKeys()
    {
        var result = Analyze("g.V().hasLabel('unknown').has('");

        Assert.Equal("age,companyName,id,label,lang,name", string.Join(",", result!.PropertyKeys));
    }

    [Theory]
    [InlineData("g.V().hasLabel('person').values('", "age,name")]
    [InlineData("g.V().hasLabel('person').valueMap('", "age,name")]
    [InlineData("g.V().hasLabel('person').property('", "age,name")]
    public void ElementTokensAreOnlyOfferedForFilters(string text, string expected)
    {
        var result = Analyze(text);

        Assert.Equal(expected, string.Join(",", result!.PropertyKeys));
    }

    [Fact]
    public void SelectOfUndeclaredLabelFallsBackToAllKeys()
    {
        var result = Analyze("g.V().hasLabel('person').select('x').values('");

        Assert.Equal("age,companyName,lang,name,role,since,weight", string.Join(",", result!.PropertyKeys));
    }

    [Fact]
    public void RepeatWidensToAnyLabelOfSameKind()
    {
        var result = Analyze("g.V().hasLabel('company').repeat(out('employs')).times(3).values('");

        Assert.Equal("age,companyName,lang,name", string.Join(",", result!.PropertyKeys));
    }

    [Theory]
    [InlineData("g.V().as('a').out().as('b').select('", "a,b")]
    [InlineData("g.V().as('a').out().where('", "a")]
    [InlineData("g.V().as('a').out().dedup('", "a")]
    [InlineData("g.V().as('a').V().addE('knows').from('", "a")]
    [InlineData("g.V().as('a').V().addE('knows').to('", "a")]
    public void StepLabelsAreOffered(string text, string expected)
    {
        var result = Analyze(text);

        Assert.Equal(expected, string.Join(",", result!.StepLabels));
    }

    [Fact]
    public void ChooseOptionsMergeBranchStates()
    {
        var result = Analyze("g.V().hasLabel('person').choose(values('age')).option(30, out('created')).option(none, out('knows')).values('");

        Assert.Equal("age,lang,name", string.Join(",", result!.PropertyKeys));
    }

    [Fact]
    public void ChooseOptionStartsFromChooseInput()
    {
        var result = Analyze("g.V().hasLabel('software').choose(values('lang')).option('java', values('");

        Assert.Equal("lang,name", string.Join(",", result!.PropertyKeys));
    }

    [Fact]
    public void SamplerParsesEndpoints()
    {
        var result = GremlinSchemaSampler.ParseEndpoints("[{\"o\":\"person\",\"i\":\"software\"}]");

        Assert.Equal([("person", "software")], result);
    }

    [Fact]
    public async Task SamplerQueriesRunOnInMemoryGraph()
    {
        using var connector = new Stardust.Paradox.Data.InMemory.InMemoryGremlinLanguageConnector();
        await connector.ExecuteAsync("g.addV('person').property('id','p1').property('name','a')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.addV('software').property('id','s1').property('lang','c')", new Dictionary<string, object>());
        await connector.ExecuteAsync("g.V('p1').addE('created').to(g.V('s1')).property('since', 1)", new Dictionary<string, object>());
        var executor = new Stardust.Paradox.GremlinStudio.Core.Execution.GremlinQueryExecutor(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Stardust.Paradox.GremlinStudio.Core.Execution.GremlinQueryExecutor>.Instance);

        var sample = await GremlinSchemaSampler.SampleAsync(
            executor, connector, ["person", "software"], ["created"],
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None);

        Assert.Equal(
            ("name", "since", "person->software"),
            (string.Join(",", sample.VertexPropertyKeys["person"]), string.Join(",", sample.EdgePropertyKeys["created"]),
             string.Join(",", sample.EdgeConnections.Select(c => $"{c.OutVertexLabel}->{c.InVertexLabel}"))));
    }
}
