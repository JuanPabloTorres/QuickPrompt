using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Memory;

public sealed record RememberEditorContext(Guid SessionId, int ExpectedDraftRevision, Guid? ExpectedDraftId,
    bool Confirmed, Guid? PromptId, int? Revision, string Title, string TemplateText, Dictionary<string, string> Values, int ExpectedMemoryRevision = 0);
public sealed record SavedEditorContext(Guid Id, Guid DraftId, int DraftRevision, Guid? SourceId, int? SourceRevision,
    string Title, string TemplateText, Dictionary<string, string> Values, string[] VariableNames, string? CompiledText, bool SourceStale, int MemoryRevision);
public sealed record InspectAccountEditor(Guid? PromptId, int? Revision, string TemplateText, Dictionary<string, string> Values);
public sealed record AccountEditorInspection(string[] VariableNames, string? CompiledText, string[] MissingVariables);

/// <summary>Explicit remembered editor context. Uses the core compiler and session transition without granting approval.</summary>
public sealed class AccountEditorMemory(AccountAgentMemory memory)
{
    /// <summary>Read-only editor inspection. Never creates remembered context, approval or inference work.</summary>
    public AccountEditorInspection Inspect(LibraryActor actor, Guid libraryId, InspectAccountEditor input) =>
        memory.InScope(actor, libraryId, database =>
        {
            if ((input.PromptId is null) != (input.Revision is null) || input.Revision is <= 0)
                throw new ValidationException("El origen y su revisión deben corresponder.");
            if (input.PromptId is { } id)
            {
                var source = database.Prompts.AsNoTracking().SingleOrDefault(prompt => prompt.Id == id && prompt.LibraryId == libraryId && !prompt.IsArchived)
                    ?? throw new LibraryUnavailableException();
                if (source.Revision != input.Revision) throw new LibraryRevisionException();
            }
            var compiled = EditorTools.Compile(input.TemplateText, input.Values);
            return new AccountEditorInspection(TemplateCompiler.Names(input.TemplateText), compiled.Text, compiled.MissingVariables);
        });

    public SavedEditorContext Save(LibraryActor actor, Guid libraryId, RememberEditorContext input)
    {
        if (!input.Confirmed || input.SessionId == Guid.Empty || input.ExpectedDraftRevision < 0 || input.ExpectedMemoryRevision < 0
            || (input.ExpectedDraftRevision > 0) != (input.ExpectedDraftId is not null))
            throw new ValidationException("Confirma el contexto y su revisión antes de conservarlo.");
        var catalog = memory.Catalog(actor, libraryId);
        var draft = EditorDraftPreparation.Compile(new(input.PromptId, input.Revision, input.Title, input.TemplateText, input.Values, input.SessionId),
            catalog, new DeterministicPromptEvaluator());
        var source = draft.Candidates.SingleOrDefault(x => x.Prompt.Id == draft.SourceId)?.Prompt;
        var session = memory.RememberedSessions(actor, libraryId, input.ExpectedMemoryRevision).ObserveDraft(new(input.SessionId, source,
            draft with { Remembered = true }, new(input.Values), DateTime.UtcNow), expectedDraftRevision: input.ExpectedDraftRevision,
            expectedDraftId: input.ExpectedDraftId);
        return Project(session, sourceStale: false);
    }

    public SavedEditorContext Read(LibraryActor actor, Guid libraryId, Guid id)
    {
        var session = memory.RememberedSessions(actor, libraryId).Get(id) ?? throw new MissingPromptException();
        var stale = false;
        if (session.Draft?.SourceId is { } sourceId)
        {
            try { stale = memory.Catalog(actor, libraryId).Get(sourceId).Revision != session.Draft.SourceRevision; }
            catch (MissingPromptException) { stale = true; }
        }
        return Project(session, stale);
    }

    private static SavedEditorContext Project(AgentSession session, bool sourceStale)
    {
        var draft = session.Draft ?? throw new MissingPromptException();
        return new(session.Id, draft.Id, session.DraftRevision, draft.SourceId, draft.SourceRevision,
            draft.Title, draft.TemplateText, new(session.Values), draft.Variables.Select(x => x.Name).ToArray(), draft.CompiledText, sourceStale, session.StorageRevision);
    }
}
