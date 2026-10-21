using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Completion item for a property key discovered from the database, or a step label declared with <c>as()</c>.
/// </summary>
public sealed class PropertyKeyCompletionData : ICompletionData
{
    public PropertyKeyCompletionData(string key, bool wrapInQuotes, bool isStepLabel = false)
    {
        Text = key;
        WrapsInQuotes = wrapInQuotes;
        IsStepLabel = isStepLabel;
    }

    public string Text { get; }
    public bool WrapsInQuotes { get; }
    public bool IsStepLabel { get; }
    public ImageSource? Image => null;
    public double Priority => 0;
    public object Description => IsStepLabel ? $"Step label '{Text}' declared with as()" : $"Property key '{Text}'";

    public object Content
    {
        get
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = IsStepLabel ? "S " : "P ",
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

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var escaped = Text.Replace("\\", "\\\\").Replace("'", "\\'");
        textArea.Document.Replace(completionSegment, WrapsInQuotes ? $"'{escaped}'" : escaped);
    }
}
