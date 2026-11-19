# Asset360 – Mobile API handoff: Real-Time Location + Locate (blink / beep)

Generated 2026-11-19 from the code on the demo branches of `Ajithcode/Asset_Fullstack`
(backend `demo/customer-api` @ `a93108b5`, frontend `demo/customer` @ `ce1b84c0`).
Base URL for testing: **https://a360demo.purpleiq.ai** (dev stack, DB `Asset360Dev`).
No credentials are included in this document. Ask the project owner for a test user.

All sample JSON is trimmed and uses made-up values.

---

## 0. Basics every call needs

### 0.1 Sign in (JWT)

The web app signs in with username + password, then a Google Authenticator (TOTP) code.

| Step | Call | Body | Response |
|---|---|---|---|
| 1 | `POST /api/auth/login` | `{"userName":"…","password":"…"}` | `LoginResponse`, see below |
| 2 | `POST /api/auth/verify-totp` | `{"userName":"…","loginToken":"<from step 1>","code":"123456"}` | `AuthTokenResponse` |
| Refresh | `POST /api/auth/refresh` | `{"refreshToken":"…"}` | `AuthTokenResponse` |
| Who am I | `GET /api/auth/me` | – | user |
| Logout | `POST /api/auth/logout` | – | – |

```jsonc
// POST /api/auth/login → 200
{ "twoFactorRequired": true, "method": "totp", "message": "…", "otpExpiresInSeconds": 300,
  "loginToken": "eyJ…", "setupRequired": false, "qrCodeImage": null, "manualEntryKey": null }
// setupRequired=true → the user has no authenticator yet: show qrCodeImage / manualEntryKey, then verify-totp.

// POST /api/auth/verify-totp → 200
{ "message": "Signed in.", "accessToken": "eyJ…", "accessTokenExpiresAt": "2026-11-19T12:00:00Z",
  "refreshToken": "r-…", "refreshTokenExpiresAt": "2026-11-26T11:00:00Z", "tokenType": "Bearer",
  "user": { "id": "…", "userId": "USR-0001", "userName": "demo.user", "email": "user@example.com",
            "roleName": "User", "userRoleId": "…", "clientId": "CLT-1001", "tenantId": "TENANT-DEFAULT" } }
```

Contract file: `backend/Services/UserAccount/Api/A360.UserAccount.Api/Contracts/AuthContracts.cs`.

### 0.2 Headers and tenant query parameters

* REST: `Authorization: Bearer <accessToken>`. Without it every `/api/*` call returns **401**.
* Every Tracking / Administration call from the web also sends `?clientId=CLT-1001&tenantId=TENANT-DEFAULT`.
  Use the `clientId` / `tenantId` from the `user` object of the sign-in response.
  (Web helper: `tenantParams()` in `frontend/projects/tracking-iot/src/app/shared/tracking-api.ts`.)
* Every backend URL is same-origin: `https://a360demo.purpleiq.ai/api/...`. Caddy sends `/api/*` and `/ws/*` to the API gateway, and the gateway routes them to the right service.
* Errors: `{ "message": "…" }` or ASP.NET validation `{ "title": "…", "errors": { "Field": ["…"] } }`.

---

## 1. Real-Time Location page: every API it calls

Web page: **Tracking & IoT › Real-Time Location** (`/tracking-iot/real-time-location`).
Code: `frontend/projects/tracking-iot/src/app/real-time-location/real-time-location.ts`.

