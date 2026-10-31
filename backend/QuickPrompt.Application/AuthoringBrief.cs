using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

public sealed record AuthoringBriefReference(string Uri, AuthoringReferenceRole? Role);
public sealed record AuthoringBriefContent(string Title, string Description, string Task, string[] Steps,
    string[] Constraints, string[] OutputRequirements, AuthoringBriefReference[] References);
public sealed record AuthoringBriefReview(string BasisHash, string OriginalRequest, DraftSourceSnapshot? Source,
    string[] Inputs, AuthoringBriefContent Proposed)
{
    public string Stage { get; init; } = AuthoringBriefs.ReviewContent;
    public AuthoringBriefContent? EstablishedTask { get; init; }
    public AuthoringBriefContent? Suggested { get; init; }
}
public sealed record AuthoringBriefReply(string BasisHash, AuthoringBriefContent Content, bool Confirmed);

/// <summary>A human confirmed future task, distinct from both the AI suggestion and permission to save or execute.</summary>
public sealed record ConfirmedAuthoringBrief(AuthoringBriefReview Review, AuthoringBriefContent Content)
{
    public bool Edited => LiveRuns.Hash(Review.Proposed) != LiveRuns.Hash(Content);

    public void ValidateBasis(DraftProposalContract contract)
    {
        if (Review.BasisHash != contract.AuthoringContextFingerprint || Review.OriginalRequest != contract.AuthoringRequest
            || LiveRuns.Hash(new { Review.Source }) != LiveRuns.Hash(new { contract.Source })
            || !Review.Inputs.SequenceEqual(contract.FieldContract.Names))
            throw new ConflictException("El encargo, el origen o las variables cambiaron. Revisa un nuevo contrato antes de redactar.");
        AuthoringBriefs.Validate(Content, Review);
    }

    public string Render()
    {
        if (Review.Stage == AuthoringBriefs.EstablishTask)
            throw new ValidationException("La tarea está establecida, pero el contenido final todavía requiere revisión.");
        return ComposeContent();
    }

    internal void ValidateComposition() => _ = ComposeContent();

    private string ComposeContent()
    {
        AuthoringBriefs.Validate(Content, Review);
        var parts = new List<string> { Content.Task };
        Add(Content.Steps, true); Add(Content.Constraints); Add(Content.OutputRequirements);
        var authoredReferences = new AuthoringReferenceCoverage(string.Join("\n\n", parts)).References;
        foreach (var reference in Content.References.Where(item => item.Role != AuthoringReferenceRole.CURRENT_SESSION))
            if (!authoredReferences.Contains(reference.Uri)) parts.Add(reference.Uri);
        foreach (var name in Review.Inputs)
            if (!TemplateCompiler.Names(string.Join("\n\n", parts)).Contains(name)) parts.Add($"{name}: <{name}>");
        var body = string.Join("\n\n", parts);
        var references = new AuthoringReferenceCoverage(body).References;
        if (Content.References.Any(item => item.Role == AuthoringReferenceRole.CURRENT_SESSION && references.Contains(item.Uri)))
            throw new ValidationException("Una referencia sólo para esta sesión aparece en el contenido. Retírala del encargo futuro o cambia su papel.");
        if (body.Length > 32000) throw new ValidationException("El contrato confirmado excede el límite completo; no se truncará.");
        return body;

        void Add(string[] items, bool numbered = false) { if (items.Length > 0) parts.Add(string.Join("\n", items.Select((item, index) => (numbered ? $"{index + 1}. " : "- ") + item))); }
    }
}

public static class AuthoringBriefs
{
    public const string EstablishTask = "ESTABLISH_TASK";
    public const string ReviewContent = "REVIEW_CONTENT";

    /// <summary>No inference or inferred task: the author establishes the task before the writer is eligible.</summary>
    public static AuthoringBriefReview Establish(DraftProposalContract contract, string title, AuthoringBriefContent? draft = null)
    {
        var review = Propose(contract, draft is null ? new(title, contract.Source?.Description ?? "", contract.SourceText ?? "", [], [], [], []) : Copy(draft));
        // A prior task is only an editable seed after an answer changes the basis.
        // New references stay unassigned; every confirmation starts unchecked.
        return review with { Stage = EstablishTask, Proposed = review.Proposed with {
            References = review.Proposed.References.Select(item => item with {
                Role = draft?.References.FirstOrDefault(prior => prior.Uri == item.Uri)?.Role
            }).ToArray()
        } };
    }

    /// <summary>The established contract stays selected. Model suggestions require an explicit human change.</summary>
    public static AuthoringBriefReview Suggest(DraftProposalContract contract, AuthoringBriefContent suggestion)
    {
        var established = contract.ConfirmedBrief;
        if (established?.Review.Stage != EstablishTask)
            throw new ConflictException("Establece primero la tarea y los requisitos de este encargo.");
        return Propose(contract, established.Content) with {
            Proposed = Copy(established.Content), EstablishedTask = Copy(established.Content),
            Suggested = Copy(suggestion with { References = established.Content.References })
        };
    }

