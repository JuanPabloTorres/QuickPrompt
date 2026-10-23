using System.Windows.Input;

namespace QuickPrompt.Components.Buttons
{
    /// <summary>
    /// Danger button component for destructive actions (delete, remove, etc.).
    /// Built with 100% Design System token compliance.
    /// Uses red/danger color to signal destructive action.
    /// </summary>
    public partial class DangerButton : ContentView
    {
        #region Bindable Properties

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(DangerButton), string.Empty);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(DangerButton), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(DangerButton), null);

        public static readonly BindableProperty BackgroundColorProperty =
            BindableProperty.Create(
                nameof(BackgroundColor), 
                typeof(Color), 
                typeof(DangerButton), 
                Application.Current?.Resources["Danger"] as Color ?? Colors.Red);

        public static readonly BindableProperty TextColorProperty =
            BindableProperty.Create(
                nameof(TextColor), 
                typeof(Color), 
                typeof(DangerButton), 
                Application.Current?.Resources["White"] as Color ?? Colors.White);

        public static readonly BindableProperty ImageSourceProperty =
            BindableProperty.Create(
                nameof(ImageSource), 
                typeof(ImageSource), 
                typeof(DangerButton), 
                null,
                propertyChanged: OnImageSourceChanged);

        public static readonly BindableProperty ShowIconProperty =
            BindableProperty.Create(nameof(ShowIcon), typeof(bool), typeof(DangerButton), false);

        public static readonly BindableProperty OpacityProperty =
            BindableProperty.Create(nameof(Opacity), typeof(double), typeof(DangerButton), 1.0);

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

        public new Color BackgroundColor
        {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
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

        public DangerButton()
        {
            InitializeComponent();

            SemanticProperties.SetDescription(this, "Danger action button - destructive");
            AutomationId = "DangerButton";

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

            await border.ScaleTo(0.95, (uint)(animationDuration / 2), Easing.CubicOut);
            await border.ScaleTo(1.0, (uint)(animationDuration / 2), Easing.CubicIn);
        }

        #endregion

        #region Property Changed Handlers

        private static void OnImageSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is DangerButton button)
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
                BackgroundColor = (Color)Application.Current.Resources["StateDisabledBackground"];
                TextColor = (Color)Application.Current.Resources["StateDisabledText"];
                Opacity = (double)Application.Current.Resources["OpacityDisabled"];
            }
            else
            {
                BackgroundColor = (Color)Application.Current.Resources["Danger"];
                TextColor = (Color)Application.Current.Resources["White"];
                Opacity = (double)Application.Current.Resources["OpacityFull"];
            }
        }

        #endregion
    }
}
