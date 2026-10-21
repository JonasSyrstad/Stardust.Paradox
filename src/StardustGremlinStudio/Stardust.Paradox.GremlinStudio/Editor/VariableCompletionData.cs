using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Completion data for a query variable. Inserts <c>${key}</c> tokens into the
/// editor and shows the resolved value in the tooltip.
/// </summary>
public class VariableCompletionData : ICompletionData
{
    private readonly string _key;
    private readonly string _value;
    private readonly bool _isConnectionScoped;

    public VariableCompletionData(string key, string value, bool isConnectionScoped = false)
    {
        _key = key;
        _value = value;
        _isConnectionScoped = isConnectionScoped;
        Text = key;
    }

    public string Text { get; }
    public double Priority => _isConnectionScoped ? 0 : 1;
    public ImageSource? Image => null;

    public object Description => BuildDescription();
    public object Content => BuildContent();

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // The completion is triggered by '$'. The '$' is already in the document
        // at (completionSegment.Offset - 1). We extend the replacement one char
        // back to include the '$' and insert the full '${key}' token.
        //
        // When '}' triggers the insertion, the brace will be typed by the text
        // area after this method returns, so we insert '${key' (no closing brace).
        bool closingBraceTriggered = insertionRequestEventArgs is System.Windows.Input.TextCompositionEventArgs tce
                                     && tce.Text == "}";

        var insertText = closingBraceTriggered ? "${" + _key : "${" + _key + "}";

        // Replace from the '$' character through the completion segment
        int dollarOffset = completionSegment.Offset > 0 ? completionSegment.Offset - 1 : completionSegment.Offset;
        int length = completionSegment.EndOffset - dollarOffset;
        textArea.Document.Replace(dollarOffset, length, insertText);
    }

    private object BuildContent()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new TextBlock
        {
            Text = _isConnectionScoped ? "⊕ " : "$ ",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray
        });

        panel.Children.Add(new TextBlock
        {
            Text = "${" + _key + "}",
            FontFamily = new FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center
        });

        return panel;
    }

    private object BuildDescription()
    {
        var panel = new StackPanel { MaxWidth = 350 };

        var scope = _isConnectionScoped ? "connection" : "global";

        panel.Children.Add(new TextBlock
        {
            Text = $"variable ({scope})",
            FontSize = 10,
            Foreground = Application.Current.TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue,
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = _key,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Foreground = Application.Current.TryFindResource("PrimaryForegroundBrush") as Brush ?? Brushes.White
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"→ {_value}",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = Application.Current.TryFindResource("SecondaryForegroundBrush") as Brush ?? Brushes.LightGray,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        return panel;
    }
}
