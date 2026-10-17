using System.Text.Json;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Nova.Api;

public record EditorCompileInput(string TemplateText, Dictionary<string, string> Values);
public record EditorDraftInput(Guid? PromptId, int? Revision, string Title, string TemplateText, Dictionary<string, string> Values, Guid? SessionId = null);
public record UiObservedInput(Guid PreparedId, Guid? SourceId, int? SourceRevision, string RenderedText);
public static class LiveEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void MapLive(this RouteGroupBuilder api, string origin, int maxSteps)
    {
        api.MapPost("/nova/live/observe", (LiveStartInput input, LiveRuns live) =>
        { if (input.Mode != "LIVE") throw new ValidationException("Modo de observación inválido."); return live.Create(input); });
        api.MapPost("/nova/live", (LiveStartInput input, LiveRuns live, AgentOrchestrator engine, IUiDecisionModel model, IVisibleBrowserFactory browser, PromptLibrary library, SameTabUiDispatcher dispatcher, HttpContext context, IConfiguration configuration) =>
        {
            if (input.Mode is not ("SAME_TAB" or "BROWSER")) throw new ValidationException("Selecciona el control visual en esta pestaña.");
            if (input.Mode == "BROWSER" && !configuration.GetValue<bool>("QuickPrompt:Live:AllowQaBrowser")) throw new ValidationException("El navegador separado solo está disponible en QA explícito.");
            var capability = context.Request.Headers["X-Nova-Surface"].ToString();
            if (input.Mode == "SAME_TAB" && capability.Length is < 32 or > 128) throw new ValidationException("Falta la pestaña emisora.");
            var run = live.Create(input);
            if (input.Mode == "SAME_TAB") dispatcher.Bind(input.RunId, capability);
            if (live.TryLaunch(input.RunId)) _ = engine.RunVisible(input, origin, live, model, input.Mode == "SAME_TAB" ? dispatcher.Factory() : browser, library, maxSteps);
            return run;
        });
        api.MapGet("/nova/live/{id:guid}/actions", (Guid id, bool visible, HttpContext context, SameTabUiDispatcher dispatcher) => dispatcher.Poll(id, context.Request.Headers["X-Nova-Surface"].ToString(), visible));
        api.MapPost("/nova/live/{id:guid}/actions/progress", (Guid id, UiActionProgress progress, HttpContext context, SameTabUiDispatcher dispatcher) => { dispatcher.Progress(id, context.Request.Headers["X-Nova-Surface"].ToString(), progress); return Results.NoContent(); });
        api.MapPost("/nova/live/{id:guid}/actions/receipt", (Guid id, UiActionReceipt receipt, HttpContext context, SameTabUiDispatcher dispatcher) => { dispatcher.Acknowledge(id, context.Request.Headers["X-Nova-Surface"].ToString(), receipt); return Results.NoContent(); });
        api.MapGet("/nova/live/{id:guid}", (Guid id, LiveRuns live) => live.View(id));
        api.MapGet("/nova/live/{id:guid}/events", async (Guid id, HttpContext context, LiveRuns live) =>
        {
            var raw = context.Request.Headers["Last-Event-ID"].FirstOrDefault() ?? context.Request.Query["after"].FirstOrDefault() ?? "0";
            if (raw.Contains(':')) { var parts = raw.Split(':'); if (parts.Length != 2 || parts[0] != id.ToString()) throw new ValidationException("Cursor de eventos inválido."); raw = parts[1]; }
            if (!long.TryParse(raw, out var cursor) || cursor < 0 || cursor > live.Get(id).Sequence) throw new ValidationException("Cursor de eventos inválido.");
            context.Response.ContentType = "text/event-stream"; context.Response.Headers.CacheControl = "no-store";
            var ct = context.RequestAborted;
            await context.Response.StartAsync(ct);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var change = live.Changed(id); // Capture before reading to avoid a lost wake-up.
                    foreach (var item in live.Events(id, cursor))
                    {
                        await context.Response.WriteAsync($"id: {id}:{item.Sequence}\nevent: activity\ndata: {JsonSerializer.Serialize(item, Json)}\n\n", ct);
                        cursor = item.Sequence;
                    }
                    await context.Response.Body.FlushAsync(ct);
                    if (LiveRuns.Terminal(live.Get(id).Status)) break;
                    if (await Task.WhenAny(change, Task.Delay(TimeSpan.FromSeconds(15), ct)) != change)
                    { await context.Response.WriteAsync(": keep-alive\n\n", ct); await context.Response.Body.FlushAsync(ct); }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* Reconnecting observers never replay tools or cancel the independent browser run. */ }
        });
        api.MapPost("/nova/live/{id:guid}/pause", (Guid id, LiveRuns live) => { live.Pause(id); return live.Get(id); });
        api.MapPost("/nova/live/{id:guid}/resume", (Guid id, LiveRuns live) => { live.Resume(id); return live.Get(id); });
        api.MapPost("/nova/live/{id:guid}/cancel", (Guid id, LiveRuns live) => { live.Cancel(id); return live.Get(id); });
        api.MapPost("/nova/live/{id:guid}/approve", (Guid id, LiveApprovalInput input, LiveRuns live) => { live.Approve(id, input); return live.Get(id); });
        api.MapPost("/nova/live/{id:guid}/proposal", (Guid id, PromptInput input, LiveRuns live) => { live.RefreshProposal(id, input); return live.View(id); });
        api.MapPost("/nova/live/{id:guid}/ui-observed", (Guid id, UiObservedInput input, LiveRuns live, IPreparedDraftStore drafts) =>
        {
            var draft = drafts.Get(input.PreparedId) ?? throw new MissingPromptException();
            if (id != draft.Id || live.Get(id).Mode != "LIVE" || input.SourceId != draft.SourceId || input.SourceRevision != draft.SourceRevision || input.RenderedText != (draft.CompiledText ?? draft.TemplateText) && input.RenderedText != draft.TemplateText)
                throw new ValidationException("El resultado mostrado no corresponde al borrador preparado.");
            if (!live.Events(id, 0).Any(item => item.Stage == "UiReceipt")) live.Emit(id, LiveStatus.UI_UPDATED, LiveActor.SYSTEM_VALIDATION, "React presentó el resultado y confirmó su identidad y contenido.", "UiReceipt", target: draft.SourceId);
            return Results.NoContent();
        });
        api.MapPost("/nova/live/{id:guid}/commit", (Guid id, PromptInput input, LiveRuns live) => live.Commit(id, input));
        api.MapPost("/nova/live/{id:guid}/focus", async (Guid id, LiveRuns live, CancellationToken ct) =>
        { var browser = live.Active(id).Browser ?? throw new ConflictException("No hay un navegador abierto."); await browser.Focus(ct); return Results.NoContent(); });
        api.MapPost("/templates/compile", (EditorCompileInput input) =>
        {
            ValidateValues(input.TemplateText, input.Values); var names = TemplateCompiler.Names(input.TemplateText);
            var missing = names.Where(name => !input.Values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
            return new { text = missing.Length == 0 ? TemplateCompiler.Render(input.TemplateText, input.Values) : null, missingVariables = missing };
        });
        api.MapPost("/nova/editor-drafts", (EditorDraftInput input, PromptLibrary library, IPreparedDraftStore drafts, AgentSessions sessions) =>
        {
            ValidateValues(input.TemplateText, input.Values);
            if ((input.PromptId is null) != (input.Revision is null) || input.SessionId == Guid.Empty) throw new ValidationException("Identidad del editor inválida.");
            var source = input.PromptId is { } sourceId ? library.Get(sourceId) : null;
            if (source is not null && (source.Revision != input.Revision || source.IsArchived)) throw new ConflictException("El origen cambió.");
            var id = Guid.NewGuid(); var request = new PrepareInput(id, "Preparación explícita desde el editor.", SourcePromptId: source?.Id, SourceRevision: source?.Revision, Values: input.Values, SessionId: input.SessionId);
            var result = new DeterministicPromptEvaluator().Evaluate(request, new(source is null ? PreparationIntent.CREATE_PROMPT : PreparationIntent.ADAPT_PROMPT, "", ""),
                new(source is null ? PreparationDecision.CREATE_NEW : PreparationDecision.ADAPT_EXISTING, source?.Id, input.Title, source?.Description ?? "", input.TemplateText, [], "", "Borrador del editor; original conservado."),
                source is null ? [] : [new(source, 10, "Origen seleccionado")], input.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Select(v => new VariableValue(v.Key, v.Value, "USER_INPUT", "VALIDATED")).ToArray())
                with { SessionId = input.SessionId };
            drafts.Put(result, LiveRuns.Hash(request));
            if (input.SessionId is { } sessionId) {
                var previous = sessions.Get(sessionId);
                var same = previous?.Draft is { } prior && prior.SourceId == source?.Id && prior.SourceRevision == source?.Revision && prior.TemplateText == input.TemplateText;
                var searchContext = (previous?.Candidates ?? []).Select(candidate => source is not null && candidate.Id == source.Id ? source : candidate).Take(6).ToArray();
                sessions.Put(new(sessionId, source ?? previous?.Source, result, source is null ? previous?.Values ?? [] : input.Values, DateTime.UtcNow,
                    searchContext, same ? previous?.LastProposal : null, null, "NOT_GRANTED"));
            }
            return result;
        });
    }
    private static void ValidateValues(string template, Dictionary<string, string>? values)
    { if (template is null || template.Length > 32000 || values is null || values.Count > 60 || values.Any(v => v.Key.Length > 80 || v.Value is null || v.Value.Length > 8000 || !TemplateCompiler.Names(template).Contains(v.Key))) throw new ValidationException("Contenido o valores inválidos."); }
}
