using System.Net;

namespace QuickPrompt.Local;

/// <summary>The local adapter never accepts remote hosts, credentials or paths from a task.</summary>
public static class LocalModelEndpoint
{
    public static Uri Require(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "http"
            || !IPAddress.TryParse(uri.Host, out var ip) || !IPAddress.IsLoopback(ip) || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ValidationException("Ollama requiere una IP loopback HTTP / Ollama requires HTTP loopback IP.");
        return uri;
    }
}
