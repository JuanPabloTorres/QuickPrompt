using CommunityToolkit.Maui.Views;
using QuickPrompt.PopUps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public static class GenericToolBox
    {
        public static string SanitizeFileName(string input)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }
            return input.Trim();
        }

        public static async Task ShowLottieMessageAsync(string lottieFile, string message, int delay = 2000)
        {
            await Task.Delay(2000);

            var popup = new LottieMessagePopup(lottieFile, message, delay);

            await Shell.Current.CurrentPage.ShowPopupAsync(popup);
        }

    }
}
