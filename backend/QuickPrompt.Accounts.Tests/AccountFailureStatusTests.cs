using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Accounts.Libraries;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class AccountFailureStatusTests
{
    [Fact]
    public void ConnectionCapacityHasAnActionablePublicErrorWithoutChangingTheLimitOrStatus()
    {
        var error = new ConnectionLimitException();
        Assert.Equal(5, DelegationPolicy.MaximumActiveConnections);
        Assert.Equal(400, AccountFailureStatus.Classify(error));
        Assert.Equal(ConnectionLimitException.MessageText, AccountFailureStatus.PublicTitle(error, 400));
        Assert.Contains("Revoca", AccountFailureStatus.PublicTitle(error, 400));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(409)]
    [InlineData(500)]
    public void UnrecognizedErrorsNeverPublishTheirPrivateMessage(int status)
    {
        var title = AccountFailureStatus.PublicTitle(new Exception("SYNTHETIC_PRIVATE_DETAIL"), status);
        Assert.Equal("No se pudo completar la operación.", title);
    }

    [Theory]
    [InlineData("DAILY_LIMIT", "Alcanzaste el límite diario")]
    [InlineData("OWNER_QUEUE_FULL", "Tu cola está llena")]
    [InlineData("GLOBAL_QUEUE_FULL", "La cola del servicio está llena")]
    public void ExistingExecutionQuotaMessagesRemainPublic(string code, string prefix)
        => Assert.StartsWith(prefix, AccountFailureStatus.PublicTitle(new ExecutionQuotaException(code), 429));

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
