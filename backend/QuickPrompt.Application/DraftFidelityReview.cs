namespace QuickPrompt.Nova.Application;

// Process-owned provenance of a body copied from one exact original occurrence.
// It cannot approve content or grant effects; the reviewer still sees the full task.
public sealed record VerbatimBodyBinding(string RequestHash, int Start, int Length)
{
    public string MetadataAuthority(string request, string body)
    {
        if (RequestHash != LiveRuns.Hash(request) || Start < 0 || Length <= 0
            || Start > request.Length - Length || request.Substring(Start, Length) != body)
            throw new QuickPrompt.Local.ValidationException("El cuerpo literal dejó de coincidir con su encargo original.");
        return request.Remove(Start, Length).Insert(Start, "[BOUND_INSTRUCTION_BODY]");
    }
}

// Reviews one exact proposal; it never authorizes persistence or other effects.
public interface IDraftFidelityReviewer
{
    Task<AuthoringObligationContract> ReadAuthoringObligations(AuthoringObligationContract material, string model, CancellationToken ct)
        => throw new PreparationToolException("AUTHORING_INVENTORY_UNAVAILABLE", "read_authoring_obligations", "Falta el propietario del inventario previo de autoría.");
    Task<AuthoringReferenceInterpretation> ReadReferenceScopes(AuthoringReferenceCoverage references, string model, CancellationToken ct)
        => throw new PreparationToolException("REFERENCE_SCOPE_UNAVAILABLE", "read_reference_scope", "El revisor debe interpretar referencias desde el encargo original antes de redactar.");
    Task<string?> ReadRequestedTitle(string request,string[] inputNames,string model,CancellationToken ct)
        => throw new NotSupportedException("El revisor debe declarar su capacidad de interpretar el título solicitado.");
    Task ReviewDraftFidelity(PrepareInput input, PromptProposal proposal, RetrievalCandidate[] candidates,
        CancellationToken ct, string? boundTitleLiteral = null, Action<int, string, string?>? diagnostic = null);
}
