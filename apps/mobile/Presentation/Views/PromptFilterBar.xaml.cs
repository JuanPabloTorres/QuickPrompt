using System.Windows.Input;

namespace QuickPrompt.Views;

public partial class PromptFilterBar : ContentView
{
    public PromptFilterBar() => InitializeComponent();

    // ========================== ?? Search Text ==========================
    public static readonly BindableProperty SearchTextProperty =
        BindableProperty.Create(nameof(SearchText), typeof(string), typeof(PromptFilterBar), string.Empty, BindingMode.TwoWay);

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    // ========================== ?? Search Command ==========================
    public static readonly BindableProperty SearchCommandProperty =
        BindableProperty.Create(nameof(SearchCommand), typeof(ICommand), typeof(PromptFilterBar));

    public ICommand SearchCommand
    {
        get => (ICommand)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    // ========================== ?? Selected Filter ==========================
    public static readonly BindableProperty SelectedFilterProperty =
        BindableProperty.Create(nameof(SelectedFilter), typeof(string), typeof(PromptFilterBar), "Today", BindingMode.TwoWay);

    public string SelectedFilter
    {
        get => (string)GetValue(SelectedFilterProperty);
        set => SetValue(SelectedFilterProperty, value);
    }

    public static readonly BindableProperty CategoriesProperty =
    BindableProperty.Create(
        nameof(Categories),
        typeof(IEnumerable<string>),
        typeof(PromptFilterBar),
        propertyChanged: OnCategoriesChanged);

    public IEnumerable<string> Categories
    {
        get => (IEnumerable<string>)GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    private static void OnCategoriesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // opcional: lógica si necesitas hacer algo al cambiar la lista
    }

    public static readonly BindableProperty SelectedCategoryProperty =
    BindableProperty.Create(
        nameof(SelectedCategory),
        typeof(string),
        typeof(PromptFilterBar),
        defaultValue: "All",
        BindingMode.TwoWay);

    public string SelectedCategory
    {
        get => (string)GetValue(SelectedCategoryProperty);
        set => SetValue(SelectedCategoryProperty, value);
    }
}