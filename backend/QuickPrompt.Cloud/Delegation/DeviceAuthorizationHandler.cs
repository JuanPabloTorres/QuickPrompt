using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using QuickPrompt.Accounts.Delegation;
using QuickPrompt.Nova.Application;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuickPrompt.Cloud.Delegation;

/// <summary>Enriches the library-owned device flow with a fixed local-model profile, not domain authority.</summary>
public sealed class DeviceAuthorizationHandler(DelegationOrigin origin) : IOpenIddictServerHandler<OpenIddictServerEvents.HandleDeviceAuthorizationRequestContext>
{
    public ValueTask HandleAsync(OpenIddictServerEvents.HandleDeviceAuthorizationRequestContext context)
    {
        var request = context.Request;
        var label = (string?)request.GetParameter("device_name");
        var profile = (string?)request.GetParameter("profile_id");
        if (request.ClientId != DelegationPolicy.ClientId || label is not { Length: > 0 and <= 80 }
            || !LocalCompanionProfiles.Known(profile)
            || !request.HasScope(LocalCompanionProfiles.Scope(profile!))
            || request.GetScopes().Any(x => x != LocalCompanionProfiles.Scope(profile!) && x != Scopes.OfflineAccess)
            || !request.GetResources().SequenceEqual([origin.ForProfile(profile!)]))
            context.Reject(Errors.InvalidRequest, "La vinculación no corresponde al perfil permitido.");
        else
        {
            // A device request has no authenticated person yet. OpenIddict requires
            // an anonymous principal here; identity is attached only after consent.
            var identity = new ClaimsIdentity();
            identity.SetClaim(DelegationPolicy.LabelClaim, label);
            identity.SetClaim(DelegationPolicy.ProfileClaim, profile);
            identity.SetScopes(request.GetScopes()); identity.SetResources(origin.ForProfile(profile!));
            context.SignIn(new ClaimsPrincipal(identity));
        }
        return ValueTask.CompletedTask;
    }
}
