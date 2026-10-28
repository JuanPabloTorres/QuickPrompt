using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Sharing;
using QuickPrompt.Accounts.Memory;

namespace QuickPrompt.Accounts;

public static class AccountModule
{
    /// <summary>Explicit remote-account composition. Caller supplies the limited application data source.</summary>
    public static IdentityBuilder AddQuickPromptAccounts(this IServiceCollection services, NpgsqlDataSource applicationData)
    {
        services.AddDbContext<AccountDatabase>(options => options.UseNpgsql(applicationData,
            postgres => postgres.CommandTimeout(10).MigrationsHistoryTable("history", "qp_migrations")));
        services.AddScoped<LibraryAccess>();
        services.AddScoped<SharingAccess>();
        services.AddScoped<AccountAgentMemory>();
        services.AddScoped<AccountEditorMemory>();
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AccountDatabase>().ReplaceDefaultEntities<Guid>());
        return services.AddIdentityCore<AccountUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.SignIn.RequireConfirmedEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<AccountDatabase>().AddSignInManager().AddDefaultTokenProviders();
    }
}