| # | What it feeds | Method + path | Query params | When the web calls it |
|---|---|---|---|---|
| 1 | KPI counters, asset list/markers, gateway list | `GET /api/real-time-location/overview` | `clientId`, `tenantId`, `siteId` (optional), `floorPlanId` (optional: the plan shown on the map; empty = all floors) | on load + **every 10 s** |
| 2 | Recent location events table | `GET /api/real-time-location/events` | `clientId`, `tenantId`, `range` = `1h`\|`24h` (default)\|`7d`, `siteId`, `page` (1…), `pageSize` (1–100, web uses 10/25/50), `assetId` (optional), `eventType` (optional) | on load, page/filter change |
| 3 | Selected asset's zone trail | `GET /api/real-time-location/assets/{assetId}/trail` | `clientId`, `tenantId`, `range` = `30m` (default)\|`1h`\|`24h`\|`7d` | when an asset is selected |
| 4 | Floor dropdown (Site › Building › Floor) | `GET /api/master-data/sites`, `GET /api/master-data/buildings`, `GET /api/master-data/floors`, `GET /api/master-data/zones` | none | once |
| 5 | Floor plan per floor | `GET /api/administration/maps/floor-plans` | `clientId`, `tenantId` | once |
| 6 | Zones drawn on the plan | `GET /api/administration/maps/floor-plans/{floorPlanId}/zones` | `clientId`, `tenantId` | on floor change |
| 7 | Gateways / assets plotted on the plan (x/y) | `GET /api/administration/maps/floor-plans/{floorPlanId}/plots` | `clientId`, `tenantId`, optional `kind` = `device`\|`asset` | on floor change |
| 8 | Live "assets per gateway" counts on the map | **WebSocket** `wss://a360demo.purpleiq.ai/api/dashboards/network-status/gateways/live?access_token=<JWT>` | `access_token` | kept open, reconnect with backoff 1 s…30 s |

Other related endpoints (not used by this page, but useful):
* `GET /api/tracking-telemetry/...`: the Tracking & Telemetry page, same location engine.
* `GET|POST|PUT|DELETE /api/location-events[/{eventId}]`: older manual location-event CRUD (`location-event-api.service.ts`). The Real-Time Location page now uses #2 instead.
* `GET /api/asset-registrations?clientId&tenantId&page=1&pageSize=500`: registered asset list.

### 1.1 How location works (read this first)

* **BLE proximity, no coordinates.** A tag's location is the **zone of the gateway that hears it strongest** (`NetworkTimeSeries` readings). The backend never returns a lat/lng or metre x/y for an asset.
* To draw a marker, put the asset at its **gateway's plot position**: `location.gatewayId` → find the plot with `kind:"device"` and `itemId`/`code` = gateway id in call #7 → `x`,`y` (percent of the plan image, 0–100, measured from the top-left corner). Several assets on one gateway share that point, so spread or cluster them on the client.
* Thresholds (backend `Telemetry/RealTimeLocationService.cs`, `TelemetryOverviewService.cs`):
  * **freshness**: `Live` (heard ≤ 2 min), `Stale` (≤ 10 min), `Offline` (older), `Unknown` (never heard).
  * **movement**: `Moving` (zone changed in the last 5 min), `Stationary` (live, same zone for 5 min), `Unknown` (not live).
  * **accuracy**: `Zone level` (gateway plotted inside a zone), `Gateway range` (gateway not in a zone), `Unknown` (no location).
  * **confidence** (last 2 min of readings): `High` (one gateway hears it, or the strongest is ≥ 8 dB above the next), `Medium` (≥ 3 dB), `Low` (< 3 dB, or strongest RSSI < −90 dBm), `Unknown` (not live).

### 1.2 `GET /api/real-time-location/overview`

