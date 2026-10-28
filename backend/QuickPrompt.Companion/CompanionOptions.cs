using QuickPrompt.Local;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Companion;

/// <summary>Operator configuration only. Jobs cannot supply origins, endpoints, models or executable paths.</summary>
public sealed record CompanionOptions(string Command, Uri Origin, string Slot, string Label, AdaptiveCoreOptions Inference, int MaximumJobs = 1, string Profile = LocalWorkProtocol.Profile)
{
    public string Resource => new Uri(Origin, LocalCompanionProfiles.ResourcePath(Profile)).AbsoluteUri;
    public string Scope => LocalCompanionProfiles.Scope(Profile);
    public static CompanionOptions Parse(string[] args)
    {
        if (args.Length < 1 || args[0] is not ("pair" or "run" or "status" or "disconnect")) throw new ArgumentException("Usa pair, run, status o disconnect y --server https://…");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var supported = new HashSet<string> { "--server", "--slot", "--name", "--python", "--worker", "--sdk-root", "--endpoint", "--max-jobs", "--capability" };
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !supported.Contains(args[i]) || !values.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Opción inválida o repetida.");
        if (!values.TryGetValue("--server", out var server) || !Uri.TryCreate(server, UriKind.Absolute, out var origin) || origin.Scheme != "https"
            || origin.AbsolutePath != "/" || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new ArgumentException("Configura el origen HTTPS exacto del servidor, sin rutas ni credenciales.");
        var slot = values.GetValueOrDefault("--slot", "default");
        if (slot.Length is < 1 or > 40 || slot.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("El perfil local admite letras, números y guiones.");
        var label = values.GetValueOrDefault("--name", Environment.MachineName);
        if (label.Length is < 1 or > 80) throw new ArgumentException("Nombre de equipo inválido.");
        var jobs = 1;
        if (values.TryGetValue("--max-jobs", out var count) && (!int.TryParse(count, out jobs) || jobs is < 1 or > 50)) throw new ArgumentException("El lote admite de 1 a 50 trabajos.");
        var endpoint = values.GetValueOrDefault("--endpoint", "http://127.0.0.1:11434/");
        _ = LocalModelEndpoint.Require(endpoint);
        var inference = new AdaptiveCoreOptions(values.GetValueOrDefault("--python", ""), values.GetValueOrDefault("--worker", ""), values.GetValueOrDefault("--sdk-root", ""), endpoint, Thinking: false);
        if (args[0] == "run") inference.Validate();
        var profile = values.GetValueOrDefault("--capability", "prompts") switch
        {
            "prompts" => LocalWorkProtocol.Profile,
            "nova" => LocalProposalProtocol.Profile,
            _ => throw new ArgumentException("Usa la capacidad prompts o nova; requiere su propia aprobación.")
        };
        return new(args[0], origin, slot, label, inference, jobs, profile);
    }
}
