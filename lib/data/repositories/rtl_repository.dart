import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart' show showServerSwitcher, trackingBaseUrl;
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/api/tracking/rtl_api.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/data/tracking/locate_session.dart';
import 'package:asset360_mobile/data/tracking/rtl_logic.dart';

export 'package:asset360_mobile/data/api/tracking/rtl_api.dart';
export 'package:asset360_mobile/data/api/admin/map_models.dart';
export 'package:asset360_mobile/data/tracking/rtl_logic.dart';

/// Real-Time Location data (Tracking & IoT → Live Map / Geofencing in API mode):
/// - overview polled every [RtlNotifier.interval] (10 s) while the screen is visible ([LivePoller]
///   through `LivePollingScope`), for the selected site + shown floor plan;
/// - events / trail / locate history on demand (autoDispose families);
/// - floors from Master Data, plans / zones / device plots from Administration.

final rtlApiProvider = Provider<RtlApi>((ref) => RtlApi(ref.watch(trackingClientProvider)));

class RtlState {
  const RtlState({this.overview, this.error, this.fetchedAt, this.polling = false, this.siteId, this.floorPlanId});

  /// Null until the first answer.
  final RtlOverview? overview;
  final Object? error;
  final DateTime? fetchedAt;
  final bool polling;

  /// The scope sent with every poll.
  final String? siteId, floorPlanId;

  /// Polling works = "LIVE"; the last round failed = "Paused".
  bool get live => polling && error == null;

  RtlState copyWith({
    RtlOverview? overview,
    Object? error,
    bool clearError = false,
    DateTime? fetchedAt,
    bool? polling,
    String? siteId,
    String? floorPlanId,
    bool scope = false,
  }) => RtlState(
    overview: overview ?? this.overview,
    error: clearError ? null : (error ?? this.error),
    fetchedAt: fetchedAt ?? this.fetchedAt,
    polling: polling ?? this.polling,
    siteId: scope ? siteId : this.siteId,
    floorPlanId: scope ? floorPlanId : this.floorPlanId,
  );
}

/// GET /api/real-time-location/overview on attach and every 10 s while at least one visible
/// screen is attached; stops when hidden / backgrounded.
class RtlNotifier extends Notifier<RtlState> implements LivePoller {
  static const interval = Duration(seconds: 10);

  final _owners = <Object>{};
  Timer? _timer;
  bool _busy = false;
  bool _again = false;

  @visibleForTesting
  int rounds = 0;

