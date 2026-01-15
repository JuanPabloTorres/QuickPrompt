using System.Windows.Input;

namespace QuickPrompt.Controls
{
    /// <summary>
    /// Reusable error state component with retry capability.
    /// Provides clear error feedback and actionable recovery options.
    /// </summary>
    public partial class ErrorStateView : ContentView
    {
        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(ErrorStateView), "Something went wrong");

        public static readonly BindableProperty MessageProperty =
            BindableProperty.Create(nameof(Message), typeof(string), typeof(ErrorStateView), "An unexpected error occurred");

        public static readonly BindableProperty DetailsProperty =
            BindableProperty.Create(nameof(Details), typeof(string), typeof(ErrorStateView), string.Empty);

        public static readonly BindableProperty ShowDetailsProperty =
            BindableProperty.Create(nameof(ShowDetails), typeof(bool), typeof(ErrorStateView), false);

        public static readonly BindableProperty RetryButtonTextProperty =
            BindableProperty.Create(nameof(RetryButtonText), typeof(string), typeof(ErrorStateView), "Try Again");

        public static readonly BindableProperty RetryCommandProperty =
            BindableProperty.Create(nameof(RetryCommand), typeof(ICommand), typeof(ErrorStateView), null);

        public static readonly BindableProperty ShowRetryButtonProperty =
            BindableProperty.Create(nameof(ShowRetryButton), typeof(bool), typeof(ErrorStateView), true);

        public static readonly BindableProperty SecondaryButtonTextProperty =
            BindableProperty.Create(nameof(SecondaryButtonText), typeof(string), typeof(ErrorStateView), "Go Back");

        public static readonly BindableProperty SecondaryCommandProperty =
            BindableProperty.Create(nameof(SecondaryCommand), typeof(ICommand), typeof(ErrorStateView), null);

        public static readonly BindableProperty ShowSecondaryButtonProperty =
            BindableProperty.Create(nameof(ShowSecondaryButton), typeof(bool), typeof(ErrorStateView), false);

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public string Details
        {
            get => (string)GetValue(DetailsProperty);
            set => SetValue(DetailsProperty, value);
        }

        public bool ShowDetails
        {
            get => (bool)GetValue(ShowDetailsProperty);
            set => SetValue(ShowDetailsProperty, value);
        }

        public string RetryButtonText
        {
            get => (string)GetValue(RetryButtonTextProperty);
            set => SetValue(RetryButtonTextProperty, value);
        }

        public ICommand RetryCommand
        {
            get => (ICommand)GetValue(RetryCommandProperty);
            set => SetValue(RetryCommandProperty, value);
        }

        public bool ShowRetryButton
        {
            get => (bool)GetValue(ShowRetryButtonProperty);
            set => SetValue(ShowRetryButtonProperty, value);
        }

        public string SecondaryButtonText
        {
            get => (string)GetValue(SecondaryButtonTextProperty);
            set => SetValue(SecondaryButtonTextProperty, value);
        }

        public ICommand SecondaryCommand
        {
            get => (ICommand)GetValue(SecondaryCommandProperty);
            set => SetValue(SecondaryCommandProperty, value);
        }

        public bool ShowSecondaryButton
        {
            get => (bool)GetValue(ShowSecondaryButtonProperty);
            set => SetValue(ShowSecondaryButtonProperty, value);
        }

        public ErrorStateView()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Predefined error messages and configurations.
    /// </summary>
    public static class ErrorMessages
    {
        public const string NetworkError = "No internet connection";
        public const string NetworkErrorDetail = "Please check your connection and try again";
        
        public const string DatabaseError = "Database error";
        public const string DatabaseErrorDetail = "Could not access local storage";
        
        public const string UnknownError = "Something went wrong";
        public const string UnknownErrorDetail = "An unexpected error occurred. Please try again";
        
        public const string ValidationError = "Validation failed";
        public const string ValidationErrorDetail = "Please check your input and try again";
        
        public const string SyncError = "Sync failed";
        public const string SyncErrorDetail = "Could not sync with cloud. Your data is saved locally";
    }
}
