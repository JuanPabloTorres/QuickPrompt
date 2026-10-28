using System.Security.Cryptography;
using System.Text;

namespace QuickPrompt.Nova.Mcp;

/// <summary>Startup-owned configuration. Tool arguments cannot change the library endpoint or write access.</summary>
public sealed class McpSettings
{
    public Uri ApiBaseUrl { get; }
    public string Transport { get; }
    public int Port { get; }
    public bool AllowCreate { get; }
    internal byte[]? HttpTokenHash { get; }

    private McpSettings(Uri apiBaseUrl, string transport, int port, bool allowCreate, byte[]? httpTokenHash)
    { ApiBaseUrl = apiBaseUrl; Transport = transport; Port = port; AllowCreate = allowCreate; HttpTokenHash = httpTokenHash; }

    /// <summary>Requires a loopback API and a strong bearer credential for the optional local HTTP transport.</summary>
    public static McpSettings Read(IConfiguration configuration)
    {
        var endpoint = configuration["QuickPromptMcp:ApiBaseUrl"] ?? "http://127.0.0.1:5277";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "http"
            || uri.Host is not ("127.0.0.1" or "localhost") || uri.Port is < 1024 or > 65535
            || uri.AbsolutePath != "/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Configura una API QuickPrompt HTTP en loopback, sin ruta, credenciales ni query.");
        var transport = configuration["QuickPromptMcp:Transport"] ?? "stdio";
        if (transport is not ("stdio" or "http")) throw new InvalidOperationException("Transport debe ser stdio o http.");
        var port = configuration.GetValue("QuickPromptMcp:Port", 5278);
        if (port is < 1024 or > 65535 || port == uri.Port) throw new InvalidOperationException("Puerto MCP local inválido o igual a la API.");
        if (configuration.GetSection("Kestrel:Endpoints").Exists()) throw new InvalidOperationException("Usa QuickPromptMcp:Port, sin endpoints Kestrel adicionales.");
        var token = configuration["QuickPromptMcp:HttpToken"];
        if (transport == "http" && (token is null || token.Length is < 32 or > 256 || token.Any(c => c <= 32 || c > 126)))
            throw new InvalidOperationException("HTTP requiere QuickPromptMcp__HttpToken en el entorno, de 32–256 caracteres ASCII sin espacios.");
        return new(uri, transport, port, configuration.GetValue("QuickPromptMcp:AllowCreate", false),
            transport == "http" ? SHA256.HashData(Encoding.UTF8.GetBytes(token!)) : null);
    }
}
