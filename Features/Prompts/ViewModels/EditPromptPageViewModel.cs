using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using QuickPrompt.ApplicationLayer.Common.Interfaces;
using QuickPrompt.ApplicationLayer.Prompts.UseCases;
using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services;
using QuickPrompt.Shared.Mappers;
using QuickPrompt.Tools;
using QuickPrompt.Tools.Messages;

namespace QuickPrompt.ViewModels;

/// <summary>
/// ViewModel for editing an existing prompt.
/// Refactored to use Use Cases and services - Phase 1.
/// </summary>
public partial class EditPromptPageViewModel : BaseViewModel, IQueryAttributable
{
    // 🆕 Use Cases and Services (injected)
    private readonly GetPromptByIdUseCase _getPromptByIdUseCase;
    private readonly UpdatePromptUseCase _updatePromptUseCase;
    private readonly IDialogService _dialogService;
    private readonly AdmobService _adMobService;

    // Properties
    [ObservableProperty] private PromptTemplate promptTemplate = new();
    [ObservableProperty] private int cursorPosition;
    [ObservableProperty] private int selectionLength;
    [ObservableProperty] private string promptText = string.Empty;
    [ObservableProperty] public string selectedCategory = string.Empty;

    private bool isNavigateFromRoot;

    // Constructor with dependency injection
    public EditPromptPageViewModel(
        GetPromptByIdUseCase getPromptByIdUseCase,
        UpdatePromptUseCase updatePromptUseCase,
        IDialogService dialogService,
        AdmobService admobService) : base(admobService) // ✅ Pass AdmobService to base
    {
        _getPromptByIdUseCase = getPromptByIdUseCase ?? throw new ArgumentNullException(nameof(getPromptByIdUseCase));
        _updatePromptUseCase = updatePromptUseCase ?? throw new ArgumentNullException(nameof(updatePromptUseCase));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _adMobService = admobService ?? throw new ArgumentNullException(nameof(admobService));
    }

