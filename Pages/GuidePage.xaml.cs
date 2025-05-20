using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;
using QuickPrompt.Models;
using QuickPrompt.Tools;
using QuickPrompt.Tools.Messages;
using System.Collections.ObjectModel;

namespace QuickPrompt.Pages;

public partial class GuidePage : ContentPage, IQueryAttributable
{
    public ObservableCollection<GuideStep> GuideSteps { get; } = new();

    public GuidePage()
    {
        InitializeComponent();

        BindingContext = this;

        LoadSteps();

        UpdateButtonStates();
    }

    public GuidePage(bool showbackButton)
    {
        InitializeComponent();

        BindingContext = this;

        LoadSteps();

        UpdateButtonStates();

        titleHeader.ShowBackButton = showbackButton;
    }

    private void LoadSteps()
    {
        GuideSteps.Add(new GuideStep { Title = "AI-Powered Creativity", Description = "QuickPrompt helps you create, reuse, and organize AI prompts for ChatGPT, Gemini, Grok or Copilot — faster and smarter." });

        GuideSteps.Add(new GuideStep { Title = "✨ What is a Prompt?", Description = "A prompt is the instruction you give an AI to generate a response or content." });

        GuideSteps.Add(new GuideStep { Title = "👉 Choose or Create a Prompt", Description = "Pick one from our library or start from scratch. Personalize it!" });

        GuideSteps.Add(new GuideStep { Title = "✔️ Add Variables", Description = "Use ⟨angle brackets⟩ to mark values you’ll complete later.", Example = "Write an ad for ⟨product_name⟩ targeting ⟨audience⟩." });

        GuideSteps.Add(new GuideStep { Title = "✍️ Fill in the Variables", Description = "Tap to insert values fast.", Example = "⟨product_name⟩ → QuickPrompt\n⟨audience⟩ → Beginners" });

        GuideSteps.Add(new GuideStep { Title = "📋 Copy and Use Prompt", Description = "Copy and paste the generated prompt into any AI tool." });

        GuideSteps.Add(new GuideStep { Title = "⭐ Save Favorites", Description = "Tap ⚡ to save prompts and reuse them anytime." });

        GuideSteps.Add(new GuideStep { Title = "♻️ Reuse Final Prompts", Description = "Every filled prompt is saved for you to reuse fast." });

        GuideSteps.Add(new GuideStep { Title = "🌐 AI Web Tab", Description = "Launch ChatGPT, Gemini, Grok or Copilot with your prompt preloaded." });

        GuideSteps.Add(new GuideStep { Title = "🔍 Full Example", Description = "\nResult: Write an ad for QuickPrompt for beginners.", Example = "Prompt: Write an ad for ⟨product_name⟩  for ⟨audience⟩." });

        GuideSteps.Add(new GuideStep { Title = "🚀 Ready to Try?", Description = "Tap below to create your first prompt and unlock your AI’s potential!", IsFinalStep = true });
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (GuideCarousel.Position < GuideSteps.Count - 1)
        {
            GuideCarousel.Position++;
        }
        else
        {
            string message = GuideMessages.GetRandomGuideCompleteMessage();

            await GenericToolBox.ShowLottieMessageAsync("CompleteAnimation.json", message);

            // Esperar a que la animación sea visible por un tiempo suficiente
            await Task.Delay(1000); // espera 2 segundos (ajustable)

            await Shell.Current.GoToAsync("..");

            await Shell.Current.GoToAsync("//AIWeb");
        }

        UpdateButtonStates();
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (GuideCarousel.Position > 0)
        {
            GuideCarousel.Position--;
        }

        UpdateButtonStates();
    }

    private void OnCarouselPositionChanged(object sender, PositionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        // Habilitar botón de retroceso solo si no estamos en el primer paso
        BackButton.IsEnabled = GuideCarousel.Position > 0;

        // Verificar si estamos en el paso final
        bool isFinal = GuideSteps[GuideCarousel.Position].IsFinalStep;

        if (isFinal)
        {
            NextButton.Text = "⚡ Start Now";

            NextButton.ImageSource = null; // Remueve el ícono si no es necesario

            NextButton.BackgroundColor = Color.FromArgb("#23486A"); // PrimaryRed

            NextButton.TextColor = Colors.White;
        }
        else
        {
            NextButton.Text = string.Empty;

            NextButton.ImageSource = new FontImageSource
            {
                FontFamily = "MaterialIconsOutlined-Regular",
                Glyph = "\ue5e1", // Icono de flecha adelante
                Color = Colors.White
            };
            NextButton.BackgroundColor = Color.FromArgb("#EFB036"); // PrimaryYellow
        }
    }

    private async void OnNavigateToCreatePrompt(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");

        await Shell.Current.GoToAsync("//Create");
    }

    [RelayCommand]
    public async Task MyBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("showbackButton", out var value) && bool.TryParse(value?.ToString(), out var isVisible))
        {
            titleHeader.ShowBackButton = isVisible;
        }
        else
        {
            titleHeader.ShowBackButton = false; // Valor por defecto
        }
    }

    protected override void OnDisappearing()
    {
        TabBarHelperTool.SetVisibility(true);
    }
}