using QuickPrompt.Local;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>Per-request local operator assertion. It grants nothing without the matching persisted device profile.</summary>
internal static class PairedReasoningHeaders
{
    internal static bool Read(HttpRequest request)
    {
        var profiles = request.Headers[PairedReasoningProtocol.ProfileHeader];
        if (profiles.Count == 0) return false;
        if (profiles.Count != 1 || profiles[0] != PairedReasoningProtocol.Profile)
            throw new ValidationException("El perfil de razonamiento anunciado no es compatible.");
        return true;
    }
    internal static void Acknowledge(HttpResponse response, bool asserted)
    {
        if (asserted) response.Headers[PairedReasoningProtocol.AcceptedHeader] = PairedReasoningProtocol.Profile;
    }
}
