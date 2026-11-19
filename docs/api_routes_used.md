# Asset360 mobile: backend routes used in API mode

Every HTTP route (and the one WebSocket) the Flutter app calls when it runs in API mode (`USE_API=true`).
Taken from the code in `lib/` (API classes in `lib/data/api/`, repositories in `lib/data/repositories/`,
callers in `lib/features/`). Mock mode calls none of these.

- Base URL: **https://a360demo.purpleiq.ai**. Every service is reached through the one API gateway
  (YARP, `docs/backend_ref/ApiGateway/A360.ApiGateway/appsettings.json`), so the paths below are
  appended to that base.
- **R** = Read (GET). **W** = Write (POST / PUT / PATCH / DELETE). The app sends no PATCH.
- Every `/api/*` call carries `Authorization: Bearer <access token>` (except the public login calls). On a 401
  the app refreshes the token once and retries.
- `{id}` = a path value. Query params are only sent when they have a value (marked "when set").
- "Used in" names the screen / feature that triggers the call.

## 1. Login / UserAccount service (user-account cluster)

Own Dio (`lib/data/api/auth_api.dart`, `authDioProvider`). Called from `AuthNotifier` (`auth_repository.dart`).

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| W | POST | `/api/auth/login` | – (body `userName`, `password`) | Login screen |
| W | POST | `/api/auth/verify-totp` | – (body `userName`, `loginToken`, `code`) | Authenticator screen (TOTP 2FA) |
| W | POST | `/api/auth/verify-otp` | – (body `userName`, `otp`) | Authenticator screen (email OTP 2FA) |
| W | POST | `/api/auth/refresh` | – (body `refreshToken`) | Splash (session restore) + any 401 on another service |
| W | POST | `/api/auth/logout` | – (body `refreshToken`) | Profile / Settings → Logout; server switch |
| R | GET | `/api/auth/me` | – | After sign-in / restore (Profile, header user name) |
| R | GET | `/api/auth/roles` | – | After sign-in / restore (role name of the user) |

## 2. Asset service (asset cluster)

