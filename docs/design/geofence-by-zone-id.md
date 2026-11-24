# Geofence rules on Maps & Floor Plans zones: keep the "zone:{id}" reference (A) or add `Geofence.ZoneId` (B)

Status: proposal, not implemented. Date: 2026-11-24. Related: "Geofence rules: protect against deleted zones" (zone delete guard + "Zone missing").

## Today (A)

- Zones are drawn and owned by Administration (`maps_zones`, `zone_id` = GUID). Zones are edited in place (redraw = `PUT /maps/zones/{id}`, same id); floor plan versions only swap the background file, they do not create zones.
- Tracking has two unrelated targets for an `AlertRule`:
  - outdoor `Geofence` (Tracking-owned, lat/lng `Coordinates`, `Shape` required) → `AlertRule.GeofenceId = geofence._id`;
  - floor plan zone → `AlertRule.MapZoneId = zoneId`, `MapFloorPlanId`, and `GeofenceId = "zone:{zoneId}"` (keeps rule names unique per target and lets every "target id" API take one string).
- `ZoneGeofenceEngine` reads `maps_zones` + `maps_plots` (gateways in the zone) directly; `GeofenceAlertsService` lists zones as "Indoor" targets with status computed from their rules (Monitored / Not monitored / now Zone missing). There is no Geofence document for an indoor zone, so zone-level settings (asset groups, schedule, enable/disable with reason) do not exist for indoor targets; "disable zone" in the UI pauses all of its rules.
- No outdoor evaluation engine exists today; outdoor geofences are stored and listed only.

## Option B

Add `ZoneId` (and `FloorPlanId`) to `Geofence`. A Geofence with `ZoneId` is an indoor geofence: `Shape` / `Coordinates` / `RadiusMeters` become optional and are ignored; the outline comes from `maps_zones`. Rules always reference a Geofence `_id`; the engine resolves `Geofence.ZoneId → maps_zones`.

| Area | A (today) | B |
|---|---|---|
| Data model | AlertRule.MapZoneId + "zone:{id}" GeofenceId; no indoor Geofence doc | Geofence.ZoneId/FloorPlanId (unique, sparse index on zone_id among non-deleted); Shape nullable; AlertRule.MapZoneId deprecated |
| Indoor zone settings | none; status derived from rules | same as outdoor: Status + StatusReason, AssetGroups, Schedule, Monitor entry/exit, RuleCount |
| API contracts | `POST /alert-rules` takes `MapZoneId` or `GeofenceId`; target ids are "zone:{id}" strings | `POST /geofences` takes `ZoneId` (validated against maps_zones, Shape not required); `POST /alert-rules` takes only `GeofenceId`; summary target `Id` becomes the Geofence id (+ `ZoneId` field). "zone:{id}" accepted for one release and mapped |
| Engine | rules → MapZoneId → maps_zones | rules → Geofence (Active, ZoneId set) → maps_zones; geofence-level disable/schedule honoured once, not per rule |
| Zone deleted | guard in Administration DeleteZone (409 / disableRules) + "Zone missing" on rules | same guard, but it disables one Geofence (StatusReason) instead of N rules; rules inherit |
| UI | "Configure Geofence" picks a zone id directly | "Configure Geofence" = create/find the indoor Geofence for the zone, then rules on it; indoor and outdoor rows share one model and editor |

### Migration (B)

One idempotent startup backfill in Tracking: for each distinct zone id in non-deleted rules (`MapZoneId` or `GeofenceId` "zone:{id}"), upsert a Geofence `{ZoneId, FloorPlanId, Name = zone name or the rule's GeofenceName, Status = Active, or Disabled with "Zone missing" when the zone is gone}`, then set each rule's `GeofenceId` to that Geofence id and recompute `RuleCount`. Keep `MapZoneId` on rules (read-only) for one release for rollback; old alerts keep `IoTAlert.GeofenceId = "zone:{id}"`, so event history filters must match both the new id and the old "zone:{id}" (store `LegacyTargetId` on the Geofence). Concurrency: two instances starting together must not create two Geofences per zone → unique index on `zone_id` + upsert.

### Risks

- B duplicates zone identity: the zone's name/site lives in maps_zones and a copy in Geofence; renames must be read live (as today) or synced.
- Two places can now "disable" an indoor target (Geofence status and rule status); the UI must show which one applies.
- The frontend currently keys indoor targets by "zone:{id}" in several places (rule dialog, map, summary, events filter); every one must change in lock-step with the API, or the alias must stay.
- Migration touches the shared Atlas DB; needs a dry-run count and a backup of `AlertRule`.
- A keeps a stringly-typed prefix convention spread over both services (Tracking `AlertNotifyDefaults.MapZonePrefix`, Administration `ZoneAlertRules.MapZonePrefix`, frontend `MAP_ZONE_PREFIX`).

## Recommendation

Stay on **A** for now. With the delete guard and "Zone missing" flag, A no longer fails silently, needs no data migration, and the frontend "Configure Geofence" flow already works on zone ids. Move to **B** only when indoor zones need geofence-level settings (asset groups, schedule, enable/disable with reason independent of rules) or when an outdoor engine is built and both kinds should share one evaluation path; at that point do the backfill above and keep "zone:{id}" as an accepted alias for one release.
