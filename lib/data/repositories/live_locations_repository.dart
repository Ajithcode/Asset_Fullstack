import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart' show showServerSwitcher;
import 'package:asset360_mobile/data/api/assets/assets_api.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/repositories/dashboard_repository.dart' show dashboardApiProvider;
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/data/tracking/live_locations.dart';

export 'package:asset360_mobile/data/tracking/live_locations.dart';

/// Live location tree data (Tracking → Live Map / Geofencing):
/// - Asset GET /api/asset-dashboard/location-updates every [LiveLocNotifier.interval] (10 s) while a
///   tree / map is visible ([LivePoller], driven by `LivePollingScope`);
/// - Tracking GET /api/iot-devices/gateways and Dashboard GET /api/dashboards/network-status/tags on
///   the first round, every [LiveLocNotifier.slowEvery]-th round and on refresh;
/// - tag ↔ asset links also from the Tracking devices of [trkLiveProvider];
/// - registered places from the Asset service (one GET per linked asset, cached per id set).

const locationUpdatesPath = '/api/asset-dashboard/location-updates';
const gatewaysPath = '/api/iot-devices/gateways';

class LiveLocState {
  const LiveLocState({
    this.updates,
    this.gateways,
    this.networkTags = const [],
    this.error,
    this.fetchedAt,
    this.polling = false,
  });

  /// Null until the first round finished.
  final List<TagLocationUpdate>? updates;
  final List<TrkGateway>? gateways;
  final List<({String tagId, String? assetRegistrationId, String? assetName})> networkTags;
  final Object? error;
  final DateTime? fetchedAt;
  final bool polling;

  bool get loaded => updates != null && gateways != null;

  LiveLocState copyWith({
    List<TagLocationUpdate>? updates,
    List<TrkGateway>? gateways,
    List<({String tagId, String? assetRegistrationId, String? assetName})>? networkTags,
    Object? error,
    bool clearError = false,
    DateTime? fetchedAt,
    bool? polling,
  }) => LiveLocState(
    updates: updates ?? this.updates,
    gateways: gateways ?? this.gateways,
    networkTags: networkTags ?? this.networkTags,
    error: clearError ? null : (error ?? this.error),
    fetchedAt: fetchedAt ?? this.fetchedAt,
    polling: polling ?? this.polling,
  );
}

class LiveLocNotifier extends Notifier<LiveLocState> implements LivePoller {
  static const interval = Duration(seconds: 10);
  static const slowEvery = 6;

  final _owners = <Object>{};
  Timer? _timer;
  int _round = 0;
  bool _busy = false;

  @override
  LiveLocState build() {
    ref.onDispose(() => _timer?.cancel());
    return const LiveLocState();
  }

  @visibleForTesting
  int get listeners => _owners.length;

  @override
  void attach(Object owner) {
    _owners.add(owner);
    _sync();
  }

  @override
  void detach(Object owner) {
    _owners.remove(owner);
    _sync();
  }

  void _sync() {
    if (!ref.mounted) return;
    final want = _owners.isNotEmpty;
    if (want == state.polling) return;
    state = state.copyWith(polling: want);
    if (want) {
      unawaited(_run());
    } else {
      _timer?.cancel();
      _timer = null;
    }
  }

  void _schedule() {
    _timer?.cancel();
    if (!state.polling) return;
    // Tests replace the timer through the Tracking poller's hook.
    _timer = TrkLiveNotifier.startTimer(interval, () => unawaited(_run()));
  }

  /// Pull-to-refresh / Retry: everything now.
  Future<void> refresh() => _run(full: true);

  Future<void> _run({bool full = false}) async {
    if (_busy) return;
    _busy = true;
    final slow = full || state.gateways == null || _round % slowEvery == 0;
    _round++;
    try {
      // Future.wait listens to every call, so a second failure is never left unhandled.
      final r = await Future.wait<Object?>([
        ref.read(apiClientProvider).get<Object?>(locationUpdatesPath, query: {'days': 1}).then((r) => r.data),
        if (slow) ref.read(trackingClientProvider).get<Object?>(gatewaysPath).then((r) => r.data),
        if (slow) _networkTags(),
      ]);
      final u = TagLocationUpdate.parseList(r[0]);
      final g = slow ? TrkGateway.parseList(r[1]) : null;
      final t = slow ? r[2] as List<({String tagId, String? assetRegistrationId, String? assetName})>? : null;
      if (!ref.mounted) return;
      state = state.copyWith(
        updates: u,
        gateways: g,
        networkTags: t,
        fetchedAt: DateTime.now().toUtc(),
        clearError: true,
      );
    } catch (e) {
      if (!ref.mounted) return;
      state = state.copyWith(error: e);
    } finally {
      _busy = false;
      if (ref.mounted) _schedule();
    }
  }

  /// The Dashboard tag links are a fallback only: a failure keeps the previous ones.
  Future<List<({String tagId, String? assetRegistrationId, String? assetName})>?> _networkTags() async {
    try {
      return [
        for (final t in await ref.read(dashboardApiProvider).getNetworkStatusTags())
          (tagId: t.tagId, assetRegistrationId: t.assetRegistrationId, assetName: t.assetName),
      ];
    } catch (_) {
      return null;
    }
  }
}

final liveLocProvider = NotifierProvider<LiveLocNotifier, LiveLocState>(LiveLocNotifier.new);