```jsonc
{
  "generatedAt": "2026-11-19T10:15:00Z",
  "kpis": { "assetsInView": 3, "moving": 1, "stationary": 1, "unknown": 1, "locationAlerts": 1 },
  "assets": [
    {
      "assetId": "AR-000123",              // asset registration id (used for trail / events filter)
      "assetTag": "PUMP-01",
      "name": "Cooling Pump 01",
      "assetType": "Pump",
      "operationalStatus": "Active",
      "technology": "BLE",
      "tracker": "C30000AABB01",           // first linked tag id (upper case)
      "tags": ["C30000AABB01"],            // all linked tag ids, needed for Locate (section 2)
      "location": {
        "gatewayId": "AC233FC0FFEE",
        "gatewayName": "GW Hall A",
        "zoneId": "zone-a1", "zoneName": "Hall A",
        "floorName": "Ground Floor", "buildingName": "Block 1",
        "siteId": "site-1", "siteName": "Head Office",
        "floorPlanId": "fp-001",
        "locationPath": "Head Office › Block 1 › Ground Floor › Hall A",
        "method": "Estimated",             // Estimated (BLE gateway zone) | Measured (GPS)
        "basis": "Strongest gateway",      // text shown under the location
        "accuracy": "Zone level",
        "at": "2026-11-19T10:14:52Z"
      },
      "freshness": "Live",                 // Live | Stale | Offline | Unknown
      "movement": "Moving",                // Moving | Stationary | Unknown
      "lastZoneChangeAt": "2026-11-19T10:12:30Z",
      "lastValidLocationAt": "2026-11-19T10:14:52Z",
      "locationSource": "BLE Proximity",
      "accuracy": "Zone level",            // Zone level | Gateway range | Unknown
      "confidence": "High",                // High | Medium | Low | Unknown
      "confidenceBasis": "Strongest gateway 9 dB above the next of 2 hearing it",
      "gatewaysHearing": 2,
      "signalMarginDb": 9.0,               // null when < 2 gateways
      "rssi": -61,                         // dBm, latest
      "activeLocationAlerts": 1,
      "expected": {                        // location registered on the asset (where it should be)
        "zoneId": "zone-a1", "zoneName": "Hall A", "floorId": "fl-1", "floorName": "Ground Floor",
        "buildingName": "Block 1", "siteName": "Head Office", "locationPath": "Head Office › Block 1 › Ground Floor › Hall A"
      },
      "comparisonStatus": "Match",         // Match | Mismatch | Unknown | Not set
      "comparisonNote": "Detected in its registered zone"
    }
  ],
  "gateways": [
    { "deviceId": "AC233FC0FFEE", "name": "GW Hall A", "type": "Gateway", "model": "G1",
      "connectivity": "Online",            // Online | Idle | Offline (same 2 / 10 min rule)
      "lastSeenAt": "2026-11-19T10:14:55Z", "tagsHeard": 4,
      "zoneName": "Hall A", "floorName": "Ground Floor", "floorPlanId": "fp-001",
      "plotted": true,                      // has an x/y on a floor plan
      "siteId": "site-1" }
  ]
}
```

Web counter mapping: **In view** = `kpis.assetsInView` (= moving + stationary + unknown), **Moving** = `kpis.moving`, **Stationary** = `kpis.stationary`, **Alerts** = `kpis.locationAlerts` (open geofence / location alerts of assets in view).

### 1.3 `GET /api/real-time-location/events`

```jsonc
{
  "items": [
    { "eventId": "ENT-202611191012-BB01", "at": "2026-11-19T10:12:30Z",
      "assetId": "AR-000123", "assetName": "Cooling Pump 01",
      "eventType": "Zone Entry",           // Zone Entry | Zone Exit | Floor Change | Location Detected |
                                            // Stale Location | Location Lost | geofence alert types (e.g. Geofence Entry)
      "zone": "Hall A", "floor": "Ground Floor",
      "details": "Entered from Store Room",
      "severity": "Info",                  // Info | Warning | Critical …
      "source": "C30000AABB01",            // tag id or device id
      "status": "Recorded" }               // Recorded | Active | Open | Acknowledged | Resolved …
  ],
  "total": 37, "page": 1, "pageSize": 10, "totalPages": 4,
  "rangeLabel": "Last 24 hours",
  "eventTypes": ["Location Detected", "Zone Entry", "Zone Exit"]   // for the filter dropdown
}
```

### 1.4 `GET /api/real-time-location/assets/{assetId}/trail`

404 `{ "message": "No tracked tags are linked to '…'." }` when the asset has no tag.

```jsonc
{
  "assetId": "AR-000123", "name": "Cooling Pump 01", "range": "30m",
  "from": "2026-11-19T09:45:00Z", "to": "2026-11-19T10:15:00Z",
  "segments": [
    { "gatewayId": "AC233FC0BEEF", "zoneName": "Store Room", "floorName": "Ground Floor",
      "from": "2026-11-19T09:45:00Z", "to": "2026-11-19T10:12:30Z", "minutes": 27.5, "initial": true },
    { "gatewayId": "AC233FC0FFEE", "zoneName": "Hall A", "floorName": "Ground Floor",
      "from": "2026-11-19T10:12:30Z", "to": "2026-11-19T10:15:00Z", "minutes": 2.5, "initial": false }
  ],
  "zoneChanges": 1,
  "timeInZones": [ { "zone": "Store Room", "minutes": 27.5 }, { "zone": "Hall A", "minutes": 2.5 } ],
  "note": "BLE proximity: zone transitions, not a precise path."
}
```
`initial: true` means the segment started before the range (the asset was already there).

### 1.5 Floor plans, zones, plots (Administration service)

