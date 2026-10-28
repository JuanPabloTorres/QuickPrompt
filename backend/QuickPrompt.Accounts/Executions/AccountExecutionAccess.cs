using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Memory;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Accounts.Executions;

/// <summary>Authenticated caller boundary. No request can select its owner, lease, processor or endpoint.</summary>
public sealed class AccountExecutionAccess(AccountAgentMemory memory, ExecutionQueue queue, IExecutionConnectionAuthority? connections = null)
{
    public PromptRendering Preview(LibraryActor actor, Guid libraryId, Guid promptId, CompileInput input)
    {
        var prompt = memory.Catalog(actor, libraryId).Get(promptId);
        if (prompt.IsArchived) throw new LibraryUnavailableException();
        if (prompt.Revision != input.Revision) throw new LibraryRevisionException();
        var compiled = EditorTools.Compile(prompt.TemplateText, input.Values);
        return new(promptId, prompt.Revision, compiled.Text, compiled.MissingVariables);
    }

    public Task<ExecutionView> Compile(LibraryActor actor, Guid libraryId, Guid promptId, CompileSubmission input, CancellationToken cancellation = default)
        => Submit(actor, libraryId, promptId, input, null, cancellation);

    public Task<ExecutionView> RunLocal(LibraryActor actor, Guid libraryId, Guid promptId, LocalSubmission input, CancellationToken cancellation = default)
    {
        if (input.ConnectionId == Guid.Empty || connections is null) throw new ValidationException("Elige una conexión local autorizada.");
        return Submit(actor, libraryId, promptId, new(input.OperationId, input.Revision, input.Values), input.ConnectionId, cancellation);
    }

    private Task<ExecutionView> Submit(LibraryActor actor, Guid libraryId, Guid promptId, CompileSubmission input, Guid? connectionId, CancellationToken cancellation)
    {
        if (input.OperationId == Guid.Empty) throw new ValidationException("Identificador de operación requerido.");
        // Fingerprint only the user request: a retry must still find its receipt after the source changes.
        if (input.Values is null || input.Values.Count > 60 || input.Values.Any(x => x.Key.Length > 80 || x.Value is null || x.Value.Length > 8000))
            throw new ValidationException("Valores inválidos.");
        var request = JsonSerializer.Serialize(new { libraryId, promptId, input.Revision, Values = input.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray() }, CompileExecutionHandler.Json);
        // Preserve the existing compile receipt across this additive migration.
        if (connectionId is not null) request = JsonSerializer.Serialize(new { kind = "RunLocalPrompt", connectionId, request }, CompileExecutionHandler.Json);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
        // Existing receipts survive edits, but a deleted private source cannot be
        // reused as a new compile/run request through an old operation identity.
        var source = memory.Catalog(actor, libraryId).Get(promptId);
        var work = new CompileWork(source.Id, source.Revision, source.Title, source.TemplateText, new(input.Values));
        var payload = JsonSerializer.Serialize(work, CompileExecutionHandler.Json);
        if (Encoding.UTF8.GetByteCount(payload) > 512000) throw new ValidationException("La solicitud supera el límite permitido.");
        return queue.Admit(actor, libraryId, input.OperationId, hash, payload, cancellation, connectionId,
            connectionId is { } target ? token => connections!.Owned(actor, target, token) : null,
            (replay, token) =>
            {
                token.ThrowIfCancellationRequested();
                // Snapshot capture alone is insufficient: erasure can win before
                // admission. Re-read with the application owner under fence 5283.
                var current = memory.Catalog(actor, libraryId).Get(promptId);
                if (replay) return; // Existing receipts survive ordinary edits/archive, never deletion.
                if (current.IsArchived) throw new LibraryUnavailableException();
                if (current.Revision != input.Revision || current.Revision != work.Revision
                    || current.Id != work.PromptId || current.TemplateText != work.Template || current.Title != work.Title)
                    throw new LibraryRevisionException();
                if (EditorTools.Compile(work.Template, work.Values).Text is null)
                    throw new ValidationException("Completa las variables antes de continuar.");
            });
    }

