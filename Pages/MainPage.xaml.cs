using Microsoft.Maui.Controls.Shapes;
using QuickPrompt.Models;
using QuickPrompt.ViewModels;
using System.Text.RegularExpressions;

namespace QuickPrompt.Pages
{
    public partial class MainPage : ContentPage
    {
        private MainPageViewModel _viewModel;

        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;

            BindingContext = viewModel;

            // Subscribe to property changes for chip rendering
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(_viewModel.IsVisualModeActive))
                {
                    if (_viewModel.IsVisualModeActive && !string.IsNullOrWhiteSpace(_viewModel.PromptText))
                    {
                        RenderPromptAsChips(_viewModel.PromptText);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.OnViewModelPropertyChanged] Error: {ex.Message}");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // ✅ Inicializar AdMob con manejo de errores
                _viewModel?.Initialize();

                // ✅ Validar antes de procesar chips
                if (!string.IsNullOrEmpty(_viewModel?.PromptText))
                {
                    RenderPromptAsChips(_viewModel.PromptText);
                }

                // ✅ Posicionar floating button después de que la página se haya renderizado
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                {
                    PositionFloatingButton();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.OnAppearing] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MainPage.OnAppearing] StackTrace: {ex.StackTrace}");
            }
        }

        // ✅ PHASE 2: Unsubscribe event handlers to prevent memory leaks
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            try
            {
                // Unsubscribe from ViewModel events
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.OnDisappearing] Error: {ex.Message}");
            }
        }

        private void PositionFloatingButton()
        {
            try
            {
                // ✅ Validar que el botón existe y que las dimensiones son válidas
                if (FloatingButton == null || this.Width <= 0 || this.Height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainPage.PositionFloatingButton] Cannot position button. Width={this.Width}, Height={this.Height}, Button={FloatingButton != null}");
                    return;
                }

                const double buttonSize = 50;
                const double visiblePortion = 40;

                // Centro de la pantalla (vertical)
                double y = (this.Height - buttonSize) / 2;

                // Parte derecha sobresaliendo 10px
                double x = this.Width - visiblePortion - 12;

                AbsoluteLayout.SetLayoutBounds(FloatingButton, new Rect(x, y, buttonSize, buttonSize));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.PositionFloatingButton] Error: {ex.Message}");
            }
        }

        private void ResetFloatingButtonPosition(object sender, EventArgs e)
        {
            PositionFloatingButton();
        }

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            if (e.StatusType == GestureStatus.Running && sender is Button btn)
            {
                try
                {
                    var layoutBounds = AbsoluteLayout.GetLayoutBounds(btn);

                    var newX = layoutBounds.X + e.TotalX;
                    var newY = layoutBounds.Y + e.TotalY;

                    // Limita movimiento
                    newX = Math.Max(0, Math.Min(this.Width - btn.Width, newX));
                    newY = Math.Max(0, Math.Min(this.Height - btn.Height, newY));

                    AbsoluteLayout.SetLayoutBounds(btn, new Rect(newX, newY, layoutBounds.Width, layoutBounds.Height));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainPage.OnPanUpdated] Error: {ex.Message}");
                }
            }
        }

        private async void OnFloatingButtonTapped(object sender, EventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.OnFloatingButtonTapped] Error: {ex.Message}");
            }
        }

        public List<PromptPart> ParsePrompt(string rawText)
        {
            var parts = new List<PromptPart>();
            
            try
            {
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.ParsePrompt] Error: {ex.Message}");
            }

            return parts;
        }

        private void RenderPromptAsChips(string promptText)
        {
            try
            {
                PromptChipContainer.Children.Clear();

                if (string.IsNullOrWhiteSpace(promptText))
                    return;

                var parts = ParsePrompt(promptText);

                foreach (var part in parts)
                {
                    if (part.IsVariable)
                    {
                        // ✅ Safely get color resources with fallback
                        Color backgroundColor = GetColorResource("Info200", Color.FromRgba(173, 216, 230, 255)); // Light blue fallback
                        Color textColor = GetColorResource("TextPrimary", Colors.Black);

                        var border = new Border
                        {
                            BackgroundColor = backgroundColor,
                            StrokeShape = new RoundRectangle { CornerRadius = 10 },
                            Padding = new Thickness(10, 5),
                            Margin = new Thickness(4),
                            Content = new Label
                            {
                                Text = part.Text,
                                FontSize = 14,
                                TextColor = textColor
                            }
                        };

                        border.GestureRecognizers.Add(new TapGestureRecognizer
                        {
                            Command = new Command(async () =>
                            {
                                try
                                {
                                    var result = await DisplayPromptAsync("Edit Variable", "Rename It:", initialValue: part.Text.Trim('<', '>'));

                                    if (!string.IsNullOrWhiteSpace(result))
                                    {
                                        part.Text = $"<{result}>";
                                        if (border.Content is Label label)
                                        {
                                            label.Text = part.Text;
                                        }
                                        UpdateRawPrompt(parts);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[MainPage.RenderPromptAsChips.TapGesture] Error: {ex.Message}");
                                }
                            })
                        });

                        PromptChipContainer.Children.Add(border);
                    }
                    else
                    {
                        Color textColor = GetColorResource("TextPrimary", Colors.Black);

                        PromptChipContainer.Children.Add(new Label
                        {
                            Text = part.Text,
                            FontSize = 14,
                            TextColor = textColor,
                            Margin = new Thickness(2, 4)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.RenderPromptAsChips] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MainPage.RenderPromptAsChips] StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Safely retrieves a color resource with fallback
        /// </summary>
        private Color GetColorResource(string resourceKey, Color fallback)
        {
            try
            {
                if (Application.Current?.Resources != null && 
                    Application.Current.Resources.TryGetValue(resourceKey, out var resource) && 
                    resource is Color color)
                {
                    return color;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.GetColorResource] Failed to get resource '{resourceKey}': {ex.Message}");
            }

            return fallback;
        }

        private void UpdateRawPrompt(List<PromptPart> parts)
        {
            try
            {
                var updated = string.Join("", parts.Select(p => p.Text));
                _viewModel.PromptText = updated;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.UpdateRawPrompt] Error: {ex.Message}");
            }
        }

        private void SwitchToChips(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_viewModel.PromptText))
                {
                    _viewModel.IsVisualModeActive = true;
                    RenderPromptAsChips(_viewModel.PromptText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.SwitchToChips] Error: {ex.Message}");
            }
        }

        private void SwitchToEditor(object sender, EventArgs e)
        {
            try
            {
                _viewModel.IsVisualModeActive = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage.SwitchToEditor] Error: {ex.Message}");
            }
        }
    }
}