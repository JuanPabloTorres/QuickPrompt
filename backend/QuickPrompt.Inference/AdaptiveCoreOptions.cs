using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed record AdaptiveCoreOptions(string Python, string Worker, string SdkRoot, string Endpoint, bool Thinking = true, bool QwenThinkingSamplingEnabled = false)
{
    public void Validate()
    {
        if (!File.Exists(Python) || !File.Exists(Worker) || !File.Exists(Path.Combine(SdkRoot, "adaptive_core", "host.py")) || !File.Exists(Path.Combine(SdkRoot, "adaptive_core", "inference.py")))
            throw new InvalidOperationException("Adaptive Core requiere Python, worker y SDK existentes configurados explícitamente.");
        _ = LocalModelEndpoint.Require(Endpoint);
    }
}
