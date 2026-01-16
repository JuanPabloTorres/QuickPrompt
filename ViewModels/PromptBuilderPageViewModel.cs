using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;

namespace QuickPrompt.ViewModels
{
    public partial class PromptBuilderPageViewModel : BaseViewModel
    {
        // ----------------------------------
        // 1) Listado global de formatos
        // ----------------------------------
        public ObservableCollection<string> AvailableFormats { get; } = new ObservableCollection<string>
        {
            "List",
            "Comparison Table",
            "Short Paragraph",
            "Numbered Outline"
        };

        // ----------------------------------
        // 2) Colección de pasos
        // ----------------------------------
        public ObservableCollection<StepModel> Steps { get; set; }

        // ----------------------------------
        // 3) Propiedades para controlar navegación y estado de UI
        // ----------------------------------
        [ObservableProperty]
        private int currentStep;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string nextButtonText;

        [ObservableProperty]
        private string nextButtonIcon;

        [ObservableProperty]
        private Color nextButtonBackground;

        [ObservableProperty]
        private Color nextButtonTextColor;

        [ObservableProperty]
        private bool canGoNext;


        public PromptBuilderPageViewModel(


        )
        {
            // 4) Crear cada StepModel (sin formatos aún)
            var contextStep = new StepModel(StepType.Context, "Step 1: Context", "E.g., You are a digital marketing expert...");
            var taskStep = new StepModel(StepType.Task, "Step 2: Task", "E.g., Generate 5 creative post ideas...");
            var examplesStep = new StepModel(StepType.Examples, "Step 3: Examples", "E.g., '5 tips to improve photography', '3 common mistakes when creating reels'");
            var formatStep = new StepModel(StepType.Format, "Step 4: Format", "E.g., List, Comparison Table...");
            var limitsStep = new StepModel(StepType.Limits, "Step 5: Constraints", "E.g., Max 7 titles, 10 words each.");
            var previewStep = new StepModel(StepType.Preview, "Step 6: Preview", string.Empty, isPreviewStep: true);

            // 5) Copiar AvailableFormats al StepModel de tipo Format
            foreach (var fmt in AvailableFormats)
            {
                formatStep.AvailableFormats.Add(fmt);
            }

            // 6) Construir la colección de Steps
            Steps = new ObservableCollection<StepModel>
            {
                contextStep,
                taskStep,
                examplesStep,
                formatStep,
                limitsStep,
                previewStep
            };

            // 7) Suscribirse a cambios de PROPERTY de cada StepModel (InputText, SelectedOption, IsValid, etc.)
            foreach (var step in Steps)
            {
                step.PropertyChanged += Step_PropertyChanged;
            }

            // 8) Inicializar el paso actual y estados iniciales
            CurrentStep = 0;
            UpdatePreviewStep();
            UpdateNavigationState();
        }

        // ----------------------------------
        // 9) Cada vez que CurrentStep cambie, actualizamos la Preview y la navegación
        // ----------------------------------
        partial void OnCurrentStepChanged(int value)
        {
            // Si el usuario llegó al paso Preview, recalculamos el contenido
            if (value == Steps.Count - 1)
            {
                UpdatePreviewStep();
            }

            UpdateNavigationState();
        }

        // ----------------------------------
        // 10) Responder a cambios en cualquier StepModel (InputText, SelectedOption, IsValid, etc.)
        // ----------------------------------
        private void Step_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Si cambian los textos o la selección de formato, volvemos a calcular Preview y habilitamos/deshabilitamos “Next”
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

