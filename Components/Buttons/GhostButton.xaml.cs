using System.Windows.Input;

namespace QuickPrompt.Components.Buttons
{
    /// <summary>
    /// Ghost button component for subtle tertiary actions.
    /// Built with 100% Design System token compliance.
    /// Uses transparent background with colored text only.
    /// </summary>
    public partial class GhostButton : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(GhostButton), string.Empty);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(GhostButton), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(GhostButton), null);

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(
                nameof(TextColor), 
                typeof(Color), 
                typeof(GhostButton), 
                Application.Current?.Resources["PrimaryBlue"] as Color ?? Colors.Blue);

        public static readonly BindableProperty ImageSourceProperty =
            BindableProperty.Create(
                nameof(ImageSource), 
                typeof(ImageSource), 
                typeof(GhostButton), 
                null,
                propertyChanged: OnImageSourceChanged);

        public static readonly BindableProperty ShowIconProperty =
            BindableProperty.Create(nameof(ShowIcon), typeof(bool), typeof(GhostButton), false);

        public static readonly BindableProperty OpacityProperty =
            BindableProperty.Create(nameof(Opacity), typeof(double), typeof(GhostButton), 1.0);

        #endregion

        #region Properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        public new double Opacity
        {
            get => (double)GetValue(OpacityProperty);
            set => SetValue(OpacityProperty, value);
        }

        #endregion

        #region Constructor

        public GhostButton()
        {
            InitializeComponent();

            SemanticProperties.SetDescription(this, "Ghost action button - tertiary");
            AutomationId = "GhostButton";

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsEnabled))
                {
                    UpdateVisualState();
                }
            };
        }

        #endregion

        #region Event Handlers

        private async void OnButtonTapped(object sender, TappedEventArgs e)
        {
            if (!IsEnabled || sender is not Border border)
                return;

            var animationDuration = (int)Application.Current.Resources["AnimationDurationFast"];

            // Subtle opacity animation for ghost button
            await border.FadeTo(0.5, (uint)(animationDuration / 2), Easing.CubicOut);
            await border.FadeTo(1.0, (uint)(animationDuration / 2), Easing.CubicIn);
        }

        #endregion

        #region Property Changed Handlers

        private static void OnImageSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is GhostButton button)
            {
                button.ShowIcon = newValue != null;
            }
        }

        #endregion

        #region Visual State Management

        private void UpdateVisualState()
        {
            if (!IsEnabled)
            {
                TextColor = (Color)Application.Current.Resources["StateDisabledText"];
                Opacity = (double)Application.Current.Resources["OpacityDisabled"];
            }
            else
            {
                TextColor = (Color)Application.Current.Resources["PrimaryBlue"];
                Opacity = (double)Application.Current.Resources["OpacityFull"];
            }
        }

        #endregion
    }
}
