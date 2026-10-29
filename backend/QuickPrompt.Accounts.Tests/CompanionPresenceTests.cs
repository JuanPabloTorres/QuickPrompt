using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

public sealed class CompanionPresenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MissingObservationDoesNotInferPresenceFromAnAuthorization()
    {
        Assert.Equal("NOT_OBSERVED", CompanionAvailability.View(null, Now).Status);
        Assert.False(CompanionAvailability.Recent(CompanionAvailability.View(null, Now)));
    }

    [Theory]
    [InlineData("POLLING", 1, "POLLING", true)]
    [InlineData("WORKING", 1, "WORKING", true)]
    [InlineData("POLLING", 0, "STALE", false)]
    [InlineData("WORKING", -1, "STALE", false)]
    [InlineData("STOPPED", 1, "STOPPED", false)]
    public void ServerClockAndSignalDetermineBoundedAvailability(string signal, int seconds, string expected, bool recent)
    {
        var row = new CompanionPresence { Signal = signal, ObservedAt = Now.AddSeconds(-5), ExpiresAt = Now.AddSeconds(seconds) };
        var view = CompanionAvailability.View(row, Now);
        Assert.Equal(expected, view.Status); Assert.Equal(recent, CompanionAvailability.Recent(view));
        Assert.Equal(row.ObservedAt, view.ObservedAt); Assert.Equal(row.ExpiresAt, view.ExpiresAt);
    }

    [Fact]
    public void AbsenceIsTypedAvailabilityRatherThanRevocation()
    {
        Assert.Equal(503, AccountFailureStatus.Classify(new CompanionUnavailableException()));
        Assert.Equal(401, AccountFailureStatus.Classify(new UnauthorizedAccessException()));
        Assert.DoesNotContain("modelo listo", CompanionUnavailableException.MessageText);
    }
}
