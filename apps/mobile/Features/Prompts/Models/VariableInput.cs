using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickPrompt.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace QuickPrompt.Models
{
    public partial class VariableInput : ObservableObject
    {
        [ObservableProperty] private string? name;

        [ObservableProperty] private string? value;

        [ObservableProperty] private bool isFocused;

        public bool ShowSuggestions => IsFocused && Suggestions?.Count > 0;
        public List<string> Suggestions => string.IsNullOrWhiteSpace(Name) ? new() : PromptVariableCache.GetSuggestions(Name);

        public IRelayCommand<string> ApplySuggestionCommand => new RelayCommand<string>(ApplySuggestion);

        private void ApplySuggestion(string suggestion)
        {
            Value = suggestion;

            IsFocused = false;

            OnPropertyChanged(nameof(ShowSuggestions));
        }


        partial void OnIsFocusedChanged(bool oldValue, bool newValue)
        {
            OnPropertyChanged(nameof(ShowSuggestions));
        }

        partial void OnValueChanged(string? oldValue, string? newValue)
        {
            // Opcional: si el valor cambia manualmente, puedes refrescar visibilidad también
            OnPropertyChanged(nameof(ShowSuggestions));
        }

        public void ForceShowSuggestions()
        {
            IsFocused = false;

            IsFocused = true;
        }


    }
}