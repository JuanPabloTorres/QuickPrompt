namespace QuickPrompt.Components.Containers
{
    /// <summary>
    /// OutlinedCard component with border outline and no shadow.
    /// Built with 100% Design System token compliance.
    /// </summary>
    public partial class OutlinedCard : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty BackgroundColorProperty =
            BindableProperty.Create(
                nameof(BackgroundColor),
                typeof(Color),
                typeof(OutlinedCard),
                Application.Current?.Resources["CardBackground"] as Color ?? Colors.White);

        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(
                nameof(BorderColor),
                typeof(Color),
                typeof(OutlinedCard),
                Application.Current?.Resources["BorderLight"] as Color ?? Colors.Gray);

        public static readonly BindableProperty BorderThicknessProperty =
            BindableProperty.Create(
                nameof(BorderThickness),
                typeof(double),
                typeof(OutlinedCard),
                2.0);

        public static readonly BindableProperty PaddingProperty =
            BindableProperty.Create(
                nameof(Padding),
                typeof(Thickness),
                typeof(OutlinedCard),
                Application.Current?.Resources["ThicknessMd"] as Thickness? ?? new Thickness(16));

        public static readonly BindableProperty CornerRadiusProperty =
            BindableProperty.Create(
                nameof(CornerRadius),
                typeof(double),
                typeof(OutlinedCard),
                (double)(Application.Current?.Resources["RadiusMd"] ?? 8.0));

        #endregion

        #region Properties

        public new Color BackgroundColor
        {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }

        public Color BorderColor
        {
            get => (Color)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        public double BorderThickness
        {
            get => (double)GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public new Thickness Padding
        {
            get => (Thickness)GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        #endregion

        #region Constructor

        public OutlinedCard()
        {
            InitializeComponent();

            SemanticProperties.SetDescription(this, "Outlined card container");
            AutomationId = "OutlinedCard";
        }

        #endregion
    }
}
