using Microsoft.Maui.Controls.Shapes;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.Models;
using QuickPrompt.ViewModels;
using System.Text.RegularExpressions;

namespace QuickPrompt.Pages;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;
    private readonly IThemeService _themeService;
    private string _selectedText = string.Empty;
    private System.Timers.Timer? _selectionCheckTimer;

    public MainPage(MainPageViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.IsVisualModeActive) || e.PropertyName == nameof(_viewModel.PromptText))
        {
            if (_viewModel.IsVisualModeActive)
                RenderChips();
            else
                MainThread.BeginInvokeOnMainThread(() => PromptChipContainer.Children.Clear());
        }
        
        // ✅ Monitor selection changes from ViewModel
        if (e.PropertyName == nameof(_viewModel.SelectionLength) || 
            e.PropertyName == nameof(_viewModel.CursorPosition))
        {
            System.Diagnostics.Debug.WriteLine($"[VM PropertyChanged] Cursor: {_viewModel.CursorPosition}, Length: {_viewModel.SelectionLength}");
            if (_selectionCheckTimer != null && _selectionCheckTimer.Enabled)
            {
                CheckTextSelection();
            }
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

    #region Floating Variable Button

    private void OnEditorFocused(object sender, FocusEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[Editor] Focused - Starting selection monitoring");
        StartSelectionMonitoring();
    }

    private void OnEditorUnfocused(object sender, FocusEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[Editor] Unfocused - Stopping monitoring");
        StopSelectionMonitoring();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FloatingVariableButton.IsVisible = false;
            FloatingUndoVariableButton.IsVisible = false;
            _selectedText = string.Empty;
        });
    }

    private void StartSelectionMonitoring()
    {
        _selectionCheckTimer = new System.Timers.Timer(300);
        _selectionCheckTimer.Elapsed += (s, e) => CheckTextSelection();
        _selectionCheckTimer.Start();
        System.Diagnostics.Debug.WriteLine("[Timer] Selection monitoring started");
    }

    private void StopSelectionMonitoring()
    {
        if (_selectionCheckTimer != null)
        {
            _selectionCheckTimer.Stop();
            _selectionCheckTimer.Dispose();
            _selectionCheckTimer = null;
            System.Diagnostics.Debug.WriteLine("[Timer] Selection monitoring stopped");
        }
    }

    private void CheckTextSelection()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var editor = PromptRawEditor;
            
            if (editor == null || string.IsNullOrEmpty(editor.Text))
            {
                FloatingVariableButton.IsVisible = false;
                FloatingUndoVariableButton.IsVisible = false;
                _selectedText = string.Empty;
                return;
            }
            
            int cursorPos = _viewModel.CursorPosition;
            int selectionLen = _viewModel.SelectionLength;
            
            System.Diagnostics.Debug.WriteLine($"[Selection] Cursor: {cursorPos}, Length: {selectionLen}, Text Length: {editor.Text.Length}");
            
            var selectedText = GetSelectedTextFromViewModel();
            
            System.Diagnostics.Debug.WriteLine($"[Selection] Text: '{selectedText}' (len: {selectedText?.Length ?? 0})");
            
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                FloatingVariableButton.IsVisible = false;
                FloatingUndoVariableButton.IsVisible = false;
                _selectedText = string.Empty;
                return;
            }

            _selectedText = selectedText;
            
            var isVariable = IsExistingVariable(selectedText);
            System.Diagnostics.Debug.WriteLine($"[Detection] IsVariable: {isVariable}");
            
            if (isVariable)
            {
                System.Diagnostics.Debug.WriteLine(">>> SHOWING RED BUTTON (Remove Variable) <<<");
                FloatingVariableButton.IsVisible = false;
                FloatingUndoVariableButton.IsVisible = true;
            }
            else if (IsValidWordSelection(editor, selectedText))
            {
                System.Diagnostics.Debug.WriteLine(">>> SHOWING BLUE BUTTON (Make Variable) <<<");
                FloatingVariableButton.IsVisible = true;
                FloatingUndoVariableButton.IsVisible = false;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(">>> HIDING BOTH BUTTONS (Invalid) <<<");
                FloatingVariableButton.IsVisible = false;
                FloatingUndoVariableButton.IsVisible = false;
            }
        });
    }

    private string GetSelectedTextFromViewModel()
    {
        if (string.IsNullOrEmpty(_viewModel.PromptText))
            return string.Empty;

        int start = _viewModel.CursorPosition;
        int length = _viewModel.SelectionLength;

        if (length <= 0 || start < 0 || start + length > _viewModel.PromptText.Length)
            return string.Empty;

        return _viewModel.PromptText.Substring(start, length);
    }

    private bool IsExistingVariable(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        var trimmed = text.Trim();
        
        bool hasAngleBrackets = trimmed.StartsWith("<") && trimmed.EndsWith(">");
        bool hasContent = trimmed.Length > 2;
        bool noNestedBrackets = trimmed.Length > 2 && 
                               !trimmed.Substring(1, trimmed.Length - 2).Contains('<') && 
                               !trimmed.Substring(1, trimmed.Length - 2).Contains('>');
        
        System.Diagnostics.Debug.WriteLine($"[IsExistingVariable] '{trimmed}': brackets={hasAngleBrackets}, content={hasContent}, noNested={noNestedBrackets}");
        
        return hasAngleBrackets && hasContent && noNestedBrackets;
    }

    private bool IsValidWordSelection(Editor editor, string selectedText)
    {
        if (string.IsNullOrWhiteSpace(selectedText))
            return false;

        int start = _viewModel.CursorPosition;
        int length = _viewModel.SelectionLength;

        if (length <= 0)
            return false;

        bool startsAtWordBoundary = start == 0 || 
                                    char.IsWhiteSpace(editor.Text[start - 1]) || 
                                    char.IsPunctuation(editor.Text[start - 1]);

        bool endsAtWordBoundary = (start + length >= editor.Text.Length) || 
                                  char.IsWhiteSpace(editor.Text[start + length]) || 
                                  char.IsPunctuation(editor.Text[start + length]);

        bool isNotAlreadyVariable = !IsExistingVariable(selectedText);

        System.Diagnostics.Debug.WriteLine($"[IsValidWord] starts={startsAtWordBoundary}, ends={endsAtWordBoundary}, notVar={isNotAlreadyVariable}");

        return startsAtWordBoundary && endsAtWordBoundary && isNotAlreadyVariable;
    }

    private async void OnCreateVariableFromSelection(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[CreateVariable] Called with: '{_selectedText}'");
        
        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            FloatingVariableButton.IsVisible = false;
            return;
        }

        var editor = PromptRawEditor;
        int selectionStart = _viewModel.CursorPosition;
        int selectionLength = _viewModel.SelectionLength;

        var variableName = await DisplayPromptAsync(
            "✨ Create Variable",
            "Enter variable name:",
            initialValue: _selectedText.Replace(" ", "_"),
            placeholder: "variable_name",
            accept: "Create",
            cancel: "Cancel");

        FloatingVariableButton.IsVisible = false;

        if (string.IsNullOrWhiteSpace(variableName))
        {
            System.Diagnostics.Debug.WriteLine("[CreateVariable] Cancelled by user");
            _selectedText = string.Empty;
            _viewModel.SelectionLength = 0;
            return;
        }

        variableName = Regex.Replace(variableName, @"[^\w]", "_").Trim('_');
        
        if (string.IsNullOrWhiteSpace(variableName))
        {
            System.Diagnostics.Debug.WriteLine("[CreateVariable] Invalid name after cleaning");
            _selectedText = string.Empty;
            _viewModel.SelectionLength = 0;
            return;
        }

        if (selectionStart >= 0 && selectionLength > 0 && 
            !string.IsNullOrEmpty(_viewModel.PromptText) && 
            selectionStart + selectionLength <= _viewModel.PromptText.Length)
        {
            var newText = _viewModel.PromptText.Remove(selectionStart, selectionLength)
                                                .Insert(selectionStart, $"<{variableName}>");
            
            _viewModel.PromptText = newText;
            System.Diagnostics.Debug.WriteLine($"[CreateVariable] Created: {newText}");
            
            await Task.Delay(50);
            _viewModel.CursorPosition = selectionStart + variableName.Length + 2;
            _viewModel.SelectionLength = 0;
        }

        _selectedText = string.Empty;
    }

    private async void OnUndoVariableFromSelection(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[UndoVariable] Called with: '{_selectedText}'");
        
        if (string.IsNullOrWhiteSpace(_selectedText) || !IsExistingVariable(_selectedText))
        {
            FloatingUndoVariableButton.IsVisible = false;
            return;
        }

        int selectionStart = _viewModel.CursorPosition;
        int selectionLength = _viewModel.SelectionLength;

        FloatingUndoVariableButton.IsVisible = false;

        bool confirm = await DisplayAlert(
            "🔄 Remove Variable",
            $"Convert '{_selectedText}' back to plain text?",
            "Remove",
            "Cancel");

        if (!confirm)
        {
            System.Diagnostics.Debug.WriteLine("[UndoVariable] Cancelled by user");
            _selectedText = string.Empty;
            _viewModel.SelectionLength = 0;
            return;
        }

        var plainText = _selectedText.Trim('<', '>');

        if (selectionStart >= 0 && selectionLength > 0 && 
            !string.IsNullOrEmpty(_viewModel.PromptText) && 
            selectionStart + selectionLength <= _viewModel.PromptText.Length)
        {
            var newText = _viewModel.PromptText.Remove(selectionStart, selectionLength)
                                                .Insert(selectionStart, plainText);
            
            _viewModel.PromptText = newText;
            System.Diagnostics.Debug.WriteLine($"[UndoVariable] Removed: {newText}");
            
            await Task.Delay(50);
            _viewModel.CursorPosition = selectionStart + plainText.Length;
            _viewModel.SelectionLength = 0;
        }

        _selectedText = string.Empty;
    }

    #endregion

    #region Chips Rendering

    private void RenderChips()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PromptChipContainer.Children.Clear();

            if (string.IsNullOrWhiteSpace(_viewModel?.PromptText))
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

            var parts = ParsePrompt(_viewModel.PromptText);
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
                    _viewModel.PromptText = string.Join("", allParts.Select(p => p.Text));
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