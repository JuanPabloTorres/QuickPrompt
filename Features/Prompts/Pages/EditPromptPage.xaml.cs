using Microsoft.Maui.Controls.Shapes;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Models;
using QuickPrompt.ViewModels;
using System.Text.RegularExpressions;

namespace QuickPrompt.Pages;

public partial class EditPromptPage : ContentPage
{
    private readonly EditPromptPageViewModel _viewModel;
    private readonly IThemeService _themeService;
    private string _selectedText = string.Empty;
    private System.Timers.Timer? _selectionCheckTimer;
    
    private int _capturedCursorPosition;
    private int _capturedSelectionLength;

    public EditPromptPage(EditPromptPageViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.IsVisualModeActive))
        {
            if (_viewModel.IsVisualModeActive)
                RenderChips();
            else
                MainThread.BeginInvokeOnMainThread(() => PromptChipContainer.Children.Clear());
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel?.Initialize();
        if (_viewModel?.IsVisualModeActive == true) RenderChips();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        StopSelectionMonitoring();
    }

    #region Selection Monitoring

    private void OnEditorFocused(object sender, FocusEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[EditPrompt] Editor Focused - Starting selection monitoring");
        StartSelectionMonitoring();
    }

    private void OnEditorUnfocused(object sender, FocusEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[EditPrompt] Editor Unfocused - Stopping monitoring");
        StopSelectionMonitoring();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Clear selection state when editor loses focus
            _selectedText = string.Empty;
        });
    }

    private void StartSelectionMonitoring()
    {
        _selectionCheckTimer = new System.Timers.Timer(300);
        _selectionCheckTimer.Elapsed += (s, e) => CheckTextSelection();
        _selectionCheckTimer.Start();
        System.Diagnostics.Debug.WriteLine("[EditPrompt][Timer] Selection monitoring started");
    }

    private void StopSelectionMonitoring()
    {
        if (_selectionCheckTimer != null)
        {
            _selectionCheckTimer.Stop();
            _selectionCheckTimer.Dispose();
            _selectionCheckTimer = null;
            System.Diagnostics.Debug.WriteLine("[EditPrompt][Timer] Selection monitoring stopped");
        }
    }

    private void CheckTextSelection()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var editor = PromptRawEditor;
            
            if (editor == null || string.IsNullOrEmpty(editor.Text))
            {
                System.Diagnostics.Debug.WriteLine("[EditPrompt][CheckTextSelection] EARLY EXIT: Editor null or empty text");
                _selectedText = string.Empty;
                return;
            }
            
            int cursorPos = editor.CursorPosition;
            int selectionLen = editor.SelectionLength;
            
            System.Diagnostics.Debug.WriteLine($"[EditPrompt][Selection] Editor.Cursor: {cursorPos}, Editor.Length: {selectionLen}");
            
            var selectedText = GetSelectedTextFromEditor(editor);
            
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                System.Diagnostics.Debug.WriteLine("[EditPrompt][CheckTextSelection] EARLY EXIT: selectedText is null or whitespace");
                _selectedText = string.Empty;
                return;
            }

            _selectedText = selectedText;
            _capturedCursorPosition = cursorPos;
            _capturedSelectionLength = selectionLen;
            
            bool isVariable = IsSelectionAVariable(editor.Text, cursorPos, selectionLen, selectedText);
            
            System.Diagnostics.Debug.WriteLine($"[EditPrompt][Detection] Selected: '{selectedText}', IsVariable: {isVariable}");

            // No floating buttons anymore; chips/UI respond via toolbar and inline chips.
        });
    }

    #endregion

    /// <summary>
    /// Detecta si la selección es una variable
    /// </summary>
    private bool IsSelectionAVariable(string fullText, int cursorPos, int selectionLen, string selectedText)
    {
        if (IsTextAVariable(selectedText)) return true;
        return IsSelectionWrappedInBrackets(fullText, cursorPos, selectionLen);
    }

    private bool IsSelectionWrappedInBrackets(string fullText, int cursorPos, int selectionLen)
    {
        if (cursorPos < 1 || cursorPos + selectionLen >= fullText.Length) return false;
        char before = fullText[cursorPos - 1];
        char after = fullText[cursorPos + selectionLen];
        return before == '<' && after == '>';
    }

    private void ExpandSelectionToIncludeBrackets(string fullText, ref int cursorPos, ref int selectionLen)
    {
        int newStart = cursorPos - 1;
        int newLen = selectionLen + 2;
        if (newStart >= 0 && newStart + newLen <= fullText.Length)
        {
            _capturedCursorPosition = newStart;
            _capturedSelectionLength = newLen;
            _selectedText = fullText.Substring(newStart, newLen);
        }
    }

    private bool IsTextAVariable(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (trimmed.Length < 3) return false;
        if (trimmed[0] != '<' || trimmed[^1] != '>') return false;
        var inner = trimmed.Substring(1, trimmed.Length - 2);
        if (string.IsNullOrWhiteSpace(inner)) return false;
        if (inner.Contains('<') || inner.Contains('>')) return false;
        return true;
    }

    private string GetSelectedTextFromEditor(Editor editor)
    {
        if (string.IsNullOrEmpty(editor.Text)) return string.Empty;
        int start = editor.CursorPosition;
        int length = editor.SelectionLength;
        if (length <= 0 || start < 0 || start + length > editor.Text.Length) return string.Empty;
        return editor.Text.Substring(start, length);
    }

    private async void OnCreateVariableFromSelection(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[EditPrompt][CreateVariable] Called with: '{_selectedText}'");
        if (string.IsNullOrWhiteSpace(_selectedText)) return;

        var editor = PromptRawEditor;
        int selectionStart = _capturedCursorPosition;
        int selectionLength = _capturedSelectionLength;

        var variableName = await DisplayPromptAsync(
            "✨ Create Variable",
            "Enter variable name:",
            initialValue: _selectedText.Replace(" ", "_"),
            placeholder: "variable_name",
            accept: "Create",
            cancel: "Cancel");

        _selectedText = string.Empty;
        if (string.IsNullOrWhiteSpace(variableName))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (editor != null)
                {
                    editor.SelectionLength = 0;
                    _viewModel.SelectionLength = 0;
                }
            });
            return;
        }

        variableName = Regex.Replace(variableName, @"[^\w]", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(variableName))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (editor != null)
                {
                    editor.SelectionLength = 0;
                    _viewModel.SelectionLength = 0;
                }
            });
            return;
        }

        var currentTemplate = _viewModel.PromptTemplate?.Template ?? string.Empty;
        if (selectionStart >= 0 && selectionLength > 0 &&
            !string.IsNullOrEmpty(currentTemplate) &&
            selectionStart + selectionLength <= currentTemplate.Length)
        {
            var newText = currentTemplate.Remove(selectionStart, selectionLength)
                                         .Insert(selectionStart, $"<{variableName}>");
            _viewModel.PromptTemplate.Template = newText;

            await Task.Delay(50);
            _viewModel.CursorPosition = selectionStart + variableName.Length + 2;
            _viewModel.SelectionLength = 0;
            if (editor != null)
            {
                editor.CursorPosition = _viewModel.CursorPosition;
                editor.SelectionLength = 0;
            }
        }
    }

    private async void OnUndoVariableFromSelection(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[EditPrompt][UndoVariable] Called with: '{_selectedText}'");
        if (string.IsNullOrWhiteSpace(_selectedText) || !IsTextAVariable(_selectedText)) return;

        int selectionStart = _capturedCursorPosition;
        int selectionLength = _capturedSelectionLength;

        bool confirm = await DisplayAlert(
            "🔄 Remove Variable",
            $"Convert '{_selectedText}' back to plain text?",
            "Remove",
            "Cancel");

        _selectedText = string.Empty;
        if (!confirm)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var editor = PromptRawEditor;
                if (editor != null)
                {
                    editor.SelectionLength = 0;
                    _viewModel.SelectionLength = 0;
                }
            });
            return;
        }

        var currentTemplate = _viewModel.PromptTemplate?.Template ?? string.Empty;
        if (!string.IsNullOrEmpty(currentTemplate) &&
            selectionStart >= 0 && selectionLength > 0 &&
            selectionStart + selectionLength <= currentTemplate.Length)
        {
            var selectedVariable = currentTemplate.Substring(selectionStart, selectionLength);
            var plainText = selectedVariable.Trim().Trim('<', '>');
            var newText = currentTemplate.Remove(selectionStart, selectionLength)
                                         .Insert(selectionStart, plainText);
            _viewModel.PromptTemplate.Template = newText;

            await Task.Delay(50);
            var newCursorPosition = selectionStart + plainText.Length;
            _viewModel.CursorPosition = newCursorPosition;
            _viewModel.SelectionLength = 0;
            var editor = PromptRawEditor;
            if (editor != null)
            {
                editor.CursorPosition = newCursorPosition;
                editor.SelectionLength = 0;
            }
        }
    }

    #region Chips Rendering

    private void RenderChips()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PromptChipContainer.Children.Clear();
            var template = _viewModel?.PromptTemplate?.Template;

            if (string.IsNullOrWhiteSpace(template))
            {
                PromptChipContainer.Children.Add(new Label
                {
                    Text = "📝 Write your prompt in Text mode first.\nUse <variable_name> syntax to create variables.",
                    FontSize = 13,
                    FontFamily = "Nasa21",
                    TextColor = Color.FromArgb("#6B7280"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap,
                    Margin = new Thickness(8)
                });
                return;
            }

            var parts = ParsePrompt(template);
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var chipTextColor = _themeService.GetColor("PrimaryBlueDark");
            var chipBackgroundColor = _themeService.GetColor(isDark ? "SurfaceElevatedDark" : "SurfaceElevatedLight");
            var chipBorderColor = _themeService.GetColor("PrimaryBlueLight");

            foreach (var part in parts)
            {
                PromptChipContainer.Children.Add(part.IsVariable
                    ? CreateChip(part, parts, chipTextColor, chipBackgroundColor, chipBorderColor)
                    : CreateTextSpan(part.Text));
            }
        });
    }

    private List<PromptPart> ParsePrompt(string text)
    {
        var parts = new List<PromptPart>();
        var regex = new Regex(@"<[^>]+>");
        int lastIdx = 0;

        foreach (Match m in regex.Matches(text))
        {
            if (m.Index > lastIdx)
                parts.Add(new PromptPart { Text = text[lastIdx..m.Index], IsVariable = false });
            parts.Add(new PromptPart { Text = m.Value, IsVariable = true });
            lastIdx = m.Index + m.Length;
        }
        if (lastIdx < text.Length)
            parts.Add(new PromptPart { Text = text[lastIdx..], IsVariable = false });

        return parts;
    }

    private Border CreateChip(PromptPart part, List<PromptPart> allParts, Color textColor, Color bgColor, Color borderColor)
    {
        var lbl = new Label
        {
            Text = part.Text,
            FontSize = 14,
            FontFamily = "Nasa21",
            TextColor = textColor,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold
        };

        var chip = new Border
        {
            BackgroundColor = bgColor,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = borderColor,
            StrokeThickness = 1,
            Padding = new Thickness(14, 8),
            Margin = new Thickness(4),
            Content = lbl
        };

        chip.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                await chip.ScaleTo(0.95, 50);
                await chip.ScaleTo(1.0, 50);

                var result = await DisplayPromptAsync(
                    "✏️ Edit Variable",
                    "Enter new variable name:",
                    initialValue: part.Text.Trim('<', '>'),
                    placeholder: "variable_name");

                if (!string.IsNullOrWhiteSpace(result))
                {
                    part.Text = $"<{result}>";
                    lbl.Text = part.Text;
                    _viewModel.PromptTemplate.Template = string.Join("", allParts.Select(p => p.Text));
                }
            })
        });

        return chip;
    }

    private Label CreateTextSpan(string text)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var textColor = _themeService.GetColor(isDark ? "TextPrimaryDark" : "TextPrimaryLight");

        return new Label
        {
            Text = text,
            FontSize = 14,
            FontFamily = "Nasa21",
            TextColor = textColor,
            VerticalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(2, 4)
        };
    }

    #endregion
}