using QuickPrompt.Local;

namespace QuickPrompt.Nova.Application;

/// <summary>A user's literal answer to a prior content question. It cannot select application actions or grant approval.</summary>
public sealed record AuthoringClarification(string Question, string Answer);

public static class AuthoringClarifications
{
    public const int MaximumCount = 4;
    public const int MaximumAnswerLength = 2000;
    public static AuthoringClarification[] Copy(IEnumerable<AuthoringClarification>? values)
    {
        var items = values?.ToArray() ?? [];
        if (items.Length > MaximumCount || items.Any(item => item is null || string.IsNullOrWhiteSpace(item.Question)
            || item.Question.Length > 500 || string.IsNullOrWhiteSpace(item.Answer) || item.Answer.Length > MaximumAnswerLength))
            throw new ValidationException("Las aclaraciones requieren una pregunta vigente y una respuesta de hasta 2000 caracteres; se admiten hasta cuatro.");
        return items;
    }

    /// <summary>The host supplies the original in-memory request and current question, never a replacement command from HTTP.</summary>
    public static LiveStartInput Continue(LiveStartInput original, LiveRecord blocked, string question,
        Guid nextRunId, long questionSequence, string answer)
    {
        ValidateContinuation(original, blocked, nextRunId, questionSequence);
        var replies = Copy([.. original.UserClarifications, new(question, answer)]);
        // A new answer changes the authoring basis. Neither task establishment
        // nor final content confirmation can survive that change as authority.
        return original with { RunId = nextRunId, UserClarifications = replies, ConfirmedBrief = null,
            AuthoringTaskDraft = original.ConfirmedBrief is { } prior ? AuthoringBriefs.Copy(prior.Content)
                : original.AuthoringTaskDraft is { } draft ? AuthoringBriefs.Copy(draft) : null,
            Values = original.Values?.ToDictionary(item => item.Key, item => item.Value),
            Command = original.Command! with { InputNames = original.Command.InputNames?.ToArray(), RequestedOutcomes = original.Command.RequestedOutcomes?.ToArray() } };
    }

    internal static void ValidateContinuation(LiveStartInput original, LiveRecord blocked, Guid nextRunId, long questionSequence)
    {
        if (nextRunId == Guid.Empty || nextRunId == original.RunId || blocked.RunId != original.RunId
            || blocked.SessionId != original.SessionId || blocked.Mode != "SAME_TAB" || original.Mode != "SAME_TAB"
            || blocked.Status != LiveStatus.BLOCKED || blocked.ErrorCode != "NEEDS_CLARIFICATION" || blocked.BrowserOpen
            || blocked.EffectCommitted || blocked.Approval != "NONE" || questionSequence != blocked.Sequence
            || original.Command?.Operation is not (SemanticOperation.CREATE_DRAFT or SemanticOperation.PROPOSE_ADAPTATION))
            throw new ConflictException("La pregunta ya no permite continuar este encargo. Revisa la actividad y el origen antes de solicitar otra acción.");
    }
}