/// Floor plans the phone shows ([planVisible]: Published; Draft too in developer builds).
final visibleFloorPlansProvider = FutureProvider.autoDispose<List<MapFloorPlan>>((ref) async {
  final plans = await ref.watch(adminMapsApiProvider).getFloorPlans();
  return [
    for (final p in plans)
      if (planVisible(p, dev: showServerSwitcher)) p,
  ];
});

/// The plan shown for a Master Data location (floor) id, or null.
final floorPlanForProvider = FutureProvider.autoDispose.family<MapFloorPlan?, String>((ref, locationId) async {
  final plans = await ref.watch(visibleFloorPlansProvider.future);
  return planForLocation(plans, locationId, dev: showServerSwitcher);
});

/// Zones + plots of every visible plan (4 plans at a time; a plan that fails is left out): the
/// device plots that place tags (KEY RULE).
final planDataProvider = FutureProvider.autoDispose<List<PlanData>>((ref) async {
  final api = ref.watch(adminMapsApiProvider);
  final plans = await ref.watch(visibleFloorPlansProvider.future);
  final out = <PlanData>[];
  for (var i = 0; i < plans.length; i += 4) {
    final chunk = plans.skip(i).take(4).toList();
    final got = await Future.wait(
      chunk.map((p) async {
        try {
          final r = await Future.wait<Object>([api.getZones(p.floorPlanId), api.getPlots(p.floorPlanId)]);
          return (plan: p, zones: r[0] as List<MapZone>, plots: r[1] as List<MapPlot>);
        } catch (_) {
          return null;
        }
      }),
    );
    out.addAll(got.nonNulls);
  }
  return out;
});

/// Registered places of the linked assets; key = registration ids joined by ",". One GET per
/// asset, 4 at a time; an asset that fails to load just has no registered place.
final registeredPlacesProvider = FutureProvider.autoDispose.family<Map<String, RegisteredPlace>, String>((
  ref,
  joined,
) async {
  final api = ref.watch(assetsApiProvider);
  final ids = joined.split(',').where((s) => s.isNotEmpty).toList();
  final out = <String, RegisteredPlace>{};
  for (var i = 0; i < ids.length; i += 4) {
    final chunk = ids.skip(i).take(4).toList();
    await Future.wait(
      chunk.map((id) async {
        try {
          final l = (await api.get(id)).locationAssignment.location;
          out[id.toLowerCase()] = RegisteredPlace(
            siteId: l.siteId,
            siteName: l.siteName,
            buildingId: l.buildingId,
            buildingName: l.buildingName,
            floorId: l.floorId,
            floorName: l.floorName,
            zoneId: l.zoneId,
            zoneName: l.zoneName,
          );
        } catch (_) {}
      }),
    );
  }
  return out;
});

/// What the tree / map screens show.
class LocationTreeData {
  const LocationTreeData({this.tree, this.md = const LocationMasterData(), this.error, this.fetchedAt});

  /// Null while the first round loads (or failed: [error]).
  final LocationTree? tree;
  final LocationMasterData md;
  final Object? error;
  final DateTime? fetchedAt;

  bool get mapPossible => hasMapCoordinates(md);
}

/// The tree for [query] (asset name, tag, registration id or gateway).
final locationTreeProvider = Provider.autoDispose.family<LocationTreeData, String>((ref, query) {
  final live = ref.watch(liveLocProvider);
  final devices = ref.watch(trkLiveProvider.select((s) => s.snapshot?.devices)) ?? const <TrkDevice>[];
  final md = LocationMasterData(
    sites: ref.watch(mdSitesProvider).value ?? const [],
    buildings: ref.watch(mdBuildingsProvider).value ?? const [],
    floors: ref.watch(mdFloorsProvider).value ?? const [],
    zones: ref.watch(mdZonesProvider).value ?? const [],
    planLocationIds: {
      for (final p in ref.watch(visibleFloorPlansProvider).value ?? const <MapFloorPlan>[]) p.locationId,
    },
  );
  final places = devicePlaces(ref.watch(planDataProvider).value ?? const [], md);
  if (!live.loaded) return LocationTreeData(md: md, error: live.error);
  final links = tagLinks(devices, live.networkTags);
  final heard = {for (final u in live.updates!) u.tagId.trim().toLowerCase()};
  final ids = {
    for (final e in links.entries)
      if (heard.contains(e.key) && e.value.assetRegistrationId != null) e.value.assetRegistrationId!,
  }.toList()..sort();
  final registered = ref.watch(registeredPlacesProvider(ids.join(','))).value ?? const {};
  final alerts = ref.watch(trkAlertsProvider(7)).value ?? const <TrkAlert>[];
  return LocationTreeData(
    tree: buildLocationTree(
      updates: live.updates!,
      gateways: live.gateways!,
      links: links,
      registered: registered,
      alerts: alerts,
      md: md,
      devicePlaces: places,
      query: query,
    ),
    md: md,
    error: live.error,
    fetchedAt: live.fetchedAt,
  );
});

/// Pull-to-refresh of the tree.
void refreshLocations(WidgetRef ref) {
  unawaited(ref.read(liveLocProvider.notifier).refresh());
  ref
    ..invalidate(registeredPlacesProvider)
    ..invalidate(visibleFloorPlansProvider)
    ..invalidate(planDataProvider);
}
