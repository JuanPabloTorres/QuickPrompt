using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QuickPrompt.Components
{
    /// <summary>
    /// Component Catalog Page - Interactive showcase of all Design System components.
    /// 
    /// Features:
    /// - Live component examples
    /// - Code snippets
    /// - Design token showcase
    /// - Interactive testing
    /// </summary>
    public partial class ComponentCatalogPage : ContentPage
    {
        public ComponentCatalogPage()
        {
            InitializeComponent();
            BindingContext = new ComponentCatalogViewModel();
        }
    }

    /// <summary>
    /// ViewModel for Component Catalog Page
    /// </summary>
    public partial class ComponentCatalogViewModel : ObservableObject
    {
        /// <summary>
        /// Command to show toast/alert for button interactions
        /// </summary>
        [RelayCommand]
        private async Task ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message))
                message = "Button tapped!";

            await Application.Current?.MainPage?.DisplayAlert(
                "Component Interaction",
                message,
                "OK");
        }

        /// <summary>
        /// Command placeholder for component actions
        /// </summary>
        [RelayCommand]
        private void ComponentAction(string actionName)
        {
            // Placeholder for component interaction logging
            System.Diagnostics.Debug.WriteLine($"[ComponentCatalog] Action: {actionName}");
        }
    }
}
