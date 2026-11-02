using QuickPrompt.Nova.Application;
using Xunit;
using Xunit.Abstractions;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Captures only the synthetic fixture's schema policy, never account content or credentials.</summary>
public sealed class PairedReasoningSchemaCaptureTests(PostgresFixture postgres, ITestOutputHelper output) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task NewPresencePolicyRetainsOwnerPredicatesAndCapturesItsExactDeparsedFingerprint()
    {
        await using var command = postgres.Application.CreateCommand("""
            SELECT qual,with_check,encode(sha256(convert_to(qual || E'\n' || with_check,'UTF8')),'hex')
            FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence' AND policyname='companion_presence_actor';
            """);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
        var qual = reader.GetString(0); var check = reader.GetString(1); var hash = reader.GetString(2);
        Assert.Contains("\"OwnerId\"", qual); Assert.Contains("quickprompt.actor_id", qual);
        Assert.Contains(PairedReasoningProtocol.GrantProfile, check); Assert.Contains("c.\"OwnerId\"", check);
        Assert.Contains("c.\"LibraryId\" IS NULL", check);
        Assert.Equal("40e71595196dd4db30a906d50253d0834f35f91ff2adaf58e8d4e90a901d8d72", hash);
        output.WriteLine("PolicySha256=" + hash); output.WriteLine("USING=" + qual); output.WriteLine("WITH_CHECK=" + check);
        Assert.False(await reader.ReadAsync());
    }
}
