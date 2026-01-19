namespace QuickPrompt.Components.Containers
{
    /// <summary>
    /// ElevatedCard component with shadow elevation.
    /// Built with 100% Design System token compliance.
    /// </summary>
    public partial class ElevatedCard : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty BackgroundColorProperty =
            BindableProperty.Create(
                nameof(BackgroundColor),
                typeof(Color),
                typeof(ElevatedCard),
                Application.Current?.Resources["CardBackground"] as Color ?? Colors.White);

        public static readonly BindableProperty PaddingProperty =
            BindableProperty.Create(
                nameof(Padding),
                typeof(Thickness),
                typeof(ElevatedCard),
                Application.Current?.Resources["ThicknessMd"] as Thickness? ?? new Thickness(16));

        public static readonly BindableProperty CornerRadiusProperty =
            BindableProperty.Create(
                nameof(CornerRadius),
                typeof(double),
                typeof(ElevatedCard),
                (double)(Application.Current?.Resources["RadiusMd"] ?? 8.0));

        #endregion

        #region Properties

        public new Color BackgroundColor
        {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
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

        public ElevatedCard()
        {
            InitializeComponent();

            SemanticProperties.SetDescription(this, "Elevated card container");
            AutomationId = "ElevatedCard";
        }

        #endregion
    }
}
