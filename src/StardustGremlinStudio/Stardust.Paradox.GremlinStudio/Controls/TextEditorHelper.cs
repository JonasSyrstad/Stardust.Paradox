using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Rendering;
using Stardust.Paradox.GremlinStudio.Editor;

namespace Stardust.Paradox.GremlinStudio.Controls;

/// <summary>
/// Attached property to enable binding to AvalonEdit TextEditor.Text.
/// AvalonEdit's Text property is not a DependencyProperty, so we need this helper.
/// </summary>
public static class TextEditorHelper
{
    public static readonly DependencyProperty BoundTextProperty =
        DependencyProperty.RegisterAttached(
            "BoundText",
            typeof(string),
            typeof(TextEditorHelper),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundTextChanged));

    public static readonly DependencyProperty EnableGremlinFeaturesProperty =
        DependencyProperty.RegisterAttached(
            "EnableGremlinFeatures",
            typeof(bool),
            typeof(TextEditorHelper),
            new PropertyMetadata(false, OnEnableGremlinFeaturesChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(TextEditorHelper),
            new PropertyMetadata(false));

    private static readonly DependencyProperty CompletionWindowProperty =
        DependencyProperty.RegisterAttached(
            "CompletionWindow",
            typeof(CompletionWindow),
            typeof(TextEditorHelper),
            new PropertyMetadata(null));

    private static readonly DependencyProperty TooltipProviderProperty =
        DependencyProperty.RegisterAttached(
            "TooltipProvider",
            typeof(GremlinTooltipProvider),
            typeof(TextEditorHelper),
            new PropertyMetadata(null));


    private static readonly DependencyProperty FindReplacePanelProperty =
        DependencyProperty.RegisterAttached(
            "FindReplacePanel",
            typeof(FindReplacePanel),
            typeof(TextEditorHelper),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SyntaxModeProperty =
        DependencyProperty.RegisterAttached(
            "SyntaxMode",
            typeof(string),
            typeof(TextEditorHelper),
            new PropertyMetadata(null, OnSyntaxModeChanged));

    public static string GetBoundText(DependencyObject obj) => (string)obj.GetValue(BoundTextProperty);
    public static void SetBoundText(DependencyObject obj, string value) => obj.SetValue(BoundTextProperty, value);

    public static bool GetEnableGremlinFeatures(DependencyObject obj) => (bool)obj.GetValue(EnableGremlinFeaturesProperty);
    public static void SetEnableGremlinFeatures(DependencyObject obj, bool value) => obj.SetValue(EnableGremlinFeaturesProperty, value);

    public static string GetSyntaxMode(DependencyObject obj) => (string)obj.GetValue(SyntaxModeProperty);
    public static void SetSyntaxMode(DependencyObject obj, string value) => obj.SetValue(SyntaxModeProperty, value);

    private static bool GetIsUpdating(DependencyObject obj) => (bool)obj.GetValue(IsUpdatingProperty);
    private static void SetIsUpdating(DependencyObject obj, bool value) => obj.SetValue(IsUpdatingProperty, value);

    private static CompletionWindow? GetCompletionWindow(DependencyObject obj) => (CompletionWindow?)obj.GetValue(CompletionWindowProperty);
    private static void SetCompletionWindow(DependencyObject obj, CompletionWindow? value) => obj.SetValue(CompletionWindowProperty, value);

    private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
        {
            return;
        }

        // Subscribe to TextChanged only once
        editor.TextChanged -= Editor_TextChanged;
        editor.TextChanged += Editor_TextChanged;

        if (GetIsUpdating(editor))
        {
            return;
        }

        var newText = (string)e.NewValue ?? string.Empty;

        // Avoid re-entrancy loops, especially for read-only editors where we only want VM -> UI updates.
        if (editor.Text != newText)
        {
            SetIsUpdating(editor, true);
            editor.Text = newText;
            SetIsUpdating(editor, false);
        }
    }

    private static void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        if (editor.IsReadOnly)
        {
            return;
        }

        // Guard against re-entrancy: OnBoundTextChanged sets IsUpdating before
        // assigning editor.Text, which synchronously fires this handler.
        // Without this check, the flag is cleared prematurely.
        if (GetIsUpdating(editor))
        {
            return;
        }

        SetIsUpdating(editor, true);
        SetBoundText(editor, editor.Text);
        SetIsUpdating(editor, false);
    }

    private static void OnEnableGremlinFeaturesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            // Apply syntax highlighting
            editor.SyntaxHighlighting = GremlinSyntaxHighlighting.Definition;

            // Setup completion handler
            editor.TextArea.TextEntering -= TextArea_TextEntering;
            editor.TextArea.TextEntered -= TextArea_TextEntered;
            editor.TextArea.TextEntering += TextArea_TextEntering;
            editor.TextArea.TextEntered += TextArea_TextEntered;

            // Add keyboard shortcut for completion
            editor.TextArea.KeyDown -= TextArea_KeyDown;
            editor.TextArea.KeyDown += TextArea_KeyDown;

            // Add error marker renderer
            var errorRenderer = new GremlinErrorRenderer(editor);
            editor.TextArea.TextView.BackgroundRenderers.Add(errorRenderer);
            editor.Tag = errorRenderer;

            // Install hover tooltip provider (uninstall any previous one first)
            if (editor.GetValue(TooltipProviderProperty) is GremlinTooltipProvider oldProvider)
            {
                oldProvider.Uninstall();
            }

            var tooltipProvider = new GremlinTooltipProvider(editor);
            tooltipProvider.Install();
            editor.SetValue(TooltipProviderProperty, tooltipProvider);

            // Validate on text change
            editor.TextChanged -= Editor_ValidateOnChange;
            editor.TextChanged += Editor_ValidateOnChange;

            // Bind Ctrl+F and Ctrl+H to custom FindReplacePanel
            editor.PreviewKeyDown += Editor_OpenFindReplace;
        }
        else
        {
            editor.SyntaxHighlighting = null;
            editor.TextArea.TextEntering -= TextArea_TextEntering;
            editor.TextArea.TextEntered -= TextArea_TextEntered;
            editor.TextArea.KeyDown -= TextArea_KeyDown;
            editor.TextChanged -= Editor_ValidateOnChange;
            editor.PreviewKeyDown -= Editor_OpenFindReplace;

            // Uninstall hover tooltip provider
            if (editor.GetValue(TooltipProviderProperty) is GremlinTooltipProvider provider)
            {
                provider.Uninstall();
                editor.SetValue(TooltipProviderProperty, null);
            }
        }
    }

    private static FindReplacePanel GetOrCreateFindReplacePanel(TextEditor editor)
    {
        if (editor.GetValue(FindReplacePanelProperty) is FindReplacePanel existing)
        {
            return existing;
        }

        var panel = new FindReplacePanel();
        editor.SetValue(FindReplacePanelProperty, panel);

        // Use an adorner to overlay the panel on the editor.
        // This avoids re-parenting the editor which can break
        // DataTemplate bindings and content presenters.
        editor.Loaded += (_, _) =>
        {
            var layer = AdornerLayer.GetAdornerLayer(editor);
            if (layer is not null && !panel.IsLoaded)
            {
                var adorner = new FindReplacePanelAdorner(editor, panel);
                layer.Add(adorner);
            }
        };

        // If the editor is already loaded, install immediately
        if (editor.IsLoaded)
        {
            var layer = AdornerLayer.GetAdornerLayer(editor);
            if (layer is not null)
            {
                var adorner = new FindReplacePanelAdorner(editor, panel);
                layer.Add(adorner);
            }
        }

        return panel;
    }

    private static void Editor_OpenFindReplace(object sender, KeyEventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var panel = GetOrCreateFindReplacePanel(editor);
            panel.ShowFind(editor);
            e.Handled = true;
        }
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var panel = GetOrCreateFindReplacePanel(editor);
            panel.ShowReplace(editor);
            e.Handled = true;
        }
    }

    private static void TextArea_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ICSharpCode.AvalonEdit.Editing.TextArea textArea)
        {
            return;
        }

        // Tab for completion (when no completion window is open)
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
        {
            var editor = textArea.GetService(typeof(TextEditor)) as TextEditor;
            if (editor != null && GetCompletionWindow(editor) == null)
            {
                e.Handled = true;
                ShowCompletion(editor);
            }
        }
    }

    private static void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (sender is not ICSharpCode.AvalonEdit.Editing.TextArea textArea)
        {
            return;
        }

        var editor = textArea.GetService(typeof(TextEditor)) as TextEditor;
        if (editor == null) return;

        var completionWindow = GetCompletionWindow(editor);
        if (completionWindow != null && e.Text.Length > 0)
        {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private static void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (sender is not ICSharpCode.AvalonEdit.Editing.TextArea textArea)
        {
            return;
        }

        var editor = textArea.GetService(typeof(TextEditor)) as TextEditor;
        if (editor == null) return;

        // Show completion after typing a dot
        if (e.Text == ".")
        {
            ShowCompletion(editor);
        }
    }

    private static void ShowCompletion(TextEditor? editor)
    {
        if (editor == null) return;

        var existingWindow = GetCompletionWindow(editor);
        if (existingWindow != null)
        {
            return;
        }

        // Get the text before the caret
        var offset = editor.CaretOffset;
        var textBefore = editor.Text.Substring(0, offset);

        var completions = GremlinCompletionProvider.GetCompletions(textBefore).ToList();
        if (completions.Count == 0)
        {
            return;
        }

        var completionWindow = new CompletionWindow(editor.TextArea);
        var data = completionWindow.CompletionList.CompletionData;

        foreach (var item in completions)
        {
            data.Add(item);
        }

        SetCompletionWindow(editor, completionWindow);

        completionWindow.Closed += (s, args) => SetCompletionWindow(editor, null);
        completionWindow.Show();
    }

    private static DispatcherTimer? _validationTimer;
    private static TextEditor? _pendingValidationEditor;

    private static void Editor_ValidateOnChange(object? sender, EventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        // Debounce validation
        _pendingValidationEditor = editor;

        if (_validationTimer == null)
        {
            _validationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _validationTimer.Tick += (s, args) =>
            {
                _validationTimer.Stop();
                if (_pendingValidationEditor?.Tag is GremlinErrorRenderer renderer)
                {
                    var errors = GremlinValidator.Validate(_pendingValidationEditor.Text);
                    renderer.SetErrors(errors);
                    _pendingValidationEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                }
            };
        }

        _validationTimer.Stop();
        _validationTimer.Start();
    }

    private static void OnSyntaxModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
        {
            return;
        }

        var mode = e.NewValue as string;
        
        editor.SyntaxHighlighting = mode?.ToLowerInvariant() switch
        {
            "json" => ExportSyntaxHighlighting.JsonDefinition,
            "csharp" or "c#" => ExportSyntaxHighlighting.CSharpDefinition,
            "gremlin" => GremlinSyntaxHighlighting.Definition,
            _ => null
        };
    }
}

