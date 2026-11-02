namespace QuickPrompt.Recovery;

/// <summary>Closed operator CLI contract. Verification has no output path or password option.</summary>
internal sealed record RecoveryCommand(string Mode, IReadOnlyDictionary<string, string> Options)
{
    internal static RecoveryCommand Parse(string[] args)
    {
        if (args.Length < 1 || args[0] is not ("protect" or "recover" or "verify"))
            throw new InvalidDataException("Select protect, recover or verify.");
        var mode = args[0];
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(args[index], args[index + 1]))
                throw new InvalidDataException("Malformed or duplicate operator option.");
        var required = new[] { "--repository-root", "--input", "--plaintext-sha256", "--recipient-sha256" }
            .Concat(mode == "protect" ? ["--recipient-cert", "--retain-until"] : ["--recipient-pfx", "--archive-sha256"])
            .Concat(mode == "verify" ? [] : new[] { "--output" }).ToArray();
        if (options.Count != required.Length || required.Any(option => !options.ContainsKey(option)))
            throw new InvalidDataException("Unexpected or missing operator option.");
        return new(mode, options);
    }

    internal void RequireInteractiveInput(bool isRedirected)
    {
        if (Mode != "protect" && isRedirected)
            throw new InvalidOperationException("Recovery and verification require an interactive operator console.");
    }
}
