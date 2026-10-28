using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Nova.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Cloud.Delegation;

public sealed record DelegationOrigin(Uri Value)
{
    public string Resource => new Uri(Value, "/api/companion").AbsoluteUri;
    public string McpResource => new Uri(Value, "/mcp").AbsoluteUri;
    public string ProposalResource => new Uri(Value, LocalProposalProtocol.ResourcePath).AbsoluteUri;
    public string ForProfile(string profile) => new Uri(Value, LocalCompanionProfiles.ResourcePath(profile)).AbsoluteUri;
}
public sealed record DelegationAvailability(bool Enabled);

public static class DelegationModule
{
    public const string Policy = "companion-delegation";
    public const string ProposalPolicy = "companion-proposals";
    public const string AnyCompanionPolicy = "companion-profile";

    public static void AddAccountDelegation(this WebApplicationBuilder builder, Uri origin)
    {
        builder.Services.AddSingleton(new DelegationOrigin(origin));
        var mcp = McpAccountSettings.Read(builder.Configuration);
        builder.Services.AddSingleton(mcp);
        builder.Services.AddSingleton(_ => DelegationKeys.Load(builder.Configuration["QuickPrompt:Delegation:KeyDirectory"]));
        builder.Logging.AddFilter("OpenIddict", LogLevel.Warning);
        builder.Services.AddOpenIddict().AddServer(options =>
        {
            options.SetIssuer(origin);
            // Vercel reserves /.well-known against rewrites. Keep public keys
            // dynamic through the proxy while preserving the previous endpoint.
            options.SetJsonWebKeySetEndpointUris("/connect/jwks", "/.well-known/jwks");
            options.SetDeviceAuthorizationEndpointUris("/connect/device");
            options.SetEndUserVerificationEndpointUris("/connect/verify");
            options.SetTokenEndpointUris("/connect/token");
            options.SetRevocationEndpointUris("/connect/revoke");
            options.AllowDeviceAuthorizationFlow().AllowRefreshTokenFlow();
            if (mcp.Enabled)
            {
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                options.Configure(configuration => configuration.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
                options.RegisterScopes(McpDelegationPolicy.ReadScope, McpDelegationPolicy.ImportScope, McpDelegationPolicy.CreateScope, McpDelegationPolicy.CollectionsScope);
                options.RegisterResources(new Uri(new DelegationOrigin(origin).McpResource));
                // The fixed MCP client is public and redeems a PKCE-bound code
                // without a client secret. Advertise that existing transport
                // capability explicitly so clients can negotiate it from discovery.
                // This does not change client validation or admit new clients.
                options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(handler => handler.UseInlineHandler(context =>
                {
                    context.TokenEndpointAuthenticationMethods.Add("none");
                    return ValueTask.CompletedTask;
                }));
            }
            options.RegisterScopes(DelegationPolicy.Scope, LocalProposalProtocol.Scope);
            options.RegisterResources(new Uri(new DelegationOrigin(origin).Resource));
            options.RegisterResources(new Uri(new DelegationOrigin(origin).ProposalResource));
            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
            options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
            options.SetDeviceCodeLifetime(TimeSpan.FromMinutes(10));
            options.SetUserCodeLifetime(TimeSpan.FromMinutes(10));
            options.UseReferenceAccessTokens().UseReferenceRefreshTokens();
            options.UseDataProtection();
            options.AddEventHandler<OpenIddictServerEvents.ApplyDeviceAuthorizationResponseContext>(handler => handler.UseInlineHandler(context =>
            {
                if (context.Transaction.Response?.Error is { } error)
                {
                    var reference = System.Text.RegularExpressions.Regex.Match(context.Transaction.Response.ErrorUri ?? "", @"/errors/(ID[0-9]{4})$");
                    context.Logger.LogWarning(5286, "Device authorization rejected: {Code}; reference={Reference}; fixedClient={FixedClient}; labelPresent={LabelPresent}; profilePresent={ProfilePresent}; resourceMatches={ResourceMatches}",
                        error, reference.Success ? reference.Groups[1].Value : "none", context.Request?.ClientId == DelegationPolicy.ClientId,
                        context.Request is not null && (string?)context.Request.GetParameter("device_name") is not null,
                        context.Request is not null && (string?)context.Request.GetParameter("profile_id") is not null,
                        context.Request is not null && context.Request.GetResources().SequenceEqual([new DelegationOrigin(origin).Resource]));
                }
                return ValueTask.CompletedTask;
            }));
            options.AddEventHandler<OpenIddictServerEvents.HandleDeviceAuthorizationRequestContext>(handler => handler.UseScopedHandler<DeviceAuthorizationHandler>());
            var integration = options.UseAspNetCore().EnableEndUserVerificationEndpointPassthrough().EnableTokenEndpointPassthrough();
            if (mcp.Enabled) integration.EnableAuthorizationEndpointPassthrough();
        }).AddValidation(options =>
        {
            options.UseLocalServer(); options.AddAudiences(new DelegationOrigin(origin).Resource);
            options.AddAudiences(new DelegationOrigin(origin).ProposalResource);
            if (mcp.Enabled) options.AddAudiences(new DelegationOrigin(origin).McpResource);
            options.UseDataProtection();
            options.EnableTokenEntryValidation(); options.EnableAuthorizationEntryValidation();
            // Resource credentials travel only in Authorization: Bearer. Never accept
            // URL/form tokens that can leak through links, logs or request history.
            // This configures validation, not the OAuth token endpoint's form protocol.
            options.UseAspNetCore().DisableAccessTokenExtractionFromQueryString().DisableAccessTokenExtractionFromBodyForm();
        });
        builder.Services.AddOptions<OpenIddictServerOptions>().Configure<DelegationKeys>((options, keys) =>
        {
            options.SigningCredentials.Add(new(keys.Signing, SecurityAlgorithms.RsaSha256));
            options.EncryptionCredentials.Add(new(keys.Encryption, SecurityAlgorithms.RsaOAEP, SecurityAlgorithms.Aes256CbcHmacSha512));
        });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policy, CompanionPolicy(context => context.User.HasScope(DelegationPolicy.Scope)));
            options.AddPolicy(ProposalPolicy, CompanionPolicy(context => context.User.HasScope(LocalProposalProtocol.Scope)));
            options.AddPolicy(AnyCompanionPolicy, CompanionPolicy(context => context.User.HasScope(DelegationPolicy.Scope) || context.User.HasScope(LocalProposalProtocol.Scope)));
        });
        builder.Services.AddScoped<ConnectionAccess>();
        builder.Services.AddScoped<DelegationBoundary>();
        builder.Services.AddScoped<QuickPrompt.Accounts.Executions.IExecutionConnectionAuthority>(services => services.GetRequiredService<DelegationBoundary>());
        builder.Services.AddScoped<CompanionWorkService>();
        builder.Services.AddScoped<CompanionProposalService>();
        builder.Services.AddScoped<QuickPrompt.Accounts.Executions.IProposalConnectionAuthority>(services => services.GetRequiredService<DelegationBoundary>());
        builder.Services.AddScoped<QuickPrompt.Accounts.Executions.AccountModelProposals>();
        builder.Services.AddHostedService<DelegationClientRegistration>();
        if (mcp.Enabled) builder.Services.AddAccountMcp();
    }

    private static AuthorizationPolicy CompanionPolicy(Func<AuthorizationHandlerContext, bool> allowed) =>
        new AuthorizationPolicyBuilder(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireAssertion(allowed).Build();
}
