using System.Net;
using QuickPrompt.Web.Components;
using QuickPrompt.Web.Infrastructure;
using QuickPrompt.Nova.Application;

var builder=WebApplication.CreateBuilder(args);
if(builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
    throw new InvalidOperationException("Kestrel endpoint overrides are disabled. Use QuickPrompt:Port; this application only binds to loopback.");
var port=builder.Configuration.GetValue("QuickPrompt:Port",5188);
if(port is <1024 or >65535) throw new InvalidOperationException("Invalid local port.");
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Services.AddRazorComponents().AddInteractiveServerComponents(o=>o.DetailedErrors=false);
builder.Services.AddScoped<IWebPromptTransfer,WebPromptTransfer>();
builder.Services.AddSignalR(o=>o.MaximumReceiveMessageSize=6*1024*1024);
var path=builder.Configuration["QuickPrompt:DataPath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuickPromptWeb","quickprompt.sqlite");
var aiTimeout=builder.Configuration.GetValue("QuickPrompt:AiTimeoutSeconds",180);
if(aiTimeout is <5 or >180)throw new InvalidOperationException("AI timeout must be between 5 and 180 seconds.");
builder.Services.AddSingleton(new LocalPromptLibrary(path));
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Store);
builder.Services.AddSingleton<ILibraryArchiveReader>(sp=>sp.GetRequiredService<LocalPromptLibrary>().Archives);
builder.Services.AddSingleton<ILibraryArchiveWriter>(sp=>sp.GetRequiredService<LocalPromptLibrary>().Archives);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Sharing);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().LegacyConverter);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Templates);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().TemplateWriter);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Authoring);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Compiled);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Compilation);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Settings);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Proposals);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().CollectionReader);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().CollectionWriter);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().GroupAuthoring);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Memberships);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Instructions);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().InstructionWriter);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Combinations);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Assist);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().TransferHistory);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().TransferAudit);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Diagnostics);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().Panel);
builder.Services.AddSingleton(sp=>sp.GetRequiredService<LocalPromptLibrary>().StorageFailures);
builder.Services.AddSingleton(new AdaptiveCoreOptions(builder.Configuration["QuickPrompt:AdaptiveCore:Python"] ?? "",
    Path.GetFullPath(builder.Configuration["QuickPrompt:AdaptiveCore:Worker"] ?? "agents/nova-core/nova_worker.py"),
    Path.GetFullPath(builder.Configuration["QuickPrompt:AdaptiveCore:SdkRoot"] ?? "../Adaptive-AI-Core"),
    builder.Configuration["QuickPrompt:Ollama"] ?? "http://127.0.0.1:11434"));
builder.Services.AddSingleton<AdaptiveCoreModelTransport>();
builder.Services.AddSingleton<ILocalLlmClient>(sp=>OllamaClient.CreateOwned(builder.Configuration["QuickPrompt:Ollama"]??"http://127.0.0.1:11434",TimeSpan.FromSeconds(aiTimeout),
    modelTransport: sp.GetRequiredService<AdaptiveCoreModelTransport>()));
var app=builder.Build();
app.Use(async(ctx,next)=>
{
    var host=ctx.Request.Host.Host;
    if(ctx.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip) || host is not ("127.0.0.1" or "localhost")) { ctx.Response.StatusCode=403; return; }
    if(ctx.Request.Headers.Origin.FirstOrDefault() is { } origin && origin!=$"http://{ctx.Request.Host}") { ctx.Response.StatusCode=403; return; }
    ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
    ctx.Response.Headers["Referrer-Policy"]="no-referrer";
    ctx.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; connect-src 'self' ws://127.0.0.1:* ws://localhost:*; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
app.UseExceptionHandler("/Error");
app.UseStaticFiles(); app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
if(builder.Configuration.GetValue("QuickPrompt:Demo",false)) app.Services.GetRequiredService<LocalStore>().SeedDemo();
app.Run();
