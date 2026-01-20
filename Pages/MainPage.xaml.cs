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
        StartSelectionMonitoring();
    }

    private void OnEditorUnfocused(object sender, FocusEventArgs e)
    {
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
        _selectionCheckTimer = new System.Timers.Timer(300); // Check every 300ms
        _selectionCheckTimer.Elapsed += (s, e) => CheckTextSelection();
        _selectionCheckTimer.Start();
    }

    private void StopSelectionMonitoring()
    {
        if (_selectionCheckTimer != null)
        {
            _selectionCheckTimer.Stop();
            _selectionCheckTimer.Dispose();
            _selectionCheckTimer = null;
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
            
            var selectedText = GetSelectedText(editor);
            
            // Check if selection is empty
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                FloatingVariableButton.IsVisible = false;
                FloatingUndoVariableButton.IsVisible = false;
                _selectedText = string.Empty;
                return;
            }

            _selectedText = selectedText;
            
            // ✅ DEBUG: Log to see what we're detecting
            System.Diagnostics.Debug.WriteLine($"Selected text: '{selectedText}'");
            System.Diagnostics.Debug.WriteLine($"Is existing variable: {IsExistingVariable(selectedText)}");
            
            // ✅ NEW: Check if selected text is already a variable
            if (IsExistingVariable(selectedText))
            {
                System.Diagnostics.Debug.WriteLine("Showing UNDO button (red)");
                // Show UNDO button for existing variables
                FloatingVariableButton.IsVisible = false;
                FloatingUndoVariableButton.IsVisible = true;
            }
            else if (IsValidWordSelection(editor, selectedText))
            {
                System.Diagnostics.Debug.WriteLine("Showing CREATE button (blue)");
                // Show CREATE button for valid text selections
                FloatingVariableButton.IsVisible = true;
                FloatingUndoVariableButton.IsVisible = false;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Invalid selection - hiding both buttons");
                // Invalid selection - hide both buttons
                FloatingVariableButton.IsVisible = false;
                FloatingUndoVariableButton.IsVisible = false;
            }
        });
    }

    private string GetSelectedText(Editor editor)
    {
        if (string.IsNullOrEmpty(editor.Text))
            return string.Empty;

        int start = editor.CursorPosition;
        int length = editor.SelectionLength;

        if (length <= 0 || start < 0 || start + length > editor.Text.Length)
            return string.Empty;

        return editor.Text.Substring(start, length);
    }

    private bool IsExistingVariable(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        var trimmed = text.Trim();
        
        // ✅ FIX: More robust variable detection
        bool hasAngleBrackets = trimmed.StartsWith("<") && trimmed.EndsWith(">");
        bool hasContent = trimmed.Length > 2; // At least <x>
        bool noNestedBrackets = !trimmed.Substring(1, trimmed.Length - 2).Contains('<') && 
                               !trimmed.Substring(1, trimmed.Length - 2).Contains('>');
        
        System.Diagnostics.Debug.WriteLine($"IsExistingVariable check: text='{trimmed}', hasAngleBrackets={hasAngleBrackets}, hasContent={hasContent}, noNested={noNestedBrackets}");
        
        return hasAngleBrackets && hasContent && noNestedBrackets;
    }

    private bool IsValidWordSelection(Editor editor, string selectedText)
    {
        if (string.IsNullOrWhiteSpace(selectedText))
            return false;

        int start = editor.CursorPosition;
        int length = editor.SelectionLength;

        // Must have actual selection
        if (length <= 0)
            return false;

        // Check if selection starts and ends at word boundaries
        bool startsAtWordBoundary = start == 0 || 
                                    char.IsWhiteSpace(editor.Text[start - 1]) || 
                                    char.IsPunctuation(editor.Text[start - 1]);

        bool endsAtWordBoundary = (start + length >= editor.Text.Length) || 
                                  char.IsWhiteSpace(editor.Text[start + length]) || 
                                  char.IsPunctuation(editor.Text[start + length]);

        // Selection should not be already a variable (wrapped in < >)
        bool isNotAlreadyVariable = !IsExistingVariable(selectedText);

        return startsAtWordBoundary && endsAtWordBoundary && isNotAlreadyVariable;
    }

    private async void OnCreateVariableFromSelection(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            FloatingVariableButton.IsVisible = false;
            return;
        }

        // Store selection info before dialog
        var editor = PromptRawEditor;
        int selectionStart = editor.CursorPosition;
        int selectionLength = editor.SelectionLength;

        // Get variable name from user
        var variableName = await DisplayPromptAsync(
            "✨ Create Variable",
            "Enter variable name:",
            initialValue: _selectedText.Replace(" ", "_"),
            placeholder: "variable_name",
            accept: "Create",
            cancel: "Cancel");

        // ✅ IMPROVEMENT: Hide button regardless of user choice (Create or Cancel)
        FloatingVariableButton.IsVisible = false;

        // ✅ IMPROVEMENT: If user cancelled, clear selection and return
        if (string.IsNullOrWhiteSpace(variableName))
        {
            _selectedText = string.Empty;
            editor.SelectionLength = 0; // Clear selection
            return;
        }

        // ✅ IMPROVEMENT: Clean variable name (remove spaces, special chars)
        variableName = Regex.Replace(variableName, @"[^\w]", "_").Trim('_');
        
        // ✅ IMPROVEMENT: Don't create if name is empty after cleaning
        if (string.IsNullOrWhiteSpace(variableName))
        {
            _selectedText = string.Empty;
            editor.SelectionLength = 0;
            return;
        }

        // Replace selected text with variable
        if (selectionStart >= 0 && selectionLength > 0 && 
            !string.IsNullOrEmpty(editor.Text) && 
            selectionStart + selectionLength <= editor.Text.Length)
        {
            var newText = editor.Text.Remove(selectionStart, selectionLength)
                                     .Insert(selectionStart, $"<{variableName}>");
            
            _viewModel.PromptText = newText;
            
            // ✅ IMPROVEMENT: Position cursor after the new variable
            await Task.Delay(50); // Small delay to ensure text is updated
            editor.CursorPosition = selectionStart + variableName.Length + 2;
            editor.SelectionLength = 0;
        }

        _selectedText = string.Empty;
    }

    private async void OnUndoVariableFromSelection(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedText) || !IsExistingVariable(_selectedText))
        {
            FloatingUndoVariableButton.IsVisible = false;
            return;
        }

        var editor = PromptRawEditor;
        int selectionStart = editor.CursorPosition;
        int selectionLength = editor.SelectionLength;

        // ✅ Hide button immediately
        FloatingUndoVariableButton.IsVisible = false;

        // Ask for confirmation
        bool confirm = await DisplayAlert(
            "🔄 Remove Variable",
            $"Convert '{_selectedText}' back to plain text?",
            "Remove",
            "Cancel");

        if (!confirm)
        {
            _selectedText = string.Empty;
            editor.SelectionLength = 0;
            return;
        }

        // Remove < > from the variable
        var plainText = _selectedText.Trim('<', '>');

        // Replace variable with plain text
        if (selectionStart >= 0 && selectionLength > 0 && 
            !string.IsNullOrEmpty(editor.Text) && 
            selectionStart + selectionLength <= editor.Text.Length)
        {
            var newText = editor.Text.Remove(selectionStart, selectionLength)
                                     .Insert(selectionStart, plainText);
            
            _viewModel.PromptText = newText;
            
            // Position cursor after the plain text
            await Task.Delay(50);
            editor.CursorPosition = selectionStart + plainText.Length;
            editor.SelectionLength = 0;
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

            // If no prompt text, show empty state message
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

            // Get current theme colors
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var chipTextColor = _themeService.GetColor("PrimaryBlueDark");
            var chipBackgroundColor = _themeService.GetColor(isDark ? "SurfaceElevatedDark" : "SurfaceElevatedLight");
            var chipBorderColor = _themeService.GetColor("PrimaryBlueLight");

            // Always render all parts (text and variables)
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