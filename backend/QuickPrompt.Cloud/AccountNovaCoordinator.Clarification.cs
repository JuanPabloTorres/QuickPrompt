using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud;

public sealed record AccountLiveClarification(Guid RunId, Guid ConnectionId, long QuestionSequence, string Answer, string NextSurface);

public sealed partial class AccountNovaCoordinator
{
    /// <summary>Human-only content continuation. RAM surface ownership is required even when durable metadata exists.</summary>
    public LiveRecord Clarify(LibraryActor actor, Guid libraryId, Guid previousRunId, AccountLiveClarification reply, string surface)
    {
        if (reply is null || reply.RunId == Guid.Empty || reply.ConnectionId == Guid.Empty || reply.QuestionSequence < 0
            || string.IsNullOrWhiteSpace(reply.Answer) || reply.Answer.Length > AuthoringClarifications.MaximumAnswerLength
            || reply.NextSurface is not { Length: >= 32 and <= 128 } || reply.NextSurface.Any(char.IsWhiteSpace))
            throw new ValidationException("Escribe una aclaración de hasta 2000 caracteres para esta pregunta.");
        lock (admissions)
        {
            if (stopping) throw new ConflictException("El servicio se está deteniendo.");
            var previous = BoundSurface(actor, libraryId, previousRunId, surface);
            if (previous.Connection != reply.ConnectionId) throw new ConflictException("La pregunta pertenece a otra conexión.");
            var fingerprint = LiveRuns.Hash(reply);
            if (previous.Clarification is { } receipt)
            {
                if (receipt.RunId != reply.RunId || receipt.Fingerprint != fingerprint)
                    throw new ConflictException("Esta pregunta ya fue respondida. No se repetirá otra solicitud.");
                return Record(actor, libraryId, receipt.RunId);
            }
            var view = previous.Live.View(previousRunId);
            var control = previous.Live.Active(previousRunId);
            if (control.Cancellation.IsCancellationRequested) throw new ConflictException("La continuación de este encargo se detuvo.");
            var original = control.Input;
            var next = AuthoringClarifications.Continue(original, view.Run, view.Question ?? "", reply.RunId, reply.QuestionSequence, reply.Answer);
            // Start retains source/revision, account admission, connection revocation,
            // model selection and quota guards. It cannot inherit an approval.
            var started = Start(actor, libraryId, new(reply.ConnectionId, next), reply.NextSurface);
            previous.Clarification = new(reply.RunId, fingerprint);
            return started;
        }
    }
}
