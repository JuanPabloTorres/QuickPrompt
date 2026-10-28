using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Microsoft.Extensions.FileProviders;
using QuickPrompt.Accounts;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Cloud.Delegation;
using QuickPrompt.Cloud.Authentication;

namespace QuickPrompt.Cloud;

/// <summary>Explicit account host. No local-store, worker, tunnel or private-library fallback.</summary>
public static class AccountHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var hosting = AccountHosting.Read(builder.Configuration, builder.Environment);
        var origin = hosting.Origin;
        builder.WebHost.UseUrls(hosting.Listen.GetLeftPart(UriPartial.Authority));
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 128 * 1024);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.MaxDepth = 32;
        });

        var connection = builder.Configuration.GetConnectionString("Accounts")
            ?? throw new InvalidOperationException("Configure the limited Accounts PostgreSQL connection outside source control.");
        var executionConnection = builder.Configuration.GetConnectionString("Executions")
            ?? throw new InvalidOperationException("Configure the limited Executions connection outside source control.");
        var applicationTarget = new NpgsqlConnectionStringBuilder(connection);
        var executionTarget = new NpgsqlConnectionStringBuilder(executionConnection);
        hosting.RequireDatabaseTls(applicationTarget, executionTarget);
        if (!string.Equals(applicationTarget.Host, executionTarget.Host, StringComparison.OrdinalIgnoreCase)
            || applicationTarget.Port != executionTarget.Port || applicationTarget.Database != executionTarget.Database)
            throw new InvalidOperationException("Accounts and execution dispatcher must target the same database. No cross-database fallback is available.");
        // Cookies are host-scoped, not port-scoped. Separate local candidates and
        // databases cryptographically as well as by cookie name.
        var instanceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            origin.GetLeftPart(UriPartial.Authority) + "|" + new NpgsqlConnectionStringBuilder(connection).Database)))[..16].ToLowerInvariant();
        var protection = builder.Services.AddDataProtection().SetApplicationName("QuickPrompt.Accounts." + instanceKey);
        var data = NpgsqlDataSource.Create(connection);
        builder.Services.AddSingleton<NpgsqlDataSource>(_ => data);
        builder.Services.AddQuickPromptAccounts(data);
        var delegation = builder.Configuration.GetValue<bool>("QuickPrompt:Delegation:Enabled");
        if (delegation && origin.Scheme != "https") throw new InvalidOperationException("Delegation requires HTTPS.");
        builder.Services.AddSingleton(new DelegationAvailability(delegation));
        if (delegation) builder.AddAccountDelegation(origin);
        builder.Services.AddSingleton(_ => new ExecutionDataSource(NpgsqlDataSource.Create(executionConnection)));
        // Versioned candidate policy: every dispatcher uses the same bounds. No per-host override.
        builder.Services.AddSingleton(new ExecutionLimits());
        builder.Services.AddSingleton<ExecutionQueue>();
        builder.Services.AddSingleton<IAccountExecutionHandler, CompileExecutionHandler>();
        builder.Services.AddScoped<AccountExecutionAccess>();
        builder.Services.AddSingleton<AccountExecutionFeed>();
        if (delegation)
        {
            builder.Services.AddSingleton<AccountNovaCoordinator>();
            builder.Services.AddSingleton<AccountNovaFeed>();
            builder.Services.AddHostedService(services => services.GetRequiredService<AccountNovaCoordinator>());
        }
        var authentication = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authentication.AddIdentityCookies(options =>
        {
            options.ApplicationCookie!.Configure(cookie =>
            {
                cookie.Cookie.Name = (builder.Environment.IsDevelopment() ? "qp.accounts." : "__Host-qp.accounts.") + instanceKey;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                cookie.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                cookie.SlidingExpiration = false;
                cookie.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
                cookie.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
            });
            options.ExternalCookie!.Configure(cookie =>
            {
                cookie.Cookie.Name = (builder.Environment.IsDevelopment() ? "qp.external." : "__Host-qp.external.") + instanceKey;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });
        });
        authentication.AddGoogleAccounts(builder.Configuration, builder.Services, origin);
        // Revocation/password reset invalidates existing cookies on the next request.
        builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-QP-CSRF";
            options.Cookie.Name = (builder.Environment.IsDevelopment() ? "qp.csrf." : "__Host-qp.csrf.") + instanceKey;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddSingleton(new AccountOrigin(origin));
        builder.Services.AddAccountMail(builder.Configuration, builder.Environment, origin);
        builder.Services.AddScoped<AccountCredentials>();
        builder.Services.AddHostedService<AccountRoleGuard>();
        if (hosting.Public) builder.Services.AddHostedService<HostKeyRingGuard>();
        builder.Services.AddHostedService<ExecutionRoleGuard>();
        builder.Services.AddHostedService<ExecutionWorker>();
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));
        builder.Services.AddAccountRateLimiting();
        configureServices?.Invoke(builder.Services);
        var keyRingCertificate = hosting.ConfigureKeyRing(protection);
        if (keyRingCertificate is not null) builder.Services.AddSingleton(_ => keyRingCertificate);
        WebApplication app;
        try { app = builder.Build(); }
        catch { keyRingCertificate?.Dispose(); throw; }
        if (keyRingCertificate is not null) _ = app.Services.GetRequiredService<AccountKeyRingCertificates>();
        hosting.ConfigureIngress(app, CheckPhysicalHealth);
        app.Use(async (context, next) =>
        {
            var schemeMatches = string.Equals(context.Request.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase);
            var hostMatches = string.Equals(context.Request.Host.Value, origin.Authority, StringComparison.OrdinalIgnoreCase);
            var originMatches = context.Request.Headers.Origin.Count == 0 || context.Request.Path == "/signin-google"
                || string.Equals(context.Request.Headers.Origin.ToString(), origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
            if (!hosting.Accepts(context.Request))
            {
                // Log boundary decisions, never the supplied origin, OAuth query, token or body.
                app.Logger.LogWarning("Account origin rejected: schemeMatch={SchemeMatch}, hostMatch={HostMatch}, originMatch={OriginMatch}, nullOrigin={NullOrigin}",
                    schemeMatches, hostMatches, originMatches, context.Request.Headers.Origin.ToString() == "null");
                context.Response.StatusCode = 403; return;
            }
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            // no-referrer turns the Origin of a native non-CORS POST into "null"
            // (Fetch: append-a-request-origin-header), conflicting with the exact-origin guard.
            // strict-origin preserves that guard without disclosing paths or OAuth query values.
            context.Response.Headers["Referrer-Policy"] = "strict-origin";
            // Chromium also checks redirects after a native consent form POST.
            // OpenIddict still validates the exact operator-registered callback.
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; style-src-attr 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://accounts.google.com https://chatgpt.com/connector_platform_oauth_redirect";
            try { await next(); }
            catch (Exception error) when (!context.Response.HasStarted && error is not OperationCanceledException)
            {
                if (error is ExecutionQuotaException rejected) AccountOperations.QuotaRejected(app.Logger, rejected.Code);
                var status = error switch
                {
                    LibraryUnavailableException => 404,
                    LibraryRevisionException => 409,
                    ShareConflictException => 409,
                    CaptureConflictException => 409,
                    ExecutionQuotaException => 429,
                    QuickPrompt.Nova.Application.MissingPromptException => 404,
                    QuickPrompt.Local.ConflictException => 409,
                    QuickPrompt.Local.ValidationException or ArgumentException or BadHttpRequestException => 400,
                    UnauthorizedAccessException => 401,
                    _ => 500
                };
                if (status == 500) app.Logger.LogError("Account operation failed: {Type}; correlation {Correlation}", error.GetType().Name, context.TraceIdentifier);
                var title = error is ExecutionQuotaException quota ? quota.Code switch
                {
                    "DAILY_LIMIT" => "Alcanzaste el límite diario de preparaciones. Tus datos se conservan en el formulario.",
                    "OWNER_QUEUE_FULL" => "Tu cola está llena. Espera o detén una preparación antes de reintentar.",
                    _ => "La cola del servicio está llena. Vuelve a intentarlo en unos momentos."
                } : "No se pudo completar la operación.";
                await Results.Problem(statusCode: status, title: title).ExecuteAsync(context);
            }
        });
        app.UseAccountIngressLimits();
        // Public readiness must reach its own dependency check even when validating
        // an unrelated account cookie would need the same unavailable connection pool.
        app.UsePublicAccountReadiness(CheckPhysicalHealth);
        if (delegation) app.UseDelegationFence();
        if (delegation && app.Services.GetRequiredService<McpAccountSettings>().Enabled) app.UseAccountMcpChallenge();
        app.UseAuthentication();
        app.UseAuthorization();
        // Endpoint-selected schemes and authorization decide the actor before its quota.
        // The independent ingress budget above still bounds rejected authentication requests.
        app.UseRateLimiter();
        MapAccountPage(app, builder.Configuration);
        app.MapAccountEndpoints();
        app.MapAccountLibraries();
        app.MapAccountCatalog();
        app.MapAccountCollections();
        app.MapPost("/api/accounts/v1/libraries/{libraryId:guid}/captures", async (HttpContext context, Guid libraryId, AccountCaptureInput input, LibraryAccess libraries, CancellationToken cancellation) =>
            await libraries.Capture(LibraryActor.FromPrincipal(context.User), libraryId, input, cancellation)).RequireAuthorization().AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        app.MapAccountSharing();
        app.MapAccountMemory();
        app.MapAccountExecutions();
        if (delegation) app.MapAccountNova();
        if (delegation) app.MapAccountDelegation();
        if (delegation && app.Services.GetRequiredService<McpAccountSettings>().Enabled) app.MapAccountMcp();
        app.MapGet("/health", CheckHealth);
        return app;
    }

    private static async Task CheckPhysicalHealth(HttpContext context)
    {
        var result = await CheckHealth(context.RequestServices.GetRequiredService<NpgsqlDataSource>(),
            context.RequestServices.GetRequiredService<ILoggerFactory>(), context.RequestAborted);
        if (HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
            return;
        }
        await result.ExecuteAsync(context);
    }

    private static async Task<IResult> CheckHealth(NpgsqlDataSource source, ILoggerFactory logging, CancellationToken cancellation)
    {
        try
        {
            await using var databaseConnection = await source.OpenConnectionAsync(cancellation);
            await using var check = new NpgsqlCommand("SELECT 1", databaseConnection);
            await check.ExecuteScalarAsync(cancellation);
            return Results.Ok(new { status = "ok", mode = "accounts-candidate" });
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException)
        {
            AccountOperations.ReadinessUnavailable(logging.CreateLogger("QuickPrompt.Cloud.Operations"));
            return Results.Problem(statusCode: 503, title: "La base de cuentas no está disponible.");
        }
    }

    private static void MapAccountPage(WebApplication app, IConfiguration configuration)
    {
        var root = Path.GetFullPath(configuration["QuickPrompt:WebRoot"] ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        if (!File.Exists(Path.Combine(root, "index.html")))
        {
            app.MapGet("/", () => Results.Problem(statusCode: 503, title: "Compila apps/web y configura su WebRoot para la pantalla de cuentas."));
            return;
        }
        // Serve assets through the shared React build; raw HTML must also carry the account mode.
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/assets") || context.Request.Path.StartsWithSegments("/branding")
            || context.Request.Path == "/favicon.svg", branch => branch.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root) }));
        const string accountMode = "<meta name=\"quickprompt-mode\" content=\"accounts\" />";
        var page = File.ReadAllText(Path.Combine(root, "index.html"));
        // The Vercel accounts build already contains this canonical declaration.
        // The default local build still needs the server-owned account entry point.
        if (!page.Contains(accountMode, StringComparison.Ordinal))
            page = page.Replace("<head>", "<head>" + accountMode, StringComparison.Ordinal);
        foreach (var path in new[] { "/", "/index.html", "/account/confirm", "/account/reset", "/connect/verify" })
            app.MapGet(path, () => Results.Content(page, "text/html; charset=utf-8"));
    }
}

