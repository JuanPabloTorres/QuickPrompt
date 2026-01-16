namespace QuickPrompt.Controls
{
    /// <summary>
    /// Reusable skeleton loading placeholder with shimmer animation.
    /// Use while data is loading to improve perceived performance.
    /// </summary>
    public partial class SkeletonView : ContentView
    {
        public static readonly BindableProperty CornerRadiusProperty =
            BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkeletonView), 8.0);

        public static readonly BindableProperty HeightRequestProperty =
            BindableProperty.Create(nameof(HeightRequest), typeof(double), typeof(SkeletonView), 40.0);

        public static readonly BindableProperty WidthRequestProperty =
            BindableProperty.Create(nameof(WidthRequest), typeof(double), typeof(SkeletonView), -1.0);

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public new double HeightRequest
        {
            get => (double)GetValue(HeightRequestProperty);
            set => SetValue(HeightRequestProperty, value);
        }

        public new double WidthRequest
        {
            get => (double)GetValue(WidthRequestProperty);
            set => SetValue(WidthRequestProperty, value);
        }

        public SkeletonView()
        {
            InitializeComponent();
            StartShimmerAnimation();
        }

        private void StartShimmerAnimation()
        {
            var shimmer = this.FindByName<BoxView>("ShimmerOverlay");
            if (shimmer != null)
            {
                var animation = new Animation(v => shimmer.TranslationX = v, -200, 200);
                animation.Commit(this, "ShimmerAnimation", length: 1500, repeat: () => true);
            }
        }
    }
}
