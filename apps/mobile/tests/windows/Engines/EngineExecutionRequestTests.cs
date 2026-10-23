using System;
using Xunit;
using QuickPrompt.Engines.Execution;

namespace QuickPrompt.Tests.Engines
{
    public class EngineExecutionRequestTests
    {
        [Fact]
        public void CanCreateRequest()
        {
            var req = new EngineExecutionRequest { EngineName = "ChatGPT", Prompt = "Hola" };
            Assert.Equal("ChatGPT", req.EngineName);
            Assert.Equal("Hola", req.Prompt);
        }
    }
}
