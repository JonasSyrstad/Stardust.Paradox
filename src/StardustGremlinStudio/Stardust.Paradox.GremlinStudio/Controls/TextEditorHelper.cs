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
    /// <summary>
    /// Tracks the most recently focused Gremlin-enabled editor so that toolbar
    /// Undo/Redo buttons can invoke operations even when the editor loses focus.
    /// </summary>
    private static TextEditor? _activeGremlinEditor;
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

    public static readonly DependencyProperty EnableEpochTooltipsProperty =
        DependencyProperty.RegisterAttached(
            "EnableEpochTooltips",
            typeof(bool),
            typeof(TextEditorHelper),
            new PropertyMetadata(false, OnEnableEpochTooltipsChanged));

    private static readonly DependencyProperty EpochTooltipProviderProperty =
        DependencyProperty.RegisterAttached(
            "EpochTooltipProvider",
            typeof(EpochTooltipProvider),
            typeof(TextEditorHelper),
            new PropertyMetadata(null));

    /// <summary>
    /// Receives the local validation diagnostics of a Gremlin-enabled editor (bind OneWayToSource).
    /// </summary>
    public static readonly DependencyProperty DiagnosticsProperty =
        DependencyProperty.RegisterAttached(
            "Diagnostics",
            typeof(IReadOnlyList<GremlinValidationError>),
            typeof(TextEditorHelper),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static IReadOnlyList<GremlinValidationError>? GetDiagnostics(DependencyObject obj) => (IReadOnlyList<GremlinValidationError>?)obj.GetValue(DiagnosticsProperty);
    public static void SetDiagnostics(DependencyObject obj, IReadOnlyList<GremlinValidationError>? value) => obj.SetValue(DiagnosticsProperty, value);

    public static string GetBoundText(DependencyObject obj) => (string)obj.GetValue(BoundTextProperty);
    public static void SetBoundText(DependencyObject obj, string value) => obj.SetValue(BoundTextProperty, value);

    public static bool GetEnableGremlinFeatures(DependencyObject obj) => (bool)obj.GetValue(EnableGremlinFeaturesProperty);
    public static void SetEnableGremlinFeatures(DependencyObject obj, bool value) => obj.SetValue(EnableGremlinFeaturesProperty, value);

    public static string GetSyntaxMode(DependencyObject obj) => (string)obj.GetValue(SyntaxModeProperty);
    public static void SetSyntaxMode(DependencyObject obj, string value) => obj.SetValue(SyntaxModeProperty, value);

    public static bool GetEnableEpochTooltips(DependencyObject obj) => (bool)obj.GetValue(EnableEpochTooltipsProperty);
    public static void SetEnableEpochTooltips(DependencyObject obj, bool value) => obj.SetValue(EnableEpochTooltipsProperty, value);

    private static bool GetIsUpdating(DependencyObject obj) => (bool)obj.GetValue(IsUpdatingProperty);
    private static void SetIsUpdating(DependencyObject obj, bool value) => obj.SetValue(IsUpdatingProperty, value);

    private static CompletionWindow? GetCompletionWindow(DependencyObject obj) => (CompletionWindow?)obj.GetValue(CompletionWindowProperty);
    private static void SetCompletionWindow(DependencyObject obj, CompletionWindow? value) => obj.SetValue(CompletionWindowProperty, value);

    /// <summary>
    /// Gets whether the active Gremlin editor can undo.
    /// </summary>
    public static bool CanUndo => _activeGremlinEditor?.CanUndo == true;

    /// <summary>
    /// Gets whether the active Gremlin editor can redo.
    /// </summary>
    public static bool CanRedo => _activeGremlinEditor?.CanRedo == true;

    /// <summary>
    /// Performs an undo operation on the active Gremlin editor.
    /// </summary>
    public static void Undo()
    {
        if (_activeGremlinEditor?.CanUndo == true)
        {
            _activeGremlinEditor.Undo();
        }
    }

    /// <summary>
    /// Performs a redo operation on the active Gremlin editor.
    /// </summary>
    public static void Redo()
    {
        if (_activeGremlinEditor?.CanRedo == true)
        {
            _activeGremlinEditor.Redo();
        }
    }

    /// <summary>
    /// Gets the selected text in the active Gremlin editor, or an empty string.
    /// </summary>
    public static string GetActiveSelection() => _activeGremlinEditor?.SelectedText ?? string.Empty;

    /// <summary>
    /// Gets the statement under the caret in the active editor. Statements are separated
    /// by blank lines or semicolons.
    /// </summary>
    public static string GetActiveStatement()
    {
        var editor = _activeGremlinEditor;
        if (editor == null) return string.Empty;
        return GetStatementAt(editor.Text, editor.CaretOffset);
    }

    /// <summary>
    /// Extracts the statement surrounding <paramref name="offset"/> from <paramref name="text"/>.
    /// </summary>
    public static string GetStatementAt(string text, int offset)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        offset = Math.Clamp(offset, 0, text.Length);
        var separator = new System.Text.RegularExpressions.Regex(@"(\r?\n[ \t]*){2,}|;");
        var start = 0;
        foreach (System.Text.RegularExpressions.Match m in separator.Matches(text))
        {
            if (m.Index + m.Length <= offset)
            {
                start = m.Index + m.Length;
                continue;
            }
            if (m.Index >= offset || m.Index + m.Length > offset)
            {
                return text[start..m.Index].Trim();
            }
        }
        return text[start..].Trim();
    }

    /// <summary>
    /// Moves the caret of the active editor to a 1-based line/column and focuses it.
    /// </summary>
    public static void NavigateTo(int line, int column)
    {
        var editor = _activeGremlinEditor;
        if (editor == null) return;
        line = Math.Clamp(line, 1, editor.Document.LineCount);
        var docLine = editor.Document.GetLineByNumber(line);
        column = Math.Clamp(column, 1, docLine.Length + 1);
        editor.TextArea.Caret.Line = line;
        editor.TextArea.Caret.Column = column;
        editor.ScrollTo(line, column);
        editor.Focus();
    }

    /// <summary>
    /// Raised when the caret of the active editor moves (line, column).
    /// </summary>
    public static event EventHandler<(int Line, int Column)>? ActiveCaretMoved;

    internal static void RaiseCaretMoved(TextEditor editor) =>
        ActiveCaretMoved?.Invoke(editor, (editor.TextArea.Caret.Line, editor.TextArea.Caret.Column));

    /// <summary>
    /// Opens the find panel on the active Gremlin editor.
    /// </summary>
    public static void ShowFind()
    {
        if (_activeGremlinEditor is null) return;
        GetOrCreateFindReplacePanel(_activeGremlinEditor).ShowFind(_activeGremlinEditor);
    }

    /// <summary>
    /// Opens the find-and-replace panel on the active Gremlin editor.
    /// </summary>
    public static void ShowReplace()
    {
        if (_activeGremlinEditor is null) return;
        GetOrCreateFindReplacePanel(_activeGremlinEditor).ShowReplace(_activeGremlinEditor);
    }

    /// <summary>
    /// Moves keyboard focus to the active Gremlin editor.
    /// </summary>
    public static void FocusEditor() => _activeGremlinEditor?.Focus();

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
            // Track as active Gremlin editor
            _activeGremlinEditor = editor;
            editor.GotFocus += Editor_TrackActive;

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

            var tooltipProvider = new GremlinTooltipProvider(editor)
            {
                IsSuppressed = () => GetCompletionWindow(editor) != null
            };
            tooltipProvider.Install();
            editor.SetValue(TooltipProviderProperty, tooltipProvider);

            // Validate on text change
            editor.TextChanged -= Editor_ValidateOnChange;
            editor.TextChanged += Editor_ValidateOnChange;

            // Bind Ctrl+F and Ctrl+H to custom FindReplacePanel
            editor.PreviewKeyDown += Editor_OpenFindReplace;

            editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

            GremlinEditorEnhancements.Get(editor)?.Uninstall();
            var enhancements = GremlinEditorEnhancements.Install(editor);
            enhancements.IsCompletionOpen = () => GetCompletionWindow(editor) != null;
            enhancements.PairInserted += c =>
            {
                if (c is '(' or '\'' or '"')
                {
                    TryShowLabelOrPropertyCompletion(editor);
                }
            };
        }
        else
        {
            GremlinEditorEnhancements.Get(editor)?.Uninstall();
            editor.GotFocus -= Editor_TrackActive;
            if (_activeGremlinEditor == editor)
            {
                _activeGremlinEditor = null;
            }

            editor.SyntaxHighlighting = null;
            editor.TextArea.TextEntering -= TextArea_TextEntering;
            editor.TextArea.TextEntered -= TextArea_TextEntered;
            editor.TextArea.KeyDown -= TextArea_KeyDown;
            editor.TextChanged -= Editor_ValidateOnChange;
            editor.PreviewKeyDown -= Editor_OpenFindReplace;
            editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;

            // Uninstall hover tooltip provider
            if (editor.GetValue(TooltipProviderProperty) is GremlinTooltipProvider provider)
            {
                provider.Uninstall();
                editor.SetValue(TooltipProviderProperty, null);
            }
        }
    }

    private static void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_activeGremlinEditor is { } editor && sender == editor.TextArea.Caret)
        {
            RaiseCaretMoved(editor);
        }
    }

    private static void Editor_TrackActive(object sender, RoutedEventArgs e)
    {
        if (sender is TextEditor editor)
        {
            _activeGremlinEditor = editor;
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

        var editor = textArea.GetService(typeof(TextEditor)) as TextEditor;
        if (editor == null) return;

        // Ctrl+Space / Ctrl+J: explicitly show completion (Visual Studio behavior)
        if ((e.Key == Key.Space || e.Key == Key.J) && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            GetCompletionWindow(editor)?.Close();
            if (IsInsideVariableToken(editor))
            {
                ShowVariableCompletion(editor);
            }
            else
            {
                ShowCompletion(editor);
            }
            return;
        }

        // Tab only opens completion when the caret directly follows an identifier or '.';
        // otherwise Tab behaves normally (indentation).
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None && GetCompletionWindow(editor) == null)
        {
            var offset = editor.CaretOffset;
            var prev = offset > 0 ? editor.Text[offset - 1] : '\0';
            if (IsIdentifierChar(prev) || prev == '.' || prev == '$')
            {
                e.Handled = true;
                if (IsInsideVariableToken(editor))
                {
                    ShowVariableCompletion(editor);
                }
                else
                {
                    ShowCompletion(editor);
                }
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
                // When a variable completion window is active, allow '.', '{'
                // to pass through without triggering insertion so the user can
                // type dotted-path variables or the old '${' pattern.
                if (IsVariableCompletionWindow(completionWindow)
                    && (e.Text[0] == '.' || e.Text[0] == '{'))
                {
                    return;
                }

                // When '$' is typed, the user is starting a variable token.
                // Close any Gremlin step completion window so it doesn't block
                // variable completion.
                if (e.Text[0] == '$' && !IsVariableCompletionWindow(completionWindow))
                {
                    completionWindow.Close();
                    return;
                }

                // Label lists: ')' and ',' commit; a quote commits only when we are inside an
                // already-open string (closing it). Anything else closes the list.
                if (completionWindow.CompletionList.CompletionData.FirstOrDefault() is LabelCompletionData or PropertyKeyCompletionData)
                {
                    var first = completionWindow.CompletionList.CompletionData[0];
                    var wraps = first is LabelCompletionData l ? l.WrapsInQuotes : ((PropertyKeyCompletionData)first).WrapsInQuotes;
                    var selected = completionWindow.CompletionList.SelectedItem;
                    var isQuote = e.Text[0] is '\'' or '"';
                    if (selected != null && (e.Text[0] is ')' or ',' || (isQuote && !wraps)))
                    {
                        completionWindow.CompletionList.RequestInsertion(e);
                    }
                    else
                    {
                        completionWindow.Close();
                    }
                    return;
                }

                // Visual Studio-like commit characters: '(' and '.' commit; other punctuation
                // and whitespace simply close the list without inserting.
                if (e.Text[0] is '(' or '.')
                {
                    if (completionWindow.CompletionList.SelectedItem == null)
                    {
                        completionWindow.Close();
                        return;
                    }

                    completionWindow.CompletionList.RequestInsertion(e);
                    AdjustCaretAfterCommit(editor, e.Text[0]);
                    e.Handled = e.Text[0] == '(';
                    return;
                }

                completionWindow.Close();
                return;
            }
        }
    }

    /// <summary>
    /// Positions the caret after a step completion was committed with '(' or '.'.
    /// </summary>
    private static void AdjustCaretAfterCommit(TextEditor editor, char commitChar)
    {
        var doc = editor.Document;
        var caret = editor.CaretOffset;
        if (commitChar == '(')
        {
            // "name()|" -> "name(|)"
            if (caret >= 2 && doc.GetCharAt(caret - 1) == ')' && doc.GetCharAt(caret - 2) == '(')
            {
                editor.CaretOffset = caret - 1;
            }
            else if (caret >= 1 && doc.GetCharAt(caret - 1) != '(' && (caret >= doc.TextLength || doc.GetCharAt(caret) != '('))
            {
                // Traversal source such as "g" - insert the paren ourselves.
                doc.Insert(caret, "(");
            }
            else if (caret < doc.TextLength && doc.GetCharAt(caret) == '(')
            {
                editor.CaretOffset = caret + 1;
            }
        }
        else if (commitChar == '.' && caret < doc.TextLength && doc.GetCharAt(caret) == ')' && caret >= 1 && doc.GetCharAt(caret - 1) == '(')
        {
            // "has(|)" + '.' -> "has().|"
            editor.CaretOffset = caret + 1;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the given completion window contains variable
    /// completion items (as opposed to Gremlin step completions).
    /// </summary>
    private static bool IsVariableCompletionWindow(CompletionWindow window)
    {
        return window.CompletionList.CompletionData.Count > 0
               && window.CompletionList.CompletionData[0] is VariableCompletionData;
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
            // Inside a variable token, show variable completion for dotted
            // paths like ${server.host} instead of Gremlin steps.
            if (IsInsideVariableToken(editor))
            {
                ShowVariableCompletion(editor);
            }
            else
            {
                ShowCompletion(editor);
            }
            return;
        }

        // Show variable completion immediately when '$' is typed
        if (e.Text == "$")
        {
            ShowVariableCompletion(editor);
            return;
        }

        if (GetCompletionWindow(editor) != null || e.Text.Length != 1)
        {
            return;
        }

        var typed = e.Text[0];
        var textBefore = editor.Text.Substring(0, editor.CaretOffset);

        // Label suggestions after hasLabel( / out( / addV( and inside their quotes
        if (typed is '(' or '\'' or '"' or ',')
        {
            TryShowLabelOrPropertyCompletion(editor);
            return;
        }

        // Visual Studio-style: start suggesting as soon as the first identifier
        // character of a step name is typed after '.' or at the start of a statement.
        if (char.IsLetter(typed) && !IsInsideVariableToken(editor) && !IsInsideStringLiteral(textBefore))
        {
            var wordStart = GetWordStart(editor.Text, editor.CaretOffset);
            if (editor.CaretOffset - wordStart != 1) return;

            var i = wordStart - 1;
            while (i >= 0 && textBefore[i] is ' ' or '\t') i--;
            var prev = i >= 0 ? textBefore[i] : '\n';
            if (prev is '.' or '\n' or '\r' or ';' or '(' or ',')
            {
                ShowCompletion(editor);
            }
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

        var offset = editor.CaretOffset;
        var textBefore = editor.Text.Substring(0, offset);

        if (TryShowArgumentCompletion(editor, textBefore))
        {
            return;
        }

        if (IsInsideStringLiteral(textBefore))
        {
            return;
        }

        var wordStart = GetWordStart(editor.Text, offset);
        var completions = GremlinCompletionProvider.GetCompletions(string.Empty).ToList();
        if (completions.Count == 0)
        {
            return;
        }

        var completionWindow = CreateCompletionWindow(editor, wordStart);
        var data = completionWindow.CompletionList.CompletionData;

        foreach (var item in completions)
        {
            data.Add(item);
        }

        OpenCompletionWindow(editor, completionWindow, textBefore[wordStart..]);
    }

    private static void TryShowLabelOrPropertyCompletion(TextEditor editor)
    {
        if (GetCompletionWindow(editor) != null) return;
        var textBefore = editor.Text.Substring(0, editor.CaretOffset);
        TryShowArgumentCompletion(editor, textBefore);
    }

    private static bool TryShowArgumentCompletion(TextEditor editor, string textBefore)
    {
        var context = GremlinCompletionProvider.GetArgumentCompletion(textBefore);
        if (context == null)
        {
            return false;
        }

        var items = GremlinCompletionProvider.GetArgumentCompletionItems(context);
        if (items.Count == 0)
        {
            // Still suppress step completion inside a string argument.
            return context.HasOpenQuote;
        }

        var prefix = context.Prefix;
        var startOffset = editor.CaretOffset - (context.HasOpenQuote ? prefix.Length : 0);
        var completionWindow = CreateCompletionWindow(editor, startOffset);
        foreach (var item in items)
        {
            completionWindow.CompletionList.CompletionData.Add(item);
        }

        OpenCompletionWindow(editor, completionWindow, context.HasOpenQuote ? prefix : string.Empty);
        return true;
    }

    private static CompletionWindow CreateCompletionWindow(TextEditor editor, int startOffset)
    {
        var completionWindow = new CompletionWindow(editor.TextArea)
        {
            StartOffset = startOffset,
            CloseAutomatically = true,
            CloseWhenCaretAtBeginning = true,
            MinWidth = 260
        };
        completionWindow.CompletionList.IsFiltering = true;
        return completionWindow;
    }

    private static void CloseHoverTooltip(TextEditor editor) =>
        (editor.GetValue(TooltipProviderProperty) as GremlinTooltipProvider)?.Close();

    private static void OpenCompletionWindow(TextEditor editor, CompletionWindow completionWindow, string prefix)
    {
        CloseHoverTooltip(editor);
        SetCompletionWindow(editor, completionWindow);
        completionWindow.Closed += (s, args) => SetCompletionWindow(editor, null);
        completionWindow.Show();

        if (!string.IsNullOrEmpty(prefix))
        {
            completionWindow.CompletionList.SelectItem(prefix);
        }
    }

    private static int GetWordStart(string text, int offset)
    {
        var start = offset;
        while (start > 0 && IsIdentifierChar(text[start - 1]))
        {
            start--;
        }
        return start;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Returns true when the end of <paramref name="textBefore"/> is inside a quoted string on the current line.
    /// </summary>
    private static bool IsInsideStringLiteral(string textBefore)
    {
        var lineStart = textBefore.LastIndexOf('\n') + 1;
        char? quote = null;
        for (var i = lineStart; i < textBefore.Length; i++)
        {
            var c = textBefore[i];
            if (quote != null && c == '\\')
            {
                i++;
                continue;
            }
            if (c is '\'' or '"')
            {
                if (quote == null) quote = c;
                else if (quote == c) quote = null;
            }
        }
        return quote != null;
    }

    /// <summary>
    /// Shows a completion window with variable suggestions from the active variable set.
    /// Triggered when the user types <c>$</c> or presses Tab inside a variable token.
    /// Selecting a completion inserts the full <c>${key}</c> token (the <c>$</c> already
    /// in the document is included by <see cref="VariableCompletionData.Complete"/>).
    /// </summary>
    private static void ShowVariableCompletion(TextEditor editor)
    {
        var existingWindow = GetCompletionWindow(editor);
        if (existingWindow != null)
        {
            return;
        }

        var variableItems = GremlinCompletionProvider.GetVariableCompletions(string.Empty).ToList();
        if (variableItems.Count == 0)
        {
            return;
        }

        var completionWindow = new CompletionWindow(editor.TextArea);
        var data = completionWindow.CompletionList.CompletionData;

        foreach (var item in variableItems)
        {
            data.Add(item);
        }

        CloseHoverTooltip(editor);
        SetCompletionWindow(editor, completionWindow);

        completionWindow.Closed += (s, args) => SetCompletionWindow(editor, null);
        completionWindow.Show();
    }

    /// <summary>
    /// Checks whether the caret is inside an open variable token
    /// (i.e. after <c>$</c> or <c>${</c> but before the closing <c>}</c>).
    /// </summary>
    private static bool IsInsideVariableToken(TextEditor editor)
    {
        var offset = editor.CaretOffset;
        var text = editor.Text;

        // Walk backwards from caret to find '$' or '${' without hitting '}' or line break
        for (int i = offset - 1; i >= 0; i--)
        {
            if (text[i] == '$')
                return true;
            if (text[i] == '{' && i > 0 && text[i - 1] == '$')
                return true;
            if (text[i] == '}' || text[i] == '\n' || text[i] == '\r')
                return false;
            if (!char.IsLetterOrDigit(text[i]) && text[i] != '_' && text[i] != '.')
                return false;
        }

        return false;
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
                    SetDiagnostics(_pendingValidationEditor, errors);
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

    private static void OnEnableEpochTooltipsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
        {
            return;
        }

        // Uninstall any existing provider
        if (editor.GetValue(EpochTooltipProviderProperty) is EpochTooltipProvider oldProvider)
        {
            oldProvider.Uninstall();
            editor.SetValue(EpochTooltipProviderProperty, null);
        }

        if ((bool)e.NewValue)
        {
            var provider = new EpochTooltipProvider(editor);
            provider.Install();
            editor.SetValue(EpochTooltipProviderProperty, provider);
        }
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

    /// <summary>
    /// The current validation errors.
    /// </summary>
    public IReadOnlyList<GremlinValidationError> Errors => _errors;

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

            var pen = new System.Windows.Media.Pen(brush, 1.2);
            pen.Freeze();

            foreach (var rect in rects)
            {
                const double amplitude = 2;
                const double step = 3;
                var y = rect.Bottom - amplitude;
                var right = Math.Max(rect.Right, rect.Left + step * 2);
                var geometry = new System.Windows.Media.StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(new System.Windows.Point(rect.Left, y), false, false);
                    var up = true;
                    for (var x = rect.Left + step; x <= right; x += step)
                    {
                        ctx.LineTo(new System.Windows.Point(x, up ? y - amplitude : y), true, true);
                        up = !up;
                    }
                }
                geometry.Freeze();
                drawingContext.DrawGeometry(null, pen, geometry);
            }
        }
    }
}
