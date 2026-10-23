namespace QuickPrompt.Views;

public partial class ReusableLoadingOverlay : ContentView
{
    public static readonly BindableProperty MessageProperty =
       BindableProperty.Create(nameof(Message), typeof(string), typeof(ReusableLoadingOverlay), "Loading...", propertyChanged: OnMessageChanged);

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private static void OnMessageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ReusableLoadingOverlay control && newValue is string message)
        {
            control.MessageLabel.Text = message;
        }
    }

    public ReusableLoadingOverlay()
    {
        InitializeComponent();
    }
}