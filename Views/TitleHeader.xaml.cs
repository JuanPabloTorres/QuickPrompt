using System.Windows.Input;

namespace QuickPrompt.Views;

public partial class TitleHeader : ContentView
{
	public TitleHeader()
	{
		InitializeComponent();
	}

    public static readonly BindableProperty TitleProperty =
     BindableProperty.Create(nameof(Title), typeof(string), typeof(TitleHeader), default(string));

    public static readonly BindableProperty GlyphProperty =
        BindableProperty.Create(nameof(Glyph), typeof(string), typeof(TitleHeader), "\ue5e0");

    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(TitleHeader), "MaterialIconsOutlined-Regular");

    public static readonly BindableProperty BackCommandProperty =
        BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(TitleHeader));

    public static readonly BindableProperty ShowBackButtonProperty =
        BindableProperty.Create(nameof(ShowBackButton), typeof(bool), typeof(TitleHeader), true);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public ICommand BackCommand
    {
        get => (ICommand)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public bool ShowBackButton
    {
        get => (bool)GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        BackCommand?.Execute(null);
    }
}