using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using QuickPrompt.Accounts.Persistence;
using Npgsql;
using IPNetwork = System.Net.IPNetwork;

namespace QuickPrompt.Cloud;

/// <summary>Operator-owned transport settings. Public hosting never changes library authority or selects a model.</summary>
public sealed record AccountHosting(Uri Origin, Uri Listen, bool Public, IPAddress[] Proxies, IPNetwork[] Networks,
    string? CertificatePath)
{
    // Keep secrets out of the record's public projection and generated diagnostic representation.
    private string? CertificatePassword { get; init; }
    public const string OriginSecretHeader = "X-QP-Origin-Secret";
    public Uri? IngressOrigin { get; private init; }
    public bool OriginProxy => IngressOrigin is not null;
    private byte[] OriginSecret { get; init; } = [];
    private readonly record struct PreviousCertificate(string Path, string? Password);
    private PreviousCertificate[] PreviousCertificates { get; init; } = [];
    public static AccountHosting Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var mode = configuration["QuickPrompt:Hosting:Mode"] ?? "Local";
        if (mode is not ("Local" or "Public")) throw new InvalidOperationException("Hosting mode must be Local or Public.");
        var published = mode == "Public";
        var profile = configuration["QuickPrompt:Hosting:IngressProfile"] ?? "TrustedProxy";
        if (profile is not ("TrustedProxy" or "OriginProxy") || !published && profile != "TrustedProxy")
            throw new InvalidOperationException("Select an explicit supported public ingress profile; local hosting never selects a cloud edge.");
        var originProxy = profile == "OriginProxy";
        Uri? ingressOrigin = null; byte[] originSecret = [];
        if (originProxy)
        {
            var secret = configuration["QuickPrompt:Hosting:OriginSecret"];
            if (!TryOrigin(configuration["QuickPrompt:Hosting:IngressOrigin"], out ingressOrigin) || ingressOrigin.Scheme != "https" || ingressOrigin.IsLoopback
                || secret is not { Length: 64 } || !secret.All(Uri.IsHexDigit)
                || configuration.GetSection("QuickPrompt:Hosting:TrustedProxies").Exists() || configuration.GetSection("QuickPrompt:Hosting:TrustedNetworks").Exists())
                throw new InvalidOperationException("OriginProxy requires an explicit non-loopback HTTPS ingress origin and a private 64-hex secret, without forwarded-header trust settings.");
            originSecret = Convert.FromHexString(secret);
        }
        else if (configuration["QuickPrompt:Hosting:IngressOrigin"] is not null || configuration["QuickPrompt:Hosting:OriginSecret"] is not null)
            throw new InvalidOperationException("Origin proxy settings require the explicit public OriginProxy profile.");
        if (published && (configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED")
            || configuration.GetValue<bool>("FORWARDEDHEADERS_ENABLED")))
            throw new InvalidOperationException("Automatic proxy trust is incompatible with the explicit public hosting boundary.");
        var originText = configuration["QuickPrompt:Accounts:Origin"] ?? "https://127.0.0.1:5299";
        if (!TryOrigin(originText, out var origin)
            || (origin.Scheme != "https" && !(environment.IsDevelopment() && !published && origin.Scheme == "http"))
            || !published && !origin.IsLoopback || published && (origin.IsLoopback || environment.IsDevelopment()))
            throw new InvalidOperationException("Local hosting requires loopback; public hosting requires an explicit non-development HTTPS origin.");
        if (!published)
        {
            if (configuration["QuickPrompt:Hosting:ListenUrl"] is not null || Values(configuration, "TrustedProxies").Length > 0 || Values(configuration, "TrustedNetworks").Length > 0)
                throw new InvalidOperationException("Separate listeners and proxy trust require explicit Public hosting.");
            return new(origin, origin, false, [], [], null);
        }
        if (!TryOrigin(configuration["QuickPrompt:Hosting:ListenUrl"], out var listen) || listen.Scheme != "http"
            || !IPAddress.TryParse(listen.Host, out _) || listen.Port is < 1024 or > 65535)
            throw new InvalidOperationException("Public hosting requires one explicit HTTP IP listener behind a trusted TLS proxy.");
        var proxies = Values(configuration, "TrustedProxies").Select(value =>
            IPAddress.TryParse(value, out var proxy) && !proxy.Equals(IPAddress.Any) && !proxy.Equals(IPAddress.IPv6Any)
                ? proxy : throw new InvalidOperationException("Trusted proxy must be one explicit IP address.")).ToArray();
        var networks = Values(configuration, "TrustedNetworks").Select(value =>
        {
            if (!IPNetwork.TryParse(value, out var network) || network.PrefixLength < (network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 8 : 32))
                throw new InvalidOperationException("Trusted network must be explicit and cannot be a catch-all network.");
            return network;
        }).ToArray();
        if (!originProxy && proxies.Length + networks.Length == 0 || proxies.Length + networks.Length > 32)
            throw new InvalidOperationException("Public hosting requires a bounded explicit trusted proxy list.");
        var path = RequirePhysicalCertificate(configuration["QuickPrompt:Hosting:KeyRingCertificatePath"]);
        var previous = configuration.GetSection("QuickPrompt:Hosting:PreviousKeyRingCertificates").GetChildren().ToArray();
        if (previous.Length > 4) throw new InvalidOperationException("At most four previous key-ring certificates can be configured.");
        var oldCertificates = previous.Select(section => new PreviousCertificate(RequirePhysicalCertificate(section["Path"]), section["Password"])).ToArray();
        if (oldCertificates.Select(item => item.Path).Append(path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != oldCertificates.Length + 1)
            throw new InvalidOperationException("Key-ring certificate files must be distinct.");
        return new(origin, listen, true, proxies, networks, path) {
            CertificatePassword = configuration["QuickPrompt:Hosting:KeyRingCertificatePassword"], PreviousCertificates = oldCertificates,
            IngressOrigin = ingressOrigin, OriginSecret = originSecret
        };
    }

    /// <summary>Remote account and dispatcher connections must authenticate their database server; local QA keeps its explicit settings.</summary>
    public void RequireDatabaseTls(NpgsqlConnectionStringBuilder accounts, NpgsqlConnectionStringBuilder executions)
    {
        if (Public && (accounts.SslMode != SslMode.VerifyFull || executions.SslMode != SslMode.VerifyFull))
            throw new InvalidOperationException("Public account and execution PostgreSQL connections both require SSL Mode=VerifyFull.");
    }

    public ForwardedHeadersOptions Forwarding()
    {
        if (OriginProxy) throw new InvalidOperationException("Authenticated origin ingress never processes forwarded headers.");
        var options = new ForwardedHeadersOptions {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
        foreach (var proxy in Proxies) options.KnownProxies.Add(proxy);
        foreach (var network in Networks) options.KnownIPNetworks.Add(network);
        return options;
    }

    /// <summary>Validate the physical host and proxy credential before using the configured public origin. Health is a terminal unauthenticated check.</summary>
    public void ConfigureIngress(IApplicationBuilder app, RequestDelegate? healthCheck = null)
    {
        if (OriginProxy)
            app.Use(async (context, next) =>
            {
                var request = context.Request;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                if (!string.Equals(request.Host.Value, IngressOrigin!.Authority, StringComparison.OrdinalIgnoreCase) || !AllowsOrigin(request))
                { context.Response.StatusCode = 403; return; }
                if (healthCheck is not null && IsPhysicalHealth(request))
                {
                    request.Headers.Remove(OriginSecretHeader);
                    await healthCheck(context); return;
                }
                if (!AuthenticatedOrigin(request)) { context.Response.StatusCode = 403; return; }
                request.Headers.Remove(OriginSecretHeader);
                // The Uri overload adds the default port; retain the exact canonical authority used by OAuth and the origin gate.
                request.Host = new HostString(Origin.Authority);
                request.Scheme = Origin.Scheme;
                await next(context);
            });
        else if (Public)
            app.UseForwardedHeaders(Forwarding());
    }

    private bool AuthenticatedOrigin(HttpRequest request)
    {
        var values = request.Headers[OriginSecretHeader];
        return values.Count == 1 && values[0] is { Length: 64 } secret && secret.All(Uri.IsHexDigit)
            && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(secret), OriginSecret);
    }

    private static bool IsPhysicalHealth(HttpRequest request) => (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        && string.Equals(request.Path.Value, "/health", StringComparison.Ordinal) && !request.PathBase.HasValue && !request.QueryString.HasValue;

    /// <summary>The caller owns all loaded certificates until host disposal; no trust-store installation occurs.</summary>
    public AccountKeyRingCertificates? ConfigureKeyRing(IDataProtectionBuilder protection)
    {
        if (!Public) return null;
        var certificates = new List<X509Certificate2>();
        try
        {
            certificates.Add(LoadCertificate(CertificatePath!, CertificatePassword, forEncryption: true));
            foreach (var previous in PreviousCertificates)
                certificates.Add(LoadCertificate(previous.Path, previous.Password, forEncryption: false));
            protection.PersistKeysToDbContext<AccountDatabase>().ProtectKeysWithCertificate(certificates[0])
                .UnprotectKeysWithAnyCertificate(certificates.ToArray());
            return new(certificates.ToArray());
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            foreach (var certificate in certificates) certificate.Dispose();
            throw new InvalidOperationException("The public host key-ring certificate is unavailable or invalid; no fallback key ring is allowed.");
        }
    }

    private static X509Certificate2 LoadCertificate(string path, string? password, bool forEncryption)
    {
        // Recheck before loading; secret files must also be immutable/private at the deployment boundary.
        _ = RequirePhysicalCertificate(path);
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
        try
        {
            using var key = certificate.GetRSAPrivateKey();
            if (!certificate.HasPrivateKey || key is null || key.KeySize < 3072
                || forEncryption && (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow))
                throw new CryptographicException();
            return certificate;
        }
        catch { certificate.Dispose(); throw; }
    }

    private static string RequirePhysicalCertificate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
            throw new InvalidOperationException("Supply an existing private key-ring certificate through a physical secret file.");
        var file = new FileInfo(path);
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Key-ring certificate files cannot be symbolic links or junctions.");
        for (var directory = file.Directory; directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Key-ring certificate paths cannot contain symbolic links or junctions.");
        return file.FullName;
    }

    public bool Accepts(HttpRequest request) => string.Equals(request.Scheme, Origin.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(request.Host.Value, Origin.Authority, StringComparison.OrdinalIgnoreCase)
        && AllowsOrigin(request);

    private bool AllowsOrigin(HttpRequest request) => request.Headers.Origin.Count == 0 || request.Path == "/signin-google"
        || request.Headers.Origin.Count == 1 && string.Equals(request.Headers.Origin[0], Origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);

    private static string[] Values(IConfiguration configuration, string key) => configuration.GetSection("QuickPrompt:Hosting:" + key)
        .GetChildren().Select(child => child.Value ?? "").ToArray();
    private static bool TryOrigin(string? value, out Uri origin)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) && parsed.Scheme is "https" or "http"
            && parsed.AbsolutePath == "/" && parsed.Query.Length == 0 && parsed.Fragment.Length == 0 && parsed.UserInfo.Length == 0)
        { origin = parsed; return true; }
        origin = null!; return false;
    }
}

/// <summary>One lifecycle owner for the current encryption certificate and bounded decryption-only predecessors.</summary>
public sealed class AccountKeyRingCertificates(X509Certificate2[] certificates) : IDisposable
{
    public X509Certificate2 Current => certificates[0];
    public int Count => certificates.Length;
    public void Dispose()
    {
        foreach (var certificate in certificates) certificate.Dispose();
    }
}
