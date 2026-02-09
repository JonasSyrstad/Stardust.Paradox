using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Provides syntax highlighting definitions for export preview formats.
/// Matches Visual Studio 2022 Dark theme color scheme.
/// </summary>
public static class ExportSyntaxHighlighting
{
    private static IHighlightingDefinition? _jsonDefinition;
    private static IHighlightingDefinition? _csharpDefinition;

    /// <summary>
    /// Gets the JSON syntax highlighting definition.
    /// </summary>
    public static IHighlightingDefinition JsonDefinition => _jsonDefinition ??= CreateJsonDefinition();

    /// <summary>
    /// Gets the C# syntax highlighting definition.
    /// </summary>
    public static IHighlightingDefinition CSharpDefinition => _csharpDefinition ??= CreateCSharpDefinition();

    private static IHighlightingDefinition CreateJsonDefinition()
    {
        var xshd = GetJsonXshd();
        using var reader = new StringReader(xshd);
        using var xmlReader = XmlReader.Create(reader);
        return HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
    }

    private static IHighlightingDefinition CreateCSharpDefinition()
    {
        var xshd = GetCSharpXshd();
        using var reader = new StringReader(xshd);
        using var xmlReader = XmlReader.Create(reader);
        return HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
    }

    /// <summary>
    /// JSON syntax highlighting matching VS 2022 Dark theme.
    /// </summary>
    private static string GetJsonXshd()
    {
        // VS 2022 Dark theme colors:
        // Strings: #D69D85 (salmon/orange)
        // Numbers: #B5CEA8 (light green)  
        // Keywords (true/false/null): #569CD6 (blue)
        // Punctuation: #DCDCDC (light gray)
        return """
            <?xml version="1.0"?>
            <SyntaxDefinition name="JSON" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                <Color name="String" foreground="#D69D85"/>
                <Color name="Number" foreground="#B5CEA8"/>
                <Color name="TrueFalse" foreground="#569CD6"/>
                <Color name="Null" foreground="#569CD6"/>
                <Color name="Punctuation" foreground="#DCDCDC"/>
                
                <RuleSet ignoreCase="false">
                    <Span color="String" multiline="false">
                        <Begin>"</Begin>
                        <End>"</End>
                        <RuleSet>
                            <Span begin="\\" end="."/>
                        </RuleSet>
                    </Span>
                    
                    <Keywords color="TrueFalse">
                        <Word>true</Word>
                        <Word>false</Word>
                    </Keywords>
                    
                    <Keywords color="Null">
                        <Word>null</Word>
                    </Keywords>
                    
                    <Rule color="Number">
                        \b-?[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?\b
                    </Rule>
                    
                    <Rule color="Punctuation">
                        [\[\]{}:,]
                    </Rule>
                </RuleSet>
            </SyntaxDefinition>
            """;
    }

