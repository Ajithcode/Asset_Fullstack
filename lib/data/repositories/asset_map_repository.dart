import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable;

import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/assets/json_read.dart';
import 'package:asset360_mobile/data/api/dashboard/dashboard_models.dart';
import 'package:asset360_mobile/data/repositories/dashboard_repository.dart' show dashboardApiProvider;
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';

/// Assets with real coordinates for the outdoor Live Map, and "View on Map" target resolution.
/// Sources (any one is enough to show the outdoor map):
/// - Dashboard GET /api/dashboards/mobile/asset-locations (the Home Asset Location map) — wins;
/// - Asset GET /api/asset-dashboard/locations (`pins[]`, `AssetLocationPin`);
/// - Master Data site / building / floor latitude + longitude (see [hasMapCoordinates]).

export 'package:asset360_mobile/data/api/dashboard/dashboard_models.dart' show DashGeoPoint, DashLocatedAsset;

const assetDashboardLocationsPath = '/api/asset-dashboard/locations';

/// `AssetLocationPin` (Online / Idle / Offline / Alert / Unknown) as a Home map pin.
DashLocatedAsset? _pinToLocated(Map<String, dynamic> j) {
  final id = jStr(j['assetRegistrationId']);
  final lat = jNum(j['latitude']), lng = jNum(j['longitude']);
  if (id == null || lat == null || lng == null || (lat == 0 && lng == 0)) return null;
  final status = jStr(j['status']);
  return DashLocatedAsset(
    assetRegistrationId: id,
    assetTag: jStr(j['assetTag']),
    assetName: jStr(j['assetName']),
    assetType: jStr(j['assetType']),
    status: status,
    statusGroup: switch ((status ?? '').toLowerCase()) {
      'online' => 'operational',
      'alert' => 'out-of-service',
      'idle' => 'under-maintenance',
      _ => 'other',
    },
    locationLabel: jStr(j['lastZone']) ?? jStr(j['siteName']),
    latitude: lat,
    longitude: lng,
  );
}

/// GET /api/asset-dashboard/locations → pins with coordinates.
List<DashLocatedAsset> parseAssetDashboardPins(Object? body) => [
  for (final p in jList(jMap(body)['pins']))
    if (p is Map) ?_pinToLocated(jMap(p)),
];

/// Every asset with coordinates from both sources (Dashboard first; duplicates dropped).
class GeoAssets {
  const GeoAssets({this.assets = const [], this.center});

  final List<DashLocatedAsset> assets;
  final DashGeoPoint? center;

  DashLocatedAsset? byId(String id) {
    final n = id.toLowerCase();
    return assets.where((a) => a.assetRegistrationId.toLowerCase() == n).firstOrNull;
  }
}

/// Both sources in parallel; one failing is fine, both failing is an error.
final geoAssetsProvider = FutureProvider.autoDispose<GeoAssets>((ref) async {
  Object? firstError;
  Future<T?> safe<T>(Future<T> f) async {
    try {
      return await f;
    } catch (e) {
      firstError ??= e;
      return null;
    }
  }

  final r = await Future.wait<Object?>([
    safe(ref.watch(dashboardApiProvider).getAssetLocations()),
    safe<Object?>(ref.watch(apiClientProvider).get<Object?>(assetDashboardLocationsPath).then<Object?>((r) => r.data)),
  ]);
  final dash = r[0] as DashAssetLocations?;
  final pins = r[1] == null ? null : parseAssetDashboardPins(r[1]);
  if (dash == null && pins == null) throw firstError!;
  final seen = <String>{};
  final out = <DashLocatedAsset>[
    for (final a in [...?dash?.assets, ...?pins])
      if (!(a.latitude == 0 && a.longitude == 0) && seen.add(a.assetRegistrationId.toLowerCase())) a,
  ];
  return GeoAssets(assets: out, center: dash?.center);
});

// ------------------------------------------------------------------ View on Map

/// Where "View on Map" opens an asset.
sealed class AssetMapTarget {
  const AssetMapTarget();
}

/// The floor plan of [locationId] (Master Data floor), with the asset's plot selected ([plotKey])
/// and / or its live zone ([zoneId], map zone id) highlighted.
class FloorPlanTarget extends AssetMapTarget {
  const FloorPlanTarget({required this.locationId, this.plotKey, this.zoneId});

