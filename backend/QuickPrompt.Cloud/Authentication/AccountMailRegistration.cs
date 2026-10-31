using Microsoft.Extensions.Hosting;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Optional external delivery; development pickup is an explicit, mutually exclusive Development choice.</summary>
public static class AccountMailRegistration
{
    public static IServiceCollection AddAccountMail(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, Uri origin)
    {
        var selected = configuration["QuickPrompt:Accounts:MailProvider"]?.Trim();
        if (selected is not (null or "" or "Brevo" or "Smtp2Go" or "Mailjet"))
            throw new InvalidOperationException("Choose the explicit account-mail provider Brevo, Smtp2Go or Mailjet.");
        var effective = string.IsNullOrEmpty(selected) ? "Brevo" : selected;
        var providers = new Dictionary<string, AccountMailSettings?> {
            ["Brevo"] = AccountMailSettings.Read(configuration, "Brevo"),
            ["Smtp2Go"] = AccountMailSettings.Read(configuration, "Smtp2Go"),
            ["Mailjet"] = AccountMailSettings.Read(configuration, "Mailjet", requireSecret: true)
        };
        var developmentPickup = configuration.GetValue<bool>("QuickPrompt:Accounts:DevelopmentMail");
        if (providers.Any(entry => entry.Value is not null && entry.Key != effective)
            || (developmentPickup && (providers.Values.Any(value => value is not null) || !string.IsNullOrEmpty(selected))))
            throw new InvalidOperationException("Choose one account-mail provider or development pickup, not conflicting configurations.");
        var settings = providers[effective];
        if (settings is not null)
        {
            AccountMailContract.RequireOrigin(origin);
            var clientName = effective switch {
                "Smtp2Go" => Smtp2GoAccountMail.ClientName,
                "Mailjet" => MailjetAccountMail.ClientName,
                _ => BrevoAccountMail.ClientName
            };
            services.AddHttpClient(clientName, client => client.Timeout = AccountMailContract.SendTimeout)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
                .RedactLoggedHeaders(_ => true).RemoveAllLoggers();
            services.AddSingleton<IAccountMail>(provider => effective switch {
                "Smtp2Go" => new Smtp2GoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, settings),
                "Mailjet" => new MailjetAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, settings),
                _ => new BrevoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, settings)
            });
        }
        else services.AddSingleton<IAccountMail>(_ => environment.IsDevelopment() && developmentPickup
            ? new DevelopmentAccountMail() : new UnconfiguredAccountMail());
        return services;
    }
}
