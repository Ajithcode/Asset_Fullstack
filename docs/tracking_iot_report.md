# Tracking & IoT — live phase report

Services: **Tracking** (port 5490, `TRACKING_BASE_URL`, DEV default `http://172.16.100.26:5490`) and the
**Administration** map system (port 5460, `ADMIN_BASE_URL`). Master Data (5500) gives site / building / floor
coordinates. Everything the app does here is **read-only**.

Sources: live OpenAPI `docs/api/tracking.json`, `docs/api/administration.json` (downloaded 11 Nov 2026) and
`docs/backend_ref/Services/{Tracking,Administration,MasterDataManagement,Dashboard}`.

## 1. Data model (what actually exists)

### Location events — `GET /api/location-events` (`LocationEventResponse`)
| Question | Answer |
|---|---|
| Asset identity | `assetId`: **free text** "the asset as shown on the map, e.g. FL-008", plus `assetName`. **Not** `assetRegistrationId`, not a tag id, not a device id. |
| Position | **None.** No latitude / longitude, no floor-plan x / y, no zone id, no floor id. Only `zoneName` (text). |
| Event | `eventType` ∈ Entered Zone / Departed Zone / Geofence Alert / Dwell Exceeded; `details` (e.g. "dwell time 18 min"). |
| Time | `eventTime` (UTC), `createdAt`. |
| Query params | **None.** No asset filter, no time range, no latest-only, no paging: the endpoint returns the whole log, newest first. |

### Sensors — `GET /api/sensors` (`SensorResponse`)
`sensorId` (e.g. SEN-T-80900), `name`, `sensorType`, `measuredParameter`, `unit`, `deviceId` + `channel`,
`assetRegistrationId` + `assetCode` + `assetName`, `siteId/siteName`, `measurementPoint`, **`liveReading`** (latest
value), `lowAlarm/highAlarm`, `samplingIntervalSeconds`, `calibrationDueDate`, `status` (Active / Offline /
Attention / Inactive). **No query parameters** (full list). `GET /api/sensors/{sensorId}` for one; option lists
`/types`, `/statuses`.

### Telemetry readings — `GET /api/telemetry-readings` (`TelemetryReadingResponse`)
`recordedAt` (UTC), `deviceId`, `sensorId`, `assetRegistrationId`, `parameter`, `value`, `unit`.
Params: `days` (1–365, **whole UTC days ending today** — days=1 = since 00:00 UTC), `deviceId`, `sensorId`,
`limit` (default 500, max 5000). Newest first. **No asset filter, no from / to, no paging cursor.**

### Device health — `GET /api/device-health` (`DeviceHealthResponse`)
`deviceId`, `deviceModel`, `assetRegistrationId`, `siteId/siteName`, `gateway`, `connectivity`,
`signalStrengthDbm`, `snrDb`, `signalQuality` (Strong/Good/Fair/Poor), `powerSource`, `batteryLevel` (battery only),
`lastSeenAt`, `health` (Healthy/Attention/Critical/Maintenance). **No query parameters.**

### Also used
* `GET /api/iot-devices` (`IoTDeviceResponse`): `deviceId`, `deviceType` (Gateway excluded from assets),
  `assetRegistrationId`, `assetCode`, `batteryLevel`, `lastSeenAt`, `health`, `firmwareVersion`. No params = full list.
* `GET /api/tag-ids` (`TrackingTagResponse`): `tagId`, `technology`, `assetRegistrationId`, `siteName`,
  `locationName` (text), `status`, `lastSeenAt`. No params.
* `GET /api/geofences` (`GeofenceResponse`): GPS `coordinates[{latitude, longitude}]`, `shape`
  (Polygon/Rectangle/Circle), `radiusMeters`, `mapColour`, `monitorEntry/monitorExit`, `dwellAlertMinutes`, `status`.
* `GET /api/iot-alerts` (`days`, `status`, `severity`, `siteName`, `assetRegistrationId`, `search`, `limit`) and
  `GET /api/iot-alerts/count` (`days`, `siteName`) → `total`, `byStatus`, `bySeverity`, `changePercent`.
* `GET /api/iot-events/timeline?days=` — **org-wide** daily counts (telemetry, location updates, geofence events,
  alerts). No asset or site filter.
* Administration: `GET /maps/floor-plans?locationId=&status=Published` (`fileUrl`, `format`, `widthMeters`,
  `heightMeters`), `/{id}/zones` (percent of the plan: polygon `points`, rectangle `x/y/w/h`, circle `radius` = % of
  the plan **width**; `geofence`, `entryAlert`, `exitAlert`, `dwellAlert`, `dwellMinutes`), `/{id}/plots`
  (`kind` asset|device, `itemId` = registration id / device id, `x/y` %, `zoneId`) — **placed by an administrator,
  not live**, `/{id}/layers` (`enabled`, `visible` per key).