/// <summary>
/// Adorner wrapper for hosting a FindReplacePanel over the TextEditor.
/// Supports dragging by exposing an offset that the panel can update.
/// </summary>
internal sealed class FindReplacePanelAdorner : Adorner
{
    private readonly FindReplacePanel _panel;
    private Point _offset;
    private bool _hasUserPosition;

    public FindReplacePanelAdorner(UIElement adornedElement, FindReplacePanel panel)
        : base(adornedElement)
    {
        _panel = panel;
        _panel.SetAdorner(this);
        AddVisualChild(_panel);
        AddLogicalChild(_panel);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _panel;

    protected override Size MeasureOverride(Size constraint)
    {
        _panel.Measure(constraint);
        // Return the full adorned element size so we cover the editor
        return AdornedElement.RenderSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var panelSize = _panel.DesiredSize;

        if (!_hasUserPosition)
        {
            // Default: top-right corner of the editor
            _offset = new Point(
                Math.Max(0, finalSize.Width - panelSize.Width - 12),
                4);
        }

        _panel.Arrange(new Rect(_offset, panelSize));
        return finalSize;
    }

    /// <summary>
    /// Moves the panel by a delta and triggers re-arrange.
    /// </summary>
    public void MoveByDelta(double dx, double dy)
    {
        var editorSize = AdornedElement.RenderSize;
        var panelSize = _panel.DesiredSize;

        var newX = Math.Max(0, Math.Min(_offset.X + dx, editorSize.Width - panelSize.Width));
        var newY = Math.Max(0, Math.Min(_offset.Y + dy, editorSize.Height - panelSize.Height));

        _offset = new Point(newX, newY);
        _hasUserPosition = true;
        InvalidateArrange();
    }

    /// <summary>
    /// Resets position to top-right on next arrange.
    /// </summary>
    public void ResetPosition()
    {
        _hasUserPosition = false;
        InvalidateArrange();
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        // Only hit-test the panel itself; let everything else pass through to the editor
        return null;
    }
}

/// <summary>
/// Renders error markers in the text editor.
/// </summary>
public class GremlinErrorRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private List<GremlinValidationError> _errors = new();