  @override
  RtlState build() {
    ref.onDispose(() => _timer?.cancel());
    return const RtlState();
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

  /// The site picker / the floor shown changed: poll that scope now.
  void setScope({String? siteId, String? floorPlanId}) {
    if (siteId == state.siteId && floorPlanId == state.floorPlanId) return;
    state = state.copyWith(siteId: siteId, floorPlanId: floorPlanId, scope: true);
    if (state.polling) unawaited(_run());
  }

  /// Pull-to-refresh / Retry.
  Future<void> refresh() => _run();

  Future<void> _run() async {
    if (_busy) {
      _again = true;
      return;
    }
    _busy = true;
    rounds++;
    final scope = (state.siteId, state.floorPlanId);
    try {
      final o = await ref.read(rtlApiProvider).getOverview(siteId: scope.$1, floorPlanId: scope.$2);
      if (!ref.mounted) return;
      if (scope == (state.siteId, state.floorPlanId)) {
        state = state.copyWith(overview: o, fetchedAt: DateTime.now().toUtc(), clearError: true);
      }
    } catch (e) {
      if (!ref.mounted) return;
      state = state.copyWith(error: e);
    } finally {
      _busy = false;
      if (ref.mounted) {
        if (_again) {
          _again = false;
          unawaited(_run());
        } else {
          _schedule();
        }
      }
    }
  }

  void _schedule() {
    _timer?.cancel();
    if (!state.polling) return;
    _timer = TrkLiveNotifier.startTimer(interval, () => unawaited(_run()));
  }
}

final rtlProvider = NotifierProvider<RtlNotifier, RtlState>(RtlNotifier.new);

/// One asset of the overview by registration id: the polled overview when it has it, else one
/// fetch of the whole overview (asset detail / More Actions "Locate", View on Map).
final rtlAssetProvider = FutureProvider.autoDispose.family<RtlAsset?, String>((ref, assetId) async {
  final polled = ref.read(rtlProvider).overview?.asset(assetId);
  if (polled != null) return polled;
  return (await ref.watch(rtlApiProvider).getOverview()).asset(assetId);
});

/// Every asset in view on every floor of [siteId] (null = all sites): the Locate Asset list and
/// "switch to the asset's floor" need assets beyond the floor shown.
final rtlAllAssetsProvider = FutureProvider.autoDispose.family<List<RtlAsset>, String?>(
  (ref, siteId) async => (await ref.watch(rtlApiProvider).getOverview(siteId: siteId)).assets,
);

// ------------------------------------------------------------------ events / trail

typedef RtlEventsQuery = ({String range, String? eventType, String? siteId, String? assetId, int page, int pageSize});

final rtlEventsProvider = FutureProvider.autoDispose.family<RtlEventPage, RtlEventsQuery>(
  (ref, q) => ref
      .watch(rtlApiProvider)
      .getEvents(
        range: q.range,
        eventType: q.eventType,
        siteId: q.siteId,
        assetId: q.assetId,
        page: q.page,
        pageSize: q.pageSize,
      ),
);

typedef RtlTrailQuery = ({String assetId, String range});

/// 404 = "This asset has no tracked tag" (an `ApiException` with kind notFound).
final rtlTrailProvider = FutureProvider.autoDispose.family<RtlTrail, RtlTrailQuery>(
  (ref, q) => ref.watch(rtlApiProvider).getTrail(q.assetId, range: q.range),
);

final locateHistoryProvider = FutureProvider.autoDispose.family<List<LocateCommandRecord>, String>(
  (ref, tagId) => ref.watch(rtlApiProvider).getTagCommands(tagId),
);

// ------------------------------------------------------------------ floors / plans

/// "Site › Building › Floor" options from Master Data.
final rtlFloorOptionsProvider = FutureProvider.autoDispose<List<RtlFloorOption>>((ref) async {
  final r = await Future.wait<Object>([
    ref.watch(mdSitesProvider.future),
    ref.watch(mdBuildingsProvider.future),
    ref.watch(mdFloorsProvider.future),
  ]);
  return rtlFloorOptions(r[0] as List<MdSite>, r[1] as List<MdSpace>, r[2] as List<MdSpace>);
});

/// Every Administration floor plan (all statuses: [chooseFloorPlan] applies the rule).
final rtlFloorPlansProvider = FutureProvider.autoDispose<List<MapFloorPlan>>(
  (ref) => ref.watch(adminMapsApiProvider).getFloorPlans(),
);

/// What the map shows for one Master Data floor (Draft plans only in developer builds).
final rtlFloorChoiceProvider = FutureProvider.autoDispose.family<RtlFloorPlanChoice, String>((ref, floorId) async {
  final floors = await ref.watch(rtlFloorOptionsProvider.future);
  final floor = floors.where((f) => f.floorId == floorId).firstOrNull;
  if (floor == null) return const RtlNoPlan();
  final plans = await ref.watch(rtlFloorPlansProvider.future);
  return chooseFloorPlan(plans, floor.floor, dev: showServerSwitcher);
});

/// Gateway (device) plots of a plan: where assets are drawn.
final rtlDevicePlotsProvider = FutureProvider.autoDispose.family<List<MapPlot>, String>(
  (ref, planId) async => [
    for (final p in await ref.watch(adminMapsApiProvider).getPlots(planId, kind: 'device'))
      if (p.isDevice) p,
  ],
);

/// Zones of a plan (read-only).
final rtlZonesProvider = FutureProvider.autoDispose.family<List<MapZone>, String>(
  (ref, planId) => ref.watch(adminMapsApiProvider).getZones(planId),
);

// ------------------------------------------------------------------ locate

/// Opens a [LocateSession] for the Locate panel (tests override [locateConnectorProvider]).
final locateConnectorProvider = Provider<LocateConnector>((_) => ioLocateChannel);

LocateSession newLocateSession(Ref ref) {
  final auth = ref.read(authProvider.notifier);
  return LocateSession(
    uri: locateSocketUri(trackingBaseUrl),
    token: auth.validAccessToken,
    refreshToken: auth.refreshAccessToken,
    connect: ref.read(locateConnectorProvider),
    schedule: TrkLiveNotifier.startTimer,
  );
}

/// The panel asks for a session through this provider (a factory, so the widget owns it).
final locateSessionFactoryProvider = Provider<LocateSession Function()>((ref) => () => newLocateSession(ref));

/// Everything pull-to-refresh reloads.
void refreshRtl(WidgetRef ref) {
  unawaited(ref.read(rtlProvider.notifier).refresh());
  ref
    ..invalidate(rtlEventsProvider)
    ..invalidate(rtlTrailProvider)
    ..invalidate(rtlFloorPlansProvider)
    ..invalidate(rtlDevicePlotsProvider)
    ..invalidate(rtlZonesProvider);
}