* Master Data: sites / buildings / floors `latitude`, `longitude`; floors `imageUrl` (not used: the published
  Administration plan is the map source).
* Dashboard `GET /api/dashboards/network-status`: online / offline **tag** counts from the network server's
  `NetworkTimeSeries` (not used on the tracking screens; the app's counters come from the same list they filter,
  see below).

**What the app is built on:** positions are **zone-level** (the zone NAME of each asset's newest location event);
markers on a plan are **administrator-plotted positions**; freshness / battery / signal come from devices + device
health. Nothing is drawn at an invented coordinate.

## 2. Live OpenAPI vs docs/backend_ref

Compared mechanically (paths, query parameters, request-body property names):

* **Tracking (130 operations): identical.** The "new" endpoints in the brief (`/iot-alerts/count`,
  `PATCH /iot-alerts/{id}/status`, `/iot-events/timeline`, device-model option lists) are **already in
  docs/backend_ref** — that copy is newer than 10 Nov or the change was already there.
* **Administration map endpoints (35 operations): identical** paths and parameters.
* Request schemas: only Swagger artefacts differ — `CreateAssetUtilizationRequest.dateUtc` and
  `CreateDeviceMaintenanceRequest.{assignedToName, dueDateOnly}` are computed C# getters that Swashbuckle lists as
  properties; `SaveMapSettingsRequest.Unknown` is `[JsonExtensionData]`. Nothing the app sends (it sends nothing).
* Swagger documents **no response bodies** (0 of 195 operations), so every response field is taken from the C#
  `*Response` records.

**Fields I could not confirm against the server** (no token, see §5): all response bodies. Specific risks:
* Timestamps: the app reads values without an offset as UTC (`jUtc`). If the server returns local time unmarked,
  "last seen" would be off.
* `LocationEvent.assetId` ↔ asset: matched to `assetRegistrationId` or the `assetCode` of the asset's devices /
  sensors. If events use another code (e.g. RFID tag id) they stay separate rows keyed by that text.
* Circle zones: the server stores no centre; the app uses the bounding-box centre (`x/y` when `w/h` are 0).
* Rectangle GPS geofences: 4 corners, or 2 opposite corners (both handled).
* `MapPlot.kind` casing ("asset"/"device") and `MapLayer.key` names as in `MapOptions.Layers`.

## 3. Screens and their endpoints

| Screen | Route | Endpoints |
|---|---|---|
| Tracking hub — Live Map (Outdoor) | `/more/tracking?tab=map&view=outdoor[&geofence=]` | Master Data `/sites`, `/buildings`, `/floors`; Tracking `/geofences`; live poller (below) |
| Tracking hub — Live Map (Floor plan) | `/more/tracking?tab=map&view=floor&location=<md id>[&zone=]` | Admin `/maps/floor-plans?locationId=&status=Published`, `/{id}/zones`, `/{id}/plots` (every live round), `/{id}/layers`, plan file (Media URL); live poller |
| Tracking hub — Asset List | `/more/tracking?tab=list` | live poller |
| Tracking hub — Sensors | `/more/tracking?tab=sensors` | `/sensors`, `/sensors/types`, `/sensors/statuses`, `/iot-events/timeline?days=7\|30\|90` |
| Tracking hub — Geofencing | `/more/tracking?tab=zones` | `/geofences`; Admin `/maps/floor-plans?status=Published` + `/{id}/zones` per plan (4 at a time) |
| Asset Tracking (one asset) | `/more/tracking/asset/:key` | live poller; Asset `GET /asset-registrations/{id}` (location ids → plan); plan endpoints; `/sensors`; `/telemetry-readings?sensorId=&days=&limit=5000`; `/iot-alerts?assetRegistrationId=&limit=50`; `/iot-events/timeline` |
| Sensor detail | `/more/tracking/sensor/:sensorId` | `/sensors/{id}`, `/telemetry-readings?sensorId=&days=2\|8&limit=5000` |
| IoT Alerts | `/more/tracking/alerts` | `/iot-alerts?days=7`; badge `/iot-alerts/count` |
| Asset detail → IoT tab | `/assets/detail/:id?tab=iot` | one full snapshot (devices, health, tags, sensors, location events), `/sensors`, `/telemetry-readings` |

**Live poller** (`trkLiveProvider`): while a Tracking screen is visible and the app is in the foreground
(`LivePollingScope`: `TickerMode` + app lifecycle), every **5 s**: `/iot-devices`, `/device-health`,
`/iot-alerts/count`; every **6th** round (30 s) and on pull-to-refresh also `/location-events`, `/tag-ids`,
`/sensors`. It stops when the screen is covered, the tab changes, the app goes to the background, or the screen
closes. Plots are refetched after each round so a moved plot glides to its new spot.

## 4. Live vs static

| Live (5 s) | Live (30 s) | Loaded once per visit |
|---|---|---|
| Last seen, online/offline (10-min rule), grey stale markers, battery, signal, alert badge, plot positions | Zone of each asset, zone occupancy, recent activity, trail, tags, sensors' `liveReading` in the hub | Floor plan + drawing (cached in memory and on disk), zones, layers, geofences, sensor history charts, alerts list, timeline |

Counters: **Online / Offline / Low Battery** are computed from the tracked-asset list itself (last seen ≤ 10 min,
battery ≤ 20 %), so they always match the list; **Alerts** = unresolved in `/iot-alerts/count` (last 7 days) = the
"Open" tab of the alerts list (same window).

Removed in API mode: the mock "Link an IoT sensor" prompt and every mock pin / route / telemetry value; an asset
without sensors shows "No sensors linked to this asset". Mock mode is unchanged.

## 5. What the server returned

`GET /health` → **200** on Tracking (5490) and Administration (5460). Every `/api/*` call → **401** without a token.
No token was available (sign-in needs a real user + 2-step code), so **no live data was checked** — the parsing is
from the contracts and is covered by tests with contract-shaped JSON (`test/live/tracking_samples.dart`).
To check: sign in on a device in API mode and open Tracking & IoT; or provide a token (`DEV_TOKEN`) and I'll curl
the GET endpoints read-only.

## 6. What's missing in the backend for a true live map

1. **A position per asset.** Location events carry only a zone *name*. Needed: `x`/`y` (plan %) + `floorPlanId`
   (or `floorId`), and/or `latitude`/`longitude` for outdoor GPS, plus accuracy.
2. **Stable ids on events.** `assetRegistrationId` (and `zoneId`, `deviceId`/`tagId`) instead of free-text
   `assetId` / `zoneName`.
3. **Latest position endpoint.** e.g. `GET /api/location-events/latest?floorPlanId=|siteId=` (one row per asset) —
   today the app must download the **whole** event log (no filters, no paging) and keep the newest per asset.
4. **Filters + paging on history.** `assetRegistrationId`, `from`/`to` (not whole UTC days), `page`/`pageSize`
   or a cursor — for location events, and from/to for telemetry readings (`days` cannot express "last hour").
5. **Push.** A WebSocket / SSE stream of position changes (like Dashboard network-status `/live`) instead of
   polling full lists.
6. **Plots vs live.** Plots are an admin placement; there is no RTLS engine writing live x / y. Either the RTLS
   updates plots (the app already re-reads them every 5 s) or a separate live-position collection is needed.
7. **Per-asset activity timeline.** `/iot-events/timeline` is org-wide only; an `assetRegistrationId` filter would
   let the asset screen use it directly.
8. List endpoints `/iot-devices`, `/device-health`, `/tag-ids`, `/sensors` return everything; `assetRegistrationId`
   and `siteId` filters would let the asset IoT tab avoid downloading all of them.

## 7. Performance

* Location events: fetched every 30 s (not 5 s), trimmed to 7 days in memory, newest-per-asset computed once per
  fetch. (The full download is unavoidable until §6.3 exists.)
* Telemetry: always time-bounded (`days` 2 or 8) and capped (`limit`), trimmed on the phone to 1 h / 24 h / 7 d.
* Floor-plan drawings: one download per URL (concurrent loads share it), memory + disk cache
  (`FloorPlanImageCache`, app cache dir); a new plan version has a new URL.
* Polling stops whenever no live screen is visible (tested).

## 8. Code

* API: `lib/data/api/tracking/` (`tracking_models.dart`, `tracking_api.dart`), `lib/data/api/admin/`
  (`map_models.dart` incl. `PlanFrame` % → `CrsSimple` coordinates, `admin_maps_api.dart`).
* Logic: `lib/data/tracking/tracked_assets.dart` (join, freshness, ranges); providers + poller + image cache:
  `lib/data/repositories/tracking_live_repository.dart`.
* UI: `lib/features/more/tracking/live/` (hub, map tab, `floor_plan_map.dart`, `outdoor_map.dart`,
  `asset_zone_map.dart`, lists, asset tracking, sensor + alerts screens); asset IoT tab
  `lib/features/assets/live/live_detail_iot.dart`. Shared widgets extracted from the mock hub:
  `TrackingHubTabs`, `CounterTiles`, `MapRoundButton`.
* Tests: `test/live/tracking_live_test.dart` (31), samples `test/live/tracking_samples.dart`.
