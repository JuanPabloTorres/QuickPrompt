using QuickPrompt.Nova.Application;

namespace QuickPrompt.Nova.Api;

// Host configuration is read once per composition, never retained by deferred
// singleton factories. User preferences remain with their domain-owned stores.
internal sealed record NovaAgentSettings(string Endpoint, TimeSpan AiTimeout, bool QwenThinkingSampling,
    PreparationOptions Budget, bool SemanticThinking, bool AuthoringUseContract, bool DirectSourceRead,
    bool StopAfterVerifiedResult, bool ExecuteGroundedPlan)
{
    public string Runtime { get; init; } = "adaptive-core";
    public string CorePython { get; init; } = "";
    public string CoreWorker { get; init; } = "agents/nova-core/nova_worker.py";
    public string CoreSdkRoot { get; init; } = "../Adaptive-AI-Core";
    public static NovaAgentSettings Read(IConfiguration configuration)
    {
        var aiTimeout = configuration.GetValue("QuickPrompt:AiTimeoutSeconds", 180);
        if (aiTimeout is < 5 or > 180) throw new InvalidOperationException("AI timeout must be between 5 and 180 seconds.");
        var budget = new PreparationOptions(configuration.GetValue("QuickPrompt:Preparation:MaxSteps", 5),
            configuration.GetValue("QuickPrompt:Preparation:StageTimeoutSeconds", 90), configuration.GetValue("QuickPrompt:Preparation:TotalTimeoutSeconds", 180),
            configuration.GetValue("QuickPrompt:Preparation:SemanticMaxSteps", 12));
        if (budget.MaxSteps is < 3 or > 10 || budget.SemanticMaxSteps is < 3 or > 16 || budget.StageTimeoutSeconds is < 5 or > 180 || budget.TotalTimeoutSeconds is < 10 or > 300)
            throw new InvalidOperationException("Invalid preparation budget.");
        return new(configuration["QuickPrompt:Ollama"] ?? "http://127.0.0.1:11434", TimeSpan.FromSeconds(aiTimeout),
            configuration.GetValue("QuickPrompt:Preparation:QwenThinkingSampling", false),
            budget,
            configuration.GetValue("QuickPrompt:Preparation:SemanticThinking", true),
            configuration.GetValue("QuickPrompt:Preparation:AuthoringUseContract", false),
            configuration.GetValue("QuickPrompt:Preparation:DirectSourceRead", true),
            configuration.GetValue("QuickPrompt:Live:StopAfterVerifiedResult", true),
            configuration.GetValue("QuickPrompt:Live:ExecuteGroundedPlan", true)) {
                Runtime = configuration["QuickPrompt:Agent:Runtime"] ?? "adaptive-core",
                CorePython = configuration["QuickPrompt:AdaptiveCore:Python"] ?? "",
                CoreWorker = configuration["QuickPrompt:AdaptiveCore:Worker"] ?? "agents/nova-core/nova_worker.py",
                CoreSdkRoot = configuration["QuickPrompt:AdaptiveCore:SdkRoot"] ?? "../Adaptive-AI-Core"
            };
    }
}
