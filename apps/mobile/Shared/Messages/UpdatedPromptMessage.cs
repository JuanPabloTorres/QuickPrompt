using CommunityToolkit.Mvvm.Messaging.Messages;
using QuickPrompt.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools.Messages
{
    public class UpdatedPromptMessage:ValueChangedMessage<PromptTemplate>
    {
        public UpdatedPromptMessage(PromptTemplate value):base(value)
        {
            
        }
    }
}
