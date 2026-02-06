using System.IO;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Provides Gremlin syntax highlighting for AvalonEdit.
/// </summary>
public static class GremlinSyntaxHighlighting
{
    private static IHighlightingDefinition? _definition;

    /// <summary>
    /// Gets the Gremlin syntax highlighting definition.
    /// </summary>
    public static IHighlightingDefinition Definition => _definition ??= CreateDefinition();

    private static IHighlightingDefinition CreateDefinition()
    {
        var xshd = GetGremlinXshd();
        using var reader = new StringReader(xshd);
        using var xmlReader = XmlReader.Create(reader);
        return HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
    }

    /// <summary>
    /// Registers the Gremlin highlighting with the HighlightingManager.
    /// </summary>
    public static void Register()
    {
        HighlightingManager.Instance.RegisterHighlighting(
            "Gremlin",
            [".gremlin", ".groovy"],
            Definition);
    }

    private static string GetGremlinXshd()
    {
        return """
<?xml version="1.0"?>
<SyntaxDefinition name="Gremlin" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
    <Color name="Comment" foreground="#6A9955" fontStyle="italic"/>
    <Color name="String" foreground="#CE9178"/>
    <Color name="Number" foreground="#B5CEA8"/>
    <Color name="Keyword" foreground="#569CD6" fontWeight="bold"/>
    <Color name="TraversalSource" foreground="#4EC9B0" fontWeight="bold"/>
    <Color name="Step" foreground="#DCDCAA"/>
    <Color name="Predicate" foreground="#C586C0"/>
    <Color name="Modulator" foreground="#9CDCFE"/>

    <RuleSet>
        <!-- Comments - single line only -->
        <Span color="Comment" begin="//" />
        
        <!-- Strings -->
        <Span color="String" begin="'" end="'"/>
        <Span color="String" begin="&quot;" end="&quot;"/>
        
        <!-- Numbers -->
        <Rule color="Number">
            \b\d+(\.\d+)?\b
        </Rule>
        
        <!-- Traversal Source -->
        <Keywords color="TraversalSource">
            <Word>g</Word>
        </Keywords>
        
        <!-- Main Steps -->
        <Keywords color="Step">
            <Word>V</Word>
            <Word>E</Word>
            <Word>addV</Word>
            <Word>addE</Word>
            <Word>drop</Word>
            <Word>property</Word>
            <Word>properties</Word>
            <Word>values</Word>
            <Word>valueMap</Word>
            <Word>elementMap</Word>
            <Word>id</Word>
            <Word>label</Word>
            <Word>constant</Word>
            <Word>inject</Word>
            <Word>out</Word>
            <Word>in</Word>
            <Word>both</Word>
            <Word>outE</Word>
            <Word>inE</Word>
            <Word>bothE</Word>
            <Word>outV</Word>
            <Word>inV</Word>
            <Word>bothV</Word>
            <Word>otherV</Word>
            <Word>has</Word>
            <Word>hasNot</Word>
            <Word>hasLabel</Word>
            <Word>hasId</Word>
            <Word>hasKey</Word>
            <Word>hasValue</Word>
            <Word>is</Word>
            <Word>where</Word>
            <Word>not</Word>
            <Word>and</Word>
            <Word>or</Word>
            <Word>filter</Word>
            <Word>dedup</Word>
            <Word>limit</Word>
            <Word>skip</Word>
            <Word>range</Word>
            <Word>tail</Word>
            <Word>coin</Word>
            <Word>sample</Word>
            <Word>map</Word>
            <Word>flatMap</Word>
            <Word>select</Word>
            <Word>project</Word>
            <Word>unfold</Word>
            <Word>fold</Word>
            <Word>count</Word>
            <Word>sum</Word>
            <Word>max</Word>
            <Word>min</Word>
            <Word>mean</Word>
            <Word>group</Word>
            <Word>groupCount</Word>
            <Word>order</Word>
            <Word>path</Word>
            <Word>tree</Word>
            <Word>match</Word>
            <Word>math</Word>
            <Word>local</Word>
            <Word>optional</Word>
            <Word>union</Word>
            <Word>coalesce</Word>
            <Word>choose</Word>
            <Word>repeat</Word>
            <Word>until</Word>
            <Word>emit</Word>
            <Word>times</Word>
            <Word>loops</Word>
            <Word>sideEffect</Word>
            <Word>cap</Word>
            <Word>store</Word>
            <Word>aggregate</Word>
            <Word>subgraph</Word>
            <Word>toList</Word>
            <Word>toSet</Word>
            <Word>next</Word>
            <Word>iterate</Word>
            <Word>explain</Word>
            <Word>profile</Word>
        </Keywords>
        
        <!-- Predicates -->
        <Keywords color="Predicate">
            <Word>eq</Word>
            <Word>neq</Word>
            <Word>lt</Word>
            <Word>lte</Word>
            <Word>gt</Word>
            <Word>gte</Word>
            <Word>inside</Word>
            <Word>outside</Word>
            <Word>between</Word>
            <Word>within</Word>
            <Word>without</Word>
            <Word>containing</Word>
            <Word>startingWith</Word>
            <Word>endingWith</Word>
            <Word>notContaining</Word>
            <Word>notStartingWith</Word>
            <Word>notEndingWith</Word>
            <Word>regex</Word>
        </Keywords>
        
        <!-- Modulators -->
        <Keywords color="Modulator">
            <Word>as</Word>
            <Word>by</Word>
            <Word>from</Word>
            <Word>to</Word>
            <Word>with</Word>
            <Word>option</Word>
        </Keywords>
        
        <!-- Keywords -->
        <Keywords color="Keyword">
            <Word>asc</Word>
            <Word>desc</Word>
            <Word>incr</Word>
            <Word>decr</Word>
            <Word>shuffle</Word>
            <Word>true</Word>
            <Word>false</Word>
            <Word>null</Word>
            <Word>Scope</Word>
            <Word>global</Word>
            <Word>T</Word>
            <Word>Order</Word>
            <Word>Column</Word>
            <Word>Pop</Word>
            <Word>Cardinality</Word>
            <Word>single</Word>
            <Word>list</Word>
            <Word>set</Word>
        </Keywords>
    </RuleSet>
</SyntaxDefinition>
""";
    }
}
