using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Nova.Tests;

public sealed class PreparedDraftLifecycleTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "prepared-lifecycle-" + Guid.NewGuid());
    private readonly LocalStore store;
    private readonly PromptLibrary library;
    private readonly PreparedDraftStore drafts;
    private readonly AgentRunStore runs;
    private readonly AgentSessions sessions = new();
    private readonly LiveRuns live;
    public PreparedDraftLifecycleTests()
    {
        store = new(Path.Combine(directory, "synthetic.sqlite"));
        var adapter = new NovaPromptStore(store); library = new(adapter, adapter);
        drafts = new(store); runs = new(store); live = new(new LiveRunStore(store), library);
    }
    private PreparedDraft Seed(PromptDocument? source = null) => new EditorDraftPreparation(library, new DeterministicPromptEvaluator(), drafts, sessions)
        .Prepare(new(source?.Id, source?.Revision, "Borrador explícito", "Revisa <tema> para <audiencia>.", new(), Guid.NewGuid()));
    private PreparedDraftCompletion Completion(IPreparedDraftStore? buffer = null) => new(buffer ?? drafts, runs, sessions, live);
    private static CompleteDraftRequest CompleteInput() => new(Guid.NewGuid(), new() { ["tema"] = "α\nβ <literal> 🚀", ["audiencia"] = "Equipo" });

    [Fact]
    public void PartialThenCompleteValuesProduceExactTextAndUnapprovedCheckpointsWithoutSavingAPrompt()
    {
        var source = Seed(); var service = Completion();
        var partial = service.Complete(source.Id, new(Guid.NewGuid(), new() { ["tema"] = "α\nβ <literal> 🚀" }));
        Assert.Equal(PreparationState.NEEDS_INFORMATION, partial.State); Assert.Null(partial.CompiledText);
        Assert.Equal(new[] { "audiencia" }, partial.MissingVariables); Assert.Equal(LiveStatus.BLOCKED, live.Get(partial.Id).Status);
        var result = service.Complete(partial.Id, new(Guid.NewGuid(), new() { ["audiencia"] = "Equipo" }));
        Assert.Equal("Revisa α\nβ <literal> 🚀 para Equipo.", result.CompiledText); Assert.Empty(result.MissingVariables);
        Assert.Equal(LiveStatus.COMPLETED, live.Get(result.Id).Status); Assert.Equal(AgentRunState.READY, runs.Get(result.Id)!.State);
        Assert.Equal("SAVE_REQUIRES_REVIEW", runs.Get(result.Id)!.Approval); Assert.Equal("NOT_GRANTED", sessions.Get(source.SessionId!.Value)!.Approval);
        Assert.Equal(new[] { "compile_prompt", "validate_prompt" }, runs.Get(result.Id)!.Tools.Select(tool => tool.Tool));
        Assert.Equal(0, library.Search(new(Size: 1)).Total);
    }

    [Fact]
    public void ReplayUsesOrdinalValueIdentityWithoutRepeatingSessionOrLiveEffects()
    {
        var source = Seed(); var input = CompleteInput(); var service = Completion(); var result = service.Complete(source.Id, input);
        var sequence = live.Get(result.Id).Sequence; var priorSession = sessions.Get(source.SessionId!.Value);
        var replay = service.Complete(source.Id, input with { Values = new() { ["audiencia"] = "Equipo", ["tema"] = "α\nβ <literal> 🚀" } });
        Assert.Same(result, replay); Assert.Equal(sequence, live.Get(result.Id).Sequence); Assert.Same(priorSession, sessions.Get(source.SessionId.Value));
        Assert.Throws<ConflictException>(() => service.Complete(source.Id, input with { Values = new() { ["tema"] = "Otro texto" } }));
        Assert.Equal(sequence, live.Get(result.Id).Sequence); Assert.Equal(result.CompiledText, drafts.Get(result.Id)!.CompiledText);
    }

    [Fact]
    public void RetainedReceiptWithoutContentIsConflictInsteadOfNullSuccessOrAnotherExecution()
    {
        var source = Seed(); var input = CompleteInput(); var service = Completion(); var result = service.Complete(source.Id, input);
        var sequence = live.Get(result.Id).Sequence; var fingerprint = drafts.Fingerprint(result.Id); var priorSession = sessions.Get(source.SessionId!.Value);
        drafts.Forget(result.Id);
        Assert.NotNull(fingerprint); Assert.Equal(fingerprint, drafts.Fingerprint(result.Id)); Assert.Null(drafts.Get(result.Id));
        Assert.Throws<ConflictException>(() => service.Complete(source.Id, input));
        Assert.Equal(sequence, live.Get(result.Id).Sequence); Assert.Same(priorSession, sessions.Get(source.SessionId.Value));
        Assert.Null(drafts.Get(result.Id)); Assert.Equal(0, library.Search(new(Size: 1)).Total);
    }

    [Theory]
    [InlineData("empty-operation")][InlineData("source-operation")][InlineData("null-values")]
    [InlineData("unknown-variable")][InlineData("blank-value")][InlineData("long-value")][InlineData("too-many-values")]
    public void InvalidCompletionNeverReservesOrPublishesEffects(string scenario)
    {
        var source = Seed(); var input = CompleteInput();
        input = scenario switch {
            "empty-operation" => input with { OperationId = Guid.Empty },
            "source-operation" => input with { OperationId = source.Id },
            "null-values" => input with { Values = null! },
            "unknown-variable" => input with { Values = new() { ["otro"] = "Dato" } },
            "blank-value" => input with { Values = new() { ["tema"] = " " } },
            "long-value" => input with { Values = new() { ["tema"] = new string('x', 8001) } },
            "too-many-values" => input with { Values = Enumerable.Range(0, 61).ToDictionary(i => "v" + i, _ => "Dato") },
            _ => throw new InvalidOperationException()
        };
        var previous = sessions.Get(source.SessionId!.Value);
        Assert.Throws<ValidationException>(() => Completion().Complete(source.Id, input));
        Assert.Null(runs.Get(input.OperationId)); Assert.Same(previous, sessions.Get(source.SessionId.Value));
        Assert.Equal(0, library.Search(new(Size: 1)).Total);
    }

    [Fact]
    public void MissingDraftIsRejectedAndFailedReservedWritesAreNotAutomaticallyReplayed()
    {
        Assert.Throws<MissingPromptException>(() => Completion().Complete(Guid.NewGuid(), CompleteInput()));
        var source = Seed(); var input = CompleteInput(); var previous = sessions.Get(source.SessionId!.Value);
        var failure = new InvalidOperationException("Synthetic preparation write failure"); var faulty = new DraftStoreProbe(drafts) { PutFailure = failure };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Completion(faulty).Complete(source.Id, input)));
        Assert.NotNull(runs.Get(input.OperationId)); Assert.Null(drafts.Get(input.OperationId)); Assert.Null(drafts.Fingerprint(input.OperationId));
        Assert.Same(previous, sessions.Get(source.SessionId.Value)); Assert.Throws<MissingPromptException>(() => live.Get(input.OperationId));
        Assert.Throws<ConflictException>(() => Completion().Complete(source.Id, input));
        Assert.Null(drafts.Get(input.OperationId)); Assert.Equal(0, library.Search(new(Size: 1)).Total);
    }

    [Fact]
    public void SavingNeedsConfirmationAndPreservesReceiptReplayAfterContentWasForgotten()
    {
        var source = Seed(); var result = Completion().Complete(source.Id, CompleteInput()); var probe = new DraftStoreProbe(drafts);
        var service = new PreparedDraftSaving(probe, runs);
        Assert.Throws<ValidationException>(() => service.Save(result.Id, new(false))); Assert.Equal(0, probe.Saves);
        var checkpoint = runs.Get(result.Id)!; runs.Put(checkpoint with { StageDurations = new() { ["Generation"] = 12.5 } });
        var saved = service.Save(result.Id, new(true));
        Assert.Equal(result.TemplateText, saved.TemplateText); Assert.Equal(1, library.Search(new(Size: 1)).Total);
        var audit = runs.Get(result.Id)!; Assert.Equal("APPROVED_COPY_OR_REUSE", audit.Approval);
        Assert.Equal(12.5, audit.StageDurations!["Generation"]); Assert.True(audit.StageDurations["Approval"] >= 0);
        drafts.Forget(result.Id);
        Assert.Equal(saved.Id, service.Save(result.Id, new(true)).Id); Assert.Equal(1, library.Search(new(Size: 1)).Total);
        Assert.Throws<ConflictException>(() => service.Save(result.Id, new(true, true)));
    }

    [Fact]
    public void SourceVersionSavingUsesExistingRevisionGuardAndDoesNotGrantAnAlternativeEffect()
    {
        var source = library.Create(new("Origen", "Explica <tema> a <audiencia>."));
        var prepared = Seed(source); var service = new PreparedDraftSaving(drafts, runs);
        var saved = service.Save(prepared.Id, new(true, true));
        Assert.Equal(source.Id, saved.Id); Assert.Equal(source.Revision + 1, saved.Revision); Assert.Equal(prepared.TemplateText, saved.TemplateText);
        Assert.Equal(saved.Revision, service.Save(prepared.Id, new(true, true)).Revision);
        Assert.Throws<ConflictException>(() => service.Save(prepared.Id, new(true, false)));
        var stale = Seed(saved); var current = library.Update(saved.Id, new(saved.Title, "Cambio humano posterior.", Revision: saved.Revision));
        Assert.Throws<ConflictException>(() => service.Save(stale.Id, new(true, true)));
        Assert.Equal(current.Revision, library.Get(source.Id).Revision); Assert.Equal(current.TemplateText, library.Get(source.Id).TemplateText);
    }

    [Fact]
    public void SavingFactoryDoesNotRequireVisualControlOrAnyModelCapability()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["QuickPrompt:Ollama"] = "http://127.0.0.1:1" }).Build();
        Assembly.Load("QuickPrompt.Api").GetType("QuickPrompt.Nova.Api.ApplicationModules", true)!
            .GetMethod("AddNovaAgentModule", BindingFlags.Public | BindingFlags.Static)!.Invoke(null,
                [services, config, Path.Combine(directory, "unused.sqlite")]);
        services.AddSingleton<IPreparedDraftStore>(drafts); services.AddSingleton<IAgentRunStore>(runs);
        services.AddSingleton<ILocalLlmClient>(_ => throw new InvalidOperationException("Unexpected model construction"));
        using var provider = services.BuildServiceProvider();
        var saving = provider.GetRequiredService<IPreparedDraftSaving>(); Assert.IsType<PreparedDraftSaving>(saving);
        Assert.Same(saving, provider.GetRequiredService<IPreparedDraftSaving>()); Assert.Null(provider.GetService<LiveRuns>());
        var saved = saving.Save(Seed().Id, new(true)); Assert.NotNull(library.Get(saved.Id));
        var missing = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IPreparedDraftCompletion>());
        Assert.Contains(nameof(LiveRuns), missing.Message);
        Assert.False(File.Exists(Path.Combine(directory, "unused.sqlite")));
    }

    private sealed class DraftStoreProbe(IPreparedDraftStore inner) : IPreparedDraftStore
    {
        public Exception? PutFailure; public int Saves;
        public PreparedDraft? Get(Guid id) => inner.Get(id);
        public string? Fingerprint(Guid id) => inner.Fingerprint(id);
        public void Put(PreparedDraft draft, string fingerprint) { if (PutFailure is { } error) throw error; inner.Put(draft, fingerprint); }
        public PreparedDraft[] Recent() => inner.Recent();
        public void Forget(Guid id) => inner.Forget(id);
        public PromptDocument Save(Guid id, bool updateSource = false) { Saves++; return inner.Save(id, updateSource); }
    }
    public void Dispose()
    {
        var target = Path.GetFullPath(directory); var parent = Path.GetDirectoryName(target);
        if (!string.Equals(parent, Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(target).StartsWith("prepared-lifecycle-", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid owned fixture cleanup path");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }
}
