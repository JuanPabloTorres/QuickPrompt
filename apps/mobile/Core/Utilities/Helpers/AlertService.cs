namespace QuickPrompt.Tools;

public static class AlertService
{
    // Método genérico para mostrar alertas
    public static async Task ShowAlert(string title, string message)
    {
        if (Application.Current?.MainPage != null)
        {
            await AppShell.Current.DisplayAlert(title, message, "OK");
        }
    }

    // Método opcional para alertas con opciones "Sí" y "No"
    public static async Task<bool> ShowConfirmationAlert(string title, string message)
    {
        if (Application.Current?.MainPage != null)
        {
            return await AppShell.Current.DisplayAlert(title, message, "Sí", "No");
        }

        return false;
    }
}
