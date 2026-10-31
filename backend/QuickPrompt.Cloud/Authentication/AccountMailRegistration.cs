using Microsoft.Extensions.Hosting;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Optional external delivery; development pickup is an explicit, mutually exclusive Development choice.</summary>
public static class AccountMailRegistration
{
    public static IServiceCollection AddAccountMail(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, Uri origin)
    {
        var selected = configuration["QuickPrompt:Accounts:MailProvider"]?.Trim();
        if (selected is not (null or "" or "Brevo" or "Smtp2Go"))
            throw new InvalidOperationException("Choose the explicit account-mail provider Brevo or Smtp2Go.");
        var brevo = AccountMailSettings.Read(configuration, "Brevo");
        var smtp2Go = AccountMailSettings.Read(configuration, "Smtp2Go");
        var developmentPickup = configuration.GetValue<bool>("QuickPrompt:Accounts:DevelopmentMail");
        if ((brevo is not null && smtp2Go is not null)
            || (smtp2Go is not null && selected != "Smtp2Go")
            || (brevo is not null && selected == "Smtp2Go")
            || (developmentPickup && (brevo is not null || smtp2Go is not null || !string.IsNullOrEmpty(selected))))
            throw new InvalidOperationException("Choose one account-mail provider or development pickup, not conflicting configurations.");
        var settings = selected == "Smtp2Go" ? smtp2Go : brevo;
        if (settings is not null)
        {
            AccountMailContract.RequireOrigin(origin);
            var clientName = selected == "Smtp2Go" ? Smtp2GoAccountMail.ClientName : BrevoAccountMail.ClientName;
            services.AddHttpClient(clientName, client => client.Timeout = AccountMailContract.SendTimeout)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
                .RedactLoggedHeaders(_ => true).RemoveAllLoggers();
            services.AddSingleton<IAccountMail>(provider => selected == "Smtp2Go"
                ? new Smtp2GoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, settings)
                : new BrevoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, settings));
        }
        else services.AddSingleton<IAccountMail>(_ => environment.IsDevelopment() && developmentPickup
            ? new DevelopmentAccountMail() : new UnconfiguredAccountMail());
        return services;
    }
}
