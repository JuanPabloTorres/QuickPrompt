using CommunityToolkit.Mvvm.ComponentModel;
using QuickPrompt.Engines.Descriptors;
using QuickPrompt.Engines.Execution;
using QuickPrompt.Engines.Injection;
using QuickPrompt.Engines.Registry;
using System;

namespace QuickPrompt.Engines.WebView
{
    public partial class EngineWebViewViewModel : ObservableObject
    {
        public EngineExecutionRequest Request { get; }
        public AiEngineDescriptor Descriptor { get; }

        [ObservableProperty]
        private string targetUrl = string.Empty;
        
        [ObservableProperty]
        private bool isLoading = true;
        
        [ObservableProperty]
        private string executionStatus = string.Empty;
        
        // Track if initial prompt was successfully submitted
        public bool PromptWasSubmitted { get; private set; }

        public EngineWebViewViewModel(EngineExecutionRequest request)
        {
            Request = request;
            Descriptor = AiEngineRegistry.GetDescriptor(request.EngineName) 
                ?? throw new ArgumentException($"Engine '{request.EngineName}' not found");
            TargetUrl = Descriptor.BaseUrl;
        }

        public void SetExecutionResult(InjectionResult result)
        {
            IsLoading = false;
            ExecutionStatus = result.Status.ToString();
            
            // Mark as submitted if injection was successful
            if (result.Status == InjectionStatus.Success)
            {
                PromptWasSubmitted = true;
            }
        }
    }
}
