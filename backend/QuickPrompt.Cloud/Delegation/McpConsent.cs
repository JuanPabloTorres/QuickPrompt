using System.Text.Encodings.Web;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Accounts.Libraries;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>First-party, cookie/CSRF-protected consent. No remembered or automatic approvals.</summary>
internal static class McpConsent
{
    private const string Server = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
    private static string Html(string value) => HtmlEncoder.Default.Encode(value);
    private static readonly string[] Fields = ["client_id", "redirect_uri", "response_type", "scope", "resource", "state", "code_challenge", "code_challenge_method"];

    internal static async Task<IResult> Handle(HttpContext context, DelegationOrigin origin, DelegationBoundary boundary,
        LibraryAccess libraries, IAntiforgery antiforgery, CancellationToken cancellation)
    {
        var request = context.GetOpenIddictServerRequest()!;
        if (request.ClientId != McpDelegationPolicy.ClientId || request.ResponseType != ResponseTypes.Code
            || request.CodeChallengeMethod != CodeChallengeMethods.Sha256 || string.IsNullOrWhiteSpace(request.CodeChallenge)
            || request.State is not { Length: > 0 and <= 1024 } || request.Prompt == "none"
            || !request.GetResources().SequenceEqual([origin.McpResource]) || !request.HasScope(McpDelegationPolicy.ReadScope)
            || request.GetScopes().Any(x => x != McpDelegationPolicy.ReadScope && x != McpDelegationPolicy.ImportScope && x != McpDelegationPolicy.CreateScope && x != McpDelegationPolicy.CollectionsScope && x != Scopes.OfflineAccess))
            return Reject(Errors.InvalidRequest);
        var cookie = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!cookie.Succeeded) return Results.Content("<!doctype html><html lang=es><title>QuickPrompt · Iniciar sesión</title><main><h1>Inicia sesión en QuickPrompt</h1><p>Abre QuickPrompt, inicia sesión y vuelve a esta página para revisar el acceso.</p><a href=\"/\">Abrir QuickPrompt</a></main></html>", "text/html; charset=utf-8", statusCode: 401);
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
        var inputs = string.Concat(Fields.Select(key => $"<input type=hidden name=\"{key}\" value=\"{Html((string?)request.GetParameter(key) ?? "")}\">"));
        var choices = string.Concat(mine.Select(library => $"<option value=\"{library.Id}\">{Html(library.Name)}</option>"));
        var disabled = mine.Length == 0 ? " disabled" : "";
        var page = "<!doctype html><html lang=es><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><title>QuickPrompt · Autorizar MCP</title>"
            + "<main><h1>Conectar MCP con QuickPrompt</h1><p>Cliente: QuickPrompt MCP. Permite buscar, leer y completar variables de una biblioteca elegida.</p>"
            + (request.HasScope(McpDelegationPolicy.ImportScope)
                ? "<p><strong>También autoriza leer prompts individuales compartidos que ya aceptaste en tu cuenta e importarlos como copias en la biblioteca elegida.</strong> No permite aceptar invitaciones ni cambiar el original.</p>"
                : "<p>No permite importar prompts individuales compartidos.</p>")
            + (request.HasScope(McpDelegationPolicy.CreateScope)
                ? "<p><strong>También autoriza guardar prompts NUEVOS en la biblioteca elegida.</strong> No permite modificar prompts existentes.</p>"
                : "<p>No permite crear prompts libres a partir de contenido nuevo.</p>")
            + (request.HasScope(McpDelegationPolicy.CollectionsScope)
                ? "<p><strong>También autoriza leer COLECCIONES compartidas que ya aceptaste e importar el conjunto como una colección y prompts NUEVOS en la biblioteca elegida.</strong> No acepta invitaciones ni modifica originales o copias existentes.</p>"
                : "<p>No permite leer ni importar colecciones compartidas.</p>")
            + "<p>No permite borrar, ejecutar IA ni leer memoria, valores guardados o resultados.</p>"
            + "<p>El acceso vence como máximo en 30 días y puedes revocarlo en Conexiones."
            + (request.HasScope(Scopes.OfflineAccess) ? " Incluye renovación de sesión dentro de ese plazo." : "") + "</p>"
            + "<form method=post action=\"/connect/authorize\">" + inputs
            + $"<input type=hidden name=\"{Html(tokens.FormFieldName)}\" value=\"{Html(tokens.RequestToken!)}\">"
            + $"<label>Biblioteca <select required name=library_id{disabled}><option value=\"\">Elige una biblioteca</option>{choices}</select></label>"
            + $"<button name=action value=approve{disabled}>Aprobar lectura{(request.HasScope(McpDelegationPolicy.ImportScope) ? ", importación de prompts" : "")}{(request.HasScope(McpDelegationPolicy.CreateScope) ? ", creación" : "")}{(request.HasScope(McpDelegationPolicy.CollectionsScope) ? " y colecciones" : "")} de esta biblioteca</button><button name=action value=deny formnovalidate>Denegar</button></form></main></html>";
        return Results.Content(page, "text/html; charset=utf-8");
    }

    private static IResult Reject(string error) => Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Se requiere aprobación explícita para una biblioteca y PKCE S256."
    }), [Server]);
}