    // ============================ NAVIGATION ============================

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        IsLoading = true;
        try
        {
            if (query.TryGetValue("selectedId", out var selectedId) && 
                Guid.TryParse(selectedId?.ToString(), out Guid promptId))
            {
                isNavigateFromRoot = query.TryGetValue("isNavigateFromRoot", out var isRootNavigation)
                                && bool.TryParse(isRootNavigation?.ToString(), out var navigationRoot)
                                ? navigationRoot
                                : true;

                await LoadPromptAsync(promptId);
                IsVisualModeActive = false;
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync($"Error loading prompt: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPromptAsync(Guid promptId)
    {
        var result = await _getPromptByIdUseCase.ExecuteAsync(promptId);

        if (result.IsFailure)
        {
            await _dialogService.ShowErrorAsync(result.Error);
            await GoBackAsync();
            return;
        }

        var prompt = result.Value.ToLegacy(); // Convert Domain to Legacy
        PromptTemplate = prompt;
        PromptTemplate.Variables = AngleBraceTextHandler.ExtractVariables(prompt.Template)
            .ToDictionary(v => v, v => string.Empty);
        SelectedCategory = PromptTemplate.Category.ToString();
        UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(prompt.Template));
    }

    [RelayCommand]
    private async Task GoToDetail()
    {
        if (PromptTemplate != null)
        {
            await Shell.Current.GoToAsync($"PromptDetailsPage?selectedId={PromptTemplate.Id}", true);
        }
    }

    // ============================ UPDATE PROMPT ============================

    [RelayCommand]
    private async Task UpdateChangesAsync()
    {
        IsLoading = true;
        try
        {
            _adMobService.LoadInterstitialAd();

            // Update variables
            PromptTemplate.Variables = AngleBraceTextHandler.ExtractVariables(PromptTemplate.Template)
                .ToDictionary(v => v, v => string.Empty);

            // Create request
            var request = new UpdatePromptRequest
            {
                PromptId = PromptTemplate.Id,
                Title = PromptTemplate.Title,
                Description = PromptTemplate.Description,
                Template = PromptTemplate.Template,
                Category = SelectedCategory
            };

            // Execute use case
            var result = await _updatePromptUseCase.ExecuteAsync(request);

            if (result.IsFailure)
            {
                await _dialogService.ShowErrorAsync(result.Error);
                return;
            }

            // Update local instance
            PromptTemplate = result.Value.ToLegacy(); // Convert Domain to Legacy

            // Show ad
            await _adMobService.ShowInterstitialAdAndWaitAsync();

            // Show success
            await _dialogService.ShowLottieMessageAsync(
                "CompleteAnimation.json",
                AppMessagesEng.Prompts.PromptUpdatedSuccess);

            // Navigate back
            await GoBackAsync();

            // Notify other ViewModels
            WeakReferenceMessenger.Default.Send(new UpdatedPromptMessage(PromptTemplate));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync($"{AppMessagesEng.Prompts.PromptSaveError}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ============================ VARIABLE HANDLING ============================

    [RelayCommand]
    private async Task CreateVariableAsync()
    {
        if (IsSelectionValid(PromptTemplate.Template, SelectionLength))
        {
            EncloseSelectedTextWithBraces();
        }
        else
        {
            await _dialogService.ShowErrorAsync(AppMessagesEng.Warnings.SelectWordError);
        }
    }

    private async void EncloseSelectedTextWithBraces()
    {
        var handler = new AngleBraceTextHandler(PromptTemplate.Template);

        if (!handler.IsSelectionValid(CursorPosition, SelectionLength))
        {
            await _dialogService.ShowErrorAsync(AppMessagesEng.Warnings.SelectWordError);
            return;
        }

        if (handler.IsSurroundedByAngleBraces(CursorPosition, SelectionLength))
        {
            await RemoveBracesFromSelectedText();
            return;
        }

        string selectedText = PromptTemplate.Template.Substring(CursorPosition, SelectionLength);

        if (AngleBraceTextHandler.ContainsVariable($"<{selectedText}>", PromptTemplate.Template))
        {
            var nextVersion = AngleBraceTextHandler.GetNextVariableSuffixVersion(PromptTemplate.Template, selectedText);
            selectedText += nextVersion;
        }

        handler.UpdateText(CursorPosition, SelectionLength, $"<{selectedText}>");
        PromptTemplate = InitializePromptTemplate(PromptTemplate, handler.Text);
        UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
    }

    private async Task RemoveBracesFromSelectedText()
    {
        await HandleSelectedTextAsync(CursorPosition, SelectionLength);
    }

    public async Task HandleSelectedTextAsync(int cursorPosition, int selectionLength)
    {
        var handler = new AngleBraceTextHandler(PromptTemplate.Template);

        if (handler.IsSelectionValid(cursorPosition, selectionLength))
        {
            var (startIndex, length) = handler.AdjustSelectionForAngleBraces(cursorPosition, selectionLength);
            string selectedText = handler.ExtractTextWithoutAngleBraces(startIndex, length);
            selectedText = AngleBraceTextHandler.RemoveVariableSuffix(selectedText);
            handler.UpdateText(startIndex, length, selectedText);
            PromptTemplate = InitializePromptTemplate(PromptTemplate, handler.Text);
            UpdateSelectedTextLabelCount(AngleBraceTextHandler.CountWordsWithAngleBraces(handler.Text));
        }
        else
        {
            await _dialogService.ShowAlertAsync("Warning", AppMessagesEng.Warnings.InvalidTextSelection);
        }
    }

    // ============================ HELPERS ============================

    private PromptTemplate InitializePromptTemplate(PromptTemplate existingPrompt, string newTemplate)
    {
        return new PromptTemplate
        {
            Id = existingPrompt.Id,
            Template = newTemplate,
            Title = existingPrompt.Title,
            Description = existingPrompt.Description,
            Variables = AngleBraceTextHandler.ExtractVariables(newTemplate).ToDictionary(v => v, v => string.Empty)
        };
    }

    public override async Task MyBack()
    {
        IsLoading = true;
        try
        {
            if (isNavigateFromRoot)
                await GoBackAsync();
            else
                await GoToDetail();
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

    // ============================ MODE SWITCHING ============================

    [RelayCommand]
    private void SwitchToEditor()
    {
        IsVisualModeActive = false;
    }

    [RelayCommand]
    private void SwitchToChips()
    {
        if (!string.IsNullOrWhiteSpace(PromptTemplate?.Template))
        {
            IsVisualModeActive = true;
        }
    }
}