using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Pages;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;
using System.Text;

namespace QuickPrompt.ViewModels;

/// <summary>
/// ViewModel for viewing prompt details and filling variables.
/// Refactored to use Use Cases and services - Phase 1.
/// </summary>
public partial class PromptDetailsPageViewModel : BaseViewModel, IQueryAttributable
{
    // 🆕 Use Cases and Services (injected)
    private readonly GetPromptByIdUseCase _getPromptByIdUseCase;
    private readonly ExecutePromptUseCase _executePromptUseCase;
    private readonly IPromptCacheService _promptCacheService;
    private readonly IDialogService _dialogService;
    private readonly AdmobService _adMobService;

    // Properties
    [ObservableProperty] private string promptTitle = string.Empty;
    [ObservableProperty] private string promptText = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private PromptCategory category;
    [ObservableProperty] private string finalPrompt = string.Empty;
    [ObservableProperty] private bool isShareButtonVisible = false;
    [ObservableProperty] private Guid promptID;
    [ObservableProperty] private ObservableCollection<VariableInput> variables = new();

    // Constructor with dependency injection
    public PromptDetailsPageViewModel(
        GetPromptByIdUseCase getPromptByIdUseCase,
        ExecutePromptUseCase executePromptUseCase,
        IPromptCacheService promptCacheService,
        IDialogService dialogService,
        AdmobService admobService)
    {
        _getPromptByIdUseCase = getPromptByIdUseCase ?? throw new ArgumentNullException(nameof(getPromptByIdUseCase));
        _executePromptUseCase = executePromptUseCase ?? throw new ArgumentNullException(nameof(executePromptUseCase));
        _promptCacheService = promptCacheService ?? throw new ArgumentNullException(nameof(promptCacheService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _adMobService = admobService ?? throw new ArgumentNullException(nameof(admobService));
    }

    // ============================ NAVIGATION & LOADING ============================

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("selectedId") && Guid.TryParse(query["selectedId"]?.ToString(), out Guid id))
        {
            await LoadPromptAsync(id);
            Clear();
        }
        else
        {
            await _dialogService.ShowErrorAsync("Invalid prompt ID.");
        }
    }

    public async Task LoadPromptAsync(Guid selectedId)
    {
        IsLoading = true;
        try
        {
            var result = await _getPromptByIdUseCase.ExecuteAsync(selectedId);

            if (result.IsFailure)
            {
                await _dialogService.ShowErrorAsync(result.Error);
                await GoBackAsync();
                return;
            }

            var prompt = result.Value;
            PromptTitle = prompt.Title;
            Description = prompt.Description;
            PromptText = prompt.Template;
            PromptID = prompt.Id;
            Category = prompt.Category;

            // Initialize variables - cached suggestions will be loaded automatically via PromptVariableCache
            var variableList = new List<VariableInput>();
            foreach (var variable in prompt.Variables)
            {
                variableList.Add(new VariableInput 
                { 
                    Name = variable.Key, 
                    Value = string.Empty
                    // Suggestions are loaded automatically from PromptVariableCache in the model
                });
            }

            Variables = new ObservableCollection<VariableInput>(variableList);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync($"{AppMessagesEng.Prompts.PromptLoadError}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ============================ PROMPT GENERATION ============================

    [RelayCommand]
    private async Task GeneratePromptAsync()
    {
        IsLoading = true;
        try
        {
            if (!AreVariablesFilled())
            {
                await _dialogService.ShowErrorAsync(AppMessagesEng.Prompts.PromptVariablesError);
                return;
            }

            // Show ad
            await _adMobService.ShowInterstitialAdAsync();

            // Create variable dictionary
            var variableDict = Variables.ToDictionary(v => v.Name!, v => v.Value!);

            // Execute prompt via Use Case
            var request = new ExecutePromptRequest
            {
                PromptId = PromptID,
                Variables = variableDict
            };

            var result = await _executePromptUseCase.ExecuteAsync(request);

            if (result.IsFailure)
            {
                await _dialogService.ShowErrorAsync(result.Error);
                return;
            }

            // Update UI
            FinalPrompt = result.Value.CompletedText;
            UpdateVisibility();

            var toast = Toast.Make("Prompt generated successfully!", ToastDuration.Short);
            await toast.Show();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync($"{AppMessagesEng.GenericError}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool AreVariablesFilled()
    {
        return Variables.All(v => !string.IsNullOrWhiteSpace(v.Value));
    }

    private void UpdateVisibility()
    {
        IsShareButtonVisible = !string.IsNullOrWhiteSpace(FinalPrompt);
        ShowPromptActions = !string.IsNullOrWhiteSpace(FinalPrompt);
    }

    // ============================ AI INTEGRATION ============================

    [RelayCommand]
    private async Task SendPromptToAsync(NavigationParams param)
    {
        IsLoading = true;
        try
        {
            await SendPromptToAsync(param.PageName, param.ToolName, PromptID, FinalPrompt);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    protected async Task SharePromptAsync()
    {
        if (string.IsNullOrEmpty(PromptTitle) || string.IsNullOrEmpty(FinalPrompt))
        {
            await _dialogService.ShowErrorAsync("No prompt selected for sharing.");
            return;
        }

        IsLoading = true;
        try
        {
            await SharePromptService.SharePromptAsync(PromptTitle, FinalPrompt);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync($"{AppMessagesEng.Prompts.PromptSharedError}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ============================ SUGGESTIONS ============================

    [RelayCommand]
    private void SelectSuggestion(VariableSuggestionSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection?.VariableName) || string.IsNullOrWhiteSpace(selection.SuggestedValue))
            return;

        var variable = Variables.FirstOrDefault(v => v.Name == selection.VariableName);
        if (variable != null)
        {
            variable.Value = selection.SuggestedValue;
        }
    }

    // ============================ NAVIGATION ============================

    [RelayCommand]
    private async Task NavigateToEditPrompt(Guid promptId)
    {
        if (promptId == Guid.Empty)
        {
            await _dialogService.ShowErrorAsync("Invalid prompt ID.");
            return;
        }

        await NavigateToAsync(nameof(EditPromptPage), new Dictionary<string, object>
        {
            { "selectedId", promptId },
            { "isNavigateFromRoot", false }
        });
    }

    [RelayCommand]
    private void ClearText() => Clear();

    public void Clear()
    {
        if (Variables.All(v => string.IsNullOrEmpty(v.Value)))
            return;

        foreach (var variable in Variables)
        {
            variable.Value = string.Empty;
        }

        FinalPrompt = string.Empty;
        UpdateVisibility();
    }

    public override async Task MyBack()
    {
        await Shell.Current.GoToAsync("//Quick");
    }
}