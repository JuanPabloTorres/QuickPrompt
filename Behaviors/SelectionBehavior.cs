using Microsoft.Maui.Controls;
using System.Reflection;

namespace QuickPrompt.Behaviors;

public class SelectionBehavior : Behavior<Editor>
{
    public static readonly BindableProperty SelectionCommandProperty =
        BindableProperty.Create(nameof(SelectionCommand), typeof(Command<(int start, int length)>), typeof(SelectionBehavior));

    public Command<(int start, int length)> SelectionCommand
    {
        get => (Command<(int, int)>)GetValue(SelectionCommandProperty);
        set => SetValue(SelectionCommandProperty, value);
    }

    private Editor _editor;

    protected override void OnAttachedTo(Editor editor)
    {
        base.OnAttachedTo(editor);
        _editor = editor;
        _editor.TextChanged += OnTextChanged;
    }

    protected override void OnDetachingFrom(Editor editor)
    {
        base.OnDetachingFrom(editor);
        _editor.TextChanged -= OnTextChanged;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        int cursorPosition = _editor.GetType().GetProperty("CursorPosition", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_editor) as int? ?? 0;
        int selectionLength = _editor.GetType().GetProperty("SelectionLength", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_editor) as int? ?? 0;

        if (SelectionCommand?.CanExecute(null) == true)
        {
            SelectionCommand.Execute((cursorPosition, selectionLength));
        }
    }

}
