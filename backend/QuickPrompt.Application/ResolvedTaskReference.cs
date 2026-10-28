using System.Security.Cryptography;
using System.Text;
using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

// Ephemeral application-owned knowledge. It grants no UI selection or permission.
public sealed record ResolvedTaskReference(string RequestFingerprint, SemanticReferenceChoice Reference, int? SourceRevision)
{
    private static string Fingerprint(string request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
    public static ResolvedTaskReference Bind(string request, SemanticReferenceChoice reference, SemanticContext context)
    {
        var entity = Entity(reference.ResolvedTargetId, context);
        var bound = new ResolvedTaskReference(Fingerprint(request), reference, entity?.Revision);
        bound.Validate(request, context); return bound;
    }
    private static SemanticEntity? Entity(Guid? id, SemanticContext context) => id is null ? null
        : new[] { context.ResolvedSource, context.Selected, context.DraftSource }.Concat(context.Candidates).FirstOrDefault(entity => entity?.Id == id);
    public SemanticReferenceChoice Validate(string request, SemanticContext context)
    {
        if (RequestFingerprint != Fingerprint(request)) throw new ValidationException("La referencia pertenece a otra petición.");
        if (Reference.ResolvedTargetId is { } id) {
            var entity = Entity(id, context);
            if (entity is null || entity.Stale || entity.Revision != SourceRevision || !context.Ids.Contains(id))
                throw new ConflictException("La referencia ya no está ligada a un origen vigente.");
        } else if (Reference.TargetReference == TargetReference.ACTIVE_DRAFT && context.ActiveDraft is null)
            throw new ValidationException("El borrador de la referencia ya no está disponible.");
        return Reference;
    }
}
