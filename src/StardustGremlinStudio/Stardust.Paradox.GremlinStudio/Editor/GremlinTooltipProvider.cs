using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Provides hover tooltips for Gremlin step names in the query editor.
/// Shows step description, parameter list, and overload permutations.
/// Uses a standalone <see cref="Popup"/> to avoid conflicts with WPF's ToolTipService.
/// All colors are resolved from the application's current theme resources.
/// </summary>
public class GremlinTooltipProvider
{
    private readonly TextEditor _editor;
    private Popup? _popup;
    private bool _closing;

    public GremlinTooltipProvider(TextEditor editor)
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

        var stepName = GetStepNameAtOffset(offset);
        if (stepName == null)
            return;

        var def = GremlinStepDatabase.Get(stepName);
        if (def == null)
            return;

        ClosePopup();

        var content = BuildTooltipContent(def);

        var border = new Border
        {
            Child = content,
            Background = GetBrush("TertiaryBackgroundBrush"),
            BorderBrush = GetBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            MaxWidth = 500,
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
    /// Extracts the Gremlin step name at the given document offset.
    /// </summary>
    private string? GetStepNameAtOffset(int offset)
    {
        var doc = _editor.Document;
        if (offset < 0 || offset >= doc.TextLength)
            return null;

        int start = offset;
        int end = offset;

        while (start > 0 && IsIdentifierChar(doc.GetCharAt(start - 1)))
        {
            start--;
        }

        while (end < doc.TextLength && IsIdentifierChar(doc.GetCharAt(end)))
        {
            end++;
        }

        if (start >= end)
            return null;

        var word = doc.GetText(start, end - start);

        if (GremlinStepDatabase.TryGet(word, out _))
        {
            return word;
        }

        return null;
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    /// <summary>
    /// Resolves a <see cref="Brush"/> from the application's current theme resources.
    /// Falls back to a transparent brush if the key is not found.
    /// </summary>
    private static Brush GetBrush(string resourceKey)
    {
        return Application.Current.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
    }

    private static UIElement BuildTooltipContent(GremlinStepDefinition def)
    {
        var panel = new StackPanel { MaxWidth = 480 };

        var accentBrush = GetBrush("AccentBrush");
        var primaryFg = GetBrush("PrimaryForegroundBrush");
        var secondaryFg = GetBrush("SecondaryForegroundBrush");
        var mutedFg = GetBrush("MutedForegroundBrush");
        var separatorBrush = GetBrush("BorderBrush");

        // Step name header
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        headerPanel.Children.Add(new TextBlock
        {
            Text = def.Kind switch
            {
                GremlinCompletionKind.TraversalSource => "source",
                GremlinCompletionKind.Step => "step",
                GremlinCompletionKind.Predicate => "predicate",
                GremlinCompletionKind.Modulator => "modulator",
                _ => "keyword"
            },
            FontSize = 10,
            Foreground = accentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = def.Name,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            Foreground = primaryFg
        });
        panel.Children.Add(headerPanel);

        // Description
        panel.Children.Add(new TextBlock
        {
            Text = def.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = secondaryFg,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Separator
        panel.Children.Add(new Border
        {
            Height = 1,
            Background = separatorBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Overloads
        if (def.Overloads.Length == 1)
        {
            var sigPanel = BuildSignaturePanel(def.Name, def.Overloads[0]);
            panel.Children.Add(sigPanel);
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Overloads ({def.Overloads.Length}):",
                FontWeight = FontWeights.SemiBold,
                Foreground = accentBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });

            for (int i = 0; i < def.Overloads.Length; i++)
            {
                var sigPanel = BuildSignaturePanel(def.Name, def.Overloads[i], i + 1);
                sigPanel.Margin = new Thickness(0, 0, 0, 4);
                panel.Children.Add(sigPanel);
            }
        }

        return panel;
    }

    private static StackPanel BuildSignaturePanel(string stepName, GremlinStepOverload overload, int? index = null)
    {
        var panel = new StackPanel();

        var primaryFg = GetBrush("PrimaryForegroundBrush");
        var secondaryFg = GetBrush("SecondaryForegroundBrush");
        var mutedFg = GetBrush("MutedForegroundBrush");
        var accentBrush = GetBrush("AccentBrush");
        var accentLightBrush = GetBrush("AccentLightBrush");
        // AccentLightBrush may not exist in all themes; fall back to AccentBrush
        if (accentLightBrush == Brushes.Transparent)
            accentLightBrush = accentBrush;

        var sigText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 2)
        };

        if (index != null)
        {
            sigText.Inlines.Add(new Run($"{index}. ") { Foreground = mutedFg });
        }

        sigText.Inlines.Add(new Run(stepName)
        {
            Foreground = primaryFg,
            FontWeight = FontWeights.Bold
        });

        sigText.Inlines.Add(new Run("(") { Foreground = secondaryFg });

        for (int j = 0; j < overload.Parameters.Length; j++)
        {
            if (j > 0)
            {
                sigText.Inlines.Add(new Run(", ") { Foreground = secondaryFg });
            }

            var p = overload.Parameters[j];

            if (p.IsOptional)
            {
                sigText.Inlines.Add(new Run("[") { Foreground = mutedFg });
            }

            sigText.Inlines.Add(new Run(p.Name) { Foreground = accentLightBrush });
            sigText.Inlines.Add(new Run($": {p.TypeName}") { Foreground = accentBrush });

            if (p.IsOptional)
            {
                sigText.Inlines.Add(new Run("]") { Foreground = mutedFg });
            }
        }

        sigText.Inlines.Add(new Run(")") { Foreground = secondaryFg });

        panel.Children.Add(sigText);

        // Overload description
        if (overload.Description != null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"  {overload.Description}",
                FontSize = 11,
                Foreground = mutedFg,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            });
        }

        // Parameter descriptions
        foreach (var p in overload.Parameters.Where(p => p.Description != null))
        {
            var paramDesc = new TextBlock
            {
                FontSize = 11,
                Margin = new Thickness(12, 0, 0, 0)
            };
            paramDesc.Inlines.Add(new Run($"@{p.Name}") { Foreground = accentLightBrush });
            paramDesc.Inlines.Add(new Run($" — {p.Description}") { Foreground = mutedFg });
            panel.Children.Add(paramDesc);
        }

        return panel;
    }
}
