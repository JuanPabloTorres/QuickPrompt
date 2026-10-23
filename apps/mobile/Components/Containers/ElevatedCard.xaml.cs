namespace QuickPrompt.Components.Containers
{
    /// <summary>
    /// ElevatedCard component with shadow elevation.
    /// Built with 100% Design System token compliance.
    /// ? PHASE 4: Safe resource access to prevent NullReferenceException
    /// ? FIX: Renamed BackgroundColor to CardBackground to avoid property conflict
    /// </summary>
    public partial class ElevatedCard : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty CardBackgroundProperty =
            BindableProperty.Create(
                nameof(CardBackground),
                typeof(Color),
                typeof(ElevatedCard),
                GetSafeColor("CardBackground", Colors.White));

        public static readonly BindableProperty CardPaddingProperty =
            BindableProperty.Create(
                nameof(CardPadding),
                typeof(Thickness),
                typeof(ElevatedCard),
                GetSafeThickness("ThicknessMd", new Thickness(16)));

        public static readonly BindableProperty CardCornerRadiusProperty =
            BindableProperty.Create(
                nameof(CardCornerRadius),
                typeof(double),
                typeof(ElevatedCard),
                GetSafeDouble("RadiusMd", 8.0));

        #endregion

        #region Properties

        public Color CardBackground
        {
            get => (Color)GetValue(CardBackgroundProperty);
            set => SetValue(CardBackgroundProperty, value);
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

        public ElevatedCard()
        {
            InitializeComponent();

            SemanticProperties.SetDescription(this, "Elevated card container");
            AutomationId = "ElevatedCard";
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