public sealed record AccountOrigin(Uri Value);

/// <summary>Public read-only readiness framing; place after ingress/origin guards and before authentication.</summary>
public static class AccountReadiness
{
    /// <summary>Only GET/HEAD of the public health path bypass cookie validation. All resource routes keep normal authentication.</summary>
    public static void UsePublicAccountReadiness(this WebApplication app, RequestDelegate probe) =>
        app.UseWhen(context => context.Request.Path == "/health" && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)),
            branch => branch.Run(probe));
}

/// <summary>Account-host admission budgets. These bounds are protective limits, not a capacity guarantee.</summary>
public static class AccountRateLimiting
{
    /// <summary>Registers the shared ingress and endpoint budgets without choosing authentication schemes.</summary>
    public static void AddAccountRateLimiting(this IServiceCollection services)
    {
        services.AddSingleton<IngressBudget>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("credentials", context => RateLimitPartition.GetFixedWindowLimiter(
                CredentialPartition(context), _ => Window(60)));
        });
    }

    /// <summary>Run after routing and before authentication: OAuth handlers can complete without reaching endpoints.</summary>
    public static void UseAccountIngressLimits(this WebApplication app)
    {
        var budget = app.Services.GetRequiredService<IngressBudget>();
        app.Use(async (context, next) =>
        {
            using var lease = budget.Limiter.AttemptAcquire(context);
            if (!lease.IsAcquired) { context.Response.StatusCode = 429; return; }
            await next();
        });
    }

    private static string CredentialPartition(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        // Called only after AuthorizationMiddleware. Its endpoint-selected cookie or
        // bearer scheme is authoritative; an unrelated cookie cannot select the actor.
        // Public login/recovery/consent endpoints keep their shared IP budget even
        // when a valid cookie accompanies the request.
        var authorized = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is null
            && (endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0
                || endpoint?.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count > 0);
        if (authorized && context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var owner) && owner != Guid.Empty)
            return "actor:" + owner.ToString("N");
        return "anonymous:" + PhysicalAddress(context);
    }

    private static string PhysicalAddress(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    private static bool UsesCredentials(HttpContext context) => context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "credentials";
    private static FixedWindowRateLimiterOptions Window(int permits) => new()
    { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true };

    private sealed class IngressBudget : IDisposable
    {
        public PartitionedRateLimiter<HttpContext> Limiter { get; } = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            // Never trust X-Forwarded-For/Host or client actor headers as a partition.
            // Keep the previous protocol budget before the delegation fence. The
            // separate 600/min envelope bounds authentication work for other limited
            // endpoints while allowing independent authorized owners their own quota.
            if (context.Request.Path.StartsWithSegments("/connect"))
                return RateLimitPartition.GetFixedWindowLimiter("protocol:" + PhysicalAddress(context), _ => Window(60));
            return UsesCredentials(context)
                ? RateLimitPartition.GetFixedWindowLimiter("ingress:" + PhysicalAddress(context), _ => Window(600))
                : RateLimitPartition.GetNoLimiter("unlimited");
        });
        public void Dispose() => Limiter.Dispose();
    }
}

/// <summary>Cookie-backed mutations require the framework's per-client antiforgery token.</summary>
public sealed class AccountCsrf : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        try { await invocation.HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(invocation.HttpContext); }
        catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = "Renueva la página antes de continuar." }); }
        return await next(invocation);
    }
}
