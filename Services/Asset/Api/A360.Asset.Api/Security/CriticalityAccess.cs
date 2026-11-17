using System.Security.Claims;
using A360.Security;

namespace A360.Asset.Api.Security;

/// <summary>
/// Who may do what in Asset Criticality (checked by the API, not only the menu):
/// User assesses; Manager also approves Low / Medium / High; Administrator also approves Critical, any lowering of an
/// asset's criticality, and manages the configuration. Anyone signed in may view. The role is the token's "role"
/// claim (the user's primary role). Applies to /api/asset-criticality only.
/// </summary>
public static class CriticalityAccess
{
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Administrator = "Administrator";

    private static readonly string[] Ranked = [User, Manager, Administrator];

    /// <summary>0 = signed in with another role, 1 = User, 2 = Manager, 3 = Administrator.</summary>
    public static int RankOf(ClaimsPrincipal principal) =>
        principal.FindAll(A360ClaimTypes.Role)
            .Select(claim => Array.FindIndex(Ranked, role => role.Equals(claim.Value.Trim(), StringComparison.OrdinalIgnoreCase)) + 1)
            .DefaultIfEmpty(0)
            .Max();

    public static bool CanAssess(ClaimsPrincipal principal) => RankOf(principal) >= 1;

    public static bool CanConfigure(ClaimsPrincipal principal) => RankOf(principal) >= 3;

    /// <summary>
    /// Approving (or directly publishing) <paramref name="level"/>: Critical, or lower than
    /// <paramref name="previousLevel"/>, needs an Administrator; Low / Medium / High a Manager.
    /// </summary>
    public static bool CanApprove(ClaimsPrincipal principal, string? level, string? previousLevel)
    {
        var needed = IsCritical(level) || IsReduction(previousLevel, level) ? 3 : 2;
        return RankOf(principal) >= needed;
    }

    public static bool IsReduction(string? from, string? to) =>
        LevelRank(from) > 0 && LevelRank(to) > 0 && LevelRank(to) < LevelRank(from);

    /// <summary>1 = Low … 4 = Critical; 0 for anything else.</summary>
    public static int LevelRank(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "low" => 1,
        "medium" => 2,
        "high" => 3,
        "critical" => 4,
        _ => 0
    };

    private static bool IsCritical(string? level) => LevelRank(level) == 4;

    public static IResult Forbidden(string message) =>
        Results.Json(new { message }, statusCode: StatusCodes.Status403Forbidden);

    /// <summary>Endpoint filter for the whole group: 401 without a signed-in user.</summary>
    public static RouteGroupBuilder RequireSignedIn(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (context, next) =>
            context.HttpContext.User.Identity?.IsAuthenticated == true
                ? await next(context)
                : Results.Json(new { message = "Sign in to continue." }, statusCode: StatusCodes.Status401Unauthorized));
        return group;
    }

    /// <summary>Endpoint filter: 403 unless the caller's role may assess (User, Manager or Administrator).</summary>
    public static RouteHandlerBuilder RequireAssessor(this RouteHandlerBuilder builder)
    {
        builder.AddEndpointFilter(async (context, next) =>
            CanAssess(context.HttpContext.User)
                ? await next(context)
                : Forbidden("Your role doesn't allow you to assess asset criticality."));
        return builder;
    }
}
