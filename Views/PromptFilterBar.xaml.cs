using System.Windows.Input;

namespace QuickPrompt.Views;

public partial class PromptFilterBar : ContentView
{
    public PromptFilterBar() => InitializeComponent();

    // ========================== ?? Search Text ==========================
    public static readonly BindableProperty SearchTextProperty =
        BindableProperty.Create(nameof(SearchText), typeof(string), typeof(PromptFilterBar), string.Empty, BindingMode.TwoWay);

    // ========================== ?? Search Command ==========================
    public static readonly BindableProperty SearchCommandProperty =
        BindableProperty.Create(nameof(SearchCommand), typeof(ICommand), typeof(PromptFilterBar));

    // ========================== ?? Selected Filter ==========================
    public static readonly BindableProperty SelectedFilterProperty =
        BindableProperty.Create(nameof(SelectedFilter), typeof(string), typeof(PromptFilterBar), "Today", BindingMode.TwoWay);

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public ICommand SearchCommand
    {
        get => (ICommand)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    public string SelectedFilter
    {
        get => (string)GetValue(SelectedFilterProperty);
        set => SetValue(SelectedFilterProperty, value);
    }
}