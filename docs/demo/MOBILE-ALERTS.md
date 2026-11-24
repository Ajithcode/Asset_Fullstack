# Mobile alerts: polling API

The web bell and the mobile app read the same Tracking alert feed. Poll every **10 seconds**. All calls need the normal bearer token (`Authorization: Bearer <jwt>`) and go through the gateway (`/api/alerts/...`).

## GET /api/alerts/feed

| Query | Meaning |
|---|---|
| `since` | ISO-8601 time. Returns alerts **raised or acknowledged after** it (so the app also learns about acknowledgements made elsewhere). Omit for the latest alerts. |
| `status` | Only this status, e.g. `Open` (= unacknowledged) for a "to do" list. |
| `limit` | 1–200, default 20. Newest first. |

Response:

```json
{
  "serverTime": "2026-11-24T07:34:38Z",
  "unacknowledged": 3,
  "items": [{
    "alertId": "AL000074", "raisedAt": "2026-11-24T07:30:01Z", "severity": "High",
    "eventType": "Geofence Exit", "assetId": "AR000151", "assetName": "Defibrillator 04",
    "zone": "Zone A · floor2", "siteName": "Seahouse", "message": "…",
    "status": "Open", "ruleName": "Defibrillator 04 leaves Zone A",
    "channels": ["In-app", "Push"], "sound": true,
    "acknowledgedBy": null, "acknowledgedAt": null
  }]
}
```

Polling loop: store `serverTime` from each response and send it as `since` in the next call (use the server's clock, not the phone's). Show a notification for each item whose `status` is `Open` and whose `channels` contains `In-app`; play the alert sound when `sound` is true. Drop an item from the list when it comes back with another status. `unacknowledged` is the number for the app-icon badge.

## POST /api/alerts/{alertId}/acknowledge

No body. Acknowledges one Open alert as the signed-in user (`acknowledgedBy` / `acknowledgedAt` are stored, the alert leaves the active list and shows in Geofence & Alerts › Event History). Returns `{ alertId, status, acknowledgedBy, acknowledgedAt }`; 404 for an unknown id; acknowledging twice is harmless.

## POST /api/alerts/acknowledge-all

Acknowledges every Open alert. Returns `{ acknowledged: <count> }`.

## GET /api/alerts/channels

`{ inApp: {available}, push: {available}, email: {available, missing: [...]} }`. `email.available` is false until the server has its `Smtp__*` settings.

## Notes

- Alert status values: `Open` (unacknowledged), `Acknowledged`, `Assigned`, `In Progress`, `Resolved`, `Closed`.
- One exit = one alert: after an exit alert the same rule does not alert again for that asset until the asset has been back in the zone for `Alerts__ReArmSeconds` (default 120) or the rule's cool-down (minutes) has passed.
- The existing Web Push desktop notifications (`/api/push/*`) are separate and unchanged.
