using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using QuickPrompt.Accounts.Delegation;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>
/// A short-lived return to one already validated MCP authorization request, never
/// an actor, library selection, approval or general-purpose redirect capability.
/// Each login carries its own ticket through the framework-protected OIDC state.
/// </summary>
public static class McpLoginContinuation
{
    public const string FormField = "mcp_login";
    public const string Property = "quickprompt.mcp-login";
    public const string Purpose = "QuickPrompt.McpLoginContinuation.v1";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly string[] Fields = ["client_id", "redirect_uri", "response_type", "response_mode", "scope", "resource", "state", "code_challenge", "code_challenge_method"];
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 4 };

    /// <summary>Shared application constraints; OpenIddict still validates the request before consent and again on return.</summary>
    public static bool Eligible(OpenIddictRequest request, DelegationOrigin origin, McpAccountSettings settings) =>
        settings.Enabled && request.ClientId == McpDelegationPolicy.ClientId
        && request.RedirectUri == settings.RedirectUri!.AbsoluteUri && request.ResponseType == ResponseTypes.Code
        && request.ResponseMode is null or ResponseModes.Query or ResponseModes.FormPost or ResponseModes.Fragment
        && request.CodeChallengeMethod == CodeChallengeMethods.Sha256
        && request.CodeChallenge is { Length: >= 43 and <= 128 } challenge
        && challenge.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
        && request.State is { Length: > 0 and <= 1024 } && request.Prompt != "none"
        && request.GetResources().SequenceEqual([origin.McpResource]) && request.HasScope(McpDelegationPolicy.ReadScope)
        && request.GetScopes().All(scope => scope is McpDelegationPolicy.ReadScope or McpDelegationPolicy.ImportScope
            or McpDelegationPolicy.CreateScope or McpDelegationPolicy.CollectionsScope or Scopes.OfflineAccess);

    public static Dictionary<string, string?> Parameters(OpenIddictRequest request) => Fields
        .Where(key => request.GetParameter(key) is not null)
        .ToDictionary(key => key, key => key == "resource" ? request.GetResources().Single() : (string?)request.GetParameter(key), StringComparer.Ordinal);

    public static string Issue(IDataProtectionProvider provider, DelegationOrigin origin, McpAccountSettings settings, OpenIddictRequest request)
    {
        if (!Eligible(request, origin, settings)) throw new InvalidOperationException("MCP login requires a validated authorization request.");
        return Protector(provider, origin).Protect(JsonSerializer.Serialize(Parameters(request), Json), Lifetime);
    }

    /// <summary>Rejects expired/tampered tickets and rechecks the configured callback, origin and fixed policy.</summary>
    public static bool TryRestore(IDataProtectionProvider provider, DelegationOrigin origin, McpAccountSettings settings,
        string? ticket, out string path)
    {
        path = "";
        if (ticket is not { Length: > 0 and <= 16384 }) return false;
        try
        {
            using var document = JsonDocument.Parse(Protector(provider, origin).Unprotect(ticket), new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
            var request = new OpenIddictRequest();
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (!Fields.Contains(entry.Name) || entry.Value.ValueKind != JsonValueKind.String
                    || !parameters.TryAdd(entry.Name, entry.Value.GetString())) return false;
                request.SetParameter(entry.Name, entry.Value.GetString());
            }
            if (!Eligible(request, origin, settings)) return false;
            path = QueryHelpers.AddQueryString("/connect/authorize", parameters);
            return true;
        }
        catch (Exception error) when (error is CryptographicException or JsonException or FormatException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Only verified external authentication properties may supply the continuation after Google.</summary>
    public static bool TryComplete(HttpContext context, AuthenticationProperties? properties, out string path)
    {
        path = "/";
        if (properties?.Items.TryGetValue(Property, out var ticket) != true) return true;
        return TryRestore(context, ticket, out path);
    }

    public static bool TryRestore(HttpContext context, string? ticket, out string path)
    {
        path = "";
        var provider = context.RequestServices.GetService<IDataProtectionProvider>();
        var origin = context.RequestServices.GetService<DelegationOrigin>();
        var settings = context.RequestServices.GetService<McpAccountSettings>();
        return provider is not null && origin is not null && settings is not null
            && TryRestore(provider, origin, settings, ticket, out path);
    }

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider provider, DelegationOrigin origin) =>
        provider.CreateProtector(Purpose, origin.Value.AbsoluteUri).ToTimeLimitedDataProtector();
}
