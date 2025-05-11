using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public static class PromptDateFilterLabels
    {
        public static readonly Dictionary<Filters, string> Labels = new()
    {
        { Filters.All, "All" },
        { Filters.Favorites, "Favorites" },
        { Filters.NonFavorites, "Others" },
        { Filters.Today, "Today" },
        { Filters.Last7Days, "Last 7 Days" }
    };
    }

    public enum Filters
    {
        All,
        Favorites,
        NonFavorites,
        Today,
        Last7Days
    }
}