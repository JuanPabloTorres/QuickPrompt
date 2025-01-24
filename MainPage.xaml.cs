using QuickPrompt.ViewModels;

namespace QuickPrompt
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();

            this.BindingContext = viewModel;
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            if (BindingContext is MainPageViewModel viewModel && sender is Editor editor)
            {
                viewModel.CursorPosition = editor.CursorPosition;

                viewModel.SelectionLength = editor.SelectionLength;

                // Llamar al comando de manejo de selección
                viewModel.HandleSelectionChangedCommand.Execute(null);
            }
        }

        private void OnEditorUnfocused(object sender, FocusEventArgs e)
        {
            if (BindingContext is MainPageViewModel viewModel)
            {

                var editor = sender as Editor;  

                viewModel.CursorPosition = editor.CursorPosition;

                viewModel.SelectionLength = editor.SelectionLength;
            }
        }
    }
}