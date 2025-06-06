using QuickPrompt.ViewModels;

namespace QuickPrompt.Pages;

public partial class PromptBuilderPage : ContentPage
{
    private readonly PromptBuilderPageViewModel viewModel;

    public PromptBuilderPage(PromptBuilderPageViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = this.viewModel;

        // Inicialmente, colocamos el carousel en la posición 0
        PromptCarousel.Position = viewModel.CurrentStep;
    }

    private void OnNextClicked(object sender, EventArgs e)
    {
        if (viewModel.CurrentStep < viewModel.Steps.Count - 1)
        {
            viewModel.CurrentStep++;
            PromptCarousel.Position = viewModel.CurrentStep;
        }
        else
        {
            // Si estamos en el último paso (Preview), disparamos el comando FinishPrompt
            viewModel.FinishPromptCommand.Execute(null);
        }
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (viewModel.CurrentStep > 0)
        {
            viewModel.CurrentStep--;
            PromptCarousel.Position = viewModel.CurrentStep;
        }
    }

    private void OnCarouselPositionChanged(object sender, PositionChangedEventArgs e)
    {
        // Cuando el usuario desliza el Carousel, sincronizamos el CurrentStep
        viewModel.CurrentStep = e.CurrentPosition;
    }
}