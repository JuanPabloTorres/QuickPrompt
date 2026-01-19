using QuickPrompt.Domain.Entities;
using PromptTemplateLegacy = QuickPrompt.Models.PromptTemplate;
using QuickPrompt.Services;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.ViewModels.Prompts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Extensions;

public static class CollectionExtensions
{
    public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        if (items == null) return;

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    public static ObservableCollection<PromptTemplateViewModel> ToViewModelObservableCollection(
        this IEnumerable<PromptTemplateLegacy> prompts,
        IPromptRepository promptDatabaseService,
        Action<PromptTemplateViewModel> onSelectToDelete,
        Action<PromptTemplateViewModel> onItemToDelete,
          Action<string, PromptTemplateLegacy> onSelectToNavigate)
    {
        return new ObservableCollection<PromptTemplateViewModel>(prompts.Select(p =>
        new PromptTemplateViewModel(p,
        promptDatabaseService,
        onSelectToDelete,
        onItemToDelete,
        onSelectToNavigate)));
    }

    /// <summary>
    /// Temporary extension for Domain.Entities.PromptTemplate until UI layer is refactored.
    /// TODO: Remove in FASE 4 when UI uses Domain entities directly.
    /// </summary>
    public static ObservableCollection<PromptTemplateViewModel> ToViewModelObservableCollection(
        this IEnumerable<PromptTemplate> prompts,
        IPromptRepository promptDatabaseService,
        Action<PromptTemplateViewModel> onSelectToDelete,
        Action<PromptTemplateViewModel> onItemToDelete,
          Action<string, PromptTemplateLegacy> onSelectToNavigate)
    {
        // Convert Domain entities to Legacy models temporarily
        var legacyPrompts = prompts.Select(p => new PromptTemplateLegacy
        {
            Id = p.Id,
            Title = p.Title,
            Template = p.Template,
            Description = p.Description,
            Category = (QuickPrompt.Models.Enums.PromptCategory)p.Category, // Cast enum
            Variables = p.Variables,
            IsFavorite = p.IsFavorite,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            IsActive = p.IsActive,
            DeletedAt = p.DeletedAt
        });

        return legacyPrompts.ToViewModelObservableCollection(
            promptDatabaseService,
            onSelectToDelete,
            onItemToDelete,
            onSelectToNavigate);
    }
}