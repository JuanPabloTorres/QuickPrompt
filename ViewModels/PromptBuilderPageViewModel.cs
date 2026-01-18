using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels
{
    /// <summary>
    /// ViewModel for the Prompt Builder wizard.
    /// Refactored to use Use Cases and services - Phase 1.
    /// </summary>
    public partial class PromptBuilderPageViewModel : BaseViewModel
    {
        // 🆕 Use Cases and Services (injected)
        private readonly CreatePromptUseCase _createPromptUseCase;
        private readonly IDialogService _dialogService;
        private readonly AdmobService _adMobService;

        // Properties
        public ObservableCollection<string> AvailableFormats { get; } = new ObservableCollection<string>
        {
            "List",
            "Comparison Table",
            "Short Paragraph",
            "Numbered Outline"
        };

        public ObservableCollection<StepModel> Steps { get; set; }

        [ObservableProperty] private int currentStep;
        [ObservableProperty] private string nextButtonText = string.Empty;
        [ObservableProperty] private string nextButtonIcon = string.Empty;
        [ObservableProperty] private Color nextButtonBackground;
        [ObservableProperty] private Color nextButtonTextColor;
        [ObservableProperty] private bool canGoNext;

        // Constructor with dependency injection
        public PromptBuilderPageViewModel(
            CreatePromptUseCase createPromptUseCase,
            IDialogService dialogService,
            AdmobService adMobService)
        {
            _createPromptUseCase = createPromptUseCase ?? throw new ArgumentNullException(nameof(createPromptUseCase));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _adMobService = adMobService ?? throw new ArgumentNullException(nameof(adMobService));

            // ✅ Initialize colors from Design System tokens
            nextButtonBackground = (Color)Application.Current.Resources["Gray400"];
            nextButtonTextColor = (Color)Application.Current.Resources["White"];

            InitializeSteps();
        }

        private void InitializeSteps()
        {
            var contextStep = new StepModel(StepType.Context, "Step 1: Context", "E.g., You are a digital marketing expert...");
            var taskStep = new StepModel(StepType.Task, "Step 2: Task", "E.g., Generate 5 creative post ideas...");
            var examplesStep = new StepModel(StepType.Examples, "Step 3: Examples", "E.g., '5 tips to improve photography'");
            var formatStep = new StepModel(StepType.Format, "Step 4: Format", "E.g., List, Comparison Table...");
            var limitsStep = new StepModel(StepType.Limits, "Step 5: Constraints", "E.g., Max 7 titles, 10 words each.");
            var previewStep = new StepModel(StepType.Preview, "Step 6: Preview", string.Empty, isPreviewStep: true);

            foreach (var fmt in AvailableFormats)
            {
                formatStep.AvailableFormats.Add(fmt);
            }

            Steps = new ObservableCollection<StepModel>
            {
                contextStep, taskStep, examplesStep, formatStep, limitsStep, previewStep
            };

            foreach (var step in Steps)
            {
                step.PropertyChanged += Step_PropertyChanged;
            }

            CurrentStep = 0;
            UpdatePreviewStep();
            UpdateNavigationState();
        }

        // ============================ STEP NAVIGATION ============================

        partial void OnCurrentStepChanged(int value)
        {
            if (value == Steps.Count - 1)
            {
                UpdatePreviewStep();
            }
            UpdateNavigationState();
        }

        private void Step_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(StepModel.InputText)
                or nameof(StepModel.SelectedOption)
                or nameof(StepModel.IsContextValid)
                or nameof(StepModel.IsTaskValid)
                or nameof(StepModel.AreExamplesValid)
                or nameof(StepModel.IsFormatValid)
                or nameof(StepModel.AreLimitsValid))
            {
                UpdatePreviewStep();
                UpdateNavigationState();
            }
        }

        public void UpdatePreviewStep()
        {
            if (Steps == null || Steps.Count == 0)
                return;

            var previewStep = Steps.FirstOrDefault(s => s.Type == StepType.Preview);
            if (previewStep is null) return;

            var contextText = Steps.FirstOrDefault(s => s.Type == StepType.Context)?.InputText?.Trim();
            var taskText = Steps.FirstOrDefault(s => s.Type == StepType.Task)?.InputText?.Trim();
            var examplesText = Steps.FirstOrDefault(s => s.Type == StepType.Examples)?.InputText?.Trim();
            var formatText = Steps.FirstOrDefault(s => s.Type == StepType.Format)?.SelectedOption?.Trim();
            var limitsText = Steps.FirstOrDefault(s => s.Type == StepType.Limits)?.InputText?.Trim();

            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(contextText) && !string.IsNullOrWhiteSpace(taskText))
            {
                segments.Add($"As {contextText.TrimEnd('.')}, {taskText.TrimEnd('.')}");
            }
            else if (!string.IsNullOrWhiteSpace(taskText))
            {
                segments.Add(taskText.TrimEnd('.'));
            }

            if (!string.IsNullOrWhiteSpace(examplesText))
            {
                segments.Add($"For example, {examplesText.TrimEnd('.')}");
            }

            if (!string.IsNullOrWhiteSpace(formatText))
            {
                segments.Add($"Present your results in a {formatText.ToLower()} format");
            }

            if (!string.IsNullOrWhiteSpace(limitsText))
            {
                segments.Add($"Limit it to {limitsText.TrimEnd('.')}");
            }

            var combined = string.Join(". ", segments);
            if (!string.IsNullOrWhiteSpace(combined) && !combined.EndsWith("."))
            {
                combined += ".";
            }

            previewStep.PreviewContent = combined;

            // Update validation flags
            var contextStep = Steps.FirstOrDefault(s => s.Type == StepType.Context);
            var taskStep = Steps.FirstOrDefault(s => s.Type == StepType.Task);
            var examplesStep = Steps.FirstOrDefault(s => s.Type == StepType.Examples);
            var formatStep = Steps.FirstOrDefault(s => s.Type == StepType.Format);
            var limitsStep = Steps.FirstOrDefault(s => s.Type == StepType.Limits);

            if (contextStep != null) previewStep.IsContextValid = contextStep.IsContextValid;
            if (taskStep != null) previewStep.IsTaskValid = taskStep.IsTaskValid;
            if (examplesStep != null) previewStep.AreExamplesValid = examplesStep.AreExamplesValid;
            if (formatStep != null) previewStep.IsFormatValid = formatStep.IsFormatValid;
            if (limitsStep != null) previewStep.AreLimitsValid = limitsStep.AreLimitsValid;
        }

        private void UpdateNavigationState()
        {
            if (Steps == null || Steps.Count == 0 || CurrentStep >= Steps.Count)
            {
                CanGoNext = false;
                return;
            }

            var step = Steps[CurrentStep];

            if (step.IsPreviewStep)
            {
                CanGoNext = true;
                NextButtonText = "⚡ Complete";
                NextButtonIcon = string.Empty;
                // ✅ Use Design System token
                NextButtonBackground = (Color)Application.Current.Resources["PrimaryBlueDark"];
                NextButtonTextColor = (Color)Application.Current.Resources["White"];
                return;
            }

            CanGoNext = step.IsValid;

            if (CanGoNext)
            {
                NextButtonText = string.Empty;
                NextButtonIcon = "\ue5e1";
                // ✅ Use Design System token
                NextButtonBackground = (Color)Application.Current.Resources["PrimaryYellow"];
                NextButtonTextColor = (Color)Application.Current.Resources["White"];
            }
            else
            {
                NextButtonText = string.Empty;
                NextButtonIcon = "\ue5e1";
                // ✅ Use Design System token
                NextButtonBackground = (Color)Application.Current.Resources["StateDisabledBackground"];
                NextButtonTextColor = (Color)Application.Current.Resources["StateDisabledText"];
            }
        }

        // ============================ COMMANDS ============================

        [RelayCommand]
        public async Task FinishPromptAsync()
        {
            UpdatePreviewStep();

            var preview = Steps.FirstOrDefault(s => s.Type == StepType.Preview);
            if (preview == null) return;

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Final Prompt",
                preview.PreviewContent,
                "Accept",
                "Cancel");

            if (confirmed)
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        private async Task SavePromptAsync()
        {
            IsLoading = true;
            try
            {
                UpdatePreviewStep();
                var preview = Steps.First(s => s.Type == StepType.Preview);

                if (!preview.IsContextValid || !preview.IsTaskValid || 
                    !preview.IsFormatValid || !preview.AreLimitsValid)
                {
                    await _dialogService.ShowErrorAsync(
                        "Please complete Context, Task, Format, and Constraints.");
                    return;
                }

                string fullTemplate = preview.PreviewContent;
                string taskText = Steps.First(s => s.Type == StepType.Task).InputText ?? string.Empty;

                string autoTitle = taskText.Trim();
                if (autoTitle.Length > 0)
                {
                    var tokens = autoTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    autoTitle = tokens.Length <= 5
                        ? string.Join(' ', tokens)
                        : string.Join(' ', tokens, 0, 5) + "...";
                }
                else
                {
                    autoTitle = "Generated Prompt";
                }

                var request = new CreatePromptRequest
                {
                    Title = autoTitle,
                    Description = string.Empty,
                    Template = $"<prompt>{fullTemplate}</prompt>",
                    Category = PromptCategory.General.ToString()
                };

                var result = await _createPromptUseCase.ExecuteAsync(request);

                if (result.IsFailure)
                {
                    await _dialogService.ShowErrorAsync(result.Error);
                    return;
                }

                await _adMobService.ShowInterstitialAdAndWaitAsync();

                await _dialogService.ShowLottieMessageAsync(
                    "CompleteAnimation.json",
                    "Prompt saved successfully.");

                // Clear all steps
                foreach (var step in Steps)
                {
                    step.InputText = string.Empty;
                    step.SelectedOption = string.Empty;
                }

                CurrentStep = 0;
                UpdatePreviewStep();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync($"Error saving prompt: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task TestPromptAsync()
        {
            var context = Steps.First(s => s.Type == StepType.Context).InputText;
            var task = Steps.First(s => s.Type == StepType.Task).InputText;

            if (string.IsNullOrWhiteSpace(context) || string.IsNullOrWhiteSpace(task))
            {
                await _dialogService.ShowErrorAsync("Please complete Context and Task.");
                return;
            }

            UpdatePreviewStep();
            var preview = Steps.First(s => s.Type == StepType.Preview);
            await _dialogService.ShowAlertAsync("Prompt Preview", preview.PreviewContent);
        }
    }
}
