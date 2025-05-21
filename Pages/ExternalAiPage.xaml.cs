using QuickPrompt.Models.Enums;
using QuickPrompt.Tools;
using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class ExternalAiPage : ContentPage
{
    private AiLauncherViewModel _aiLauncherViewModel;

    public ExternalAiPage(AiLauncherViewModel aiLauncherViewModel)
    {
        InitializeComponent();

        _aiLauncherViewModel = aiLauncherViewModel;

        BindingContext = _aiLauncherViewModel;

        // Página predeterminada al cargar la vista
        ExternalAiWebView.Source = "https://chat.openai.com/";

        _aiLauncherViewModel.ClearWebViewTextAction = async () =>
        {
            string clearScript = JsInjectionTool.GenerateClearPromptScript(); // Tu método JS

            await ExternalAiWebView.EvaluateJavaScriptAsync(clearScript);
        };
    }
    protected override void OnAppearing()
    {
        Shell.SetTabBarIsVisible(AppShell.Current, true);

        _aiLauncherViewModel.SelectedCategory = string.Empty;
    }

    private void OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        LoadingIndicator.IsVisible = true;
    }

    private void OnNavigated(object sender, WebNavigatedEventArgs e)
    {
        LoadingIndicator.IsVisible = false;
    }

    private void OnChatGptClicked(object sender, EventArgs e)
    {
        ExternalAiWebView.Source = "https://chat.openai.com/";
    }

    private void OnGeminiClicked(object sender, EventArgs e)
    {
        ExternalAiWebView.Source = "https://gemini.google.com/";
    }

    private void OnGrokClicked(object sender, EventArgs e)
    {
        ExternalAiWebView.Source = "https://grok.com/";
    }

    private void OnCopilotClicked(object sender, EventArgs e)
    {
        ExternalAiWebView.Source = "https://copilot.microsoft.com/chats/Wt2qDSvnmnFtgZVr6RQRc/";
    }

    private async void OnFinalPromptSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is string selectedPrompt && !string.IsNullOrWhiteSpace(selectedPrompt))
        {
            string _cleanScript = JsInjectionTool.GenerateClearPromptScript();

            await ExternalAiWebView.EvaluateJavaScriptAsync(_cleanScript);

            string _insertTextToBoxScript = JsInjectionTool.GenerateInsertPromptScript(selectedPrompt); // Usa tu método ya creado

            await ExternalAiWebView.EvaluateJavaScriptAsync(_insertTextToBoxScript);

            // Opcional: limpiar selección visual
            FinalPromptsCollection.SelectedItem = null;
        }
    }

    private void onReloadWebView(object sender, EventArgs e)
    {
        ExternalAiWebView.Reload();
    }
}