`GET /api/administration/maps/floor-plans?clientId&tenantId` returns an array or `{ "items": [...] }`. Handle both.
```jsonc
[ { "floorPlanId": "fp-001", "locationId": "fl-1",    // locationId = Master Data floor id
    "name": "Ground Floor plan", "format": "png", "fileUrl": "/uploads/maps/fp-001.png",
    "widthMeters": 60, "heightMeters": 40, "status": "Published",   // Draft | Published | Archived
    "updatedAt": "2026-11-18T08:00:00Z", "createdAt": "2026-11-10T08:00:00Z" } ]
```
Web rule: per floor, use the `Published` plan, else the newest non-archived plan that has a `fileUrl`. PDF/DWG plans have no image. In that case the web falls back to the Master Data floor `imageUrl`/`images[0]`.

`GET /api/administration/maps/floor-plans/{floorPlanId}/zones`
```jsonc
[ { "zoneId": "zone-a1", "code": "Z-A1", "name": "Hall A", "type": "Production",   // type/accessRule containing "restrict" = restricted
    "color": "#7c3aed", "shape": "Rectangle",      // Rectangle | Circle | Polygon (or Custom with points)
    "x": 10, "y": 15, "w": 30, "h": 25,            // % of plan; rectangle top-left + size
    "points": [ {"x":10,"y":15}, {"x":40,"y":15}, {"x":40,"y":40} ],   // polygon, % of plan
    "radius": null,                                 // circle radius in % of plan WIDTH; centre = x,y
    "accessRule": null, "active": true } ]
```

`GET /api/administration/maps/floor-plans/{floorPlanId}/plots[?kind=device|asset]`
```jsonc
[ { "kind": "device", "itemId": "AC233FC0FFEE", "code": "AC233FC0FFEE", "name": "GW Hall A",
    "type": "Gateway", "assetType": null, "status": "Active",
    "x": 22.5, "y": 30.0,                            // % of plan from top-left; null = not placed
    "zoneId": "zone-a1" } ]
```

Master Data (`/api/master-data/sites|buildings|floors|zones`, no query params): rows with `id`, `name`, `code`, `status`, `isDeleted`. Floors also have `buildingId`, `buildingName`, `site` (`{id}` or string), `imageUrl`, `images[]`. Zones have `floorId`. Skip rows with `isDeleted:true` or `status:"Deleted"`.

### 1.6 Live gateway feed (WebSocket)

`wss://a360demo.purpleiq.ai/api/dashboards/network-status/gateways/live?access_token=<JWT>`. The server pushes a full array whenever the data changes:
```jsonc
[ { "gatewayId": "ac233fc0ffee", "assetCount": 2,
    "assets": [ { "assetId": "AR-000123", "assetName": "Cooling Pump 01" } ] } ]
```
Match `gatewayId` case-insensitively against plot `itemId`/`code`. Optional for mobile: polling `overview` every 10 s gives the same picture.

---

## 2. Locate command (blink LED / beep buzzer)

**Status in the web UI:** the backend feature exists (Tracking service, commit `5ac3a4b2` + token requirement `90d1b6d`). **No page in the `demo/customer` frontend sends LED/buzzer commands yet.** The "Locate" button on Real-Time Location only centres and highlights the asset on the map. The protocol below is the backend contract, taken from `LocatingSession.cs` / `LocateCommands.cs`. Mobile would be the first UI client.

### 2.1 Endpoints

| Purpose | URL |
|---|---|
| Command channel (WebSocket) | `wss://a360demo.purpleiq.ai/ws/locate?access_token=<JWT>[&tagid=<tagId>]` |
| Which gateway hears a tag (REST) | `GET /api/locating/tags/{tagid}/gateway` |
| Command history + outcome (REST) | `GET /api/locating/tags/{tagid}/commands?limit=20` (1–100) |
| Broker connection health | `GET /api/locating/health` → `{ "mqttConnected": true }` |

**There is no REST endpoint to send a command.** Commands go only over the WebSocket.

