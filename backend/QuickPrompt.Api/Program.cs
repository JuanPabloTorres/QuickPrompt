using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Api;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
    throw new InvalidOperationException("Use QuickPrompt:Port. Only loopback endpoints are allowed.");
var port = builder.Configuration.GetValue("QuickPrompt:Port", 5190);
if (port is < 1024 or > 65535) throw new InvalidOperationException("Invalid local port.");
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = LocalStore.MaxArchiveBytes + 1024);
// Request logs must not include private search terms or model inputs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.MaxDepth = 32);
var dataPath = builder.Configuration["QuickPrompt:DataPath"] ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "quickprompt.sqlite");
NovaDatabaseGuard.Validate(dataPath);
builder.Services.AddSingleton(new LocalStore(dataPath));
builder.Services.AddSingleton<NovaPromptStore>();
builder.Services.AddSingleton<IPromptReader>(sp => sp.GetRequiredService<NovaPromptStore>());
builder.Services.AddSingleton<IPromptWriter>(sp => sp.GetRequiredService<NovaPromptStore>());
builder.Services.AddSingleton<PromptLibrary>();
var aiTimeout = builder.Configuration.GetValue("QuickPrompt:AiTimeoutSeconds", 180);
if (aiTimeout is < 5 or > 180) throw new InvalidOperationException("AI timeout must be between 5 and 180 seconds.");
builder.Services.AddSingleton<ILocalLlmClient>(_ => new OllamaClient(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan },
    builder.Configuration["QuickPrompt:Ollama"] ?? "http://127.0.0.1:11434", TimeSpan.FromSeconds(aiTimeout)));
builder.Services.AddSingleton<INovaInference>(sp => new NovaInference(sp.GetRequiredService<ILocalLlmClient>(),
    builder.Configuration["QuickPrompt:Ollama"] ?? "http://127.0.0.1:11434", TimeSpan.FromSeconds(aiTimeout)));
builder.Services.AddSingleton<NovaAssistant>();
var preparationOptions = new PreparationOptions(builder.Configuration.GetValue("QuickPrompt:Preparation:MaxSteps", 12),
    builder.Configuration.GetValue("QuickPrompt:Preparation:StageTimeoutSeconds", 90), builder.Configuration.GetValue("QuickPrompt:Preparation:TotalTimeoutSeconds", 180));
if (preparationOptions.MaxSteps is < 6 or > 16 || preparationOptions.StageTimeoutSeconds is < 5 or > 180 || preparationOptions.TotalTimeoutSeconds is < 10 or > 300)
    throw new InvalidOperationException("Invalid preparation budget.");
