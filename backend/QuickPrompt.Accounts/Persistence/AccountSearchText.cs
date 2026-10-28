using Microsoft.EntityFrameworkCore;

namespace QuickPrompt.Accounts.Persistence;

/// <summary>SQL-only Latin accent folding for recall. Stored definitions and authorization remain unchanged.</summary>
internal static class AccountSearchText
{
    public static string Latin(string value) => throw new InvalidOperationException("Search folding must execute in PostgreSQL.");
    internal static void Configure(ModelBuilder model) => model.HasDbFunction(typeof(AccountSearchText).GetMethod(nameof(Latin))!)
        .HasSchema("qp_library").HasName("search_text_latin");
}
