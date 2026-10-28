using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Api;

namespace QuickPrompt.Nova.Api;

// Trusted composition only; request data cannot select or replace services.
public static class QuickPromptHost
{
    public static void Run(string[] args, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        // Discover only trusted declarations and reject invalid catalogs before serving requests.
        _ = UiToolCatalog.All;
        _ = SemanticCapabilityCatalog.All;
        if (builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
            throw new InvalidOperationException("Use QuickPrompt:Port. Only loopback endpoints are allowed.");
        var port = builder.Configuration.GetValue("QuickPrompt:Port", 5190);
        if (port is < 1024 or > 65535) throw new InvalidOperationException("Invalid local port.");
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = LocalStore.MaxArchiveBytes + 1024);
        // Request logs must not include private search terms or model inputs.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.MaxDepth = 32);
        var labRequested=builder.Configuration.GetValue("QuickPrompt:Diagnostics:Lab:Enabled",false);
        if(labRequested && !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Lab observation requires Development; it is disabled in Production.");
        var dataPath = builder.Configuration["QuickPrompt:DataPath"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "quickprompt.sqlite");
        NovaDatabaseGuard.Validate(dataPath);
        // Local DEBUG opt-in, never an HTTP feature or a normal product log.
        // Existing QA helpers capture the bounded metadata stream outside the action path.
        var captureDebugPoints = builder.Configuration.GetValue("QuickPrompt:Diagnostics:DebugPoints", false);
#if DEBUG
        var qaRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".playwright")) + Path.DirectorySeparatorChar;
        if (captureDebugPoints && !Path.GetFullPath(dataPath).StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Debug point capture requires an owned .playwright QA database.");
        using var debugPoints = captureDebugPoints ? NovaTelemetry.CaptureDebugPoints(observe: point =>
            Console.WriteLine("NOVA_DEBUG_POINT " + JsonSerializer.Serialize(point))) : null;
        if (captureDebugPoints) {
            Console.WriteLine("NOVA_DEBUG_CAPTURE enabled");
            NovaTelemetry.DebugPoint("capture.started", false, () => new() { ["limit"] = debugPoints!.Limit });
            Console.WriteLine($"NOVA_DEBUG_CAPTURE startup points={debugPoints!.Snapshot().Length} observerErrors={debugPoints.ObserverErrors} listeners={NovaTelemetry.Activities.HasListeners()}");
        }
#else
        if (captureDebugPoints) throw new InvalidOperationException("Debug points require a DEBUG build.");
#endif
        builder.Services.AddPromptLibraryModule(dataPath)
            .AddNovaAgentModule(builder.Configuration, dataPath)
            .AddVisibleExecutionModule(builder.Configuration);
        if(DevelopmentLabObserver.Allowed(builder.Environment.IsDevelopment(),labRequested)) {
            var labDirectory=builder.Configuration["QuickPrompt:Diagnostics:Lab:Directory"] ?? throw new InvalidOperationException("Configure the local Lab directory.");
            var captureSynthetic=builder.Configuration.GetValue("QuickPrompt:Diagnostics:Lab:CaptureSyntheticContent",false);
            var labQaRoot=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),".playwright"))+Path.DirectorySeparatorChar;
            if(captureSynthetic && !Path.GetFullPath(dataPath).StartsWith(labQaRoot,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Synthetic Lab content requires an owned .playwright QA database.");
            builder.Services.AddSingleton(_=>new DevelopmentLabObserver(true,labDirectory,Directory.GetCurrentDirectory(),captureSynthetic));
            builder.Services.AddSingleton<ILiveRunStore>(sp=>new ObservedLiveRunStore(new LiveRunStore(sp.GetRequiredService<LocalStore>()),sp.GetRequiredService<DevelopmentLabObserver>()));
        }
        var visibleSteps = builder.Configuration.GetValue("QuickPrompt:Live:MaxSteps", 12);
        if (visibleSteps is < 3 or > 16) throw new InvalidOperationException("Visible UI budget must be between 3 and 16 decisions.");
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddConcurrencyLimiter("ai", limiter => { limiter.PermitLimit = 1; limiter.QueueLimit = 0; });
        });
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        if(labRequested) _=app.Services.GetRequiredService<DevelopmentLabObserver>();
#if DEBUG
        if (debugPoints is not null) app.Lifetime.ApplicationStopping.Register(() =>
            Console.WriteLine("NOVA_DEBUG_CAPTURE " + JsonSerializer.Serialize(new { points = debugPoints.Snapshot().Length, debugPoints.Dropped, debugPoints.ObserverErrors })));
#endif
        app.Use(async (context, next) =>
        {
            if (LocalAccessDenial(context, port) is { } denial)
            {
                // Fixed reason only: never log URLs, queries, origins, prompt bodies or credentials.
                app.Logger.LogWarning("Local request rejected: {Reason}", denial);
                context.Response.StatusCode = 403;
                context.Response.Headers["X-QuickPrompt-Rejection"] = denial;
                return;
            }
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; style-src-attr 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
            try { await next(); }
            catch (Exception error) when (!context.Response.HasStarted && error is ValidationException or ConflictException or MissingPromptException or BadHttpRequestException or JsonException or LlmUnavailableException or PreparationToolException)
            {
                var status = error switch { ConflictException => 409, MissingPromptException => 404, LlmUnavailableException => 503, PreparationToolException { Code: "MODEL_UNAVAILABLE" or "TIMEOUT" } => 503, BadHttpRequestException bad => bad.StatusCode, _ => 400 };
                await Results.Problem(statusCode: status, title: status == 409 ? "Conflicto de revisión" : "Solicitud no válida",
                    detail: error is JsonException or BadHttpRequestException ? "Revisa el formato de la solicitud." : error.Message,
                    extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
            }
            catch (OperationCanceledException) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
            { await Results.Problem(statusCode: 503, title: "Tiempo de espera agotado", detail: "El modelo no completó la operación dentro del límite. No se aplicaron cambios pendientes.").ExecuteAsync(context); }
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
        api.MapLive($"http://127.0.0.1:{port}", visibleSteps);
        api.MapLibrary();
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

    }

    private static string? LocalAccessDenial(HttpContext context, int port)
    {
        var host = context.Request.Host;
        if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)) return "LOOPBACK_REQUIRED";
        if (host.Host is not ("127.0.0.1" or "localhost") || host.Port != port) return "HOST_MISMATCH";
        if (context.Request.Headers.Origin.FirstOrDefault() is { } origin && origin != $"http://{host}") return "ORIGIN_MISMATCH";
        // Opening the static SPA from a link is safe; its subsequent API reads remain same-origin.
        // Never apply this exception to API paths, fetches, frames, assets or writes.
        var publicEntryNavigation = context.Request.Method == "GET" && context.Request.Path == "/"
            && context.Request.Headers["Sec-Fetch-Mode"] == "navigate"
            && context.Request.Headers["Sec-Fetch-Dest"] == "document";
        if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site" && !publicEntryNavigation) return "CROSS_SITE_REQUEST";
        if (context.Request.Method is not ("GET" or "HEAD") && context.Request.Headers["X-QuickPrompt-Client"] != "nova-v1") return "CLIENT_MARKER_REQUIRED";
        return null;
    }
}