    public static AuthoringBriefReview Propose(DraftProposalContract contract, AuthoringBriefContent content)
    {
        // The reference inventory is application-owned and complete before the user
        // assigns roles. Answers are author statements; generated questions are not.
        // Parse each source independently so fragments cannot manufacture a new URI.
        var references = new[] { contract.AuthoringRequest, contract.SourceText ?? "" }
            .Concat(contract.UserClarifications.Select(item => item.Answer))
            .SelectMany(text => new AuthoringReferenceCoverage(text).References).Distinct(StringComparer.Ordinal)
            .Select(uri => new AuthoringBriefReference(uri, null)).ToArray();
        if (references.Length > 16 || references.Sum(item => item.Uri.Length) > 8000)
            throw new ValidationException("El encargo y su origen contienen demasiadas referencias para revisarlas completas. Reduce el alcance.");
        var proposed = content with { References = references };
        return new(contract.AuthoringContextFingerprint, contract.AuthoringRequest, contract.Source, contract.FieldContract.Names.ToArray(), proposed);
    }

    public static ConfirmedAuthoringBrief Confirm(AuthoringBriefReview review, AuthoringBriefReply reply)
    {
        if (!reply.Confirmed || reply.BasisHash != review.BasisHash)
            throw new ConflictException("Confirma el contrato mostrado para esta pregunta, sin autorizar guardado.");
        Validate(reply.Content, review);
        var confirmed = new ConfirmedAuthoringBrief(review with { Inputs = review.Inputs.ToArray(), Proposed = Copy(review.Proposed),
            EstablishedTask = review.EstablishedTask is null ? null : Copy(review.EstablishedTask),
            Suggested = review.Suggested is null ? null : Copy(review.Suggested) }, Copy(reply.Content));
        confirmed.ValidateComposition(); // Bounds and reference roles apply in both phases; establishment produces no draft.
        return confirmed;
    }

    public static void Validate(AuthoringBriefContent content, AuthoringBriefReview review)
    {
        if (review.Stage is not (EstablishTask or ReviewContent)
            || review.Stage == EstablishTask && (review.Suggested is not null || review.EstablishedTask is not null))
            throw new ValidationException("La fase del contrato de autoría no es válida.");
        if (content is null || string.IsNullOrWhiteSpace(content.Title) || content.Title.Length > 160
            || content.Title.IndexOfAny(['\r', '\n']) >= 0 || content.Description is null || content.Description.Length > 2000
            || string.IsNullOrWhiteSpace(content.Task) || content.Task.Length > 8000
            || new[] { content.Steps, content.Constraints, content.OutputRequirements }.Any(items => items is null
                || items.Length > 40 || items.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 8000)))
            throw new ValidationException("Revisa la tarea y sus requisitos completos antes de confirmarlos.");
        if (content.References is null || content.References.Length != review.Proposed.References.Length
            || content.References.Any(item => item is null)
            || !content.References.Select(item => item.Uri).SequenceEqual(review.Proposed.References.Select(item => item.Uri))
            || content.References.Any(item => item.Role is null || !Enum.IsDefined(item.Role.Value)))
            throw new ValidationException("Elige explícitamente para qué se usa cada referencia del encargo.");
        var body = string.Join("\n", new[] { content.Task }.Concat(content.Steps).Concat(content.Constraints).Concat(content.OutputRequirements));
        var extra = TemplateCompiler.Names(body).Except(review.Inputs).ToArray();
        if (extra.Length > 0)
            throw new ValidationException("Campos no autorizados en el contrato: " + string.Join(", ", extra) + ". Corrige el contenido o cambia la selección de variables.");
    }

    internal static AuthoringBriefContent Copy(AuthoringBriefContent content) => content with { Steps = content.Steps.ToArray(),
        Constraints = content.Constraints.ToArray(), OutputRequirements = content.OutputRequirements.ToArray(), References = content.References.ToArray() };

    public static LiveStartInput Continue(LiveStartInput original, LiveRecord blocked, AuthoringBriefReview review,
        Guid nextRunId, long sequence, AuthoringBriefReply reply)
    {
        // Reuse the existing host-owned continuation guards; no semantic answer is
        // interpreted and no model is called when a future task is confirmed.
        AuthoringClarifications.ValidateContinuation(original, blocked, nextRunId, sequence);
        return original with { RunId = nextRunId, UserClarifications = AuthoringClarifications.Copy(original.UserClarifications),
            Values = original.Values?.ToDictionary(item => item.Key, item => item.Value),
            Command = original.Command! with { InputNames = original.Command.InputNames?.ToArray(), RequestedOutcomes = original.Command.RequestedOutcomes?.ToArray() },
            ConfirmedBrief = Confirm(review, reply), AuthoringTaskDraft = null };
    }
}
