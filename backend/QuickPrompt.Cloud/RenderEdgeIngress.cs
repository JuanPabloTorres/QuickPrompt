using System.Net;
using System.Text.RegularExpressions;

namespace QuickPrompt.Cloud;

/// <summary>
/// Explicit adapter for Render's assigned HTTPS hostname and edge-only public port.
/// Render terminates TLS and redirects public HTTP; forwarded headers are never authority.
/// Custom domains and other providers require a separate verified configuration.
/// </summary>
internal sealed record RenderEdgeIngress(string Host)
{
    public static RenderEdgeIngress Read(IConfiguration configuration, Uri origin, Uri listen)
    {
        var host = configuration["RENDER_EXTERNAL_HOSTNAME"];
        if (configuration["RENDER"] != "true"
            || !Regex.IsMatch(configuration["RENDER_SERVICE_ID"] ?? "", "^srv-[a-z0-9]+$", RegexOptions.CultureInvariant)
            || host is null || !Regex.IsMatch(host, "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.onrender\\.com$", RegexOptions.CultureInvariant)
            || !string.Equals(origin.Authority, host, StringComparison.Ordinal) || origin.Scheme != "https"
            || !listen.Host.Equals(IPAddress.Any.ToString(), StringComparison.Ordinal)
            || !int.TryParse(configuration["PORT"], out var port) || listen.Port != port)
            throw new InvalidOperationException("RenderEdge requires explicit Render service metadata, its assigned HTTPS hostname and the platform HTTP port.");
        return new(host);
    }

    public void Configure(IApplicationBuilder application, Func<HttpRequest, bool> allowsOrigin) =>
        application.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (!string.Equals(context.Request.Host.Value, Host, StringComparison.Ordinal) || !allowsOrigin(context.Request))
            { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            foreach (var header in new[] { "Forwarded", "X-Forwarded-For", "X-Forwarded-Host", "X-Forwarded-Proto", AccountHosting.OriginSecretHeader })
                context.Request.Headers.Remove(header);
            // Deployment metadata and Render's documented edge topology supply
            // the scheme. No header or caller chooses the host or HTTPS identity.
            context.Request.Scheme = "https";
            await next(context);
        });
}
