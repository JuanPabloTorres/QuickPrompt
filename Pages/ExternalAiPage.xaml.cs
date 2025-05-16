namespace QuickPrompt.Pages;

public partial class ExternalAiPage : ContentPage
{
    public ExternalAiPage()
    {
        InitializeComponent();

        // Página predeterminada al cargar la vista
        ExternalAiWebView.Source = "https://chat.openai.com/";
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
}