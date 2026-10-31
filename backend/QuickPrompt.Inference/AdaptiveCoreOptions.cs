using QuickPrompt.Local;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("QuickPrompt.Companion.Tests")]

namespace QuickPrompt.Web.Infrastructure;

/// <summary>Operator-owned physical provider binding; inference packets retain the logical qwen3:8b model contract.</summary>
public sealed record AdaptiveCoreOptions(string Python, string Worker, string SdkRoot, string Endpoint, bool Thinking = true,
    bool QwenThinkingSamplingEnabled = false, string Provider = "ollama", string? PhysicalModel = null)
{
    public const string ModelAlias = "qwen3:8b";

    /// <summary>Validates provider/model/endpoint without launching processes or inspecting executable contents.</summary>
    public void ValidateModelConfiguration()
    {
        if (Provider is not ("ollama" or "docker-model-runner"))
            throw new InvalidOperationException("Configura explícitamente ollama o docker-model-runner; no hay fallback.");
        var endpoint = LocalModelEndpoint.Require(Endpoint);
        if (Provider == "ollama")
        {
            if (PhysicalModel is not null) throw new InvalidOperationException("Ollama conserva el modelo lógico; no admite un mapping físico de DMR.");
            return;
        }
        if (endpoint.Port != 12434 || PhysicalModel is not { Length: > 0 and <= 240 } || PhysicalModel.Contains("://", StringComparison.Ordinal)
            || !Regex.IsMatch(PhysicalModel, @"^[A-Za-z0-9][A-Za-z0-9._:/-]*@sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("DMR requiere loopback HTTP 12434 y una referencia física fijada por @sha256.");
    }

    public void Validate()
    {
        ValidateModelConfiguration();
        if (!File.Exists(Python) || !File.Exists(Worker) || !File.Exists(Path.Combine(SdkRoot, "adaptive_core", "host.py")) || !File.Exists(Path.Combine(SdkRoot, "adaptive_core", "inference.py")))
            throw new InvalidOperationException("Adaptive Core requiere Python, worker y SDK existentes configurados explícitamente.");
        if (Provider == "docker-model-runner" && !File.Exists(Path.Combine(SdkRoot, "adaptive_core", "dmr_framework.py")))
            throw new InvalidOperationException("El SDK configurado no contiene el adaptador DMR; no se usará otro proveedor.");
    }
}
