namespace QuickPrompt.Cloud.Delegation;

/// <summary>Operator-registered callback only; no dynamic registration, client-selected redirect or public exposure.</summary>
public sealed record McpAccountSettings(Uri? RedirectUri)
{
    public bool Enabled => RedirectUri is not null;
    public static McpAccountSettings Read(IConfiguration configuration)
    {
        var value = configuration["QuickPrompt:Mcp:RedirectUri"];
        if (string.IsNullOrWhiteSpace(value)) return new((Uri?)null);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || value.Length > 256)
            throw new InvalidOperationException("MCP requires one explicit HTTPS callback without credentials, query or fragment.");
        return new(uri);
    }
}
