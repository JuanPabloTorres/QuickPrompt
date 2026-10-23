using QuickPrompt.Domain.Enums;

namespace QuickPrompt.Nova.Application;

public record PromptCategoryOption(int Id, string Name);

// The persisted domain enum owns identities. Labels are presentation metadata,
// never a separate category list or natural-language intent classifier.
public static class PromptCategories
{
    public static PromptCategoryOption[] All => Enum.GetValues<PromptCategory>()
        .Select(category => new PromptCategoryOption((int)category, category switch {
            PromptCategory.General => "General",
            PromptCategory.Productivity => "Productividad",
            PromptCategory.HealthAndFitness => "Salud y ejercicio",
            PromptCategory.Education => "Educación",
            PromptCategory.CareerAndResume => "Carrera y currículum",
            PromptCategory.LanguageLearning => "Aprendizaje de idiomas",
            PromptCategory.Translation => "Traducción",
            PromptCategory.Technology => "Tecnología",
            PromptCategory.ArtificialIntelligence => "Inteligencia artificial",
            PromptCategory.BusinessAndStartups => "Negocios y emprendimiento",
            PromptCategory.SocialMedia => "Redes sociales",
            PromptCategory.Writing => "Escritura",
            PromptCategory.CreativeWriting => "Escritura creativa",
            PromptCategory.Marketing => "Marketing",
            PromptCategory.FinanceAndBudgeting => "Finanzas y presupuesto",
            PromptCategory.TravelAndLeisure => "Viajes y ocio",
            PromptCategory.CookingAndRecipes => "Cocina y recetas",
            PromptCategory.InterviewPreparation => "Preparación de entrevistas",
            PromptCategory.MentalHealth => "Salud mental",
            PromptCategory.Parenting => "Crianza",
            PromptCategory.Entertainment => "Entretenimiento",
            PromptCategory.SportsAndAnalysis => "Deportes y análisis",
            PromptCategory.LegalAndPolicies => "Legal y políticas",
            PromptCategory.Medical => "Medicina",
            PromptCategory.Inspirational => "Inspiración",
            PromptCategory.EventPlanning => "Planificación de eventos",
            PromptCategory.UXAndDesign => "UX y diseño",
            PromptCategory.Programming => "Programación",
            PromptCategory.Gaming => "Videojuegos",
            _ => category.ToString()
        })).ToArray();
}