`dioProvider` / `apiClientProvider` (`lib/data/api/assets/assets_api.dart` unless noted).

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| R | GET | `/api/asset-registrations` | `page`, `pageSize` (20; 1 / 10 for summaries); when set: `search`, `category`, `assetType`, `status`, `locationId`, `department`, `custodianUserId`, `criticality`, `manufacturer`, `condition`, `sortBy`, `sortDirection` | Assets list (+ filter/sort), Tracking → Asset List tab, Home asset summary / recent assets (live_home), asset pickers, Register Asset, scan fallback search |
| R | GET | `/api/asset-registrations/filters` | – | Assets list → filter sheet |
| R | GET | `/api/asset-registrations/lookup` | `code`; `type` when set (`assetTag`, `barcode`, `qr`, `rfid`, `nfc`, `assetRegistrationId`) | Scan (every scan, `live_asset_lookup.dart`), Assets list scan button, Registration hub scan, Home dashboard asset-tag links (`type=assetTag`), More Actions asset resolve, Tracking → Asset List (unregistered-records check) |
| R | GET | `/api/asset-registrations/options` | – | Register / Edit Asset form (fixed lists), Asset 360° upload |
| R | GET | `/api/asset-registrations/{id}` | – | Asset 360° detail, Scan result, Edit Asset, Asset Tracking, asset picker, Tracking → Live Map (registered place of tracked assets), scan fallback |
| W | PUT | `/api/asset-registrations/{id}` | – | Edit Asset, Asset 360° Key Information inline edit, Move / Transfer (location), custodian changes |
| W | DELETE | `/api/asset-registrations/{id}` | – | Asset 360° ⋮ → Delete |
| R | GET | `/api/asset-registrations/{id}/components/tree` | – | Asset 360° → Overview tab (components) |
| R | GET | `/api/asset-registrations/drafts` | – | Registration hub → My Drafts |
| W | POST | `/api/asset-registrations/drafts` | `saveAsDraft=true` when saving as draft | Register Asset → step 1 Next / Save as Draft |
| R | GET | `/api/asset-registrations/drafts/{id}` | – | Register Asset (open a draft) |
| W | PUT | `/api/asset-registrations/drafts/{id}/basic-information` | `saveAsDraft=true` when set | Register Asset step 1 |
| W | PUT | `/api/asset-registrations/drafts/{id}/classification` | `saveAsDraft=true` when set | Register Asset step 2 |
| W | PUT | `/api/asset-registrations/drafts/{id}/location-assignment` | `saveAsDraft=true` when set | Register Asset step 3 |
| W | PUT | `/api/asset-registrations/drafts/{id}/purchase-warranty` | `saveAsDraft=true` when set | Register Asset step 4 |
| W | PUT | `/api/asset-registrations/drafts/{id}/additional-information` | `saveAsDraft=true` when set | Register Asset step 5 |
| W | DELETE | `/api/asset-registrations/drafts/{id}` | – | My Drafts → delete |
| W | POST | `/api/asset-registrations/drafts/{id}/submit` | – | Register Asset → Review → Submit |
| R | GET | `/api/asset-images` | `assetRegistrationId`, `pageSize=200` | Asset 360° header photo + Overview tab |
| W | POST | `/api/asset-images` | – | Asset 360° photo upload, Register Asset photos (after Media upload) |
| R | GET | `/api/asset-documents` | `assetRegistrationId`, `pageSize=200` | Asset 360° → Documents tab |
| W | POST | `/api/asset-documents` | – | Asset 360° document upload, Register Asset documents (after Media upload) |
| R | GET | `/api/asset-condition-assessments/current` | `assetRegistrationId` | Asset 360° header + Overview tab (404 = none) |
| R | GET | `/api/asset-condition-assessments/ratings` | – | Asset 360° → Update condition |
| W | POST | `/api/asset-condition-assessments` | – | Asset 360° → Update condition → Save |
| R | GET | `/api/asset-criticality/current` | `assetRegistrationId` | Asset 360° header + Overview tab (404 = none) |
| R | GET | `/api/asset-assignments` | `page`, `pageSize` (100, or 10 for the active one); when set `assetRegistrationId`, `status` (`Active`) | Asset 360° → Assignment History card; Check In / Return Asset (finds the active assignment) |
| W | POST | `/api/asset-assignments` | – | Assign Custodian / Check Out (More Actions, Work → Movement) |
| R | GET | `/api/asset-assignments/options` | – | Assign Custodian / Check In-Out screens |
| W | POST | `/api/asset-assignments/{id}/return` | – | Check In / Return Asset (`/work/movement/return`) |
| R | GET | `/api/asset-dashboard/locations` | – | Tracking → Live Map outdoor pins (`asset_map_repository.dart`), View on Map |
| R | GET | `/api/asset-dashboard/location-updates` | `days=1` | Tracking → Live Map / location tree (polled every 10 s, `live_locations_repository.dart`), View on Map |

## 3. Media service (media cluster)

`mediaDioProvider` / `mediaClientProvider` (`lib/data/api/media_api.dart`, `tracking_live_repository.dart`).

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| W | POST | `/api/media/upload` | – (multipart `file`, `category` = `asset-images` / `asset-documents`) | Asset 360° photo / document upload, Register Asset photos / documents |
| R | GET | `/uploads/...` | – | Asset photos (`NetworkImage`, photo strips), floor-plan images (Tracking → Live Map / Asset Tracking zone map, cached), document "Open" (external browser / viewer) |

## 4. Master Data service (master-data-management cluster)