  final String locationId;
  final String? plotKey, zoneId;
}

/// The outdoor map centred on the asset's coordinates.
class OutdoorTarget extends AssetMapTarget {
  const OutdoorTarget(this.asset);

  final DashLocatedAsset asset;
}

class NoMapTarget extends AssetMapTarget {
  const NoMapTarget();
}

/// Reads an autoDispose future once, keeping it alive until it completes.
Future<T> _once<T>(ProviderContainer c, ProviderListenable<Future<T>> p) async {
  final sub = c.listen(p, (_, _) {});
  try {
    return await sub.read();
  } finally {
    sub.close();
  }
}

Future<T?> _orNull<T>(Future<T> Function() f) async {
  try {
    return await f();
  } catch (_) {
    return null;
  }
}

/// Resolves [assetRegistrationId] with the EXISTING rules:
/// 1. its tag's newest location update names a PLOTTED device (KEY RULE) → that plan + live zone
///    (its own plot on that plan selected when it has one);
/// 2. else an asset plot on any visible plan → that plan, plot selected;
/// 3. else coordinates (Dashboard asset-locations / asset-dashboard/locations) → outdoor;
/// 4. else [NoMapTarget].
Future<AssetMapTarget> resolveAssetMapTarget(ProviderContainer c, String assetRegistrationId) async {
  final id = assetRegistrationId.trim().toLowerCase();
  final plans = await _orNull(() => _once(c, planDataProvider.future)) ?? const <PlanData>[];

  // 1. Live zone via the tag.
  final live = c.read(liveLocProvider);
  final updates =
      live.updates ??
      await _orNull(
        () async => TagLocationUpdate.parseList(
          (await c.read(apiClientProvider).get<Object?>(locationUpdatesPath, query: {'days': 1})).data,
        ),
      ) ??
      const <TagLocationUpdate>[];
  if (updates.isNotEmpty) {
    final devices =
        c.read(trkLiveProvider).snapshot?.devices ??
        (await _orNull(() => _once(c, trackingSnapshotOnceProvider.future)))?.devices ??
        const <TrkDevice>[];
    final network = live.networkTags.isNotEmpty
        ? live.networkTags
        : await _orNull(
                () async => [
                  for (final t in await c.read(dashboardApiProvider).getNetworkStatusTags())
                    (tagId: t.tagId, assetRegistrationId: t.assetRegistrationId, assetName: t.assetName),
                ],
              ) ??
              const [];
    final tags = {
      for (final e in tagLinks(devices, network).entries)
        if ((e.value.assetRegistrationId ?? '').toLowerCase() == id) e.key,
    };
    final mine = [
      for (final u in updates)
        if (tags.contains(u.tagId.trim().toLowerCase())) u,
    ]..sort((a, b) => (b.lastChangedAt ?? DateTime(0)).compareTo(a.lastChangedAt ?? DateTime(0)));
    final gw = mine.firstOrNull?.lastToGatewayId;
    final dp = gw == null ? null : devicePlaces(plans, const LocationMasterData())[gw.trim().toLowerCase()];
    if (dp != null) {
      final plan = plans.where((d) => d.plan.floorPlanId == dp.plan.floorPlanId).firstOrNull;
      final plot = plan?.plots.where((p) => p.assetId?.toLowerCase() == id).firstOrNull;
      return FloorPlanTarget(
        locationId: dp.plan.locationId,
        plotKey: plot == null ? null : plotKeyOf(plot),
        zoneId: dp.mapZoneId,
      );
    }
  }

  // 2. Plotted on a plan.
  for (final d in plans) {
    final plot = d.plots.where((p) => p.assetId?.toLowerCase() == id).firstOrNull;
    if (plot != null) return FloorPlanTarget(locationId: d.plan.locationId, plotKey: plotKeyOf(plot));
  }

  // 3. Coordinates.
  final geo = await _orNull(() => _once(c, geoAssetsProvider.future));
  final hit = geo?.byId(id);
  if (hit != null) return OutdoorTarget(hit);
  return const NoMapTarget();
}
