namespace QuickPrompt.Components.Containers
{
    /// <summary>
    /// StandardCard component for grouping content with consistent styling.
    /// Built with 100% Design System token compliance.
    /// ? PHASE 4: Safe resource access to prevent NullReferenceException
    /// ? FIX: Renamed properties to avoid ContentView base property conflicts
    /// </summary>
    public partial class StandardCard : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty CardBackgroundProperty =
            BindableProperty.Create(
                nameof(CardBackground),
                typeof(Color),
                typeof(StandardCard),
                GetSafeColor("CardBackground", Colors.White));

        public static readonly BindableProperty CardBorderColorProperty =
            BindableProperty.Create(
                nameof(CardBorderColor),
                typeof(Color),
                typeof(StandardCard),
                GetSafeColor("BorderLight", Color.FromRgb(229, 231, 235)));

        public static readonly BindableProperty CardBorderThicknessProperty =
            BindableProperty.Create(
                nameof(CardBorderThickness),
                typeof(double),
                typeof(StandardCard),
                1.0);

        public static readonly BindableProperty CardPaddingProperty =
            BindableProperty.Create(
                nameof(CardPadding),
                typeof(Thickness),
                typeof(StandardCard),
                GetSafeThickness("ThicknessMd", new Thickness(16)));

        public static readonly BindableProperty CardCornerRadiusProperty =
            BindableProperty.Create(
                nameof(CardCornerRadius),
                typeof(double),
                typeof(StandardCard),
                GetSafeDouble("RadiusMd", 8.0));

        #endregion

        #region Properties

        public Color CardBackground
        {
            get => (Color)GetValue(CardBackgroundProperty);
            set => SetValue(CardBackgroundProperty, value);
        }

        public Color CardBorderColor
        {
            get => (Color)GetValue(CardBorderColorProperty);
            set => SetValue(CardBorderColorProperty, value);
        }

        public double CardBorderThickness
        {
            get => (double)GetValue(CardBorderThicknessProperty);
            set => SetValue(CardBorderThicknessProperty, value);
        }

        public Thickness CardPadding
        {
            get => (Thickness)GetValue(CardPaddingProperty);
            set => SetValue(CardPaddingProperty, value);
        }

        public double CardCornerRadius
        {
            get => (double)GetValue(CardCornerRadiusProperty);
            set => SetValue(CardCornerRadiusProperty, value);
        }

        #endregion

        #region Constructor

        public StandardCard()
        {
            InitializeComponent();

            SemanticProperties.SetDescription(this, "Card container");
            AutomationId = "StandardCard";
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// ? PHASE 4: Safe color resource access with fallback
        /// </summary>
        private static Color GetSafeColor(string resourceKey, Color fallback)
        {
            try
            {
                if (Application.Current?.Resources == null)
                    return fallback;

                if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) && resource is Color color)
                    return color;

                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// ? PHASE 4: Safe thickness resource access with fallback
        /// </summary>
        private static Thickness GetSafeThickness(string resourceKey, Thickness fallback)
        {
            try
            {
                if (Application.Current?.Resources == null)
                    return fallback;

                if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) && resource is Thickness thickness)
                    return thickness;

                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// ? PHASE 4: Safe double resource access with fallback
        /// </summary>
        private static double GetSafeDouble(string resourceKey, double fallback)
        {
            try
            {
                if (Application.Current?.Resources == null)
                    return fallback;

                if (Application.Current.Resources.TryGetValue(resourceKey, out var resource))
                {
                    if (resource is double d)
                        return d;
                    if (resource is int i)
                        return i;
                }

                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        #endregion
    }
}