`masterDataDioProvider` / `masterDataClientProvider` (`lib/data/api/master_data/`). Every call sends
`status=Active`. Replies are cached in the local DB and reused only when offline.

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| R | GET | `/api/master-data/asset-categories` | `status=Active` | Register / Edit Asset (step 2), Assets list filter sheet |
| R | GET | `/api/master-data/asset-categories/{id}/subcategories` | `status=Active` | Register / Edit Asset (Sub Category) |
| R | GET | `/api/master-data/asset-types` | `status=Active`, `categoryId` when set | Register / Edit Asset, Assets list filter sheet |
| R | GET | `/api/master-data/asset-models` | `status=Active`, `assetTypeId` when set | Register / Edit Asset |
| R | GET | `/api/master-data/manufacturers` | `status=Active` | Register / Edit Asset, Assets list filter sheet |
| R | GET | `/api/master-data/conditions` | `status=Active` | Register / Edit Asset, Assets list filter sheet |
| R | GET | `/api/master-data/criticalities` | `status=Active` | Register / Edit Asset, Assets list filter sheet |
| R | GET | `/api/master-data/departments` | `status=Active` | Register / Edit Asset, Assets list filter sheet |
| R | GET | `/api/master-data/business-units` | `status=Active` | Register / Edit Asset |
| R | GET | `/api/master-data/suppliers` | `status=Active` | Register / Edit Asset |
| R | GET | `/api/master-data/amc-vendors` | `status=Active` | Register / Edit Asset |
| R | GET | `/api/master-data/service-providers` | `status=Active` | Register / Edit Asset |
| R | GET | `/api/master-data/teams` | `status=Active` | Register / Edit Asset |
| R | GET | `/api/master-data/shifts` | `status=Active` | Register / Edit Asset |
| R | GET | `/api/master-data/uoms` | `status=Active` | Register / Edit Asset |
| R | GET | `/api/master-data/employees` | `status=Active` | Person pickers (Assign Custodian, Check Out, Assigned To) |
| R | GET | `/api/master-data/locations/tree` | `status=Active` | Location pickers (Register step 3, Move), Scan of `LOC:` codes |
| R | GET | `/api/master-data/sites` | `status=Active` | Tracking → Live Map |
| R | GET | `/api/master-data/buildings` | `status=Active` | Tracking → Live Map |
| R | GET | `/api/master-data/floors` | `status=Active` | Tracking → Live Map |
| R | GET | `/api/master-data/zones` | `status=Active` | Tracking → Live Map (location tree) |

Defined in code but **not called** by any screen: `/api/master-data-items/type/{masterType}` (`mdListItemsProvider` is unused).

## 5. Dashboard service (dashboard cluster)

`dashboardDioProvider` / `dashboardClientProvider` (`lib/data/api/dashboard/dashboard_api.dart`). Every
`/mobile/*` call takes `site` (Home site selector; left out for "All Sites").

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| R | GET | `/api/dashboards/mobile/home` | `utcOffsetMinutes`; `site` when set | Home → greeting + counters |
| R | GET | `/api/dashboards/mobile/asset-overview` | `search`, `site` when set | Home → Asset Overview card + drill-down; Home site list; Maintenance drill-down (asset types) |
| R | GET | `/api/dashboards/mobile/maintenance-performance` | `utcOffsetMinutes`; when set `months`, `assetType`, `site` | Home → Maintenance Performance card + drill-down |
| R | GET | `/api/dashboards/mobile/upcoming-activities` | `utcOffsetMinutes`; when set `from`, `to` (yyyy-MM-dd), `type`, `site` | Home → Upcoming Activities card + Activities drill-down (list / calendar) |
| R | GET | `/api/dashboards/mobile/my-tasks` | `utcOffsetMinutes`; when set `type`, `site` | Home → My Tasks card + My Tasks screen |
| R | GET | `/api/dashboards/mobile/recent-alerts` | `page`, `pageSize` (20), `days`; when set `severity`, `site` | Home → Recent Alerts card + drill-down |
| R | GET | `/api/dashboards/mobile/asset-locations` | when set `status`, `search`, `site` | Home → Asset Location card + drill-down; Tracking → Live Map outdoor pins |
| R | GET | `/api/dashboards/mobile/asset-health` | `top`; `site` when set | Home → Asset Health card + drill-down |
| R | GET | `/api/dashboards/mobile/cost-summary` | when set `period`, `year`, `site` | Home → Cost Summary card + drill-down |
| R | GET | `/api/dashboards/mobile/recent-assets` | `page`, `pageSize` (20); when set `status`, `search`, `assetType`, `site` | Home → Recent Assets card + drill-down |
| R | GET | `/api/dashboards/network-status` | – | Home → Live Devices card (first counts), Devices screen |
| R | GET | `/api/dashboards/network-status/tags` | – | Home → Devices screen; Tracking → Live Map (every 6th poll); View on Map |

## 6. Administration service (administration cluster)

`adminDioProvider` / `adminClientProvider` (`lib/data/api/admin/admin_maps_api.dart`). Read only.

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| R | GET | `/api/administration/maps/floor-plans` | none (Live Map), or `locationId` + `status=Published` (asset zone map) | Tracking → Live Map / floor plans; Asset Tracking → zone map; View on Map |
| R | GET | `/api/administration/maps/floor-plans/{id}/zones` | – | Tracking → Live Map / floor plan; Asset Tracking zone map; View on Map |
| R | GET | `/api/administration/maps/floor-plans/{id}/plots` | – | Tracking → Live Map / floor plan (refreshed with the 5 s poll); Asset Tracking zone map; View on Map |
| R | GET | `/api/administration/maps/floor-plans/{id}/layers` | – | Tracking → Live Map / floor plan (layer toggles) |

