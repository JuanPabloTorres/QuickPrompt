using QuickPrompt.Models;
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

    /// <summary>
    /// Convierte una lista de PromptTemplate en una ObservableCollection de PromptTemplateViewModel.
    /// </summary>
    /// <param name="prompts">Lista de PromptTemplate a convertir.</param>
    /// <returns>ObservableCollection de PromptTemplateViewModel.</returns>
    public static ObservableCollection<PromptTemplateViewModel> ToViewModelObservableCollection(this IEnumerable<PromptTemplate> prompts)
    {
        return new ObservableCollection<PromptTemplateViewModel>(prompts.Select(p => new PromptTemplateViewModel(p)));
    }
}