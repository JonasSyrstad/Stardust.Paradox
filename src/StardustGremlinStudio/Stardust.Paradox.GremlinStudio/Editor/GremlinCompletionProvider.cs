using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Provides code completion data for Gremlin queries backed by <see cref="GremlinStepDefinition"/>.
/// When a step accepts parameters the cursor is placed between the parentheses.
/// </summary>
public class GremlinCompletionData : ICompletionData
{
    private readonly GremlinStepDefinition _definition;

    public GremlinCompletionData(GremlinStepDefinition definition)
    {
        _definition = definition;
        Text = definition.Name;
    }

    public string Text { get; }
    public GremlinCompletionKind Kind => _definition.Kind;
    public double Priority => (int)Kind;
    public ImageSource? Image => null;

    /// <summary>
    /// Returns a rich tooltip with description and overloads.
    /// </summary>
    public object Description => BuildDescriptionContent();

    /// <summary>
    /// Displayed in the completion list.
    /// </summary>
    public object Content => BuildContentElement();

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Traversal sources (g, __) don't get parentheses
        if (_definition.Kind == GremlinCompletionKind.TraversalSource)
        {
            textArea.Document.Replace(completionSegment, Text);
            return;
        }

        var insertText = $"{_definition.Name}()";
        textArea.Document.Replace(completionSegment, insertText);

        // If the step has parameter overloads, place cursor between the parentheses
        if (_definition.HasRequiredParameters)
        {
            // Move caret back one position (before the closing paren)
            textArea.Caret.Offset -= 1;
        }
    }

    private object BuildContentElement()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var kindLabel = new TextBlock
        {
            Text = Kind switch
            {
                GremlinCompletionKind.TraversalSource => "? ",
                GremlinCompletionKind.Step => "? ",
                GremlinCompletionKind.Predicate => "? ",
                GremlinCompletionKind.Modulator => "? ",
                _ => "  "
            },
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray
        };

        var nameBlock = new TextBlock
        {
            Text = _definition.Kind == GremlinCompletionKind.TraversalSource
                ? _definition.Name
                : $"{_definition.Name}()",
            FontFamily = new FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(kindLabel);
        panel.Children.Add(nameBlock);
        return panel;
    }

    private object BuildDescriptionContent()
    {
        return _definition.GetTooltipText();
    }
}

/// <summary>
/// Categories for Gremlin completion items.
/// </summary>
public enum GremlinCompletionKind
{
    TraversalSource = 0,
    Step = 1,
    Predicate = 2,
    Modulator = 3,
    Keyword = 4
}

/// <summary>
/// Provides Gremlin completion suggestions using <see cref="GremlinStepDatabase"/>
/// and variable completions from the active variable set.
/// </summary>
public static class GremlinCompletionProvider
{
    private static readonly List<GremlinCompletionData> AllCompletions = CreateCompletions();

    private static readonly object VariableLock = new();
    private static List<VariableCompletionData> _variableCompletions = new();

    /// <summary>
    /// Raw variable entries currently available. Used by the tooltip provider
    /// to resolve <c>${key}</c> values on hover.
    /// </summary>
    public static IReadOnlyList<(string Key, string Value, bool IsConnectionScoped)> ActiveVariables { get; private set; }
        = Array.Empty<(string, string, bool)>();

    /// <summary>
    /// Updates the active variable completions. Call when the selected variable
    /// set or connection changes.
    /// </summary>
    public static void SetActiveVariables(IReadOnlyList<(string Key, string Value, bool IsConnectionScoped)> variables)
    {
        lock (VariableLock)
        {
            ActiveVariables = variables;
            _variableCompletions = variables
                .Select(v => new VariableCompletionData(v.Key, v.Value, v.IsConnectionScoped))
                .ToList();
        }
    }

    /// <summary>
    /// Gets Gremlin step completion suggestions for the given context.
    /// </summary>
    public static IEnumerable<GremlinCompletionData> GetCompletions(string textBefore)
    {
        var lastDot = textBefore.LastIndexOf('.');
        var context = lastDot >= 0 ? textBefore[(lastDot + 1)..].Trim() : textBefore.Trim();

        if (string.IsNullOrEmpty(context))
        {
            return AllCompletions;
        }

        return AllCompletions
            .Where(c => c.Text.StartsWith(context, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Text);
    }

    /// <summary>
    /// Gets variable completion suggestions. Called when the user types <c>$</c> or <c>${</c>.
    /// </summary>
    public static IEnumerable<VariableCompletionData> GetVariableCompletions(string prefix)
    {
        List<VariableCompletionData> snapshot;
        lock (VariableLock)
        {
            snapshot = _variableCompletions;
        }

        if (string.IsNullOrEmpty(prefix))
        {
            return snapshot;
        }

        return snapshot
            .Where(v => v.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.Priority)
            .ThenBy(v => v.Text);
    }

    private static List<GremlinCompletionData> CreateCompletions()
    {
        return GremlinStepDatabase.All
            .Select(def => new GremlinCompletionData(def))
            .ToList();
    }
}
