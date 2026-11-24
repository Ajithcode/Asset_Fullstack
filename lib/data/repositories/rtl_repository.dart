import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart' show showServerSwitcher, trackingBaseUrl;
import 'package:asset360_mobile/data/api/dashboard/network_status_live.dart' show LiveSocketConnector, ioLiveSocket;
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
/// - tags/live WebSocket + overview (30 s) + /tags fallback poll (10 s) while the screen is visible ([LivePoller]
///   through `LivePollingScope`), for the selected site + shown floor plan;
/// - events / trail / locate history on demand (autoDispose families);
/// - floors from Master Data, plans / zones / device plots from Administration.

final rtlApiProvider = Provider<RtlApi>((ref) => RtlApi(ref.watch(trackingClientProvider)));

class RtlState {
  const RtlState({
    this.overview,
    this.error,
    this.fetchedAt,
    this.polling = false,
    this.socketUp = false,
    this.lastFrameAt,
    this.siteId,
    this.floorPlanId,
  });

  /// The last GET /overview with the live tag rows laid over it (`overlayTags`); null until the first answer.
  final RtlOverview? overview;
  final Object? error;
  final DateTime? fetchedAt;

  /// A visible screen is attached (the socket / polls run).
  final bool polling;

  /// The tags/live socket is connected.
  final bool socketUp;

  /// When the last frame (snapshot or update) of the tags socket arrived (header "Live · N s ago").
  final DateTime? lastFrameAt;

  /// The scope sent with every poll and the socket.
  final String? siteId, floorPlanId;

  /// Header badge "Live": attached and the socket is connected. Otherwise "Polling" (the /tags fallback)
  /// while attached, "Paused" when not.
  bool get live => polling && socketUp;

  RtlState copyWith({
    RtlOverview? overview,
    Object? error,
    bool clearError = false,
    DateTime? fetchedAt,
    bool? polling,
    bool? socketUp,
    DateTime? lastFrameAt,
    String? siteId,
    String? floorPlanId,
    bool scope = false,
  }) => RtlState(
    overview: overview ?? this.overview,
    error: clearError ? null : (error ?? this.error),
    fetchedAt: fetchedAt ?? this.fetchedAt,
    polling: polling ?? this.polling,
    socketUp: socketUp ?? this.socketUp,
    lastFrameAt: lastFrameAt ?? this.lastFrameAt,
    siteId: scope ? siteId : this.siteId,
    floorPlanId: scope ? floorPlanId : this.floorPlanId,
  );
}

/// What the Diagnostics screen shows about the tags/live socket ("Live map socket"). Never holds a token.
class RtlSocketDiag {
  const RtlSocketDiag({
    this.url,
    this.lastSnapshotAt,
    this.lastUpdateAt,
    this.frames = 0,
    this.failures = 0,
    this.closeCode,
    this.closeReason,
    this.lastError,
  });

  /// The socket URL without `access_token`.
  final String? url;
  final DateTime? lastSnapshotAt, lastUpdateAt;
  final int frames;

  /// Failed / dropped attempts since the last good connection.
  final int failures;
  final int? closeCode;
  final String? closeReason, lastError;

  RtlSocketDiag copyWith({
    String? url,
    DateTime? lastSnapshotAt,
    DateTime? lastUpdateAt,
    int? frames,
    int? failures,
    int? closeCode,
    String? closeReason,
    bool closed = false,
    String? lastError,
  }) => RtlSocketDiag(
    url: url ?? this.url,
    lastSnapshotAt: lastSnapshotAt ?? this.lastSnapshotAt,
    lastUpdateAt: lastUpdateAt ?? this.lastUpdateAt,
    frames: frames ?? this.frames,
    failures: failures ?? this.failures,
    closeCode: closed ? closeCode : this.closeCode,
    closeReason: closed ? closeReason : this.closeReason,
    lastError: lastError ?? this.lastError,
  );

  /// Live | Reconnecting (down, retrying after a drop) | Polling (down, first connect / fallback) | Paused (screen not on).
  String stateOf(RtlState s) => !s.polling
      ? 'Paused'
      : s.socketUp
      ? 'Live'
      : failures > 0
      ? 'Reconnecting'
      : 'Polling';

