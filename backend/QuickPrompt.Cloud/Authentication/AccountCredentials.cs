using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Executions;

namespace QuickPrompt.Cloud.Authentication;

public sealed record Registration(string Email, string Password);
public sealed record Login(string Email, string Password);
public sealed record TokenConfirmation(Guid UserId, string Code);
public sealed record RecoveryRequest(string Email);
public sealed record PasswordRecovery(Guid UserId, string Code, string Password);

/// <summary>Identity owns passwords/tokens/stamps; transport never accepts an actor identifier for login.</summary>
public sealed class AccountCredentials(UserManager<AccountUser> users, SignInManager<AccountUser> signIn,
    IAccountMail mail, AccountOrigin origin, ExecutionQueue queue)
{
    public async Task<IResult> Register(Registration input, CancellationToken cancellation)
    {
        if (!mail.Available) return Results.Problem(statusCode: 503, title: "El correo de cuentas todavía no está configurado.");
        if (!ValidEmail(input.Email) || !ValidPasswordLength(input.Password)) return Results.BadRequest();
        var user = new AccountUser { Id = Guid.NewGuid(), Email = input.Email.Trim(), UserName = input.Email.Trim() };
        foreach (var validator in users.PasswordValidators)
            if (!(await validator.ValidateAsync(users, user, input.Password)).Succeeded)
                return Results.BadRequest(new { error = "Usa al menos 12 caracteres, mayúsculas, minúsculas, números y un símbolo." });
        IdentityResult created;
        try { created = await users.CreateAsync(user, input.Password); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Results.Accepted(value: new { message = "Si corresponde, recibirás los pasos para confirmar tu cuenta." }); }
        if (created.Succeeded)
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            await mail.Send(new(user.Email!, "confirm", Link("confirm", user.Id, token)), cancellation);
        }
        // Same result for an existing email: the caller must prove access through the mailbox.
        return Results.Accepted(value: new { message = "Si corresponde, recibirás los pasos para confirmar tu cuenta." });
    }

    public async Task<IResult> Login(Login input)
    {
        if (!ValidEmail(input.Email) || !ValidPasswordLength(input.Password)) return Results.Unauthorized();
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null) return Results.Unauthorized();
        var result = await signIn.PasswordSignInAsync(user, input.Password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
    }

    public async Task<IResult> Confirm(TokenConfirmation input)
    {
        if (!TryDecode(input.Code, out var token)) return Results.BadRequest();
        var user = await users.FindByIdAsync(input.UserId.ToString());
        return user is not null && (await users.ConfirmEmailAsync(user, token)).Succeeded ? Results.NoContent() : Results.BadRequest();
    }

    public async Task<IResult> Recover(RecoveryRequest input, CancellationToken cancellation)
    {
        if (!mail.Available) return Results.StatusCode(503);
        if (!ValidEmail(input.Email)) return Results.BadRequest();
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is { EmailConfirmed: true })
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await mail.Send(new(user.Email!, "reset", Link("reset", user.Id, token)), cancellation);
        }
        return Results.Accepted(value: new { message = "Si corresponde, recibirás los pasos de recuperación." });
    }

    public async Task<IResult> Resend(RecoveryRequest input, CancellationToken cancellation)
    {
        if (!mail.Available) return Results.StatusCode(503);
        if (!ValidEmail(input.Email)) return Results.BadRequest();
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is { EmailConfirmed: false })
            await mail.Send(new(user.Email!, "confirm", Link("confirm", user.Id, await users.GenerateEmailConfirmationTokenAsync(user))), cancellation);
        return Results.Accepted(value: new { message = "Si corresponde, recibirás los pasos para confirmar tu cuenta." });
    }

    public async Task<IResult> Reset(PasswordRecovery input)
    {
        if (!TryDecode(input.Code, out var token) || !ValidPasswordLength(input.Password)) return Results.BadRequest();
        var user = await users.FindByIdAsync(input.UserId.ToString());
        if (user is null) return Results.BadRequest();
        return await queue.InvalidateLocalSession(user.Id, async () => (await users.ResetPasswordAsync(user, token, input.Password)).Succeeded)
            ? Results.NoContent() : Results.BadRequest();
    }

    private string Link(string purpose, Guid id, string token) => QueryHelpers.AddQueryString(
        new Uri(origin.Value, "/account/" + purpose).ToString(), new Dictionary<string, string?>
        { ["userId"] = id.ToString(), ["code"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)) });
    private static bool ValidEmail(string? email) => email is { Length: > 0 and <= 256 } && new EmailAddressAttribute().IsValid(email);
    private static bool ValidPasswordLength(string? password) => password is { Length: >= 12 and <= 256 };
    private static bool TryDecode(string? code, out string token)
    {
        token = "";
        if (code is not { Length: > 0 and <= 4096 }) return false;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); return true; }
        catch (FormatException) { return false; }
    }
}