Defined in code but **not called**: `/api/administration/maps/settings` (`mapSettingsProvider` unused).

## 7. Tracking service (tracking cluster)

`trackingDioProvider` / `trackingClientProvider` (`lib/data/api/tracking/tracking_api.dart`, `live_locations_repository.dart`). Read only.

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| R | GET | `/api/iot-devices` | – | Tracking hub (poll every 5 s), Asset Tracking, Asset 360° → IoT tab, View on Map |
| R | GET | `/api/iot-devices/gateways` | – | Tracking → Live Map / location tree (every 6th poll) |
| R | GET | `/api/device-health` | – | Tracking hub (poll every 5 s), Asset Tracking, Asset 360° → IoT tab |
| R | GET | `/api/tag-ids` | – | Tracking hub (every 6th poll), Asset Tracking, Asset 360° → IoT tab |
| R | GET | `/api/location-events` | – | Tracking hub (every 6th poll), Asset Tracking, Asset 360° → IoT tab |
| R | GET | `/api/sensors` | – | Tracking → Sensors tab, Asset Tracking → Telemetry, Asset 360° → IoT tab, hub (every 6th poll) |
| R | GET | `/api/sensors/{sensorId}` | – | Tracking → Sensor detail (`/more/tracking/sensor/:sensorId`) |
| R | GET | `/api/sensors/types` | – | Tracking → Sensors tab (filter) |
| R | GET | `/api/sensors/statuses` | – | Tracking → Sensors tab (filter) |
| R | GET | `/api/telemetry-readings` | `days`, `limit=5000`; `sensorId` or `deviceId` | Sensor detail chart, Asset Tracking → Telemetry |
| R | GET | `/api/iot-events/timeline` | `days` (7 / 30 / 90) | Tracking → Sensors tab + Asset Tracking (IoT events card) |
| R | GET | `/api/geofences` | – | Tracking → Live Map / Geofencing tab |
| R | GET | `/api/iot-alerts` | `days` (Tracking alerts screen, Live Map tree uses 7) or `assetRegistrationId` + `limit=50` (Asset Tracking) | Tracking → Alerts screen, Live Map location tree, Asset Tracking alerts |
| R | GET | `/api/iot-alerts/count` | – | Tracking hub alert counter (poll every 5 s) |

## 8. WebSocket (Dashboard service)

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| R | WS upgrade | `wss://<base>/api/dashboards/network-status/gateways/live` (fallback `/api/dashboards/network-status/live` on 404) | `access_token=<token>` (also sent as `Authorization: Bearer` header) | Home → Live Devices card (only while Home is visible and the app is in the foreground; reconnect 2 / 5 / 10 / 30 s) |

Code: `DashboardApi.liveUri` / `liveUriLegacy` (`dashboard_api.dart`), `NetworkStatusSocket` (`fallbackUri`,
`withAccessToken`, `isNotFoundUpgrade` in `network_status_live.dart`). `ws://` is used for an `http://` base
(office IPs). The token is never logged.

Checked against a360demo on 2026-11-18 (no token): both WS paths answer **401** at the gateway, so the
404 fallback can only be confirmed with a signed-in phone. **For the backend team:** the gateway's JwtBearer
setup has no `access_token` query handling (`OnMessageReceived`), so today only the `Authorization` header
authenticates the upgrade. Browsers can't set that header; the gateway needs to read `?access_token=` for
the web client.

## 9. Diagnostics

