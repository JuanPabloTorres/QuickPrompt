using QuickPrompt.Accounts.Delegation;

namespace QuickPrompt.Accounts.Tests;

public sealed partial class AccountHttpTests
{
    // Consent-only helpers remain separate. Positive worker fixtures explicitly perform
    // the same authenticated empty HTTP poll as a running client, without seeding rows.
    private static async Task<(ConnectionView Connection, TokenDocument Token)> PollingProposalDevice(HttpClient owner, HttpClient device)
    {
        var grant = await ProposalDevice(owner, device);
        await ObserveProposalPolling(device);
        return grant;
    }

    private static async Task ObserveProposalPolling(HttpClient device)
    {
        using var response = await ClaimProposal(device);
        PresenceObservation(response);
    }

    private static async Task<ConnectionView> PollingLocalDevice(HttpClient owner, HttpClient device)
    {
        var grant = await LocalDevice(owner, device);
        using var response = await ClaimLocal(device);
        PresenceObservation(response);
        return grant;
    }
}
