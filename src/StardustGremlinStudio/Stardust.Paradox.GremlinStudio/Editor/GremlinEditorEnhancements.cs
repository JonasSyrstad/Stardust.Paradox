using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;

namespace Stardust.Paradox.GremlinStudio.Editor;

/// <summary>
/// Visual Studio-like editing behaviors for the Gremlin query editor: auto-closing pairs,
/// bracket matching, folding, line commands, snippet expansion and parameter info.
/// </summary>
internal sealed class GremlinEditorEnhancements
{
    private static readonly Dictionary<char, char> Pairs = new()
    {
        ['('] = ')',
        ['['] = ']',
        ['{'] = '}',
        ['\''] = '\'',
        ['"'] = '"'
    };

    /// <summary>
    /// Built-in snippet shortcuts. <c>|</c> marks the caret position after expansion.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Snippets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["vl"] = "g.V().hasLabel('|')",
        ["el"] = "g.E().hasLabel('|')",
        ["vid"] = "g.V('|')",
        ["vh"] = "g.V().has('|', '')",
        ["vc"] = "g.V().count()|",
        ["ec"] = "g.E().count()|",
        ["gc"] = "g.V().groupCount().by(label)|",
        ["rep"] = "repeat(out()).times(|)",
        ["rpu"] = "repeat(out()).until(|)",
        ["pth"] = "path().by(|)",
        ["vm"] = "valueMap(true)|",
        ["prj"] = "project('|').by()",
        ["addv"] = "g.addV('|').property('id', '')",
        ["adde"] = "g.V('|').addE('').to(g.V(''))",
        ["drop"] = "g.V('|').drop()"
    };

    private readonly TextEditor _editor;
    private readonly BracketHighlightRenderer _bracketRenderer;
    private readonly FoldingManager _foldingManager;
    private readonly DispatcherTimer _foldingTimer;
    private Popup? _parameterPopup;
    private TextBlock? _parameterText;

    private GremlinEditorEnhancements(TextEditor editor)
    {
        _editor = editor;
        _bracketRenderer = new BracketHighlightRenderer();
        _editor.TextArea.TextView.BackgroundRenderers.Add(_bracketRenderer);
        _foldingManager = FoldingManager.Install(_editor.TextArea);
        _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _foldingTimer.Tick += (_, _) =>
        {
            _foldingTimer.Stop();
            UpdateFoldings();
        };
    }

    /// <summary>
    /// Installs the enhancements on <paramref name="editor"/>.
    /// </summary>
    public static GremlinEditorEnhancements Install(TextEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var instance = new GremlinEditorEnhancements(editor);
        editor.TextArea.TextEntering += instance.OnTextEntering;
        editor.TextArea.TextEntered += instance.OnTextEntered;
        editor.TextArea.PreviewKeyDown += instance.OnPreviewKeyDown;
        editor.TextArea.Caret.PositionChanged += instance.OnCaretPositionChanged;
        editor.TextChanged += instance.OnTextChanged;
        editor.LostKeyboardFocus += instance.OnLostFocus;
        instance.UpdateFoldings();
        editor.TextArea.SetValue(InstanceProperty, instance);
        return instance;
    }

    /// <summary>
    /// Removes all handlers and renderers.
    /// </summary>
    public void Uninstall()
    {
        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.TextArea.TextEntered -= OnTextEntered;
        _editor.TextArea.PreviewKeyDown -= OnPreviewKeyDown;
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.TextChanged -= OnTextChanged;
        _editor.LostKeyboardFocus -= OnLostFocus;
        _editor.TextArea.TextView.BackgroundRenderers.Remove(_bracketRenderer);
        _foldingTimer.Stop();
        FoldingManager.Uninstall(_foldingManager);
        HideParameterInfo();
        _editor.TextArea.ClearValue(InstanceProperty);
    }

    /// <summary>
    /// Gets or sets whether a completion window is currently open (suppresses auto-pair type-over conflicts).
    /// </summary>
    public Func<bool>? IsCompletionOpen { get; set; }

    /// <summary>
    /// Gets the active enhancements instance of the editor.
    /// </summary>
    internal static GremlinEditorEnhancements? Get(TextEditor editor) =>
        editor.TextArea.GetValue(InstanceProperty) as GremlinEditorEnhancements;

    private static readonly DependencyProperty InstanceProperty =
        DependencyProperty.RegisterAttached("Instance", typeof(GremlinEditorEnhancements), typeof(GremlinEditorEnhancements));

    #region Auto-closing pairs

    private void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (e.Handled || e.Text.Length != 1 || IsCompletionOpen?.Invoke() == true) return;

        var c = e.Text[0];
        var doc = _editor.Document;
        var caret = _editor.CaretOffset;
        var next = caret < doc.TextLength ? doc.GetCharAt(caret) : '\0';

        // Type-over closing characters
        if (c is ')' or ']' or '}' or '\'' or '"' && next == c && _editor.SelectionLength == 0)
        {
            if (c is '\'' or '"' && !IsInsideString(doc.GetText(0, caret), c))
            {
                // Not inside a string of this quote: fall through to insert a pair.
            }
            else
            {
                _editor.CaretOffset = caret + 1;
                e.Handled = true;
                return;
            }
        }

        if (!Pairs.TryGetValue(c, out var close)) return;

        // Wrap selection
        if (_editor.SelectionLength > 0)
        {
            var start = _editor.SelectionStart;
            var selected = _editor.SelectedText;
            doc.Replace(start, _editor.SelectionLength, $"{c}{selected}{close}");
            _editor.Select(start + 1, selected.Length);
            e.Handled = true;
            return;
        }

        var textBefore = doc.GetText(0, caret);
        if (c is '\'' or '"')
        {
            var prev = caret > 0 ? doc.GetCharAt(caret - 1) : '\0';
            if (char.IsLetterOrDigit(prev) || IsInsideString(textBefore, null)) return;
        }
        else if (IsInsideString(textBefore, null))
        {
            return;
        }

        // Only auto-close when the next char is whitespace, end or a closing char
        if (next != '\0' && !char.IsWhiteSpace(next) && next is not (')' or ']' or '}' or ',' or '.')) return;

        doc.Insert(caret, $"{c}{close}");
        _editor.CaretOffset = caret + 1;
        e.Handled = true;

        // TextEntered is not raised for handled input, so notify listeners explicitly.
        if (c == '(') ShowParameterInfo(force: true);
        PairInserted?.Invoke(c);
    }

    /// <summary>
    /// Raised after an auto-closing pair was inserted (caret is between the pair).
    /// </summary>
    public event Action<char>? PairInserted;

    private static bool IsInsideString(string textBefore, char? quoteFilter)
    {
        var lineStart = textBefore.LastIndexOf('\n') + 1;
        char? quote = null;
        for (var i = lineStart; i < textBefore.Length; i++)
        {
            var ch = textBefore[i];
            if (quote != null && ch == '\\')
            {
                i++;
                continue;
            }
            if (ch is '\'' or '"')
            {
                if (quote == null) quote = ch;
                else if (quote == ch) quote = null;
            }
        }
        return quote != null && (quoteFilter == null || quote == quoteFilter);
    }

    #endregion

    #region Key commands

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Back && mods == ModifierKeys.None && _editor.SelectionLength == 0)
        {
            // Delete empty pair: "(|)" -> "|"
            var caret = _editor.CaretOffset;
            var doc = _editor.Document;
            if (caret > 0 && caret < doc.TextLength
                && Pairs.TryGetValue(doc.GetCharAt(caret - 1), out var close)
                && doc.GetCharAt(caret) == close)
            {
                doc.Remove(caret - 1, 2);
                e.Handled = true;
            }
            return;
        }

        if (key == Key.Tab && mods == ModifierKeys.None && IsCompletionOpen?.Invoke() != true && TryExpandSnippet())
        {
            e.Handled = true;
            return;
        }

        if (key == Key.Escape && _parameterPopup?.IsOpen == true)
        {
            HideParameterInfo();
            e.Handled = true;
            return;
        }

        if (key == Key.D && mods == ModifierKeys.Control)
        {
            DuplicateLine();
            e.Handled = true;
        }
        else if (key == Key.OemQuestion && mods == ModifierKeys.Control)
        {
            ToggleComment();
            e.Handled = true;
        }
        else if (key == Key.Up && mods == ModifierKeys.Alt)
        {
            MoveLines(up: true);
            e.Handled = true;
        }
        else if (key == Key.Down && mods == ModifierKeys.Alt)
        {
            MoveLines(up: false);
            e.Handled = true;
        }
        else if (key == Key.Space && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ShowParameterInfo(force: true);
            e.Handled = true;
        }
        else if (key == Key.M && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ToggleAllFoldings();
            e.Handled = true;
        }
    }

    private (int Start, int End) GetSelectedLineRange()
    {
        var doc = _editor.Document;
        var start = doc.GetLineByOffset(_editor.SelectionStart);
        var endOffset = _editor.SelectionLength > 0 ? _editor.SelectionStart + _editor.SelectionLength - 1 : _editor.SelectionStart;
        var end = doc.GetLineByOffset(Math.Max(_editor.SelectionStart, endOffset));
        return (start.LineNumber, end.LineNumber);
    }

    /// <summary>
    /// Toggles <c>//</c> line comments on the selected lines.
    /// </summary>
    public void ToggleComment()
    {
        var doc = _editor.Document;
        var (startLine, endLine) = GetSelectedLineRange();
        var lines = Enumerable.Range(startLine, endLine - startLine + 1).Select(doc.GetLineByNumber).ToList();
        var nonEmpty = lines.Where(l => doc.GetText(l).Trim().Length > 0).ToList();
        if (nonEmpty.Count == 0) return;

        var allCommented = nonEmpty.All(l => doc.GetText(l).TrimStart().StartsWith("//", StringComparison.Ordinal));
        var indent = nonEmpty.Min(l => doc.GetText(l).Length - doc.GetText(l).TrimStart().Length);

        using (doc.RunUpdate())
        {
            foreach (var line in nonEmpty.OrderByDescending(l => l.Offset))
            {
                var text = doc.GetText(line);
                if (allCommented)
                {
                    var idx = text.IndexOf("//", StringComparison.Ordinal);
                    var len = idx + 2 < text.Length && text[idx + 2] == ' ' ? 3 : 2;
                    doc.Remove(line.Offset + idx, len);
                }
                else
                {
                    doc.Insert(line.Offset + indent, "// ");
                }
            }
        }
    }

    /// <summary>
    /// Duplicates the current line or selection.
    /// </summary>
    public void DuplicateLine()
    {
        var doc = _editor.Document;
        if (_editor.SelectionLength > 0)
        {
            var end = _editor.SelectionStart + _editor.SelectionLength;
            var text = _editor.SelectedText;
            doc.Insert(end, text);
            _editor.Select(end, text.Length);
            return;
        }

        var line = doc.GetLineByOffset(_editor.CaretOffset);
        var column = _editor.CaretOffset - line.Offset;
        var lineText = doc.GetText(line);
        doc.Insert(line.EndOffset, Environment.NewLine + lineText);
        var newLine = doc.GetLineByNumber(line.LineNumber + 1);
        _editor.CaretOffset = newLine.Offset + Math.Min(column, newLine.Length);
    }

    /// <summary>
    /// Moves the selected lines up or down.
    /// </summary>
    public void MoveLines(bool up)
    {
        var doc = _editor.Document;
        var (startLine, endLine) = GetSelectedLineRange();
        if (up && startLine == 1) return;
        if (!up && endLine == doc.LineCount) return;

        var first = doc.GetLineByNumber(startLine);
        var last = doc.GetLineByNumber(endLine);
        var block = doc.GetText(first.Offset, last.EndOffset - first.Offset);
        var caretDelta = _editor.CaretOffset - first.Offset;
        var selLength = _editor.SelectionLength;
        var selDelta = _editor.SelectionStart - first.Offset;

        using (doc.RunUpdate())
        {
            if (up)
            {
                var prev = doc.GetLineByNumber(startLine - 1);
                var prevText = doc.GetText(prev);
                doc.Replace(prev.Offset, last.EndOffset - prev.Offset, block + Environment.NewLine + prevText);
                SetCaretAndSelection(prev.Offset, caretDelta, selDelta, selLength);
            }
            else
            {
                var next = doc.GetLineByNumber(endLine + 1);
                var nextText = doc.GetText(next);
                doc.Replace(first.Offset, next.EndOffset - first.Offset, nextText + Environment.NewLine + block);
                SetCaretAndSelection(first.Offset + nextText.Length + Environment.NewLine.Length, caretDelta, selDelta, selLength);
            }
        }
    }

    private void SetCaretAndSelection(int blockStart, int caretDelta, int selDelta, int selLength)
    {
        if (selLength > 0)
        {
            _editor.Select(blockStart + selDelta, selLength);
        }
        _editor.CaretOffset = Math.Min(_editor.Document.TextLength, blockStart + caretDelta);
    }

    /// <summary>
    /// Expands a snippet shortcut directly before the caret. Returns true when expanded.
    /// </summary>
    public bool TryExpandSnippet()
    {
        var doc = _editor.Document;
        var caret = _editor.CaretOffset;
        var start = caret;
        while (start > 0 && char.IsLetterOrDigit(doc.GetCharAt(start - 1))) start--;
        if (start == caret) return false;

        // Only expand standalone words (start of line/statement or after '.')
        var before = start > 0 ? doc.GetCharAt(start - 1) : '\n';
        if (!char.IsWhiteSpace(before) && before is not ('.' or ';' or '(' or ',')) return false;

        var word = doc.GetText(start, caret - start);
        if (!Snippets.TryGetValue(word, out var template)) return false;

        // Steps after '.' should not repeat the traversal source.
        if (before == '.' && template.StartsWith("g.", StringComparison.Ordinal)) return false;

        var caretIndex = template.IndexOf('|');
        var text = template.Replace("|", string.Empty);
        doc.Replace(start, caret - start, text);
        _editor.CaretOffset = start + (caretIndex >= 0 ? caretIndex : text.Length);
        return true;
    }

    #endregion

    #region Bracket matching and folding

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        UpdateBracketHighlight();
        if (_parameterPopup?.IsOpen == true)
        {
            ShowParameterInfo(force: false);
        }
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        _foldingTimer.Stop();
        _foldingTimer.Start();
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e) => HideParameterInfo();

    private void UpdateBracketHighlight()
    {
        var doc = _editor.Document;
        var caret = _editor.CaretOffset;
        _bracketRenderer.Clear();

        int? at = null;
        if (caret < doc.TextLength && "()[]{}".Contains(doc.GetCharAt(caret))) at = caret;
        else if (caret > 0 && "()[]{}".Contains(doc.GetCharAt(caret - 1))) at = caret - 1;

        if (at is { } open)
        {
            var match = FindMatchingBracket(doc.Text, open);
            if (match >= 0)
            {
                _bracketRenderer.Set(open, match);
            }
        }

        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    /// <summary>
    /// Finds the offset of the bracket matching the one at <paramref name="offset"/>, or -1.
    /// </summary>
    internal static int FindMatchingBracket(string text, int offset)
    {
        var c = text[offset];
        var (open, close, dir) = c switch
        {
            '(' => ('(', ')', 1),
            ')' => ('(', ')', -1),
            '[' => ('[', ']', 1),
            ']' => ('[', ']', -1),
            '{' => ('{', '}', 1),
            '}' => ('{', '}', -1),
            _ => ('\0', '\0', 0)
        };
        if (dir == 0) return -1;

        var depth = 0;
        for (var i = offset; i >= 0 && i < text.Length; i += dir)
        {
            if (text[i] == open) depth += dir;
            else if (text[i] == close) depth -= dir;
            if (depth == 0) return i;
        }
        return -1;
    }

    private void UpdateFoldings()
    {
        var text = _editor.Document.Text;
        var foldings = new List<NewFolding>();
        var stack = new Stack<int>();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '(' or '[' or '{') stack.Push(i);
            else if (c is ')' or ']' or '}' && stack.Count > 0)
            {
                var start = stack.Pop();
                var startLine = _editor.Document.GetLineByOffset(start).LineNumber;
                var endLine = _editor.Document.GetLineByOffset(i).LineNumber;
                if (endLine > startLine)
                {
                    foldings.Add(new NewFolding(start, i + 1) { Name = $"{text[start]}...{c}" });
                }
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        _foldingManager.UpdateFoldings(foldings, -1);
    }

    private void ToggleAllFoldings()
    {
        var anyOpen = _foldingManager.AllFoldings.Any(f => !f.IsFolded);
        foreach (var f in _foldingManager.AllFoldings)
        {
            f.IsFolded = anyOpen;
        }
    }

    #endregion

    #region Parameter info

    private void OnTextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text is "(" or ",")
        {
            ShowParameterInfo(force: true);
        }
        else if (e.Text == ")" && _parameterPopup?.IsOpen == true)
        {
            ShowParameterInfo(force: false);
        }
    }

    /// <summary>
    /// Shows or refreshes the parameter-info popup for the call surrounding the caret.
    /// </summary>
    public void ShowParameterInfo(bool force)
    {
        var textBefore = _editor.Document.GetText(0, _editor.CaretOffset);
        if (!TryGetCallContext(textBefore, out var stepName, out var argIndex)
            || !GremlinStepDatabase.TryGet(stepName, out var def) || def is null
            || def.Overloads.All(o => o.Parameters.Length == 0))
        {
            HideParameterInfo();
            return;
        }

        if (!force && _parameterPopup?.IsOpen != true) return;

        EnsurePopup();
        _parameterText!.Inlines.Clear();
        var overloads = def.Overloads.Where(o => o.Parameters.Length > 0).ToList();
        var best = overloads.FirstOrDefault(o => o.Parameters.Length > argIndex) ?? overloads[^1];

        foreach (var ov in overloads.Take(6))
        {
            if (_parameterText.Inlines.Count > 0) _parameterText.Inlines.Add(new System.Windows.Documents.LineBreak());
            var isBest = ReferenceEquals(ov, best);
            _parameterText.Inlines.Add(new System.Windows.Documents.Run(def.Name + "(") { FontWeight = isBest ? FontWeights.SemiBold : FontWeights.Normal });
            for (var i = 0; i < ov.Parameters.Length; i++)
            {
                if (i > 0) _parameterText.Inlines.Add(new System.Windows.Documents.Run(", "));
                var p = ov.Parameters[i];
                var label = p.IsOptional ? $"[{p.Name}: {p.TypeName}]" : $"{p.Name}: {p.TypeName}";
                var run = new System.Windows.Documents.Run(label);
                if (isBest && i == Math.Min(argIndex, ov.Parameters.Length - 1))
                {
                    run.FontWeight = FontWeights.Bold;
                    run.TextDecorations = TextDecorations.Underline;
                }
                _parameterText.Inlines.Add(run);
            }
            _parameterText.Inlines.Add(new System.Windows.Documents.Run(")"));
        }

        if (overloads.Count > 6)
        {
            _parameterText.Inlines.Add(new System.Windows.Documents.LineBreak());
            _parameterText.Inlines.Add(new System.Windows.Documents.Run($"+{overloads.Count - 6} more overloads") { FontStyle = FontStyles.Italic });
        }

        var current = best.Parameters[Math.Min(argIndex, best.Parameters.Length - 1)];
        if (!string.IsNullOrEmpty(current.Description) || !string.IsNullOrEmpty(best.Description))
        {
            _parameterText.Inlines.Add(new System.Windows.Documents.LineBreak());
            _parameterText.Inlines.Add(new System.Windows.Documents.Run(current.Description ?? best.Description) { FontStyle = FontStyles.Italic });
        }

        var pos = _editor.TextArea.TextView.GetVisualPosition(_editor.TextArea.Caret.Position, VisualYPosition.LineTop)
                  - _editor.TextArea.TextView.ScrollOffset;
        _parameterPopup!.PlacementTarget = _editor.TextArea.TextView;
        _parameterPopup.HorizontalOffset = pos.X;
        _parameterPopup.VerticalOffset = pos.Y - 4;
        _parameterPopup.IsOpen = true;
    }

    private void EnsurePopup()
    {
        if (_parameterPopup != null) return;

        _parameterText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap
        };
        _parameterText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryForegroundBrush");

        var border = new Border
        {
            Child = _parameterText,
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(1)
        };
        border.SetResourceReference(Border.BackgroundProperty, "SecondaryBackgroundBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        _parameterPopup = new Popup
        {
            Child = border,
            Placement = PlacementMode.Relative,
            AllowsTransparency = true,
            StaysOpen = true,
            Focusable = false
        };
        _parameterPopup.Opened += (_, _) =>
        {
            // Position above the caret line.
            _parameterPopup.VerticalOffset -= border.ActualHeight > 0 ? border.ActualHeight : 20;
        };
    }

    private void HideParameterInfo()
    {
        if (_parameterPopup != null) _parameterPopup.IsOpen = false;
    }

    /// <summary>
    /// Finds the innermost unclosed call before the caret and the current argument index.
    /// </summary>
    internal static bool TryGetCallContext(string textBefore, out string stepName, out int argIndex)
    {
        stepName = string.Empty;
        argIndex = 0;
        var depth = 0;
        char? quote = null;
        var commas = new Stack<int>();
        commas.Push(0);

        // Forward scan to respect strings, tracking comma counts per depth.
        var openPositions = new Stack<int>();
        for (var i = 0; i < textBefore.Length; i++)
        {
            var c = textBefore[i];
            if (quote != null)
            {
                if (c == '\\') i++;
                else if (c == quote) quote = null;
                continue;
            }
            switch (c)
            {
                case '\'' or '"':
                    quote = c;
                    break;
                case '(' or '[' or '{':
                    openPositions.Push(i);
                    commas.Push(0);
                    depth++;
                    break;
                case ')' or ']' or '}':
                    if (openPositions.Count > 0)
                    {
                        openPositions.Pop();
                        commas.Pop();
                        depth--;
                    }
                    break;
                case ',':
                    if (commas.Count > 0) commas.Push(commas.Pop() + 1);
                    break;
                case '\n':
                    break;
            }
        }

        if (openPositions.Count == 0) return false;
        var open = openPositions.Peek();
        if (textBefore[open] != '(') return false;

        var end = open;
        var start = end;
        while (start > 0 && (char.IsLetterOrDigit(textBefore[start - 1]) || textBefore[start - 1] == '_')) start--;
        if (start == end) return false;

        stepName = textBefore[start..end];
        argIndex = commas.Peek();
        return true;
    }

    #endregion

    private sealed class BracketHighlightRenderer : IBackgroundRenderer
    {
        private int _first = -1;
        private int _second = -1;

        public KnownLayer Layer => KnownLayer.Selection;

        public void Set(int first, int second)
        {
            _first = first;
            _second = second;
        }

        public void Clear()
        {
            _first = -1;
            _second = -1;
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_first < 0 || _second < 0) return;
            var brush = new SolidColorBrush(Color.FromArgb(0x55, 0x88, 0x88, 0x88));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(0xAA, 0x88, 0x88, 0x88)), 1);
            brush.Freeze();
            pen.Freeze();

            foreach (var offset in new[] { _first, _second })
            {
                if (offset >= textView.Document.TextLength) continue;
                var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true };
                builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = 1 });
                var geometry = builder.CreateGeometry();
                if (geometry != null) drawingContext.DrawGeometry(brush, pen, geometry);
            }
        }
    }
}
