using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

public sealed partial class AccountLiveRunStore
{
    /// <summary>One actor transaction owns source locks, prompt/version writes and approval consumption.</summary>
    public PromptDocument Commit(LiveRecord expected, LiveApproval proposal, IReadOnlyList<GoalTarget> sources, Func<PromptLibrary, PromptDocument> write) => Change((database, now) =>
    {
        var row = Owned(database, expected.RunId, now); var record = Decode(row);
        if (row.Terminal || row.Sequence != expected.Sequence || record.PauseRequested || record.Approval != "APPROVED"
            || row.ApprovalId != proposal.Id || row.ApprovalHash != proposal.Hash || row.DraftHash != LiveRuns.Hash(proposal.Draft)
            || row.CommittedPromptId is not null || sources.Count > 24)
            throw new ConflictException("Falta una aprobación vigente para este contenido y turno.");
        // Hold source revisions through the write, including copying from an unchanged source.
        foreach (var basis in sources.Concat(proposal.SourceBasis is { } sourceBasis ? [sourceBasis] : []).Distinct().OrderBy(x => x.Id))
        {
            var source = database.Prompts.FromSqlInterpolated($"SELECT * FROM qp_library.prompts WHERE \"LibraryId\"={libraryId} AND \"Id\"={basis.Id} FOR SHARE")
                .AsNoTracking().SingleOrDefault() ?? throw new MissingPromptException();
            if (source.Revision != basis.Revision) throw new LibraryRevisionException();
        }
        var adapter = new AccountPromptStore(memory, actor, libraryId, database);
        var saved = write(new PromptLibrary(adapter, adapter));
        if (proposal.PromptId is { } destination && saved.Id != destination
            || saved.Revision != (proposal.Revision ?? 0) + 1
            || saved.TemplateText != proposal.Draft.TemplateText || saved.Title != proposal.Draft.Title
            || saved.Description != proposal.Draft.Description || saved.CategoryId != proposal.Draft.CategoryId)
            throw new ValidationException("El resultado no coincide con la propuesta aprobada.");
        row.CommittedPromptId = saved.Id; row.CommittedRevision = saved.Revision;
        // Persist the receipt with the effect. A crash before the later UI event cannot authorize replay.
        row.Record = System.Text.Json.JsonSerializer.Serialize(record with { Approval = "CONSUMED", EffectCommitted = true, TargetId = saved.Id, Revision = saved.Revision, UpdatedAt = now.UtcDateTime });
        database.SaveChanges(); return saved;
    });
}