**Auth:** you can send the JWT as `?access_token=` (browsers can't set headers on a WebSocket; the backend reads the query token only on WebSocket requests) **or** as an `Authorization: Bearer` header (native mobile WS clients can do that, which is preferred because it keeps the token out of URLs and logs). With no token or an invalid one you get HTTP **401** and no socket. If the token expires, reconnect with a fresh token. The server sends a WS ping every 30 s.

**Tag id format:** case-insensitive; `:` and `-` are stripped (`C3:00:00:AA:BB:01` → `c30000aabb01`). Use the ids from `overview.assets[].tags`.

`GET /api/locating/tags/{tagid}/gateway` → 200 / 404:
```jsonc
{ "tagid": "c30000aabb01", "gatewayid": "ac233fc0ffee", "lastSeen": "2026-11-19T10:14:52Z", "rssi": -61,
  "topics": { "action": "/gw/ac233fc0ffee/action", "response": "/gw/ac233fc0ffee/response" } }
```
The gateway is the one with the strongest RSSI among readings within 30 s of the tag's newest reading.

### 2.2 Client → server messages (JSON text frames)

```jsonc
// subscribe to a tag (also: send the bare string "c30000aabb01", or pass &tagid= in the URL)
{ "type": "subscribe", "tagid": "c30000aabb01" }

// LED blink (all options optional; defaults shown)
{ "type": "led", "tagid": "c30000aabb01", "color": 3, "onMs": 300, "offMs": 300, "cycles": 10, "brightness": 100 }

// Buzzer beep (defaults shown)
{ "type": "buzzer", "tagid": "c30000aabb01", "beepMs": 300, "pauseMs": 300, "cycles": 5, "level": 0 }

// LED + buzzer in one gateway message. type "both" or "locate" (also "ledbuzzer", "led+buzzer", "led_buzzer")
{ "type": "locate", "tagid": "c30000aabb01",
  "led":    { "color": 3, "onMs": 200, "offMs": 200, "cycles": 25, "brightness": 100 },
  "buzzer": { "beepMs": 200, "pauseMs": 200, "cycles": 25, "level": 0 } }
// (flat options on "both" apply the same value, e.g. "cycles", to LED and buzzer)

{ "type": "unsubscribe" }
```
* `tagid` on a command is optional once you are subscribed. If you give a different tag, the server switches to that tag first.
* Option aliases the server also accepts: LED `colour`, `light_on`/`lightOn`/`onTime`, `light_off`/`lightOff`/`offTime`, `blinkCount`/`count`. Buzzer `work`/`beepTime`, `idle`/`pause`, `beepCount`/`count`, `toneLevel`/`tone`. Numbers may be sent as JSON numbers or numeric strings.
* There is also `{ "type":"action", "payload":{…} }`, which publishes a raw payload to the gateway. It is for debugging only; don't use it from mobile.

**Option values (everything the code accepts):**

| Field | Range | Default | Notes |
|---|---|---|---|
| LED `color` | 0–7 | 3 | The code doesn't name 0–7. Per the code comment, the **MBT02 tag only supports 3 = red**. Use 3. |
| LED `onMs` | 1–65535 | 300 | light on time per blink |
| LED `offMs` | 1–65535 | 300 | light off time per blink |
| LED `cycles` | 1–65535 | 10 | blink count (duration ≈ cycles × (onMs+offMs)) |
| LED `brightness` | 1–100 | 100 | % |
| Buzzer `beepMs` | 20–65535 | 300 | |
| Buzzer `pauseMs` | 20–65535 | 300 | |
| Buzzer `cycles` | 0–65535 | 5 | **0 = off** |
| Buzzer `level` | 0–5 | 0 | tone level, 0 = tag default |

Out-of-range or non-integer values → `{"type":"error","message":"Blink count must be between 1 and 65535 (got 0)."}`. Nothing is sent to the tag.

**"Turn off":** there is **no dedicated stop command**.
* Buzzer: send `{"type":"buzzer","cycles":0}` (beep count 0 = off, per the code).
* LED: `cycles` minimum is 1, so the LED can't be switched off explicitly. It stops by itself after `cycles × (onMs + offMs)`. Keep the LED cycles short on mobile, e.g. 10 × 600 ms = 6 s. Whether a new command overrides one that is still running depends on the tag firmware and hasn't been verified on hardware.

### 2.3 Server → client messages

```jsonc
{ "type": "subscribed", "tagid": "c30000aabb01", "gatewayid": "ac233fc0ffee", "lastSeen": "2026-11-19T10:14:52Z",
  "rssi": -61, "mqttConnected": true, "topics": { "action": "/gw/ac233fc0ffee/action", "response": "/gw/ac233fc0ffee/response" } }

{ "type": "commandSent", "reqId": 503, "command": "led",          // led | buzzer | both
  "tagid": "c30000aabb01", "gatewayid": "ac233fc0ffee", "topic": "/gw/ac233fc0ffee/action",
  "options": { "color": 3, "onMs": 300, "offMs": 300, "cycles": 10, "brightness": 100 },
  // for "both": { "led": {...}, "buzzer": {...} }
  "sentAt": "2026-11-19T10:15:01Z" }

{ "type": "commandStatus", "reqId": 503, "command": "led", "tagid": "c30000aabb01", "gatewayid": "ac233fc0ffee",
  "status": "accepted",  "code": 1, "error": null, "message": "Gateway accepted the command." }
{ "type": "commandStatus", "reqId": 503, "command": "led", "tagid": "c30000aabb01", "gatewayid": "ac233fc0ffee",
  "status": "completed", "code": 2, "error": 0,    "message": "Tag is blinking." }
// or  "status":"failed",  "code":2, "error":<n≠0>, "message":"Tag did not respond (error <n>)."
// or  "status":"timeout", "code":null, "error":null, "message":"No answer from the gateway in time."

{ "type": "response", "tagid": "…", "gatewayid": "…", "topic": "/gw/…/response", "receivedAt": "…", "payload": { … } }  // raw gateway reply (debug)
{ "type": "unsubscribed" }
{ "type": "error", "message": "No NetworkTimeSeries data with a gatewayid for tag 'c30000aabb01'." }
```

**Ack flow and timeouts:** `commandSent` (published) → `commandStatus accepted` (gateway code 1) → `commandStatus completed | failed` (gateway code 2). If code 2 doesn't arrive within **60 s** (`Locating:CommandTimeoutSeconds`), you get `timeout`. Match frames by `reqId`. Every command and its outcome is also stored, so you can read it back with `GET /api/locating/tags/{tagid}/commands`. Each row has `reqId`, `command`, `tagid`, `gatewayid`, `topic`, `options`, `status` (sent/accepted/completed/failed/timeout), `sentAt`, and `responses[]` with `{receivedAt, code, error, raw}`. The row also includes the raw gateway `request`. Don't display or log that field.

Common errors: tag never heard by a gateway (no `NetworkTimeSeries` data) → `error` on subscribe; broker down → `mqttConnected:false` on subscribe, and the publish fails with an `error` frame.

### 2.4 What the backend actually publishes (for information)

The backend publishes a jengine v2 message to `/gw/{gatewayId}/action`:
```jsonc
{ "version": 2, "req_id": 503,
  "cmds": [ { "action": 42, "method": "set_req",           // 42 = LED, 56 = buzzer
              "payload": { "key": "<server-side secret>", "opcode": 90003, "single": true,
                           "color": 3, "cycles": 10, "light_on": 300, "light_off": 300, "brightness": 100,
                           "details": { "c30000aabb01": {} } } } ] }
// buzzer entry payload fields: "work", "idle", "cycles", "level"
```
The gateway replies on `/gw/{gatewayId}/response` with `req_id`, `code` (1 = accepted, 2 = done) and `error` (0 = OK), either at the top level or inside `payload`.

---

## 3. Recommendation: use the backend, not direct MQTT

**Mobile should use `wss://…/ws/locate` (plus the REST lookups), not connect to MQTT itself.**
The backend picks the right gateway, holds the broker credentials and the jengine command key server-side, checks the user's JWT, validates options, tracks acks/timeouts and logs every command. A mobile MQTT client would need broker credentials and the command key shipped inside the app, would bypass authentication and audit, and would have to reimplement gateway selection.

MQTT topics, for information only: `/gw/{gatewayId}/action` (commands, published by backend) and `/gw/{gatewayId}/response` (gateway replies, subscribed by backend). Tag readings arrive from gateways on the broker and are stored in `NetworkTimeSeries` by the network worker. On the demo stack the backend uses the dev stack's own broker (`network-server`). Live will use a different broker. No broker host or credentials are given here on purpose.

---

## 4. Where the code is (GitHub)

Repo: **https://github.com/Ajithcode/Asset_Fullstack**. Read these on the branches below, no zip needed.

**Backend: branch `demo/customer-api`** (the `backend` branch has the same Tracking code)
* `Services/Tracking/Api/A360.Tracking.Api/Endpoints/RealTimeLocationEndpoints.cs`: overview / trail / events routes
* `Services/Tracking/Api/A360.Tracking.Api/Telemetry/RealTimeLocationService.cs`: RTL logic, thresholds, DTO records (bottom of file)
* `Services/Tracking/Api/A360.Tracking.Api/Telemetry/TelemetryOverviewService.cs`: location engine, `AssetLocationDto`, `GatewayDto`
* `Services/Tracking/Api/A360.Tracking.Api/Locating/LocatingExtensions.cs`: `/ws/locate` + `/api/locating/*` routes
* `Services/Tracking/Api/A360.Tracking.Api/Locating/LocatingSession.cs`: full WebSocket protocol (header comment)
* `Services/Tracking/Api/A360.Tracking.Api/Locating/LocateCommands.cs`: LED/buzzer options, ranges, defaults, ack codes
* `Services/Tracking/Api/A360.Tracking.Api/Locating/LocatingSettings.cs`: topics, timeout, action numbers
* `Services/Tracking/Api/A360.Tracking.Api/Locating/TagGatewayLookup.cs`, `GatewayMqttBridge.cs`, `LocateCommandStore.cs`
* `Security/A360.Security/JwtAuthenticationServiceCollectionExtensions.cs`: `?access_token=` on WebSockets
* `Services/UserAccount/Api/A360.UserAccount.Api/Endpoints/AuthEndpoints.cs`, `Contracts/AuthContracts.cs`: sign-in

**Frontend: branch `demo/customer`**
* `projects/tracking-iot/src/app/real-time-location/real-time-location.ts` + `.html`: the page (10 s polling, KPI mapping)
* `projects/tracking-iot/src/app/real-time-location/rtl-api.ts`: RTL calls + TypeScript response types
* `projects/tracking-iot/src/app/geofence-alerts/floor-map-api.service.ts`: floors, plans, zones, plots, live gateway WebSocket
* `projects/tracking-iot/src/app/geofence-alerts/floor-plan-map.ts`: drawing zones/plots in % coordinates
* `projects/tracking-iot/src/app/tracking-telemetry/telemetry-overview-api.ts`: `AssetLocation`, `Gateway` types
* `projects/tracking-iot/src/app/shared/tracking-api.ts`: base URL + `clientId`/`tenantId` params
* `src/environments/environment.ts`: service URLs (all become same-origin `/` in the deployed build)

---

## 5. Test data on Asset360Dev (checked 2026-11-19)

| Item | Status |
|---|---|
| Master Data | 3 sites, 10 buildings, 20 floors (all with a floor picture), 15 zones ✅ |
| Floor plans (Maps & Floor Plans) | **0** (`maps_floor_plans` empty) ❌ |
| Zones drawn on plans | **0** ❌ |
| Gateways / assets plotted on plans | **0** ❌ |
| Gateways (`IoTDevice`) | 1 placeholder gateway (`XZXZ`, never seen, no real MAC) ⚠️ |
| Tracking tags (`TrackingTag`) | 1 (`BLE-000001`), **not linked to any asset**, never seen ⚠️ |
| Registered assets | 9 (none linked to a tag) |
| BLE readings (`NetworkTimeSeries`) | **0**: no gateway is currently reporting to the demo stack ❌ |
| Location events / geofence alerts | 0 |
| Locate commands sent (`TagLocateCommands`) | 0, so no tag has been blinked from demo yet ❌ |

**Result today:** `overview` returns empty `assets` and all KPIs are 0, `events` is empty, `trail` gives 404, and `/ws/locate` answers `error: No NetworkTimeSeries data…` for any tag. The API contracts can be built against, but there is nothing live to show yet.

**Missing for an end-to-end mobile test:**
1. A real BLE gateway (e.g. MAC `ac233f…`) that publishes to the **demo** broker, registered as an IoT Device with its real MAC as `deviceId`.
2. At least one real tag (an MBT02 tag can blink red / beep) registered in Tag Management and **linked to an asset**.
3. A floor plan image uploaded in Administration › Maps & Floor Plans for a floor, **Published**, with zones drawn and the gateway plotted (x/y) on it.
4. Then confirm `GET /api/locating/health` shows `mqttConnected:true` and a test blink returns `completed`.