        // ----------------------------------
        // 11) Construir el contenido del paso “Preview”
        // ----------------------------------
        public void UpdatePreviewStep()
        {
            // ✅ Validar que Steps no sea null
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
            else if (!string.IsNullOrWhiteSpace(contextText))
            {
                segments.Add($"Assume you are {contextText.TrimEnd('.')}");
            }

            if (!string.IsNullOrWhiteSpace(examplesText))
            {
                segments.Add($"For example, {examplesText.TrimEnd('.')}");
            }

            if (!string.IsNullOrWhiteSpace(formatText))
            {
                segments.Add($"Present your results in a {formatText.ToLower().TrimEnd('.')}{(formatText.EndsWith("format", StringComparison.OrdinalIgnoreCase) ? "" : " format")}");
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

            // ✅ Validar antes de acceder con First()
            var contextStep = Steps.FirstOrDefault(s => s.Type == StepType.Context);
            var taskStep = Steps.FirstOrDefault(s => s.Type == StepType.Task);
            var examplesStep = Steps.FirstOrDefault(s => s.Type == StepType.Examples);
            var formatStep = Steps.FirstOrDefault(s => s.Type == StepType.Format);
            var limitsStep = Steps.FirstOrDefault(s => s.Type == StepType.Limits);

            if (contextStep != null)
                previewStep.IsContextValid = contextStep.IsContextValid;
            if (taskStep != null)
                previewStep.IsTaskValid = taskStep.IsTaskValid;
            if (examplesStep != null)
                previewStep.AreExamplesValid = examplesStep.AreExamplesValid;
            if (formatStep != null)
                previewStep.IsFormatValid = formatStep.IsFormatValid;
            if (limitsStep != null)
                previewStep.AreLimitsValid = limitsStep.AreLimitsValid;
        }


        // ----------------------------------
        // 12) Actualizar estado de botón “Next” según el paso actual y su validación
        // ----------------------------------
        private void UpdateNavigationState()
        {
            // ✅ Validar que Steps no sea null y CurrentStep sea válido
            if (Steps == null || Steps.Count == 0 || CurrentStep >= Steps.Count)
            {
                CanGoNext = false;
                return;
            }

            var step = Steps[CurrentStep];

            // Si estamos en Preview, habilitamos para "Complete"
            if (step.IsPreviewStep)
            {
                CanGoNext = true;
                NextButtonText = "⚡ Complete";
                NextButtonIcon = string.Empty;
                NextButtonBackground = Color.FromArgb("#23486A");
                NextButtonTextColor = Colors.White;
                return;
            }

            // Para cualquier otro paso, "Next" solo si step.IsValid == true
            CanGoNext = step.IsValid;

            if (CanGoNext)
            {
                NextButtonText = string.Empty;
                NextButtonIcon = "\ue5e1";
                NextButtonBackground = Color.FromArgb("#EFB036");
                NextButtonTextColor = Colors.White;
            }
            else
            {
                NextButtonText = string.Empty;
                NextButtonIcon = "\ue5e1";
                NextButtonBackground = Color.FromArgb("#D3D3D3");
                NextButtonTextColor = Colors.Gray;
            }
        }

        // ----------------------------------
        // 13) Comando para terminar el prompt (último paso)
        // ----------------------------------
        [RelayCommand]
        public async Task FinishPromptAsync()
        {
            UpdatePreviewStep();

            var preview = Steps.FirstOrDefault(s => s.Type == StepType.Preview);
            if (preview == null) return;

            bool confirmed = await AppShell.Current.DisplayAlert(
                "Final Prompt",
                preview.PreviewContent,
                "Accept",
                "Cancel"
            );

            if (confirmed)
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        // ----------------------------------
        // 14) Comando para guardar el prompt
        // ----------------------------------
        [RelayCommand]
        private async Task SavePromptAsync()
        {
            await ExecuteWithLoadingAsync(async () =>
            {
                UpdatePreviewStep();
                var preview = Steps.First(s => s.Type == StepType.Preview);

                if (!preview.IsContextValid
                    || !preview.IsTaskValid
                    || !preview.IsFormatValid
                    || !preview.AreLimitsValid)
                {
                    await AppShell.Current.DisplayAlert(
                        "Error",
                        "Please complete Context, Task, Format, and Constraints.",
                        "OK"
                    );
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

                var newPrompt = PromptTemplate.CreatePromptTemplate(
                    autoTitle,
                    string.Empty,
                    fullTemplate,
                    PromptCategory.General
                );
                await _databaseService.SavePromptAsync(newPrompt);
                await _adMobService.ShowInterstitialAdAndWaitAsync();

                await GenericToolBox.ShowLottieMessageAsync(
                    "CompleteAnimation.json",
                    "Prompt saved successfully."
                );

                // Limpiar todos los pasos
                foreach (var step in Steps)
                {
                    step.InputText = string.Empty;
                    step.SelectedOption = string.Empty;
                }

                // Reiniciar al primer paso
                CurrentStep = 0;
                UpdatePreviewStep();
            },
            "Error saving prompt.");
        }

        // ----------------------------------
        // 15) Comando para probar el prompt
        // ----------------------------------
        [RelayCommand]
        private async Task TestPromptAsync()
        {
            var context = Steps.First(s => s.Type == StepType.Context).InputText;
            var task = Steps.First(s => s.Type == StepType.Task).InputText;

            if (string.IsNullOrWhiteSpace(context) || string.IsNullOrWhiteSpace(task))
            {
                await AppShell.Current.DisplayAlert(
                    "Error",
                    "Please complete Context and Task.",
                    "OK"
                );
                return;
            }

            UpdatePreviewStep();
            var preview = Steps.First(s => s.Type == StepType.Preview);
            await AppShell.Current.DisplayAlert("Prompt Preview", preview.PreviewContent, "OK");
        }
    }

    public static class TaskExtensions
    {
        public static void FireAndForget(this Task task)
        {
            _ = task.ContinueWith(t => { }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
