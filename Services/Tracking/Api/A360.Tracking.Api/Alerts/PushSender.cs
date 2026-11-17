using System.Net;
using System.Text.Json;
using MongoDB.Driver;
using WebPush;

namespace A360.Tracking.Api.Alerts;

/// <summary>
/// Sends Web Push messages (desktop notifications) to subscribed browsers. The VAPID key pair is generated once and
/// stored in the database (PushSettings), so it survives restarts and every instance uses the same keys.
/// </summary>
public sealed class PushSender(IMongoDatabase database, IConfiguration configuration, ILogger<PushSender> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _keyLock = new(1, 1);
    private PushKeysDocument? _keys;

    private IMongoCollection<PushSubscriptionDocument> Subscriptions =>
        database.GetCollection<PushSubscriptionDocument>(PushSubscriptionDocument.CollectionName);

    private string Subject => configuration["Push:Subject"] ?? "mailto:support@assets360.com";

    public async Task<string> GetPublicKeyAsync(CancellationToken cancellationToken) => (await GetKeysAsync(cancellationToken)).PublicKey;

    /// <summary>Sends one message to every browser of the tenant; drops subscriptions the push service no longer knows.</summary>
    public async Task<int> SendAsync(string? tenantId, Func<PushSubscriptionDocument, object> payload, CancellationToken cancellationToken)
    {
        var keys = await GetKeysAsync(cancellationToken);
        var filter = string.IsNullOrWhiteSpace(tenantId)
            ? Builders<PushSubscriptionDocument>.Filter.Empty
            : Builders<PushSubscriptionDocument>.Filter.Or(
                Builders<PushSubscriptionDocument>.Filter.Eq(s => s.TenantId, tenantId),
                Builders<PushSubscriptionDocument>.Filter.Eq(s => s.TenantId, null));
        var subscriptions = await Subscriptions.Find(filter).ToListAsync(cancellationToken);
        if (subscriptions.Count == 0)
        {
            return 0;
        }

        var client = new WebPushClient();
        var vapid = new VapidDetails(Subject, keys.PublicKey, keys.PrivateKey);
        var sent = 0;
        foreach (var subscription in subscriptions)
        {
            try
            {
                var message = JsonSerializer.Serialize(payload(subscription), Json);
                await client.SendNotificationAsync(new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth), message, vapid, cancellationToken);
                sent++;
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                // The browser unsubscribed or the subscription expired
                await Subscriptions.DeleteOneAsync(s => s.Id == subscription.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push to {Endpoint} failed", subscription.Endpoint[..Math.Min(60, subscription.Endpoint.Length)]);
            }
        }

        return sent;
    }

    private async Task<PushKeysDocument> GetKeysAsync(CancellationToken cancellationToken)
    {
        if (_keys is not null)
        {
            return _keys;
        }

        await _keyLock.WaitAsync(cancellationToken);
        try
        {
            if (_keys is not null)
            {
                return _keys;
            }

            var collection = database.GetCollection<PushKeysDocument>(PushKeysDocument.CollectionName);
            var keys = await collection.Find(k => k.Id == PushKeysDocument.SingletonId).FirstOrDefaultAsync(cancellationToken);
            if (keys is null)
            {
                var generated = VapidHelper.GenerateVapidKeys();
                keys = new PushKeysDocument { PublicKey = generated.PublicKey, PrivateKey = generated.PrivateKey, CreatedAt = DateTime.UtcNow };
                try
                {
                    await collection.InsertOneAsync(keys, cancellationToken: cancellationToken);
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Another instance created them first
                    keys = await collection.Find(k => k.Id == PushKeysDocument.SingletonId).FirstAsync(cancellationToken);
                }
            }

            _keys = keys;
            return keys;
        }
        finally
        {
            _keyLock.Release();
        }
    }
}
