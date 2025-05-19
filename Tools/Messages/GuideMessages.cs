using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools.Messages
{
    public static class GuideMessages
    {
        private static readonly string[] CompletionMessages =
        {
        "🔥 That’s it! You’re all set to prompt like a pro!",
        "You’re now ready to create and reuse powerful prompts!",
        "Great job! You’ve completed the QuickPrompt guide.",
        "🎉 You’ve completed the guide!"
    };

        private static readonly Random _random = new();

        public static string GetRandomGuideCompleteMessage()
        {
            int index = _random.Next(CompletionMessages.Length);

            // Ensure the message is not the same as the last one
            return CompletionMessages[index];
        }
    }

}
