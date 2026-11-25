using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Roam.Api.Security;

/// <summary>
/// Replaces the VolunteerStatus, AccountStatus and role claims baked into an access token with the
/// user's current values, so authorization policies, hubs and endpoints never act on stale claims
/// (for example a revoked volunteer or a removed admin) for the remaining lifetime of the token.
/// </summary>
public sealed class UserStateClaimsTransformation : IClaimsTransformation
{
    internal const string VolunteerStatusClaim = "VolunteerStatus";
    internal const string AccountStatusClaim = "AccountStatus";
    private const string RefreshedMarker = "roam:state_refreshed";

    private readonly IUserStateCache _userState;

    public UserStateClaimsTransformation(IUserStateCache userState)
    {
        _userState = userState;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // TransformAsync may run more than once per request; only rewrite once.
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(RefreshedMarker, "1"))
            return principal;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return principal;

        var state = await _userState.GetAsync(userId);
        if (state == null)
            return principal; // Unknown user: leave as-is; AccountStatusMiddleware/endpoints handle it.

        var source = principal.Identities.First(i => i.IsAuthenticated);
        var roleType = source.RoleClaimType;

        var identity = new ClaimsIdentity(
            source.Claims.Where(c => c.Type != VolunteerStatusClaim && c.Type != AccountStatusClaim && c.Type != roleType),
            source.AuthenticationType,
            source.NameClaimType,
            roleType);

        identity.AddClaim(new Claim(VolunteerStatusClaim, state.VolunteerStatus.ToString()));
        identity.AddClaim(new Claim(AccountStatusClaim, state.AccountStatus.ToString()));
        foreach (var role in state.Roles)
            identity.AddClaim(new Claim(roleType, role));
        identity.AddClaim(new Claim(RefreshedMarker, "1"));

        return new ClaimsPrincipal(identity);
    }
}