  /// Plain text for Copy report.
  String report(RtlState s, DateTime now) {
    String at(DateTime? t) => t == null ? 'never' : '${t.toIso8601String()} (${rtlAgo(t, now)})';
    return [
      'Live map socket',
      '  state: ${stateOf(s)}',
      '  url: ${url ?? '-'}',
      '  last snapshot: ${at(lastSnapshotAt)}',
      '  last update: ${at(lastUpdateAt)}',
      '  frames received: $frames',
      '  failed attempts: $failures',
      '  last close: ${closeCode == null && closeReason == null ? '-' : 'code ${closeCode ?? '-'}, reason ${closeReason?.isEmpty ?? true ? '-' : closeReason}'}',
      '  last error: ${lastError ?? '-'}',
    ].join('\n');
  }
}

class RtlSocketDiagNotifier extends Notifier<RtlSocketDiag> {
  @override
  RtlSocketDiag build() => const RtlSocketDiag();

  void change(RtlSocketDiag Function(RtlSocketDiag d) f) {
    if (ref.mounted) state = f(state);
  }
}

final rtlSocketDiagProvider = NotifierProvider<RtlSocketDiagNotifier, RtlSocketDiag>(RtlSocketDiagNotifier.new);

/// While at least one visible screen is attached (stops when hidden / backgrounded):
/// - the tags/live WebSocket (`TagsLiveSocket`, reconnect 1 s → 30 s with a fresh token) lays the newest tag
///   rows over the overview (location, freshness, rssi, battery, Moving / Stationary, counters);
/// - GET /overview every [interval] (30 s): alerts, confidence, expected location, gateways and the asset list;
/// - GET /tags every [tagPollInterval] (10 s) while the socket is down (and once at the start, so the
///   map has tag rows before the socket's snapshot).
class RtlNotifier extends Notifier<RtlState> implements LivePoller {
  static const interval = Duration(seconds: 30);
  static const tagPollInterval = Duration(seconds: 10);

  /// Tests replace the socket connector.
  static LiveSocketConnector? debugConnector;

  final _owners = <Object>{};
  final _tags = TagLiveList();
  final _movedAt = <String, DateTime>{};
  RtlOverview? _base;
  Timer? _timer, _tagTimer, _moveTimer;
  TagsLiveSocket? _socket;
  bool _busy = false;
  bool _again = false;
  bool _tagBusy = false;

  @visibleForTesting
  int rounds = 0;

  @visibleForTesting
  int tagRounds = 0;

  @visibleForTesting
  TagsLiveSocket? get socket => _socket;

  /// Tests pin "now".
  @visibleForTesting
  static DateTime Function()? debugNow;

  DateTime _now() => (debugNow ?? DateTime.now)().toUtc();

