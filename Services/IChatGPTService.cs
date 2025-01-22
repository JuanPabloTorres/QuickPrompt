using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public interface IChatGPTService
    {
        Task<string> GetResponseFromChatGPTAsync(string prompt);
    }
}
