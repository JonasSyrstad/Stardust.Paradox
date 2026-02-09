using System.Windows;
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

    public static string GetBoundText(DependencyObject obj) => (string)obj.GetValue(BoundTextProperty);
    public static void SetBoundText(DependencyObject obj, string value) => obj.SetValue(BoundTextProperty, value);

    public static bool GetEnableGremlinFeatures(DependencyObject obj) => (bool)obj.GetValue(EnableGremlinFeaturesProperty);
    public static void SetEnableGremlinFeatures(DependencyObject obj, bool value) => obj.SetValue(EnableGremlinFeaturesProperty, value);

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
        if (editor.Text != newText)
        {
            editor.Text = newText;
        }
    }

    private static void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextEditor editor)
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

            // Validate on text change
            editor.TextChanged -= Editor_ValidateOnChange;
            editor.TextChanged += Editor_ValidateOnChange;
        }
        else
        {
            editor.SyntaxHighlighting = null;
            editor.TextArea.TextEntering -= TextArea_TextEntering;
            editor.TextArea.TextEntered -= TextArea_TextEntered;
            editor.TextArea.KeyDown -= TextArea_KeyDown;
            editor.TextChanged -= Editor_ValidateOnChange;
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