    /// <summary>
    /// C# syntax highlighting matching VS 2022 Dark theme.
    /// </summary>
    private static string GetCSharpXshd()
    {
        // VS 2022 Dark theme colors:
        // Comments: #57A64A (green, italic)
        // Strings: #D69D85 (salmon/orange)
        // Numbers: #B5CEA8 (light green)
        // Keywords: #569CD6 (blue)
        // Control flow: #D8A0DF (purple/pink)
        // Types: #4EC9B0 (teal)
        // Interfaces: #B8D7A3 (light green)
        // Preprocessor: #9B9B9B (gray)
        return """
            <?xml version="1.0"?>
            <SyntaxDefinition name="CSharpVS" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                <Color name="Comment" foreground="#57A64A" fontStyle="italic"/>
                <Color name="String" foreground="#D69D85"/>
                <Color name="Char" foreground="#D69D85"/>
                <Color name="Number" foreground="#B5CEA8"/>
                <Color name="Keyword" foreground="#569CD6"/>
                <Color name="ControlKeyword" foreground="#D8A0DF"/>
                <Color name="ClassName" foreground="#4EC9B0"/>
                <Color name="InterfaceName" foreground="#B8D7A3"/>
                <Color name="Preprocessor" foreground="#9B9B9B"/>
                <Color name="Punctuation" foreground="#DCDCDC"/>
                <Color name="Operator" foreground="#B4B4B4"/>
                
                <RuleSet ignoreCase="false">
                    <Span color="Comment" multiline="false">
                        <Begin>///</Begin>
                    </Span>
                    
                    <Span color="Comment" multiline="true">
                        <Begin>/\*</Begin>
                        <End>\*/</End>
                    </Span>
                    
                    <Span color="Comment" multiline="false">
                        <Begin>//</Begin>
                    </Span>
                    
                    <Span color="Preprocessor" multiline="false">
                        <Begin>\#</Begin>
                    </Span>
                    
                    <Span color="String" multiline="true">
                        <Begin>@"</Begin>
                        <End>"</End>
                    </Span>
                    
                    <Span color="String" multiline="false">
                        <Begin>"</Begin>
                        <End>"</End>
                        <RuleSet>
                            <Span begin="\\" end="."/>
                        </RuleSet>
                    </Span>
                    
                    <Span color="Char" multiline="false">
                        <Begin>'</Begin>
                        <End>'</End>
                        <RuleSet>
                            <Span begin="\\" end="."/>
                        </RuleSet>
                    </Span>
                    
                    <Keywords color="ControlKeyword">
                        <Word>if</Word>
                        <Word>else</Word>
                        <Word>switch</Word>
                        <Word>case</Word>
                        <Word>default</Word>
                        <Word>for</Word>
                        <Word>foreach</Word>
                        <Word>while</Word>
                        <Word>do</Word>
                        <Word>break</Word>
                        <Word>continue</Word>
                        <Word>return</Word>
                        <Word>throw</Word>
                        <Word>try</Word>
                        <Word>catch</Word>
                        <Word>finally</Word>
                        <Word>goto</Word>
                        <Word>yield</Word>
                        <Word>await</Word>
                        <Word>when</Word>
                    </Keywords>
                    
                    <Keywords color="Keyword">
                        <Word>abstract</Word>
                        <Word>as</Word>
                        <Word>async</Word>
                        <Word>base</Word>
                        <Word>bool</Word>
                        <Word>byte</Word>
                        <Word>char</Word>
                        <Word>checked</Word>
                        <Word>class</Word>
                        <Word>const</Word>
                        <Word>decimal</Word>
                        <Word>delegate</Word>
                        <Word>double</Word>
                        <Word>dynamic</Word>
                        <Word>enum</Word>
                        <Word>event</Word>
                        <Word>explicit</Word>
                        <Word>extern</Word>
                        <Word>false</Word>
                        <Word>fixed</Word>
                        <Word>float</Word>
                        <Word>implicit</Word>
                        <Word>in</Word>
                        <Word>int</Word>
                        <Word>interface</Word>
                        <Word>internal</Word>
                        <Word>is</Word>
                        <Word>lock</Word>
                        <Word>long</Word>
                        <Word>namespace</Word>
                        <Word>new</Word>
                        <Word>null</Word>
                        <Word>object</Word>
                        <Word>operator</Word>
                        <Word>out</Word>
                        <Word>override</Word>
                        <Word>params</Word>
                        <Word>private</Word>
                        <Word>protected</Word>
                        <Word>public</Word>
                        <Word>readonly</Word>
                        <Word>record</Word>
                        <Word>ref</Word>
                        <Word>sbyte</Word>
                        <Word>sealed</Word>
                        <Word>short</Word>
                        <Word>sizeof</Word>
                        <Word>stackalloc</Word>
                        <Word>static</Word>
                        <Word>string</Word>
                        <Word>struct</Word>
                        <Word>this</Word>
                        <Word>true</Word>
                        <Word>typeof</Word>
                        <Word>uint</Word>
                        <Word>ulong</Word>
                        <Word>unchecked</Word>
                        <Word>unsafe</Word>
                        <Word>ushort</Word>
                        <Word>using</Word>
                        <Word>var</Word>
                        <Word>virtual</Word>
                        <Word>void</Word>
                        <Word>volatile</Word>
                        <Word>where</Word>
                        <Word>get</Word>
                        <Word>set</Word>
                        <Word>init</Word>
                        <Word>add</Word>
                        <Word>remove</Word>
                        <Word>value</Word>
                        <Word>partial</Word>
                        <Word>global</Word>
                        <Word>nameof</Word>
                        <Word>with</Word>
                        <Word>required</Word>
                        <Word>file</Word>
                        <Word>scoped</Word>
                    </Keywords>
                    
                    <Keywords color="ClassName">
                        <Word>DateTime</Word>
                        <Word>DateTimeOffset</Word>
                        <Word>DateOnly</Word>
                        <Word>TimeOnly</Word>
                        <Word>TimeSpan</Word>
                        <Word>Guid</Word>
                        <Word>Uri</Word>
                        <Word>String</Word>
                        <Word>Object</Word>
                        <Word>Boolean</Word>
                        <Word>Byte</Word>
                        <Word>SByte</Word>
                        <Word>Char</Word>
                        <Word>Decimal</Word>
                        <Word>Double</Word>
                        <Word>Single</Word>
                        <Word>Int16</Word>
                        <Word>Int32</Word>
                        <Word>Int64</Word>
                        <Word>UInt16</Word>
                        <Word>UInt32</Word>
                        <Word>UInt64</Word>
                        <Word>Array</Word>
                        <Word>List</Word>
                        <Word>Dictionary</Word>
                        <Word>HashSet</Word>
                        <Word>Queue</Word>
                        <Word>Stack</Word>
                        <Word>Task</Word>
                        <Word>ValueTask</Word>
                        <Word>Span</Word>
                        <Word>Memory</Word>
                        <Word>Func</Word>
                        <Word>Action</Word>
                        <Word>Exception</Word>
                        <Word>Console</Word>
                        <Word>Math</Word>
                        <Word>File</Word>
                        <Word>Directory</Word>
                        <Word>Path</Word>
                        <Word>StringBuilder</Word>
                        <Word>Regex</Word>
                        <Word>Type</Word>
                        <Word>Attribute</Word>
                        <Word>Enum</Word>
                        <Word>Convert</Word>
                        <Word>Random</Word>
                        <Word>Lazy</Word>
                        <Word>CancellationToken</Word>
                        <Word>CancellationTokenSource</Word>
                        <Word>Stopwatch</Word>
                    </Keywords>
                    
                    <Keywords color="InterfaceName">
                        <Word>IEnumerable</Word>
                        <Word>IEnumerator</Word>
                        <Word>ICollection</Word>
                        <Word>IList</Word>
                        <Word>IDictionary</Word>
                        <Word>ISet</Word>
                        <Word>IDisposable</Word>
                        <Word>IAsyncDisposable</Word>
                        <Word>IComparable</Word>
                        <Word>IEquatable</Word>
                        <Word>ICloneable</Word>
                        <Word>IFormattable</Word>
                        <Word>IObservable</Word>
                        <Word>IObserver</Word>
                        <Word>IProgress</Word>
                        <Word>IServiceProvider</Word>
                        <Word>ILogger</Word>
                        <Word>IConfiguration</Word>
                        <Word>IOptions</Word>
                    </Keywords>
                    
                    <Rule color="Number">
                        \b0[xX][0-9a-fA-F_]+[uUlL]*\b
                    </Rule>
                    <Rule color="Number">
                        \b0[bB][01_]+[uUlL]*\b
                    </Rule>
                    <Rule color="Number">
                        \b[0-9][0-9_]*(\.[0-9][0-9_]*)?([eE][+-]?[0-9_]+)?[fFdDmM]?[uUlL]*\b
                    </Rule>
                    
                    <Rule color="Punctuation">
                        [\[\]{}();,.]
                    </Rule>
                </RuleSet>
            </SyntaxDefinition>
            """;
    }
}
