using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using QuickPrompt.Accounts.Persistence;

namespace QuickPrompt.Cloud.Authentication;

public sealed record GoogleAvailability(bool Enabled);
public sealed record GoogleLink(string Password);

public static class GoogleAccounts
{
    public const string Scheme = "QuickPromptGoogle";

    public static void AddGoogleAccounts(this AuthenticationBuilder authentication, IConfiguration configuration,
        IServiceCollection services, Uri origin)
    {
        var id = configuration["QuickPrompt:Accounts:Google:ClientId"];
        var secret = configuration["QuickPrompt:Accounts:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(id) != string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Configure both Google OAuth values through private configuration.");
        var enabled = !string.IsNullOrWhiteSpace(id);
        if (enabled && origin.Scheme != "https")
            throw new InvalidOperationException("Google sign-in requires the HTTPS account origin and secure correlation cookies.");
        services.AddSingleton(new GoogleAvailability(enabled));
        if (!enabled) return;
        authentication.AddOpenIdConnect(Scheme, options =>
        {
            options.Authority = "https://accounts.google.com";
            options.ClientId = id!;
            options.ClientSecret = secret!;
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.CallbackPath = "/signin-google";
            options.ResponseType = "code";
            options.ResponseMode = "query";
            options.UsePkce = true;
            options.RequireHttpsMetadata = true;
            options.SaveTokens = false;
            options.MapInboundClaims = false;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("email");
            options.Scope.Add("profile");
            options.Events.OnRemoteFailure = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "No se pudo completar el acceso con Google." });
            };
        });
    }

    public static void MapGoogleAccounts(this RouteGroupBuilder account)
    {
        account.MapPost("/google", (GoogleAvailability available) => available.Enabled
            ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/account/google/complete" }, [Scheme])
            : Results.StatusCode(503)).AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapPost("/google/link", async (GoogleLink input, HttpContext context, GoogleAvailability available,
            UserManager<AccountUser> users, SignInManager<AccountUser> signIn) =>
        {
            if (!available.Enabled) return Results.StatusCode(503);
            var user = await users.GetUserAsync(context.User);
            if (user is null || !user.EmailConfirmed || input.Password is not { Length: >= 12 and <= 256 }
                || !(await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true)).Succeeded)
                return Results.Unauthorized();
            var properties = new AuthenticationProperties { RedirectUri = "/account/google/complete" };
            properties.Items["linkUser"] = user.Id.ToString();
            return Results.Challenge(properties, [Scheme]);
        }).RequireAuthorization().AddEndpointFilter<AccountCsrf>().RequireRateLimiting("credentials");
        account.MapGet("/google/complete", Complete);
    }

    private static async Task<IResult> Complete(HttpContext context, GoogleAvailability available,
        UserManager<AccountUser> users, SignInManager<AccountUser> signIn, AccountDatabase database)
    {
        if (!available.Enabled) return Results.StatusCode(503);
        // This principal comes from the framework's encrypted external cookie after OIDC
        // signature/issuer/audience/nonce/state validation, never from request JSON or an LLM.
        var external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        try
        {
            var subject = external.Principal?.FindFirstValue("sub");
            var email = external.Principal?.FindFirstValue("email");
            if (!external.Succeeded || subject is not { Length: > 0 and <= 255 }
                || email is not { Length: > 0 and <= 256 }
                || !string.Equals(external.Principal?.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();
            if (external.Properties?.Items.TryGetValue("linkUser", out var linkId) == true)
            {
                var user = await users.GetUserAsync(context.User);
                if (user is null || user.Id.ToString() != linkId) return Results.Unauthorized();
                return (await users.AddLoginAsync(user, new UserLoginInfo(Scheme, subject, "Google"))).Succeeded
                    ? Results.Redirect("/") : Results.Conflict(new { error = "No se pudo vincular esta cuenta." });
            }
            var linked = await users.FindByLoginAsync(Scheme, subject);
            if (linked is null)
            {
                // Matching emails never grant access to an existing account.
                if (await users.FindByEmailAsync(email) is not null)
                    return Results.Conflict(new { error = "Entra con tu método existente y vincula Google desde tu cuenta." });
                await using var transaction = await database.Database.BeginTransactionAsync(context.RequestAborted);
                var user = new AccountUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
                if (!(await users.CreateAsync(user)).Succeeded
                    || !(await users.AddLoginAsync(user, new UserLoginInfo(Scheme, subject, "Google"))).Succeeded)
                    return Results.Conflict(new { error = "No se pudo crear la cuenta." });
                await transaction.CommitAsync(context.RequestAborted);
            }
            var result = await signIn.ExternalLoginSignInAsync(Scheme, subject, isPersistent: false, bypassTwoFactor: false);
            return result.Succeeded ? Results.Redirect("/") : Results.Unauthorized();
        }
        finally { await context.SignOutAsync(IdentityConstants.ExternalScheme); }
    }
}
