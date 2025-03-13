//No esta en uso.Proposito era para abir chat gpt app ya con el final prompt creado.
#if ANDROID
using Android.App;
using Android.Content;
using Android.Net;
using Android.Util;
using System;

public static class ChatGptLauncherService
{
    private const string PackageName = "com.openai.chatgpt"; // Paquete oficial de la app de ChatGPT

    public static void OpenChatGptApp(string prompt)
    {
        try
        {
            var context = Android.App.Application.Context;
            var packageManager = context.PackageManager;

            // Verifica si la app de ChatGPT está instalada
            var intent = packageManager?.GetLaunchIntentForPackage(PackageName);

            if (intent != null)
            {
                intent.SetPackage(PackageName); // Asegura que se abra la app correcta
                intent.PutExtra(Intent.ExtraText, prompt);
                intent.SetType("text/plain");
                intent.SetAction(Intent.ActionSend);
                intent.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);
                return;
            }

            // Si la app no está instalada, abrir la Play Store
            OpenPlayStore(context);
        }
        catch (Exception ex)
        {
            Log.Error("ChatGptLauncher", $"Error al abrir ChatGPT: {ex.Message}");
        }
    }

    private static void OpenPlayStore(Context context)
    {
        try
        {
            var playStoreUri = Android.Net.Uri.Parse($"market://details?id={PackageName}");
            var playStoreIntent = new Intent(Intent.ActionView, playStoreUri);
            playStoreIntent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(playStoreIntent);
        }
        catch (Exception ex)
        {
            Log.Error("ChatGptLauncher", $"Error al abrir Play Store: {ex.Message}");

            // Si no puede abrir el market://, intenta con la URL normal
            var webUri = Android.Net.Uri.Parse($"https://play.google.com/store/apps/details?id={PackageName}");
            var webIntent = new Intent(Intent.ActionView, webUri);
            webIntent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(webIntent);
        }
    }
}
#endif
