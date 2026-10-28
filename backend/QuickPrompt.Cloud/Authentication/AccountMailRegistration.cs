using Microsoft.Extensions.Hosting;

namespace QuickPrompt.Cloud.Authentication;

/// <summary>Optional external delivery; development pickup is an explicit, mutually exclusive Development choice.</summary>
public static class AccountMailRegistration
{
    public static IServiceCollection AddAccountMail(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, Uri origin)
    {
        var settings = BrevoAccountMailSettings.Read(configuration);
        var developmentPickup = configuration.GetValue<bool>("QuickPrompt:Accounts:DevelopmentMail");
        if (settings is not null)
        {
            if (developmentPickup) throw new InvalidOperationException("Choose external account mail or development pickup, not both.");
            BrevoAccountMail.RequireOrigin(origin);
            services.AddHttpClient(BrevoAccountMail.ClientName, client => client.Timeout = BrevoAccountMail.SendTimeout)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
                .RedactLoggedHeaders(_ => true).RemoveAllLoggers();
            services.AddSingleton<IAccountMail>(provider => new BrevoAccountMail(provider.GetRequiredService<IHttpClientFactory>(), origin, settings));
        }
        else services.AddSingleton<IAccountMail>(_ => environment.IsDevelopment() && developmentPickup
            ? new DevelopmentAccountMail() : new UnconfiguredAccountMail());
        return services;
    }
}
