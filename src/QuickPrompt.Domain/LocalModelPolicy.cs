using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;

public sealed record LocalModelCapability(string Name, bool Thinking);

// Pure product policy. Namespace retained for compatibility; owned by Domain.
// Trusted composition supplies descriptors and qualified subsets, never model output.
public sealed class LocalModelPolicy
{
    // Keep the same runner profile for semantic, editor and visible UI stages.
    public const int AgentContextLength = 12288;
    public const string DefaultModelName = "qwen3:8b";
    public static LocalModelPolicy Production { get; } = new LocalModelPolicy(
        new LocalModelCapability(DefaultModelName, true), new LocalModelCapability("qwen2.5:7b-instruct", false)).WithAgentControl(DefaultModelName);
    private readonly Dictionary<string, LocalModelCapability> capabilities;
    private readonly HashSet<string> agentControlModels = new(StringComparer.Ordinal);
    public string DefaultModel { get; }
    public string[] Names => capabilities.Keys.ToArray();

    public LocalModelPolicy(params LocalModelCapability[] models)
    {
        if (models is null || models.Length == 0 || models.Any(model => model is null || string.IsNullOrWhiteSpace(model.Name) || model.Name.Length > 200)
            || models.Select(model => model.Name).Distinct(StringComparer.Ordinal).Count() != models.Length)
            throw new ArgumentException("Declare a nonempty, unique local model policy.", nameof(models));
        capabilities = models.ToDictionary(model => model.Name, StringComparer.Ordinal);
        DefaultModel = models[0].Name;
    }

    private LocalModelPolicy(LocalModelCapability[] models, string[] agentControlModels) : this(models)
    {
        if (agentControlModels is null || agentControlModels.Any(model => !Allows(model))
            || agentControlModels.Distinct(StringComparer.Ordinal).Count() != agentControlModels.Length)
            throw new ArgumentException("Agent-control identities must be a unique subset of the model policy.", nameof(agentControlModels));
        this.agentControlModels = new(agentControlModels, StringComparer.Ordinal);
    }
    public LocalModelPolicy WithAgentControl(params string[] models) => new(capabilities.Values.ToArray(), models);
    public bool AllowsAgentControl(string model) => model is not null && agentControlModels.Contains(model);

    public bool Allows(string model) => model is not null && capabilities.ContainsKey(model);
    public LocalModelCapability Require(string model) => Allows(model) ? capabilities[model]
        : throw new ValidationException("Modelo no permitido / Model not allowed.");
}