    public GremlinErrorRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void SetErrors(List<GremlinValidationError> errors)
    {
        _errors = errors;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_errors.Count == 0 || _editor.Document == null)
        {
            return;
        }

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0)
        {
            return;
        }

        foreach (var error in _errors)
        {
            if (error.StartOffset >= _editor.Document.TextLength)
            {
                continue;
            }

            var endOffset = Math.Min(error.EndOffset, _editor.Document.TextLength);

            var segment = new ICSharpCode.AvalonEdit.Document.TextSegment
            {
                StartOffset = error.StartOffset,
                Length = endOffset - error.StartOffset
            };

            var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, segment);
            var brush = error.Severity switch
            {
                GremlinErrorSeverity.Error => System.Windows.Media.Brushes.Red,
                GremlinErrorSeverity.Warning => System.Windows.Media.Brushes.Orange,
                _ => System.Windows.Media.Brushes.Blue
            };

            var pen = new System.Windows.Media.Pen(brush, 1.5)
            {
                DashStyle = System.Windows.Media.DashStyles.Dot
            };

            foreach (var rect in rects)
            {
                // Draw wavy underline
                var y = rect.Bottom - 1;
                drawingContext.DrawLine(pen, new System.Windows.Point(rect.Left, y), new System.Windows.Point(rect.Right, y));
            }
        }
    }
}