    public ExecutionView[] List(LibraryActor actor, Guid libraryId) => memory.InScope(actor, libraryId, database =>
        database.Executions.AsNoTracking().Where(x => x.OwnerId == actor.Id && x.LibraryId == libraryId && x.Kind != "ModelProposal")
            // Fixed host limits keep active work below the page bound. Do not let newer
            // terminal receipts hide it and make an observer report a false completion.
            .OrderByDescending(x => x.Status == ExecutionStatus.Queued || x.Status == ExecutionStatus.Running)
            .ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(100)
            .Select(x => new ExecutionView(x.Id, x.LibraryId, x.Kind, x.Status, x.CancelRequested, x.CreatedAt, x.StartedAt, x.FinishedAt, x.ErrorCode, x.ForgottenAt)).ToArray());

    /// <summary>Erases terminal inputs/results, retaining admission identity and quota. Never cancels or repeats work.</summary>
    public ExecutionView Forget(LibraryActor actor, Guid libraryId, Guid id) => memory.InScope(actor, libraryId, database =>
    {
        var outcome = database.Database.SqlQuery<int>($"SELECT qp_operations.forget_execution_content({libraryId}, {id}) AS \"Value\"").Single();
        if (outcome == 0) throw new LibraryUnavailableException();
        if (outcome == 3) throw new ConflictException("Detén la ejecución y espera a que termine antes de olvidar su contenido.");
        return ExecutionQueue.View(database.Executions.AsNoTracking().Single(x => x.Id == id && x.OwnerId == actor.Id && x.LibraryId == libraryId));
    });

    public ExecutionView Read(LibraryActor actor, Guid libraryId, Guid id) => memory.InScope(actor, libraryId, database =>
        ExecutionQueue.View(database.Executions.AsNoTracking().SingleOrDefault(x => x.Id == id && x.LibraryId == libraryId && x.OwnerId == actor.Id)
            ?? throw new LibraryUnavailableException()));

    public ExecutionView Cancel(LibraryActor actor, Guid libraryId, Guid id) => memory.InScope(actor, libraryId, database =>
    {
        if (!database.Executions.Any(x => x.Id == id && x.LibraryId == libraryId && x.OwnerId == actor.Id)) throw new LibraryUnavailableException();
        database.Executions.Where(x => x.Id == id && x.LibraryId == libraryId && x.OwnerId == actor.Id
            && (x.Status == ExecutionStatus.Queued || x.Status == ExecutionStatus.Running)).ExecuteUpdate(set => set.SetProperty(x => x.CancelRequested, true));
        return ExecutionQueue.View(database.Executions.AsNoTracking().Single(x => x.Id == id));
    });

    public ExecutionResultView Result(LibraryActor actor, Guid libraryId, Guid id) => memory.InScope(actor, libraryId, database =>
    {
        // Do not serve expired content while a stopped/backlogged dispatcher catches up with erasure.
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var result = database.ExecutionResults.AsNoTracking().SingleOrDefault(x => x.ExecutionId == id && x.LibraryId == libraryId && x.OwnerId == actor.Id
            && database.Executions.Any(job => job.Id == x.ExecutionId && job.ForgottenAt == null && job.FinishedAt > cutoff))
            ?? throw new LibraryUnavailableException();
        var job = database.Executions.AsNoTracking().Single(x => x.Id == id && x.LibraryId == libraryId && x.OwnerId == actor.Id);
        return new ExecutionResultView(result.ExecutionId, result.PromptId, result.PromptRevision, result.Title, result.Text, result.CreatedAt,
            result.Response, result.Model, ExecutionUsage.Verify(job, result));
    });

    /// <summary>Trusted completion hook; read does not count usage. The caller records it only after a successful local finish.</summary>
    public CompletedExecutionUse? CompletedLocalUse(LibraryActor actor, Guid connection, Guid id) => memory.InScope(actor, null, database =>
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var job = database.Executions.AsNoTracking().SingleOrDefault(x => x.Id == id && x.OwnerId == actor.Id && x.ConnectionId == connection
            && x.Kind == "RunLocalPrompt" && x.Status == ExecutionStatus.Completed && x.ForgottenAt == null && x.FinishedAt > cutoff);
        if (job is null) return null;
        var result = database.ExecutionResults.AsNoTracking().SingleOrDefault(x => x.ExecutionId == id && x.OwnerId == actor.Id && x.LibraryId == job.LibraryId);
        if (result?.Response is null || result.Model != QuickPrompt.Accounts.Delegation.DelegationPolicy.Model) return null;
        var usage = ExecutionUsage.Verify(job, result);
        return usage is null ? null : new CompletedExecutionUse(job.LibraryId, result.PromptId,
            new(result.PromptRevision, usage.Values, usage.OperationId, result.Text));
    });
}
