namespace QuickPrompt.Nova.Application;

/// <summary>Operator-selected execution profile, independent of OAuth scopes, presence and model output.</summary>
public static class PairedReasoningProtocol
{
    public const string Profile = "qwen3-thinking-r2b/1";
    public const string GrantProfile = "ollama-qwen3-8b-proposals-reasoning-1";
    public const string ProfileHeader = "X-QP-Reasoning-Profile";
    public const string AcceptedHeader = "X-QP-Reasoning-Accepted";
    public const string PhysicalModel = "docker.io/ai/qwen3@sha256:977d1540140cd0d5437d2e22ffc6853714bef103bda39754edd0f2d9e2aabb82";
}
