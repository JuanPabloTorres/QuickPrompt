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
                // ✅ Use ThemeService for empty state text color
                var emptyStateColor = _themeService.GetColor(Application.Current?.RequestedTheme == AppTheme.Dark ? "TextSecondaryDark" : "TextSecondaryLight");
                
                PromptChipContainer.Children.Add(new Label
                {
                    Text = "📝 Write your prompt in Text mode first.\nUse <variable_name> syntax to create variables.",
                    FontSize = 13,
                    FontFamily = "Nasa21",
                    TextColor = emptyStateColor,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap,
                    Margin = new Thickness(8)
                });
                return;
            }

            var parts = ParsePrompt(template);
            foreach (var part in parts)
            {
                PromptChipContainer.Children.Add(part.IsVariable
                    ? CreateChip(part, parts)
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

    private Border CreateChip(PromptPart part, List<PromptPart> allParts)
    {
        // ✅ Use ThemeService for dynamic chip colors
        var chipTextColor = _themeService.GetColor("PrimaryBlueDark");
        var chipBackgroundColor = _themeService.GetColor(Application.Current?.RequestedTheme == AppTheme.Dark ? "SurfaceElevatedDark" : "SurfaceElevatedLight");
        var chipBorderColor = _themeService.GetColor("PrimaryBlueLight");

        var lbl = new Label
        {
            Text = part.Text,
            FontSize = 14,
            FontFamily = "Nasa21",
            TextColor = chipTextColor,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold
        };

        var chip = new Border
        {
            BackgroundColor = chipBackgroundColor,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = chipBorderColor,
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
        // ✅ Use ThemeService for dynamic text color
        var textColor = _themeService.GetColor(Application.Current?.RequestedTheme == AppTheme.Dark ? "TextPrimaryDark" : "TextPrimaryLight");
        
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