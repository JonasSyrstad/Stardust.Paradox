using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Adapts syntax highlighting colors to the active theme so code keeps readable contrast
/// on both light and dark editor backgrounds (Visual Studio Light/Dark palettes).
/// </summary>
public static class SyntaxThemePalette
{
    private static readonly Dictionary<string, string> DarkColors = new(StringComparer.Ordinal)
    {
        ["Comment"] = "#6A9955",
        ["String"] = "#CE9178",
        ["Char"] = "#CE9178",
        ["Number"] = "#B5CEA8",
        ["Keyword"] = "#569CD6",
        ["TraversalSource"] = "#4EC9B0",
        ["Step"] = "#DCDCAA",
        ["Predicate"] = "#C586C0",
        ["Modulator"] = "#9CDCFE",
        ["TrueFalse"] = "#569CD6",
        ["Null"] = "#569CD6",
        ["Punctuation"] = "#DCDCDC",
        ["ControlKeyword"] = "#D8A0DF",
        ["ClassName"] = "#4EC9B0",
        ["InterfaceName"] = "#B8D7A3",
        ["Preprocessor"] = "#9B9B9B",
        ["Operator"] = "#B4B4B4"
    };

    private static readonly Dictionary<string, string> LightColors = new(StringComparer.Ordinal)
    {
        ["Comment"] = "#008000",
        ["String"] = "#A31515",
        ["Char"] = "#A31515",
        ["Number"] = "#098658",
        ["Keyword"] = "#0000FF",
        ["TraversalSource"] = "#267F99",
        ["Step"] = "#795E26",
        ["Predicate"] = "#AF00DB",
        ["Modulator"] = "#001080",
        ["TrueFalse"] = "#0000FF",
        ["Null"] = "#0000FF",
        ["Punctuation"] = "#1A1A1A",
        ["ControlKeyword"] = "#8F08C4",
        ["ClassName"] = "#2B91AF",
        ["InterfaceName"] = "#2B91AF",
        ["Preprocessor"] = "#808080",
        ["Operator"] = "#1A1A1A"
    };

    /// <summary>
    /// Gets whether the active theme has a light editor background.
    /// </summary>
    public static bool IsLightEditor { get; private set; }

    /// <summary>
    /// Re-colors all highlighting definitions for the current editor background and redraws open editors.
    /// </summary>
    public static void ApplyForCurrentTheme()
    {
        var app = Application.Current;
        if (app is null) return;

        IsLightEditor = app.TryFindResource("EditorBackgroundColor") is Color bg && GetLuminance(bg) > 0.5;
        var palette = IsLightEditor ? LightColors : DarkColors;

        foreach (var definition in new[]
                 {
                     GremlinSyntaxHighlighting.Definition,
                     ExportSyntaxHighlighting.JsonDefinition,
                     ExportSyntaxHighlighting.CSharpDefinition
                 })
        {
            foreach (var color in definition.NamedHighlightingColors)
            {
                if (color.Name != null && palette.TryGetValue(color.Name, out var hex) && !color.IsFrozen)
                {
                    color.Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(hex));
                }
            }
        }

        foreach (Window window in app.Windows)
        {
            RedrawEditors(window);
        }
    }

    private static void RedrawEditors(DependencyObject parent)
    {
        if (parent is TextEditor editor)
        {
            editor.TextArea.TextView.Redraw();
            return;
        }

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            RedrawEditors(VisualTreeHelper.GetChild(parent, i));
        }
    }

    private static double GetLuminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255d;
}
