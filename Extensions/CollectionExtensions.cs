using QuickPrompt.Models;
using QuickPrompt.Services;
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
        this IEnumerable<PromptTemplate> prompts,
        PromptDatabaseService promptDatabaseService,
        Action<PromptTemplateViewModel> onSelectToDelete,
        Action<PromptTemplateViewModel> onItemToDelete,
          Action<string, PromptTemplate> onSelectToNavigate)
    {
        return new ObservableCollection<PromptTemplateViewModel>(prompts.Select(p =>
        new PromptTemplateViewModel(p,
        promptDatabaseService,
        onSelectToDelete,
        onItemToDelete,
        onSelectToNavigate)));
    }
}