  @override
  RtlState build() {
    ref.onDispose(() {
      _timer?.cancel();
      _tagTimer?.cancel();
      _moveTimer?.cancel();
      unawaited(_socket?.stop());
    });
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
      _startSocket();
      unawaited(_pollTags());
    } else {
      _timer?.cancel();
      _timer = null;
      _tagTimer?.cancel();
      _tagTimer = null;
      _moveTimer?.cancel();
      _moveTimer = null;
      unawaited(_socket?.stop());
      _socket = null;
      // Rows older than the pause must not look like moves when the screen comes back.
      _tags.clear();
      _movedAt.clear();
      if (state.socketUp) state = state.copyWith(socketUp: false);
    }
  }

  /// The site picker / the floor shown changed: poll that scope now and reconnect the socket with it.
  void setScope({String? siteId, String? floorPlanId}) {
    if (siteId == state.siteId && floorPlanId == state.floorPlanId) return;
    state = state.copyWith(siteId: siteId, floorPlanId: floorPlanId, scope: true);
    if (!state.polling) return;
    _tags.clear();
    _movedAt.clear();
    unawaited(_socket?.stop());
    _startSocket();
    unawaited(_run());
    unawaited(_pollTags());
  }

  /// Pull-to-refresh / Retry.
  Future<void> refresh() => _run();

  // ------------------------------------------------------------------ overview (every 30 s)

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
        _base = o;
        state = state.copyWith(error: null, clearError: true);
        _publish();
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

  // ------------------------------------------------------------------ tags (socket, else GET /tags)

  void _startSocket() {
    final auth = ref.read(authProvider.notifier);
    final scope = (state.siteId, state.floorPlanId);
    late final TagsLiveSocket socket;
    socket = TagsLiveSocket(
      uri: tagsLiveUri(trackingBaseUrl, siteId: scope.$1, floorPlanId: scope.$2),
      token: auth.validAccessToken,
      connect: (uri, token) => (debugConnector ?? ioLiveSocket)(uri, token),
      schedule: TrkLiveNotifier.startTimer,
      onMessage: (m) {
        if (!ref.mounted || !identical(_socket, socket)) return;
        final moved = switch (m) {
          TagSnapshot(:final tags) => _tags.replace(tags),
          TagUpdate() => _tags.apply(m),
        };
        final at = _now();
        state = state.copyWith(lastFrameAt: at);
        ref
            .read(rtlSocketDiagProvider.notifier)
            .change(
              (d) => d.copyWith(
                frames: d.frames + 1,
                failures: 0,
                lastSnapshotAt: m is TagSnapshot ? at : null,
                lastUpdateAt: m is TagUpdate ? at : null,
              ),
            );
        _noteMoves(moved);
        _publish();
      },
      onClosed: (code, reason) {
        if (!identical(_socket, socket)) return;
        ref
            .read(rtlSocketDiagProvider.notifier)
            .change((d) => d.copyWith(failures: d.failures + 1, closed: true, closeCode: code, closeReason: reason));
      },
      onError: (message) {
        if (!identical(_socket, socket)) return;
        ref.read(rtlSocketDiagProvider.notifier).change((d) => d.copyWith(lastError: message));
      },
      onConnection: (up) {
        if (!ref.mounted || !identical(_socket, socket)) return;
        state = state.copyWith(socketUp: up);
        if (up) {
          _tagTimer?.cancel();
          _tagTimer = null;
        } else if (state.polling) {
          unawaited(_pollTags());
        }
      },
    );
    ref
        .read(rtlSocketDiagProvider.notifier)
        .change((d) => d.copyWith(url: withoutAccessToken(socket.uri).toString(), failures: 0));
    _socket = socket..start();
  }

  /// GET /tags now, then every [tagPollInterval] while attached and the socket is not connected.
  Future<void> _pollTags() async {
    _tagTimer?.cancel();
    _tagTimer = null;
    if (!state.polling || state.socketUp || _tagBusy) return;
    _tagBusy = true;
    tagRounds++;
    final scope = (state.siteId, state.floorPlanId);
    try {
      final rows = await ref.read(rtlApiProvider).getTags(siteId: scope.$1, floorPlanId: scope.$2);
      if (!ref.mounted) return;
      if (scope == (state.siteId, state.floorPlanId) && !state.socketUp) {
        _noteMoves(_tags.replace(rows));
        _publish();
      }
    } catch (_) {
      // Keep the rows we have; the next round tries again.
    } finally {
      _tagBusy = false;
      if (ref.mounted && state.polling && !state.socketUp) {
        _tagTimer = TrkLiveNotifier.startTimer(tagPollInterval, () => unawaited(_pollTags()));
      }
    }
  }

  // ------------------------------------------------------------------ overlay

  void _noteMoves(Set<String> moved) {
    final now = _now();
    for (final k in moved) {
      _movedAt[k] = now;
    }
  }

  /// The overview with the tag rows laid over it; re-run when the oldest "moving" mark expires.
  void _publish() {
    final base = _base;
    if (base == null || !ref.mounted) return;
    final now = _now();
    _movedAt.removeWhere((_, t) => now.difference(t) > movingWithin);
    state = state.copyWith(overview: overlayTags(base, _tags, _movedAt, now), fetchedAt: now);
    _moveTimer?.cancel();
    _moveTimer = null;
    if (_movedAt.isNotEmpty && state.polling) {
      final oldest = _movedAt.values.reduce((a, b) => a.isBefore(b) ? a : b);
      final wait = movingWithin - now.difference(oldest) + const Duration(seconds: 1);
      _moveTimer = TrkLiveNotifier.startTimer(wait, _publish);
    }
  }
}

final rtlProvider = NotifierProvider<RtlNotifier, RtlState>(RtlNotifier.new);

/// One asset of the overview by registration id: the polled overview when it has it, else one
/// fetch of the whole overview (asset detail / More Actions "Locate", View on Map).
/// No automatic retry: a failed lookup means "no tag known" (the Locate tile / View on Map must not
/// wait on Riverpod's retry backoff).
final rtlAssetProvider = FutureProvider.autoDispose.family<RtlAsset?, String>((ref, assetId) async {
  final polled = ref.read(rtlProvider).overview?.asset(assetId);
  if (polled != null) return polled;
  return (await ref.watch(rtlApiProvider).getOverview()).asset(assetId);
}, retry: (_, _) => null);

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