| R/W | Method | Path | Query params | Used in |
|---|---|---|---|---|
| R | GET | Office IPs: `/health` on each service's own port | – (sent **without** a token) | More → Administration → Backend API → Test connection (`connection_repository.dart`) |
| R | GET | Cloud gateway: `/health`, then `/gateway/health` (the gateway itself) | – (no token) | Test connection, "GATEWAY" line |
| R | GET | Cloud gateway, per service: `/user-account/health`, `/asset/health`, `/media/health`, `/master-data-management/health`, `/dashboard/health`, `/administration/health`, `/tracking/health` | – (no token) | Test connection |
| R | GET | Cloud gateway fallback ("via gateway"): `/api/auth/roles`, `/api/asset-registrations?pageSize=1`, `/api/media/upload` (405 = reachable), `/api/master-data/sites?pageSize=1`, `/api/dashboards/network-status`, `/api/administration/maps/summary`, `/api/iot-devices?pageSize=1` | as shown (Bearer token) | Test connection, when the service's own health path did not answer JSON |
| R | GET | any path typed in / preset (`/api/asset-dashboard/location-updates`, `/api/asset-dashboard/locations`, `/api/asset-registrations?pageSize=3`, `/api/location-events`, `/api/tag-ids`, `/api/iot-devices`, `/api/sensors`, `/api/device-health`, `/api/administration/maps/floor-plans`, `/api/administration/maps/summary`, `/api/dashboards/mobile/asset-locations`) | as typed | Settings → Developer → API samples (dev builds with `SHOW_SERVER_SWITCHER` only) |

## 10. Gateway routing check

Compared against `ReverseProxy.Routes` (`Match.Path` → `ClusterId`) in
`docs/backend_ref/ApiGateway/A360.ApiGateway/appsettings.json`.

| Service | Gateway routes that match | Result |
|---|---|---|
| Login / UserAccount | `/api/auth/{**catch-all}` → user-account-cluster | OK, all 7 paths. login / verify-* / refresh / logout / roles are public in `GatewayAuthenticationMiddleware`; `/me` needs the token (sent). |
| Asset | `/api/asset-registrations[/…]`, `/api/asset-images[/…]`, `/api/asset-documents[/…]`, `/api/asset-condition-assessments[/…]`, `/api/asset-criticality[/…]`, `/api/asset-assignments[/…]`, `/api/asset-dashboard[/…]` → asset-cluster | OK, every path routes to asset-cluster. |
| Media | `/api/media/{**catch-all}`, `/uploads/{**catch-all}` → media-cluster | OK. `GET /uploads/...` is public at the gateway. |
| Master Data | `/api/master-data/{**catch-all}` → master-data-management-cluster | OK, all 21 paths (all have a sub-path; there is no root `/api/master-data` route, which the app never calls). |
| Dashboard | `/api/dashboards[/…]` → dashboard-cluster | OK, all 12 REST paths and both WebSocket paths match. YARP proxies WebSocket upgrades, The gateway's JwtBearer setup has no `OnMessageReceived` / `access_token` query handling, so at the gateway only the `Authorization` header authenticates the upgrade. |
| Administration | `/api/administration/maps[/…]` → administration-cluster | OK, all 4 paths. |
| Tracking | `/api/iot-devices[/…]`, `/api/device-health[/…]`, `/api/tag-ids[/…]`, `/api/location-events[/…]`, `/api/sensors[/…]`, `/api/telemetry-readings[/…]`, `/api/iot-events[/…]`, `/api/geofences[/…]`, `/api/iot-alerts[/…]` → tracking-cluster | OK, all 14 paths (incl. `/api/iot-devices/gateways`, `/api/sensors/types`, `/api/sensors/statuses`, `/api/iot-alerts/count`). |

**Health paths on a360demo (checked 2026-11-18):**

- `/health`, `/gateway/health` and every `/<prefix>/health` return **200 with the web frontend's `index.html`**.
  The HTTPS front (Caddy / hosting) sends these paths to the web app instead of the gateway. So no health
  endpoint is reachable through `https://a360demo.purpleiq.ai`. `/api/...` does reach the gateway: it answers 401
  without a token, and `/api/auth/roles` returns 200 JSON.
- The app therefore treats an HTML reply as "not reachable". Each service is then tested "via gateway" with the
  cheap GETs above. **For the backend team:** route `/gateway/health` and `/<prefix>/health` to the gateway (port
  5296) if a public health check is wanted.
- API samples preset `/api/administration/maps/summary` is matched (administration-cluster) but is only used on
  the developer screen.

**Prefix served by two services:** none of the app's paths. Every app path prefix maps to exactly one cluster, and
no gateway route overlaps another for these paths. Look-alikes that are separate routes (not clashes):
`/api/asset-models` and `/api/asset-categories` (asset-cluster, not called by the app) vs
`/api/master-data/asset-models` and `/api/master-data/asset-categories` (master-data cluster, called by the app).
