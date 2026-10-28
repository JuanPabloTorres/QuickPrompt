using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class AccountFailureStatusTests
{
    private static NpgsqlException Temporary() => new("PRIVATE_DATABASE_DETAIL", new TimeoutException("PRIVATE_TIMEOUT_DETAIL"));

    [Fact]
    public void DirectAndWrappedTransientDatabaseFailuresAreUnavailable()
    {
        Assert.Equal(503, AccountFailureStatus.Classify(Temporary()));
        Assert.Equal(503, AccountFailureStatus.Classify(new InvalidOperationException("PRIVATE_EF_DETAIL", Temporary())));
        Assert.Equal(500, AccountFailureStatus.Classify(new TimeoutException("NOT_A_DATABASE_FAILURE")));
        Assert.Equal(500, AccountFailureStatus.Classify(new NpgsqlException("NONTRANSIENT")));
    }

    [Fact]
    public void ExplicitDomainOutcomesTakePrecedenceOverAnInnerDatabaseFailure()
    {
        Assert.Equal(400, AccountFailureStatus.Classify(new ArgumentException("PRIVATE_ARGUMENT", Temporary())));
        Assert.Equal(401, AccountFailureStatus.Classify(new UnauthorizedAccessException("PRIVATE_AUTH", Temporary())));
        Assert.Equal(404, AccountFailureStatus.Classify(new LibraryUnavailableException()));
        Assert.Equal(409, AccountFailureStatus.Classify(new LibraryRevisionException()));
        Assert.Equal(429, AccountFailureStatus.Classify(new ExecutionQuotaException("OWNER_QUEUE_FULL")));
    }

    [Theory]
    [InlineData("42501")]
    [InlineData("23514")]
    [InlineData("42601")]
    public void NontransientPostgresErrorsRemainServerErrors(string code)
        => Assert.Equal(500, AccountFailureStatus.Classify(new InvalidOperationException("PRIVATE_EF", new PostgresException("PRIVATE_SQL", "ERROR", "ERROR", code))));

    [Fact]
    public void InnerTraversalIsBounded()
    {
        Exception error = Temporary();
        for (var depth = 0; depth < 8; depth++) error = new InvalidOperationException("PRIVATE_WRAPPER", error);
        Assert.Equal(500, AccountFailureStatus.Classify(error));
    }
}
