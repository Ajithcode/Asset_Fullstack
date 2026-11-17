using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Alerts;
using MongoDB.Driver;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Desktop (Web Push) notifications: the browser's subscription and the notification buttons.</summary>
public static class PushEndpoints
{
    /// <summary>Called by the service worker without a sign-in token; checked with the alert's action token instead.</summary>
    public const string ActionsPath = "/api/push/actions";

    public static RouteGroupBuilder MapPushEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/push").WithTags("Desktop Notifications");

        group.MapGet("/public-key", GetPublicKeyAsync).WithName("GetPushPublicKey");
        group.MapPost("/subscriptions", SubscribeAsync).WithName("SavePushSubscription");
        group.MapPost("/subscriptions/remove", UnsubscribeAsync).WithName("RemovePushSubscription");
        group.MapPost("/actions", ActionAsync).WithName("PushNotificationAction");

        return group;
    }

    public sealed record PushSubscriptionRequest(string? Endpoint, string? P256dh, string? Auth, string? ApiUrl, string? AppUrl, string? ClientId, string? TenantId);

    public sealed record PushUnsubscribeRequest(string? Endpoint);

    /// <param name="Action">"acknowledge" or "remind".</param>
    public sealed record PushActionRequest(string? AlertId, string? Token, string? Action, string? Endpoint);

    private static async Task<IResult> GetPublicKeyAsync(PushSender sender, CancellationToken cancellationToken)
    {
        return Results.Ok(new { publicKey = await sender.GetPublicKeyAsync(cancellationToken) });
    }

    /// <summary>Saves (or refreshes) this browser's subscription for the signed-in user.</summary>
    private static async Task<IResult> SubscribeAsync(
        PushSubscriptionRequest request,
        IMongoDatabase database,
        ICurrentUserAccessor currentUserAccessor,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) || string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Endpoint"] = ["Endpoint, P256dh and Auth are required"] });
        }

        var user = currentUserAccessor.GetCurrentUser();
        var now = DateTime.UtcNow;
        var collection = database.GetCollection<PushSubscriptionDocument>(PushSubscriptionDocument.CollectionName);
        var existing = await collection.Find(s => s.Endpoint == request.Endpoint).FirstOrDefaultAsync(cancellationToken);
        var doc = existing ?? new PushSubscriptionDocument { Endpoint = request.Endpoint!, CreatedAt = now };
        doc.P256dh = request.P256dh!;
        doc.Auth = request.Auth!;
        doc.UserName = user?.UserName ?? doc.UserName;
        doc.ClientId = ContractText.FirstNonEmpty(request.ClientId, user?.ClientId) ?? doc.ClientId;
        doc.TenantId = ContractText.FirstNonEmpty(request.TenantId, user?.TenantId) ?? doc.TenantId;
        doc.ApiUrl = ContractText.TrimOrNull(request.ApiUrl) ?? doc.ApiUrl;
        doc.AppUrl = ContractText.TrimOrNull(request.AppUrl) ?? doc.AppUrl;
        doc.UserAgent = http.Request.Headers.UserAgent.ToString();
        doc.UpdatedAt = now;

        if (existing is null)
        {
            await collection.InsertOneAsync(doc, cancellationToken: cancellationToken);
        }
        else
        {
            await collection.ReplaceOneAsync(s => s.Id == existing.Id, doc, cancellationToken: cancellationToken);
        }

        return Results.Ok(new { subscribed = true });
    }

    private static async Task<IResult> UnsubscribeAsync(PushUnsubscribeRequest request, IMongoDatabase database, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            await database.GetCollection<PushSubscriptionDocument>(PushSubscriptionDocument.CollectionName)
                .DeleteOneAsync(s => s.Endpoint == request.Endpoint, cancellationToken);
        }

        return Results.NoContent();
    }

    /// <summary>Acknowledge / Remind me from a desktop notification.</summary>
    private static async Task<IResult> ActionAsync(
        PushActionRequest request,
        AlertNotifications notifications,
        IMongoDatabase database,
        CancellationToken cancellationToken)
    {
        var action = request.Action?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.AlertId) || string.IsNullOrWhiteSpace(request.Token) || action is not ("acknowledge" or "remind"))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Action"] = ["AlertId, Token and Action (acknowledge or remind) are required"] });
        }

        string? userName = null;
        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            userName = await database.GetCollection<PushSubscriptionDocument>(PushSubscriptionDocument.CollectionName)
                .Find(s => s.Endpoint == request.Endpoint).Project(s => s.UserName).FirstOrDefaultAsync(cancellationToken);
        }

        return await notifications.ApplyActionAsync(request.AlertId.Trim(), request.Token.Trim(), action, userName, cancellationToken)
            ? Results.Ok(new { done = action })
            : Results.NotFound(new { message = "Alert notification not found." });
    }
}
