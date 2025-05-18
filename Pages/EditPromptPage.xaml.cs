using Microsoft.Maui.Controls.Shapes;
using QuickPrompt.Models;
using QuickPrompt.ViewModels;
using System.Text.RegularExpressions;

namespace QuickPrompt.Pages;

public partial class EditPromptPage : ContentPage
{
    public EditPromptPageViewModel _viewModel;

    private bool _buttonPositioned = false;

    public EditPromptPage(EditPromptPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        _viewModel.Initialize(); // Inicializar AdMob cuando la página aparece

        // Solo se activa cuando se renderiza completamente
        this.SizeChanged += OnPageSizeChanged;
    }

    private void OnPageSizeChanged(object sender, EventArgs e)
    {
        if (_buttonPositioned || this.Width <= 0 || this.Height <= 0)
            return;

        const double buttonSize = 50;
        const double visiblePortion = 40;
        const double offset = 12;

        double y = (this.Height - buttonSize) / 2;
        double x = this.Width - visiblePortion - offset;

        AbsoluteLayout.SetLayoutBounds(FloatingButton, new Rect(x, y, buttonSize, buttonSize));

        _buttonPositioned = true;
    }

    private void ResetFloatingButtonPosition(object sender, EventArgs e)
    {
        const double buttonSize = 50;

        const double visiblePortion = 40;

        double y = (this.Height - buttonSize) / 2;

        double x = this.Width - visiblePortion - 12;

        AbsoluteLayout.SetLayoutBounds(FloatingButton, new Rect(x, y, buttonSize, buttonSize));
    }

    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Running && sender is Button btn)
        {
            var layoutBounds = AbsoluteLayout.GetLayoutBounds(btn);

            var newX = layoutBounds.X + e.TotalX;

            var newY = layoutBounds.Y + e.TotalY;

            // Limita movimiento si quieres
            newX = Math.Max(0, Math.Min(this.Width - btn.Width, newX));

            newY = Math.Max(0, Math.Min(this.Height - btn.Height, newY));

            AbsoluteLayout.SetLayoutBounds(btn, new Rect(newX, newY, layoutBounds.Width, layoutBounds.Height));
        }
    }

    private async void OnFloatingButtonTapped(object sender, EventArgs e)
    {
        if (FloatingButton == null)
            return;

        // Animación rápida: achica y vuelve a tamaño
        await FloatingButton.ScaleTo(0.85, 100, Easing.CubicOut);

        await FloatingButton.ScaleTo(1.0, 100, Easing.CubicIn);

        // Ejecutar comando
        if (_viewModel?.CreateVariableCommand?.CanExecute(null) == true)
        {
            _viewModel.CreateVariableCommand.Execute(null);
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    public List<PromptPart> ParsePrompt(string rawText)
    {
        var parts = new List<PromptPart>();
        var regex = new Regex(@"<[^>]+>");
        int lastIndex = 0;

        foreach (Match match in regex.Matches(rawText))
        {
            if (match.Index > lastIndex)
            {
                parts.Add(new PromptPart
                {
                    Text = rawText.Substring(lastIndex, match.Index - lastIndex),
                    IsVariable = false
                });
            }

            parts.Add(new PromptPart
            {
                Text = match.Value,
                IsVariable = true
            });

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < rawText.Length)
        {
            parts.Add(new PromptPart
            {
                Text = rawText.Substring(lastIndex),
                IsVariable = false
            });
        }

        return parts;
    }

    private void RenderPromptAsChips(string promptText)
    {
        PromptChipContainer.Children.Clear();

        if (string.IsNullOrWhiteSpace(promptText))
            return;

        var parts = ParsePrompt(promptText);

        foreach (var part in parts)
        {
            if (part.IsVariable)
            {
                var border = new Border
                {
                    BackgroundColor = Colors.LightBlue,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(10),
                    Margin = new Thickness(4),
                    Content = new Label
                    {
                        Text = part.Text,
                        FontSize = 12,
                        TextColor = Colors.Black
                    }
                };

                border.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        var result = await DisplayPromptAsync("Edit Variable", "Rename It:", $"Changed This Name:{part.Text.Trim('<', '>')}");

                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            part.Text = $"<{result}>";
                            ((Label)border.Content).Text = part.Text;
                            UpdateRawPrompt(parts);
                        }
                    })
                });

                PromptChipContainer.Children.Add(border);
            }
            else
            {
                PromptChipContainer.Children.Add(new Label
                {
                    Text = part.Text,
                    FontSize = 14,
                    TextColor = Colors.Black,
                    Margin = new Thickness(2, 4)
                });
            }
        }
    }

    private void UpdateRawPrompt(List<PromptPart> parts)
    {
        var updated = string.Join("", parts.Select(p => p.Text));

        _viewModel.PromptTemplate.Template = updated; // si usas binding, se actualiza
    }

    private void SwitchToChips(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.PromptTemplate.Template))
        {
            _viewModel.IsVisualModeActive = true;

            RenderPromptAsChips(_viewModel.PromptTemplate.Template);
        }
    }

    private void SwitchToEditor(object sender, EventArgs e)
    {
        _viewModel.IsVisualModeActive = false;
    }
}