using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Completion item for a vertex or edge label discovered from database statistics.
/// </summary>
public sealed class LabelCompletionData : ICompletionData
{
    private readonly bool _isEdge;
    private readonly long _count;
    private readonly bool _wrapInQuotes;

    public LabelCompletionData(string label, bool isEdge, long count, bool wrapInQuotes)
    {
        Text = label;
        _isEdge = isEdge;
        _count = count;
        _wrapInQuotes = wrapInQuotes;
    }

    public string Text { get; }
    public bool WrapsInQuotes => _wrapInQuotes;
    public ImageSource? Image => null;
    public double Priority => _isEdge ? 1 : 0;

    public object Content
    {
        get
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = _isEdge ? "E " : "V ",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = Text,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            });
            return panel;
        }
    }

    public object Description => $"{(_isEdge ? "Edge" : "Vertex")} label '{Text}' ({_count:N0})";

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var escaped = Text.Replace("\\", "\\\\").Replace("'", "\\'");
        textArea.Document.Replace(completionSegment, _wrapInQuotes ? $"'{escaped}'" : escaped);
    }
}
