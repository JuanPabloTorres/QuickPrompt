using QuickPrompt.Models.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Converters
{
    public class CategoryToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                PromptCategory.General => "General",
                PromptCategory.Productivity => "Productivity",
                PromptCategory.HealthAndFitness => "Health & Fitness",
                PromptCategory.Education => "Education",
                PromptCategory.CareerAndResume => "Career & Resume",
                PromptCategory.LanguageLearning => "Language Learning",
                PromptCategory.Translation => "Translation",
                PromptCategory.Technology => "Technology",
                PromptCategory.ArtificialIntelligence => "Artificial Intelligence",
                PromptCategory.BusinessAndStartups => "Business & Startups",
                PromptCategory.SocialMedia => "Social Media",
                PromptCategory.Writing => "Writing",
                PromptCategory.CreativeWriting => "Creative Writing",
                PromptCategory.Marketing => "Marketing",
                PromptCategory.FinanceAndBudgeting => "Finance & Budgeting",
                PromptCategory.TravelAndLeisure => "Travel & Leisure",
                PromptCategory.CookingAndRecipes => "Cooking & Recipes",
                PromptCategory.InterviewPreparation => "Interview Preparation",
                PromptCategory.MentalHealth => "Mental Health",
                PromptCategory.Parenting => "Parenting",
                PromptCategory.Entertainment => "Entertainment",
                PromptCategory.SportsAndAnalysis => "Sports & Analysis",
                PromptCategory.LegalAndPolicies => "Legal & Policies",
                PromptCategory.Medical => "Medical",
                PromptCategory.Inspirational => "Inspirational",
                PromptCategory.EventPlanning => "Event Planning",
                PromptCategory.UXAndDesign => "UX & Design",
                PromptCategory.Programming => "Programming",
                PromptCategory.Gaming => "Gaming",
                _ => value?.ToString() ?? "Unknown"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }


}
