using System.Text.Json;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Companion;

/// <summary>Read-only DMR configuration check for the explicitly selected execution profile. Never configures or downloads a model.</summary>
internal static class CompanionReasoningPreflight
{
    internal static void Validate(JsonElement metadata, string physicalModel)
    {
        if (physicalModel != PairedReasoningProtocol.PhysicalModel && physicalModel != PairedReasoningProtocol.PhysicalModel[10..])
            throw new InvalidOperationException("DMR_REASONING_PROFILE_NOT_READY");
        try
        {
            if (metadata.ValueKind != JsonValueKind.Array || metadata.GetArrayLength() != 1) throw new InvalidOperationException();
            var entry = metadata[0]; var config = entry.GetProperty("Config");
            if (entry.GetProperty("ModelID").GetString() != physicalModel[(physicalModel.LastIndexOf('@') + 1)..]
                || entry.GetProperty("Backend").GetString() != "llama.cpp" || entry.GetProperty("Mode").GetString() != "completion"
                || config.GetProperty("context-size").GetInt32() != 12288
                || config.GetProperty("llamacpp").GetProperty("reasoning-budget").GetInt32() != -1
                || config.GetProperty("keep_alive").GetString() != "5m0s"
                || config.GetProperty("runtime-flags").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { "--parallel", "1" }) is false)
                throw new InvalidOperationException();
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { throw new InvalidOperationException("DMR_REASONING_PROFILE_NOT_READY"); }
    }

    internal static async Task Check(CompanionOptions options, CancellationToken cancellation)
    {
        if (options.Command != "run" || options.ReasoningProfile is null) return;
        options.Inference.Validate();
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false })
            { BaseAddress = new Uri(options.Inference.Endpoint), Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 64000 };
        using var response = await http.GetAsync("engines/_configure?model=" + Uri.EscapeDataString(options.Inference.PhysicalModel!), cancellation);
        response.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        Validate(metadata.RootElement, options.Inference.PhysicalModel!);
    }
}
