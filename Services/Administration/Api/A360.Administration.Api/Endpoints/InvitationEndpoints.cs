using System.Security.Cryptography;
using System.Text;
using A360.Administration.Api.Accounts;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using Microsoft.IdentityModel.Tokens;

namespace A360.Administration.Api.Endpoints;

/// <summary>
/// Invite a pending user by e-mail: POST /api/users/{userId}/invite (administrator) creates a one-time token and e-mails the link;
/// /api/invitations/validate and /accept are public (the invited person has no account yet) and are secured by the token itself.
/// </summary>
public static class InvitationEndpoints
{
    private const string EntityType = "User";

    public sealed record InviteResponse(string InviteLink, bool EmailSent, string Message, DateTime ExpiresAt, IReadOnlyList<string> MissingSmtpSettings);

    public sealed record TokenRequest(string? Token);

    public sealed record AcceptInvitationRequest(string? Token, string? Password);

    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/users/{userId}/invite", InviteAsync).WithName("InviteUser").WithTags("Users");

        var group = routes.MapGroup("/api/invitations").WithTags("Invitations");
        group.MapPost("/validate", ValidateAsync).WithName("ValidateInvitation");
        group.MapPost("/accept", AcceptAsync).WithName("AcceptInvitation");
        return routes;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task<IResult> InviteAsync(
        string userId,
        IUserRepository repository,
        InvitationMailer mailer,
        IConfiguration configuration,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(user.LoginAccountId))
        {
            return Results.Conflict(new { message = $"{user.Name} already has a sign-in account. Nothing to invite." });
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Results.BadRequest(new { message = "The user has no email address." });
        }

        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        user.InviteTokenHash = Hash(token);
        user.InviteExpiresAt = now.AddDays(InvitationMailer.ExpiryDays(configuration));
        user.InvitedAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        if (!await repository.UpdateAsync(user.Id, user, cancellationToken))
        {
            return Results.NotFound();
        }

        var link = mailer.LinkFor(token);
        var result = await mailer.SendAsync(user, link, user.InviteExpiresAt.Value, string.IsNullOrWhiteSpace(user.UpdatedBy) ? "An administrator" : user.UpdatedBy!, cancellationToken);
        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Updated, cancellationToken);

        var message = result.Sent
            ? $"Invitation e-mailed to {user.Email}."
            : $"{result.Problem} The invitation was not e-mailed; copy the link and send it to {user.Email}.";
        return Results.Ok(new InviteResponse(link, result.Sent, message, user.InviteExpiresAt.Value, result.MissingSettings));
    }

    /// <summary>The user an unexpired, unused invitation token belongs to; null otherwise.</summary>
    private static async Task<A360.Administration.Domain.Entities.User?> FindAsync(string? token, IUserRepository repository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
        {
            return null;
        }

        var user = await repository.GetByInviteTokenHashAsync(Hash(token.Trim()), cancellationToken);
        return user is { InviteExpiresAt: { } expires } && expires > DateTime.UtcNow && string.IsNullOrWhiteSpace(user.LoginAccountId) ? user : null;
    }

    private static async Task<IResult> ValidateAsync(TokenRequest request, IUserRepository repository, CancellationToken cancellationToken)
    {
        var user = await FindAsync(request.Token, repository, cancellationToken);
        return user is null
            ? Results.NotFound(new { message = "This invitation link is invalid or has expired. Ask your administrator to send a new one." })
            : Results.Ok(new { name = user.Name, email = user.Email, username = user.Username });
    }

    private static async Task<IResult> AcceptAsync(
        AcceptInvitationRequest request,
        IUserRepository repository,
        LoginAccountClient loginAccounts,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var user = await FindAsync(request.Token, repository, cancellationToken);
        if (user is null)
        {
            return Results.NotFound(new { message = "This invitation link is invalid or has expired. Ask your administrator to send a new one." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Results.BadRequest(new { message = "Password must be at least 8 characters." });
        }

        var login = await loginAccounts.CreateAsync(user, request.Password, cancellationToken);
        if (!login.Succeeded)
        {
            return Results.BadRequest(new { message = login.Error });
        }

        user.LoginAccountId = login.AccountId;
        user.InviteTokenHash = null;
        user.InviteExpiresAt = null;
        user.Status = "Active";
        user.MfaEnabled = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = "invitation";
        if (!await repository.UpdateAsync(user.Id, user, cancellationToken))
        {
            await loginAccounts.DeleteAsync(login.AccountId!, CancellationToken.None);
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Updated, cancellationToken);
        return Results.Ok(new { username = user.Username });
    }
}
