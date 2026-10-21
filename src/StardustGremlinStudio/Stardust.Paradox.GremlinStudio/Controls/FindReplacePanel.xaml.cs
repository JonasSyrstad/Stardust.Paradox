using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace Stardust.Paradox.GremlinStudio.Controls;

/// <summary>
/// A unified find/replace panel that overlays an AvalonEdit TextEditor.
/// Supports find-only mode (Ctrl+F) and find+replace mode (Ctrl+H).
/// Draggable via the title bar; positioning is managed by the host adorner.
/// </summary>
public partial class FindReplacePanel : UserControl
{
    private TextEditor? _editor;
    private FindReplacePanelAdorner? _adorner;
    private bool _isReplaceVisible;
    private bool _isDragging;
    private Point _dragStart;

    public FindReplacePanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called by the adorner to wire up the drag coordination.
    /// </summary>
    internal void SetAdorner(FindReplacePanelAdorner adorner)
    {
        _adorner = adorner;
    }

    /// <summary>
    /// Opens the panel in find-only mode and attaches it to the specified editor.
    /// </summary>
    public void ShowFind(TextEditor editor)
    {
        _editor = editor;
        SetReplaceVisible(false);
        ShowAndFocus();
    }

    /// <summary>
    /// Opens the panel in find+replace mode and attaches it to the specified editor.
    /// </summary>
    public void ShowReplace(TextEditor editor)
    {
        _editor = editor;
        SetReplaceVisible(true);
        ShowAndFocus();
    }

    /// <summary>
    /// Hides the panel and returns focus to the editor.
    /// </summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        StatusText.Text = string.Empty;
        _editor?.Focus();
    }

    private void ShowAndFocus()
    {
        Visibility = Visibility.Visible;

        // Pre-fill with selected text if available
        if (_editor is not null && _editor.SelectionLength > 0 && !_editor.SelectedText.Contains('\n'))
        {
            FindTextBox.Text = _editor.SelectedText;
        }

        // Trigger re-arrange so the adorner positions the panel
        _adorner?.InvalidateArrange();

        FindTextBox.Focus();
        FindTextBox.SelectAll();
    }

    private void SetReplaceVisible(bool visible)
    {
        _isReplaceVisible = visible;
        ReplaceRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        // E76C = ChevronRight (collapsed), E70D = ChevronDown (expanded)
        ToggleReplaceIcon.Text = visible ? "\uE70D" : "\uE76C";
    }

    private void ToggleReplace_Click(object sender, RoutedEventArgs e)
    {
        SetReplaceVisible(!_isReplaceVisible);
        if (_isReplaceVisible)
        {
            ReplaceTextBox.Focus();
        }
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        DragHandle.CaptureMouse();
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _adorner is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;

        _adorner.MoveByDelta(dx, dy);
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        DragHandle.ReleaseMouseCapture();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();

    private void FindPrevious_Click(object sender, RoutedEventArgs e) => FindPrevious();

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void Replace_Click(object sender, RoutedEventArgs e) => ReplaceCurrent();

    private void ReplaceAll_Click(object sender, RoutedEventArgs e) => ReplaceAll();

    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            FindPrevious();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void ReplaceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ReplaceCurrent();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateMatchCount();
    }

    private void FindNext()
    {
        if (_editor is null || string.IsNullOrEmpty(FindTextBox.Text))
        {
            return;
        }

        var text = _editor.Text;
        var search = FindTextBox.Text;
        var startIndex = _editor.CaretOffset;

        var index = text.IndexOf(search, startIndex, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            // Wrap around
            index = text.IndexOf(search, 0, StringComparison.OrdinalIgnoreCase);
        }

        if (index >= 0)
        {
            _editor.Select(index, search.Length);
            _editor.ScrollTo(_editor.Document.GetLineByOffset(index).LineNumber, 0);
            UpdateMatchCount();
        }
        else
        {
            StatusText.Text = "No matches";
        }
    }

    private void FindPrevious()
    {
        if (_editor is null || string.IsNullOrEmpty(FindTextBox.Text))
        {
            return;
        }

        var text = _editor.Text;
        var search = FindTextBox.Text;
        var startIndex = Math.Max(0, _editor.SelectionStart - 1);

        var index = text.LastIndexOf(search, startIndex, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            // Wrap around from end
            index = text.LastIndexOf(search, text.Length - 1, StringComparison.OrdinalIgnoreCase);
        }

        if (index >= 0)
        {
            _editor.Select(index, search.Length);
            _editor.ScrollTo(_editor.Document.GetLineByOffset(index).LineNumber, 0);
            UpdateMatchCount();
        }
        else
        {
            StatusText.Text = "No matches";
        }
    }

    private void ReplaceCurrent()
    {
        if (_editor is null || string.IsNullOrEmpty(FindTextBox.Text))
        {
            return;
        }

        // If the current selection matches the search, replace it
        if (_editor.SelectionLength > 0
            && string.Equals(_editor.SelectedText, FindTextBox.Text, StringComparison.OrdinalIgnoreCase))
        {
            _editor.Document.Replace(_editor.SelectionStart, _editor.SelectionLength, ReplaceTextBox.Text);
        }

        // Move to next match
        FindNext();
    }

    private void ReplaceAll()
    {
        if (_editor is null || string.IsNullOrEmpty(FindTextBox.Text))
        {
            return;
        }

        var search = FindTextBox.Text;
        var replacement = ReplaceTextBox.Text;
        var count = 0;

        _editor.Document.BeginUpdate();
        try
        {
            var offset = 0;
            while (offset < _editor.Document.TextLength)
            {
                var index = _editor.Text.IndexOf(search, offset, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    break;
                }

                _editor.Document.Replace(index, search.Length, replacement);
                offset = index + replacement.Length;
                count++;
            }
        }
        finally
        {
            _editor.Document.EndUpdate();
        }

        StatusText.Text = $"{count} replacement{(count != 1 ? "s" : "")} made";
    }

    private void UpdateMatchCount()
    {
        if (_editor is null || string.IsNullOrEmpty(FindTextBox.Text))
        {
            StatusText.Text = string.Empty;
            return;
        }

        var count = 0;
        var text = _editor.Text;
        var search = FindTextBox.Text;
        var index = 0;

        while ((index = text.IndexOf(search, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += search.Length;
        }

        StatusText.Text = count == 0 ? "No matches" : $"{count} match{(count != 1 ? "es" : "")}";
    }
}
