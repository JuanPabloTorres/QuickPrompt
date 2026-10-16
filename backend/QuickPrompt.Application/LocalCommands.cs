using System.Text.RegularExpressions;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public record LocalCommandInput(string Message);
public record LocalCommand(string Kind, PromptDocument[] Candidates, string Instruction, string Message);

// Resolves navigation/edit requests against real library IDs. This service has no writer or browser capability.
public sealed class LocalCommands(IPromptRetriever retriever)
{
    private static readonly Regex Prefix = new(@"^\s*(?:NOVA[, :]\s*)?(?:por favor[, ]+)?(?:(?:quiero|quisiera|necesito|(?:me\s+)?puedes|podr[ií]as|me gustar[ií]a)(?:\s+que)?\s+)?(?<verb>actual[ií](?:za(?:lo|la|r|s)?|ces|ce)|modific(?:a|ar|as)|modifiques|cambi(?:a|ar|as|es)|mejor(?:a|ar|es)|edit(?:a|ar|es)|[aá]br(?:e(?:lo|la)?|ir|as)|ver|ve|vaya(?:s)?|ir|ll[eé]vame|mu[eé]strame)\b\s*(?<target>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Leading = new(@"^(?:(?:a|al|el|la|los|las|un|una|mi|mis|prompt|item|[ií]tem|elemento|plantilla|llamado|llamada|titulado|titulada|de)\s+)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Quoted = new("[\"«“](?<title>[^\"»”]{1,160})[\"»”](?<change>.*)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Change = new(@"\s+(?:para que|para|y\s+(?:cambia|agrega|a[nñ]ade|incluye|quita|elimina|usa|utiliza))\s+|\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly HashSet<string> Filler = new("EL LA LOS LAS DE DEL PARA UN UNA MI MIS PROMPT ITEM ELEMENTO PLANTILLA QUE USO GUARDE GUARDADO EXISTENTE".Split(' '));

    public LocalCommand Resolve(LocalCommandInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 16000) throw new ValidationException("Escribe una instrucción de hasta 16000 caracteres.");
        var match = Prefix.Match(input.Message);
        if (!match.Success) return new("NONE", [], "", "");
        var verb = PromptSearch.Normalize(match.Groups["verb"].Value);
        var update = verb.StartsWith("ACTUALI") || verb.StartsWith("MODIFI") || verb.StartsWith("CAMBI") || verb.StartsWith("MEJOR");
        var kind = update ? "UPDATE" : verb.StartsWith("EDIT") ? "EDIT" : "OPEN";
        var target = Leading.Replace(match.Groups["target"].Value.Trim(), "").Trim();
        var normalized = PromptSearch.Normalize(target).TrimEnd('.', '?', '!');
        if (normalized is "BIBLIOTECA" or "LISTA" or "LISTA DE PROMPTS" or "INICIO")
            return new(update ? "REFRESH" : "LIBRARY", [], "", "Biblioteca actualizada.");
        if (!update && normalized is "FAVORITOS" or "RECIENTES" or "ARCHIVO")
            return new(normalized == "FAVORITOS" ? "FAVORITES" : normalized == "RECIENTES" ? "RECENT" : "ARCHIVE", [], "", "Filtro de biblioteca aplicado.");
        var quoted = Quoted.Match(target);
        // A real title may itself contain “para”; prefer its exact match before splitting a change request.
        if (!quoted.Success && target.Length is > 0 and <= 160)
        {
            var fullTitle = retriever.Retrieve(target, null).Select(c => c.Prompt)
                .Where(prompt => !prompt.IsArchived && PromptSearch.Normalize(prompt.Title) == normalized).ToArray();
            if (fullTitle.Length > 0) return new(kind, fullTitle, "", kind == "UPDATE" ? "¿Qué quieres cambiar en este prompt?" : "Elemento localizado en tu biblioteca.");
        }
        string instruction;
        if (quoted.Success) { target = quoted.Groups["title"].Value.Trim(); instruction = quoted.Groups["change"].Value.Trim(' ', ',', '.', ':'); }
        else
        {
            var separator = Change.Match(target);
            instruction = separator.Success ? target[(separator.Index + separator.Length)..].Trim() : "";
            if (separator.Success) target = target[..separator.Index].Trim();
        }
        if (kind == "EDIT" && instruction.Length > 0) kind = "UPDATE";
        if (!update && kind == "OPEN") instruction = "";
        if (instruction.Length > 4000) throw new ValidationException("Describe el cambio en un máximo de 4000 caracteres.");
        var terms = Regex.Matches(PromptSearch.Normalize(target), @"[\p{L}\p{N}]+", RegexOptions.None, TimeSpan.FromMilliseconds(100))
            .Select(m => m.Value).Where(term => !Filler.Contains(term)).Distinct().Take(6).ToArray();
        if (terms.Length == 0) return new(kind, [], instruction, "¿Qué prompt quieres abrir o actualizar? Indica su título.");
        var found = retriever.Retrieve(string.Join(' ', terms), null).Select(c => c.Prompt)
            .Where(prompt => !prompt.IsArchived && (quoted.Success ? PromptSearch.Normalize(prompt.Title) == PromptSearch.Normalize(target)
                : terms.All(term => PromptSearch.Normalize(prompt.Title).Contains(term, StringComparison.Ordinal))))
            .DistinctBy(prompt => prompt.Id).Take(6).ToArray();
        var exact = found.Where(prompt => PromptSearch.Normalize(prompt.Title) == PromptSearch.Normalize(target)).ToArray();
        if (exact.Length > 0) found = exact;
        return new(kind, found, instruction, found.Length == 0 ? "No encontré ese prompt. ¿Cuál es su título?"
            : found.Length > 1 ? "Encontré varios prompts. Elige cuál quieres usar."
            : kind == "UPDATE" && instruction.Length == 0 ? "¿Qué quieres cambiar en este prompt?" : "Elemento localizado en tu biblioteca.");
    }
}