builder.Services.AddSingleton(preparationOptions);
builder.Services.AddSingleton<PreparationInference>(_ => new(builder.Configuration["QuickPrompt:Ollama"] ?? "http://127.0.0.1:11434"));
builder.Services.AddSingleton<IIntentInterpreter>(sp => sp.GetRequiredService<PreparationInference>());
builder.Services.AddSingleton<IPromptGenerator>(sp => sp.GetRequiredService<PreparationInference>());
builder.Services.AddSingleton<IPromptRetriever, SqlitePromptRetriever>();
builder.Services.AddSingleton<LocalCommands>();
builder.Services.AddSingleton<IVariableResolver, GroundedVariableResolver>();
builder.Services.AddSingleton<IPromptEvaluator, DeterministicPromptEvaluator>();
builder.Services.AddSingleton<IPreparedDraftStore, PreparedDraftStore>();
builder.Services.AddSingleton<AgentOrchestrator>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddConcurrencyLimiter("ai", limiter => { limiter.PermitLimit = 1; limiter.QueueLimit = 0; });
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    var host = context.Request.Host;
    if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
        || host.Host is not ("127.0.0.1" or "localhost") || host.Port != port)
    { context.Response.StatusCode = 403; return; }
    if (context.Request.Headers.Origin.FirstOrDefault() is { } origin && origin != $"http://{host}")
    { context.Response.StatusCode = 403; return; }
    if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site")
    { context.Response.StatusCode = 403; return; }
    if (context.Request.Method is not ("GET" or "HEAD") && context.Request.Headers["X-QuickPrompt-Client"] != "nova-v1")
    { context.Response.StatusCode = 403; return; }
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; style-src-attr 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (Exception error) when (!context.Response.HasStarted && error is ValidationException or ConflictException or MissingPromptException or BadHttpRequestException or JsonException or LlmUnavailableException)
    {
        var status = error switch { ConflictException => 409, MissingPromptException => 404, LlmUnavailableException => 503, BadHttpRequestException bad => bad.StatusCode, _ => 400 };
        await Results.Problem(statusCode: status, title: status == 409 ? "Conflicto de revisión" : "Solicitud no válida",
            detail: error is JsonException or BadHttpRequestException ? "Revisa el formato de la solicitud." : error.Message,
            extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
    catch (Exception error) when (!context.Response.HasStarted && error is not OperationCanceledException)
    {
        app.Logger.LogError("Operation failed. Correlation {CorrelationId}, type {ErrorType}", context.TraceIdentifier, error.GetType().Name);
        await Results.Problem(statusCode: 500, title: "No se pudo completar la operación.",
            extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
});
app.UseRateLimiter();
app.MapGet("/health", (LocalStore store) => Results.Ok(new { status = store.CheckIntegrity() == "ok" ? "ok" : "error", schemaVersion = LocalStore.CurrentSchemaVersion }));
var api = app.MapGroup("/api/v1");
api.MapGet("/openapi.json", () => Results.Stream(typeof(NovaEndpoints).Assembly.GetManifestResourceStream("nova-openapi.json")!, "application/json"));
api.MapNova();
api.MapPreparation();
api.MapPost("/nova/command", (LocalCommandInput input, LocalCommands commands) => commands.Resolve(input));
api.MapGet("/providers", () => ProviderRegistry.Providers);
api.MapPost("/transfers/prepare", (TransferInput input, PromptLibrary library) =>
{
    if (!input.Confirmed) throw new ValidationException("Confirma el destino antes de preparar el texto.");
    var provider = ProviderRegistry.Providers.SingleOrDefault(p => p.ProviderId == input.ProviderId && p.ModesSupported.Contains("COPY_AND_OPEN"))
        ?? throw new ValidationException("Destino no permitido para copiar y abrir.");
    var compiled = library.Compile(input.PromptId, new(input.Revision, input.Values));
    return new { compiled.Text, compiled.TemplateRevision, provider.DestinationUrl, provider.DisplayName, mode = "COPY_AND_OPEN" };
});
api.MapGet("/prompts", (HttpRequest request, PromptLibrary library) =>
{
    var q = request.Query;
    bool Flag(string key) => q[key] == "true";
    int Number(string key, int fallback) => q[key].Count == 0 ? fallback : int.TryParse(q[key], out var n) ? n : throw new ValidationException("Paginación inválida.");
    Guid? group = q["groupId"].Count == 0 ? null : Guid.TryParse(q["groupId"], out var id) ? id : throw new ValidationException("Grupo inválido.");
    return library.Search(new(q["search"].FirstOrDefault() ?? "", Favorites: Flag("favorites"),
        Active: !Flag("archived"), GroupId: group, Sort: q["sort"].FirstOrDefault() ?? "updated",
        Page: Number("page", 0), Size: Number("size", 24), Recent: Flag("recent")));
});
api.MapGet("/prompts/{id:guid}", (Guid id, PromptLibrary library) => library.Get(id));
api.MapPost("/prompts", (PromptInput input, PromptLibrary library) =>
{
    var prompt = library.Create(input); return Results.Created($"/api/v1/prompts/{prompt.Id}", prompt);
});
api.MapPut("/prompts/{id:guid}", (Guid id, PromptInput input, PromptLibrary library) => library.Update(id, input));
api.MapPost("/prompts/{id:guid}/favorite", (Guid id, RevisionInput input, PromptLibrary library) => library.Favorite(id, input.Revision));
api.MapPost("/prompts/{id:guid}/archive", (Guid id, RevisionInput input, PromptLibrary library) => library.Archive(id, input.Revision, true));
api.MapPost("/prompts/{id:guid}/restore", (Guid id, RevisionInput input, PromptLibrary library) => library.Archive(id, input.Revision, false));
api.MapPost("/prompts/{id:guid}/duplicate", (Guid id, PromptLibrary library) => library.Duplicate(id));
api.MapDelete("/prompts/{id:guid}", (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] ConfirmInput input, PromptLibrary library) => { library.Delete(id, input); return Results.NoContent(); });
api.MapPost("/prompts/{id:guid}/compile", (Guid id, CompileInput input, PromptLibrary library) => library.Compile(id, input));
api.MapPost("/prompts/{id:guid}/usage", (Guid id, UseInput input, PromptLibrary library) => { library.RecordUse(id, input); return Results.NoContent(); });
api.MapPost("/templates/inspect", (InspectInput input) => new { variableNames = TemplateCompiler.Names(input.TemplateText) });
api.MapPost("/templates/toggle-variable", (ToggleVariableInput input) => EditorTools.Toggle(input));
api.MapGet("/groups", (LocalStore store) => store.Groups());
api.MapPost("/groups", (GroupInput input, LocalStore store) => store.SaveGroup(new() { Name = input.Name }));
api.MapPut("/groups/{id:guid}", (Guid id, GroupInput input, LocalStore store) => store.NovaRenameGroup(id, input));
api.MapDelete("/groups/{id:guid}", (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] ConfirmInput input, LocalStore store) => { store.NovaDeleteGroup(id, input); return Results.NoContent(); });
api.MapGet("/prompts/{id:guid}/groups", (Guid id, PromptLibrary library, LocalStore store) => { library.Get(id); return store.GroupsFor("template", id); });
api.MapPost("/prompts/{id:guid}/groups", (Guid id, MembershipInput input, LocalStore store) => { store.NovaAssign(id, input); return Results.NoContent(); });
api.MapGet("/prompts/{id:guid}/versions", (Guid id, PromptLibrary library, LocalStore store) => { library.Get(id); return store.Versions(id); });
api.MapGet("/library/export", (LocalStore store) => Results.Text(store.Export(), "application/json"));
api.MapPost("/library/import/preview", (JsonElement input, LocalStore store) => store.PreviewImport(input.GetRawText()));
api.MapPost("/library/import", (LibraryImportInput input, LocalStore store) =>
{
    if (!input.Confirmed) throw new ValidationException("Revisa y confirma la importación.");
    return new { imported = store.Import(input.Data.GetRawText()) };
});
api.MapPost("/library/restore/preview", (JsonElement input, LocalStore store) => new { preview = store.PreviewImport(input.GetRawText(), true), expectedSnapshot = store.NovaSnapshot() });
api.MapPost("/library/restore", (LibraryImportInput input, LocalStore store) => new { imported = store.NovaRestore(input) });
api.MapPost("/library/backup", (LocalStore store) => new { fileName = store.NovaBackup(), integrity = "ok" });
api.MapPost("/library/legacy/convert", async (HttpRequest request) =>
{
    if (request.ContentLength is > 4000000) throw new ValidationException("El archivo SQLite supera 4 MB.");
    return Results.Text(await LegacyImporter.Convert(request.Body), "application/json");
});
var webRoot = Path.GetFullPath(builder.Configuration["QuickPrompt:WebRoot"] ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"));
if (Directory.Exists(webRoot))
{
    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".vtt"] = "text/vtt";
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot), ContentTypeProvider = contentTypes });
    app.MapGet("/", () => Results.File(Path.Combine(webRoot, "index.html"), "text/html"));
}
else app.MapGet("/", () => Results.Problem(statusCode: 503, title: "Compila apps/web y configura QuickPrompt:WebRoot."));
if (builder.Configuration.GetValue("QuickPrompt:Demo", false)) app.Services.GetRequiredService<LocalStore>().SeedDemo();
app.Run();
