﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Models;
using QuickPrompt.Services;
using QuickPrompt.Tools;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels;

public partial class PromptDetailsPageViewModel(PromptDatabaseService _databaseService,IChatGPTService _chatGPTService) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    private string promptTitle;

    [ObservableProperty]
    private string promptText;

    [ObservableProperty]
    private string description;

    [ObservableProperty]
    private string finalPrompt;




    [ObservableProperty]
    private ObservableCollection<VariableInput> variables = new();

    public async Task LoadPromptAsync(Guid selectedId)
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            var prompt = await _databaseService.GetPromptByIdAsync(selectedId);

            if (prompt != null)
            {
                PromptTitle = prompt.Title;
                Description = prompt.Description;
                PromptText = prompt.Template;

                // Cargar las variables con inputs vacíos
                Variables = new ObservableCollection<VariableInput>(
                    prompt.Variables.Select(v => new VariableInput { Name = v.Key, Value = string.Empty })
                );
            }
            else
            {
                await AppShell.Current.DisplayAlert("Aviso", AppMessages.Prompts.PromptNotFound, "OK");
            }
        }, AppMessages.Prompts.PromptLoadError);
    }


    //public async Task LoadPromptAsync(Guid selectedId)
    //{
    //    try
    //    {
    //        var prompt = await _databaseService.GetPromptByIdAsync(selectedId);

    //        if (prompt != null)
    //        {
    //            PromptTitle = prompt.Title;

    //            Description = prompt.Description;

    //            PromptText = prompt.Template;

    //            // Cargar las variables con inputs vacíos
    //            Variables = new ObservableCollection<VariableInput>(
    //                prompt.Variables.Select(v => new VariableInput { Name = v.Key, Value = string.Empty })
    //            );
    //        }
    //    }
    //    catch (Exception ex)
    //    {

    //        await AppShell.Current.DisplayAlert("Error", ex.Message, "OK");
    //    }
      
    //}

    [RelayCommand]
    private async Task SendPromptToChatGPTAsync()
    {
        if (string.IsNullOrWhiteSpace(FinalPrompt))
        {
            await AppShell.Current.DisplayAlert("Error", "Genera el prompt antes de enviarlo.", "OK");

            return;
        }

        try
        {
            // Llamada al servicio
            var response = await _chatGPTService.GetResponseFromChatGPTAsync(FinalPrompt);

            await AppShell.Current.DisplayAlert("Respuesta de ChatGPT", response, "OK");
        }
        catch (Exception ex)
        {
            await AppShell.Current.DisplayAlert("Error", $"Hubo un problema: {ex.Message}", "OK");
        }
    }


    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("selectedId") && Guid.TryParse(query["selectedId"]?.ToString(), out Guid id))
        {
            await LoadPromptAsync(id);
        }
        else
        {
            await AppShell.Current.DisplayAlert("Error", "ID de prompt no válido.", "OK");
        }
    }



    //[RelayCommand]
    //private async Task GeneratePromptAsync()
    //{
    //    try
    //    {
    //        IsLoading = true; // Mostrar indicador de carga

    //        if (!AreVariablesFilled())
    //        {
    //            await AppShell.Current.DisplayAlert("Error", "Debes llenar todas las variables.", "OK");
    //            return;
    //        }

    //        FinalPrompt = GenerateFinalPrompt();

    //        await AppShell.Current.DisplayAlert("Prompt Generado", FinalPrompt, "OK");
    //    }
    //    catch (Exception ex)
    //    {
    //        // Manejo de errores
    //        await AppShell.Current.DisplayAlert("Error", "Ocurrió un problema al generar el prompt. Por favor, inténtalo nuevamente.", "OK");
    //    }
    //    finally
    //    {
    //        IsLoading = false; // Asegurar que el indicador de carga se oculta siempre
    //    }
    //}

    [RelayCommand]
    private async Task GeneratePromptAsync()
    {
        await ExecuteWithLoadingAsync(async () =>
        {
            if (!AreVariablesFilled())
            {
                await AppShell.Current.DisplayAlert("Error", AppMessages.Prompts.PromptVariablesError, "OK");
                return;
            }

            FinalPrompt = GenerateFinalPrompt();

            await AppShell.Current.DisplayAlert("Prompt Generado", FinalPrompt, "OK");
        }, AppMessages.GenericError);
    }



    // Métodos auxiliares
    private bool AreVariablesFilled()
    {
        return Variables.All(v => !string.IsNullOrWhiteSpace(v.Value));
    }

    private string GenerateFinalPrompt()
    {
        var finalPromptBuilder = new StringBuilder(PromptText);

        foreach (var variable in Variables)
        {
            finalPromptBuilder.Replace($"{{{variable.Name}}}", variable.Value);
        }

        return finalPromptBuilder.ToString();
    }


  
}

public class VariableInput
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}