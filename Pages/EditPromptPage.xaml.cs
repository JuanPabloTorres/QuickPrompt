using QuickPrompt.ViewModels;

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
}