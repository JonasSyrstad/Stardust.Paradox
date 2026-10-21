using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using Stardust.Paradox.GremlinStudio.Converters;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Provides hover tooltips for numeric values that look like epoch timestamps
/// in read-only AvalonEdit editors (e.g. JSON results, export previews).
/// </summary>
internal sealed class EpochTooltipProvider
{
    private readonly TextEditor _editor;
    private Popup? _popup;
    private bool _closing;

    public EpochTooltipProvider(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// Installs this tooltip provider on the editor.
    /// </summary>
    public void Install()
    {
        _editor.TextArea.TextView.MouseHover += OnMouseHover;
        _editor.TextArea.TextView.MouseHoverStopped += OnMouseHoverStopped;
    }

    /// <summary>
    /// Uninstalls this tooltip provider from the editor.
    /// </summary>
    public void Uninstall()
    {
        _editor.TextArea.TextView.MouseHover -= OnMouseHover;
        _editor.TextArea.TextView.MouseHoverStopped -= OnMouseHoverStopped;
        ClosePopup();
    }

    private void OnMouseHover(object? sender, MouseEventArgs e)
    {
        var textView = _editor.TextArea.TextView;
        var pos = textView.GetPositionFloor(
            e.GetPosition(textView) + textView.ScrollOffset);

        if (pos == null)
            return;

        int offset;
        try
        {
            offset = _editor.Document.GetOffset(pos.Value.Location);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        var numericToken = GetNumericTokenAtOffset(offset);
        if (numericToken == null)
            return;

        var formatted = EpochToDateTimeTooltipConverter.TryFormatEpoch(numericToken);
        if (formatted == null)
            return;

        ClosePopup();

        var content = BuildTooltipContent(numericToken, formatted);

        var border = new Border
        {
            Child = content,
            Background = GetBrush("TertiaryBackgroundBrush"),
            BorderBrush = GetBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            MaxWidth = 350,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 2,
                Opacity = 0.4,
                Color = Colors.Black
            }
        };

        _popup = new Popup
        {
            Child = border,
            Placement = PlacementMode.Mouse,
            AllowsTransparency = true,
            StaysOpen = false,
            IsOpen = true
        };

        e.Handled = true;
    }

    private void OnMouseHoverStopped(object? sender, MouseEventArgs e)
    {
        ClosePopup();
    }

    private void ClosePopup()
    {
        if (_closing)
            return;

        _closing = true;
        try
        {
            if (_popup != null)
            {
                _popup.IsOpen = false;
                _popup.Child = null;
                _popup = null;
            }
        }
        finally
        {
            _closing = false;
        }
    }

    /// <summary>
    /// Extracts a contiguous sequence of digit characters at the given document offset.
    /// </summary>
    private string? GetNumericTokenAtOffset(int offset)
    {
        var doc = _editor.Document;
        if (offset < 0 || offset >= doc.TextLength)
            return null;

        var ch = doc.GetCharAt(offset);
        if (!char.IsDigit(ch))
            return null;

        int start = offset;
        int end = offset;

        while (start > 0 && char.IsDigit(doc.GetCharAt(start - 1)))
        {
            start--;
        }

        while (end < doc.TextLength && char.IsDigit(doc.GetCharAt(end)))
        {
            end++;
        }

        // Ignore if preceded/followed by letters (not a standalone number)
        if (start > 0 && char.IsLetter(doc.GetCharAt(start - 1)))
            return null;
        if (end < doc.TextLength && char.IsLetter(doc.GetCharAt(end)))
            return null;

        if (start >= end)
            return null;

        return doc.GetText(start, end - start);
    }

    private static UIElement BuildTooltipContent(string epochValue, string formattedDate)
    {
        var panel = new StackPanel();

        var accentBrush = GetBrush("AccentBrush");
        var primaryFg = GetBrush("PrimaryForegroundBrush");
        var secondaryFg = GetBrush("SecondaryForegroundBrush");

        panel.Children.Add(new TextBlock
        {
            Text = "Epoch Timestamp",
            FontSize = 10,
            Foreground = accentBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = epochValue,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = primaryFg,
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = formattedDate,
            FontSize = 12,
            Foreground = secondaryFg
        });

        return panel;
    }

    private static Brush GetBrush(string resourceKey)
    {
        return Application.Current.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
    }
}
