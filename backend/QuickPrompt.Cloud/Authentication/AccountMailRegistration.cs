using Microsoft.Extensions.Hosting;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Optional external delivery; development pickup is an explicit, mutually exclusive Development choice.</summary>
public static class AccountMailRegistration
{
    public static IServiceCollection AddAccountMail(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, Uri origin)
    {
        var selected = configuration["QuickPrompt:Accounts:MailProvider"]?.Trim();
        if (selected is not (null or "" or "Brevo" or "Smtp2Go" or "Mailjet" or "EmailJs"))
            throw new InvalidOperationException("Choose the explicit account-mail provider Brevo, Smtp2Go, Mailjet or EmailJs.");
        var effective = string.IsNullOrEmpty(selected) ? "Brevo" : selected;
        var machine = BrevoClientCredentials.Read(configuration);
        var brevo = machine.Selected ? null : AccountMailSettings.Read(configuration, "Brevo");
        var smtp2Go = AccountMailSettings.Read(configuration, "Smtp2Go");
        var mailjet = AccountMailSettings.Read(configuration, "Mailjet", requireSecret: true);
        var emailJs = EmailJsMailSettings.Read(configuration);
        var configured = new Dictionary<string, bool> {
            ["Brevo"] = brevo is not null || machine.Settings is not null,
            ["Smtp2Go"] = smtp2Go is not null,
            ["Mailjet"] = mailjet is not null,
            ["EmailJs"] = emailJs is not null
        };
        var developmentPickup = configuration.GetValue<bool>("QuickPrompt:Accounts:DevelopmentMail");
        if (configured.Any(entry => entry.Value && entry.Key != effective)
            || (developmentPickup && (configured.Values.Any(value => value) || !string.IsNullOrEmpty(selected)
                || !string.IsNullOrWhiteSpace(configuration["QuickPrompt:Accounts:Brevo:AuthMode"]))))
            throw new InvalidOperationException("Choose one account-mail provider or development pickup, not conflicting configurations.");
        if (configured[effective])
        {
            AccountMailContract.RequireOrigin(origin);
            var clientName = effective switch {
                "Smtp2Go" => Smtp2GoAccountMail.ClientName,
                "Mailjet" => MailjetAccountMail.ClientName,
                "EmailJs" => EmailJsAccountMail.ClientName,
                _ => BrevoAccountMail.ClientName
            };
            services.AddHttpClient(clientName, client => client.Timeout = AccountMailContract.SendTimeout)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
                .RedactLoggedHeaders(_ => true).RemoveAllLoggers();
            services.AddSingleton<IAccountMail>(provider => effective switch {
                "Smtp2Go" => new Smtp2GoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, smtp2Go!),
                "Mailjet" => new MailjetAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, mailjet!),
                "EmailJs" => new EmailJsAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, emailJs!),
                _ => machine.Settings is { } credentials
                    ? new BrevoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, credentials)
                    : new BrevoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, brevo!)
            });
        }
        else services.AddSingleton<IAccountMail>(_ => environment.IsDevelopment() && developmentPickup
            ? new DevelopmentAccountMail() : new UnconfiguredAccountMail());
        return services;
    }
}
