using System.Text.Encodings.Web;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud.Authentication;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>First-party, cookie/CSRF-protected consent. No remembered or automatic approvals.</summary>
internal static class McpConsent
{
    private const string Server = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
    private static string Html(string value) => HtmlEncoder.Default.Encode(value);
    private const string PageStart = """
        <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>QuickPrompt · Conectar plugin</title><link rel="stylesheet" href="/assets/mcp-consent.css"></head>
        <body><main><p class="brand">QuickPrompt</p>
        """;
    private const string PageEnd = "</main></body></html>";

    internal static async Task<IResult> Handle(HttpContext context, DelegationOrigin origin, DelegationBoundary boundary,
        LibraryAccess libraries, IAntiforgery antiforgery, CancellationToken cancellation)
    {
        var request = context.GetOpenIddictServerRequest()!;
        if (!McpLoginContinuation.Eligible(request, origin, context.RequestServices.GetRequiredService<McpAccountSettings>()))
            return Reject(Errors.InvalidRequest);
        var cookie = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!cookie.Succeeded)
            return HttpMethods.IsGet(context.Request.Method) ? LoginPage(context, request, origin, antiforgery) : Results.Unauthorized();
        var actor = LibraryActor.FromPrincipal(cookie.Principal!);
        if (HttpMethods.IsPost(context.Request.Method))
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            var form = await context.Request.ReadFormAsync(cancellation);
            if (form["action"] == "deny") return Reject(Errors.AccessDenied);
            if (form["action"] != "approve" || !Guid.TryParse(form["library_id"], out var libraryId) || libraryId == Guid.Empty) return Reject(Errors.InvalidRequest);
            return Results.SignIn(await boundary.ApproveMcp(actor, libraryId, request.HasScope(Scopes.OfflineAccess), cancellation,
                request.HasScope(McpDelegationPolicy.ImportScope), request.HasScope(McpDelegationPolicy.CreateScope), request.HasScope(McpDelegationPolicy.CollectionsScope)), authenticationScheme: Server);
        }
        var mine = await libraries.ListLibraries(actor, cancellation);
        var tokens = antiforgery.GetAndStoreTokens(context);
        var inputs = string.Concat(McpLoginContinuation.Parameters(request).Select(item => $"<input type=hidden name=\"{item.Key}\" value=\"{Html(item.Value ?? "")}\">"));
        var choices = string.Concat(mine.Select(library => $"<option value=\"{library.Id}\">{Html(library.Name)}</option>"));
        var disabled = mine.Length == 0 ? " disabled" : "";
        var page = PageStart
            + "<h1>Conecta tu biblioteca</h1><div class=guide><strong>QuickPrompt MCP · Paso 2 de 2</strong><p>¿Qué biblioteca quieres usar con el plugin? Revisa los permisos y elige si quieres continuar.</p></div>"
            + "<h2>Qué podrá hacer el plugin</h2><ul><li>Buscar, leer y completar variables en la biblioteca que elijas.</li>"
            + (request.HasScope(McpDelegationPolicy.ImportScope)
                ? "<li>Leer prompts compartidos que ya aceptaste e importarlos como copias.</li>" : "")
            + (request.HasScope(McpDelegationPolicy.CreateScope)
                ? "<li>Guardar prompts nuevos en esa biblioteca.</li>" : "")
            + (request.HasScope(McpDelegationPolicy.CollectionsScope)
                ? "<li>Leer colecciones compartidas que ya aceptaste e importarlas como colecciones y prompts nuevos.</li>" : "")
            + "</ul><details><summary>Permisos excluidos</summary><ul>"
            + (request.HasScope(McpDelegationPolicy.ImportScope) ? "" : "<li>No permite importar prompts individuales.</li>")
            + (request.HasScope(McpDelegationPolicy.CreateScope) ? "" : "<li>No permite crear prompts libres.</li>")
            + (request.HasScope(McpDelegationPolicy.CollectionsScope) ? "" : "<li>No permite leer ni importar colecciones compartidas.</li>")
            + "<li>No permite aceptar invitaciones, editar o borrar prompts existentes, ejecutar IA ni leer memoria, valores guardados o resultados.</li></ul></details>"
            + "<p>El acceso vence como máximo en 30 días y puedes revocarlo en Conexiones."
            + (request.HasScope(Scopes.OfflineAccess) ? " Incluye renovación de sesión dentro de ese plazo." : "") + "</p>"
            + "<form method=post action=\"/connect/authorize\">" + inputs
            + $"<input type=hidden name=\"{Html(tokens.FormFieldName)}\" value=\"{Html(tokens.RequestToken!)}\">"
            + $"<label>Biblioteca <select required name=library_id{disabled}><option value=\"\">Elige una biblioteca</option>{choices}</select></label>"
            + (mine.Length == 0 ? "<p>Crea una biblioteca en QuickPrompt y vuelve a iniciar la conexión.</p>" : "")
            + $"<div class=actions><button class=primary name=action value=approve{disabled}>Aprobar permisos</button><button name=action value=deny formnovalidate>Denegar</button></div></form>" + PageEnd;
        return Results.Content(page, "text/html; charset=utf-8");
    }

    private static IResult LoginPage(HttpContext context, OpenIddictRequest request, DelegationOrigin origin, IAntiforgery antiforgery)
    {
        if (!context.RequestServices.GetRequiredService<GoogleAvailability>().Enabled)
            return Results.Content(PageStart + "<h1>Inicia sesión en QuickPrompt</h1><div class=guide><strong>QuickPrompt MCP · Paso 1 de 2</strong><p>Entra a tu cuenta y vuelve al plugin para conectar tu biblioteca.</p></div><a href=\"/\">Abrir QuickPrompt</a>" + PageEnd, "text/html; charset=utf-8", statusCode: 401);
        var ticket = McpLoginContinuation.Issue(context.RequestServices.GetRequiredService<IDataProtectionProvider>(), origin,
            context.RequestServices.GetRequiredService<McpAccountSettings>(), request);
        var tokens = antiforgery.GetAndStoreTokens(context);
        var page = PageStart
            + "<h1>Inicia sesión para conectar QuickPrompt</h1><div class=guide><strong>QuickPrompt MCP · Paso 1 de 2</strong><p>Entra a tu cuenta. Después podrás elegir una biblioteca y decidir qué acceso autorizas.</p></div>"
            + "<form method=post action=\"/account/google\">"
            + $"<input type=hidden name=\"{Html(tokens.FormFieldName)}\" value=\"{Html(tokens.RequestToken!)}\">"
            + $"<input type=hidden name=\"{McpLoginContinuation.FormField}\" value=\"{Html(ticket)}\">"
            + "<button class=primary>Continuar con Google</button></form>" + PageEnd;
        return Results.Content(page, "text/html; charset=utf-8");
    }

    private static IResult Reject(string error) => Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Se requiere aprobación explícita para una biblioteca y PKCE S256."
    }), [Server]);
}
