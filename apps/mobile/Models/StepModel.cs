using CommunityToolkit.Mvvm.ComponentModel;
using QuickPrompt.Models.Enums;
using System.Collections.ObjectModel;

namespace QuickPrompt.Models
{
    public partial class StepModel : ObservableObject
    {
        [ObservableProperty] private StepType type;
        [ObservableProperty] private string title;
        [ObservableProperty] private bool isVisible;
        [ObservableProperty] private string inputText;
        [ObservableProperty] private string placeholder;
        [ObservableProperty] private bool isPreviewStep = false;
        [ObservableProperty] private string previewContent;
        [ObservableProperty] private bool isContextValid;
        [ObservableProperty] private bool isTaskValid;
        [ObservableProperty] private bool areExamplesValid;
        [ObservableProperty] private bool isFormatValid;
        [ObservableProperty] private bool areLimitsValid;
        [ObservableProperty] private bool isValid;
        [ObservableProperty] private string selectedOption;

        // Esta colección se usará solo cuando Type == StepType.Format
        public ObservableCollection<string> AvailableFormats { get; } = new ObservableCollection<string>();

        public bool IsFormatStep => Type == StepType.Format;
        public bool IsTextStep => !IsPreviewStep && Type != StepType.Format;

        public StepModel(StepType type, string title, string placeholder, bool isPreviewStep = false)
        {
            Type = type;

            Title = title;

            Placeholder = placeholder;

            IsVisible = (type == StepType.Context);

            IsPreviewStep = isPreviewStep;

            // Inicializamos las banderas
            IsContextValid = false;

            IsTaskValid = false;

            AreExamplesValid = false;

            IsFormatValid = false;

            AreLimitsValid = false;

            IsValid = false;
        }

        partial void OnInputTextChanged(string value)
        {
            switch (Type)
            {
                case StepType.Context:
                    IsContextValid = !string.IsNullOrWhiteSpace(value);
                    break;
                case StepType.Task:
                    IsTaskValid = !string.IsNullOrWhiteSpace(value);
                    break;
                case StepType.Examples:
                    AreExamplesValid = !string.IsNullOrWhiteSpace(value);
                    break;
                case StepType.Limits:
                    AreLimitsValid = !string.IsNullOrWhiteSpace(value);
                    break;
            }

            if (Type != StepType.Format && !IsPreviewStep)
                IsValid = !string.IsNullOrWhiteSpace(value);
        }

        partial void OnSelectedOptionChanged(string value)
        {
            if (Type == StepType.Format)
            {
                IsFormatValid = !string.IsNullOrWhiteSpace(value);

                IsValid = !string.IsNullOrWhiteSpace(value);
            }
        }
    }
}
