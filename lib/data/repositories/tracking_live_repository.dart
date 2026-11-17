import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';

import 'package:asset360_mobile/data/api/admin/admin_maps_api.dart';
import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/api/dashboard/network_status_live.dart' show LiveScheduler;
import 'package:asset360_mobile/data/api/media_api.dart';
import 'package:asset360_mobile/data/api/tracking/tracking_api.dart';
import 'package:asset360_mobile/data/tracking/tracked_assets.dart';

export 'package:asset360_mobile/data/api/admin/admin_maps_api.dart';
export 'package:asset360_mobile/data/api/tracking/tracking_api.dart';
export 'package:asset360_mobile/data/tracking/tracked_assets.dart';

/// API mode Tracking & IoT: providers over the Tracking service (TRACKING_BASE_URL) and the
/// Administration map system (ADMIN_BASE_URL). Everything is read-only.

final trackingApiProvider = Provider<TrackingApi>((ref) => TrackingApi(ref.watch(trackingClientProvider)));
final adminMapsApiProvider = Provider<AdminMapsApi>((ref) => AdminMapsApi(ref.watch(adminClientProvider)));

// ------------------------------------------------------------------ live polling

/// The live state the Tracking screens share.
class TrkLiveState {
  const TrkLiveState({this.snapshot, this.alertCount, this.error, this.tick = 0, this.polling = false});

  /// Null until the first fetch finished.
  final TrackingSnapshot? snapshot;
  final TrkAlertCount? alertCount;

  /// The last failed round (kept while older data stays on screen).
  final Object? error;

  /// +1 after every successful round: floor-plan views refetch their plots on it.
  final int tick;

  /// True while a screen that shows live data is visible and the app is in the foreground.
  final bool polling;

  DateTime? get updatedAt => snapshot?.fetchedAt;

  TrkLiveState copyWith({
    TrackingSnapshot? snapshot,
    TrkAlertCount? alertCount,
    Object? error,
    bool clearError = false,
    int? tick,
    bool? polling,
  }) => TrkLiveState(
    snapshot: snapshot ?? this.snapshot,
    alertCount: alertCount ?? this.alertCount,
    error: clearError ? null : (error ?? this.error),
    tick: tick ?? this.tick,
    polling: polling ?? this.polling,
  );
}

/// Polls the Tracking service every [interval] while at least one live screen is visible
/// ([attach] / [detach], driven by `LivePollingScope`), and stops when none is (hidden behind
/// another route, another tab, or the app in the background).
///
/// Each round: devices + device health (last seen, battery, signal) + the alert count. Every
/// [fullEvery]-th round (and the first) also location events, tags and sensors: GET
/// /api/location-events has NO filters or paging and returns the whole log, so it is fetched
/// 6× less often and only the last 7 days are kept (see docs/tracking_iot_report.md).
class TrkLiveNotifier extends Notifier<TrkLiveState> {
  static const interval = Duration(seconds: 5);
  static const fullEvery = 6;

  /// Tests replace the timer.
  @visibleForTesting
  static LiveScheduler? debugSchedule;

  /// Tests pin "now".
  @visibleForTesting
  static DateTime Function()? debugNow;

  final _owners = <Object>{};
  Timer? _timer;
  int _round = 0;
  bool _busy = false;

  DateTime get _now => (debugNow ?? DateTime.now)().toUtc();

  @override
  TrkLiveState build() {
    ref.onDispose(() => _timer?.cancel());
    return const TrkLiveState();
  }

  /// Number of visible screens asking for live data.
  int get listeners => _owners.length;

  void attach(Object owner) {
    _owners.add(owner);
    _sync();
  }

  void detach(Object owner) {
    _owners.remove(owner);
    _sync();
  }

  void _sync() {
    // A screen's deferred detach can arrive after the container was disposed (sign-out, tests).
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
    _timer = (debugSchedule ?? Timer.new)(interval, () => unawaited(_run()));
  }

  /// Pull-to-refresh / Retry: a full round now.
  Future<void> refresh() => _run(full: true);

  Future<void> _run({bool full = false}) async {
    if (_busy) return;
    _busy = true;
    final api = ref.read(trackingApiProvider);
    final doFull = full || state.snapshot == null || _round % fullEvery == 0;
    _round++;
    try {
      final devices = api.getDevices();
      final health = api.getDeviceHealth();
      final count = api.getAlertCount();
      final TrackingSnapshot next;
      if (doFull) {
        next = TrackingSnapshot(
          devices: await devices,
          health: await health,
          tags: await api.getTags(),
          sensors: await api.getSensors(),
          events: await api.getLocationEvents(),
          fetchedAt: _now,
        );
      } else {
        next = state.snapshot!.withDevices(await devices, await health, _now);
      }
      final c = await count;
      if (!ref.mounted) return;
      state = state.copyWith(snapshot: next, alertCount: c, clearError: true, tick: state.tick + 1);
    } catch (e) {
      if (!ref.mounted) return;
      state = state.copyWith(error: e);
    } finally {
      _busy = false;
      if (ref.mounted) _schedule();
    }
  }
}

final trkLiveProvider = NotifierProvider<TrkLiveNotifier, TrkLiveState>(TrkLiveNotifier.new);

/// One snapshot for screens that are not live (asset IoT tab): the poller's when it is less than
/// a minute old, else one full fetch.
final trackingSnapshotOnceProvider = FutureProvider.autoDispose<TrackingSnapshot>((ref) async {
  final live = ref.read(trkLiveProvider).snapshot;
  final now = DateTime.now().toUtc();
  if (live != null && now.difference(live.fetchedAt) < const Duration(minutes: 1)) return live;
  final api = ref.watch(trackingApiProvider);
  final r = await Future.wait([
    api.getDevices(),
    api.getDeviceHealth(),
    api.getTags(),
    api.getSensors(),
    api.getLocationEvents(),
  ]);
  return TrackingSnapshot(
    devices: r[0] as List<TrkDevice>,
    health: r[1] as List<TrkDeviceHealth>,
    tags: r[2] as List<TrkTag>,
    sensors: r[3] as List<TrkSensor>,
    events: r[4] as List<TrkLocationEvent>,
    fetchedAt: now,
  );
});

// ------------------------------------------------------------------ sensors + telemetry

final trkSensorsProvider = FutureProvider.autoDispose<List<TrkSensor>>(
  (ref) => ref.watch(trackingApiProvider).getSensors(),
);

final trkSensorProvider = FutureProvider.autoDispose.family<TrkSensor, String>(
  (ref, id) => ref.watch(trackingApiProvider).getSensor(id),
);

final trkSensorTypesProvider = FutureProvider.autoDispose<List<TrkSensorType>>(
  (ref) => ref.watch(trackingApiProvider).getSensorTypes(),
);

final trkSensorStatusesProvider = FutureProvider.autoDispose<List<String>>(
  (ref) => ref.watch(trackingApiProvider).getSensorStatuses(),
);

/// Readings of one sensor (or one device when [sensorId] is null) for [range], OLDEST first.
/// Asks the server's max [TrackingApi.maxReadings]; a fast sampler can still exceed it over 7 d,
/// in which case the chart starts later (the server sends the newest).
typedef ReadingsQuery = ({String? sensorId, String? deviceId, TrackRange range});

final trkReadingsProvider = FutureProvider.autoDispose.family<List<TrkReading>, ReadingsQuery>((ref, q) async {
  final rows = await ref
      .watch(trackingApiProvider)
      .getReadings(
        days: q.range.serverDays,
        sensorId: q.sensorId,
        deviceId: q.sensorId == null ? q.deviceId : null,
        limit: TrackingApi.maxReadings,
      );
  return readingsIn(rows, q.range, DateTime.now().toUtc());
});

/// Org-wide IoT events per day (7 / 30 / 90 days).
final trkTimelineProvider = FutureProvider.autoDispose.family<TrkTimeline, int>(
  (ref, days) => ref.watch(trackingApiProvider).getTimeline(days: days),
);

// ------------------------------------------------------------------ alerts + geofences

/// Alerts raised in the last [days] (the same window as the alert count), newest first.
final trkAlertsProvider = FutureProvider.autoDispose.family<List<TrkAlert>, int>(
  (ref, days) => ref.watch(trackingApiProvider).getAlerts(days: days),
);

/// All alerts of one asset (registration id), newest first.
final trkAssetAlertsProvider = FutureProvider.autoDispose.family<List<TrkAlert>, String>(
  (ref, id) => ref.watch(trackingApiProvider).getAlerts(assetRegistrationId: id, limit: 50),
);

final trkGeofencesProvider = FutureProvider.autoDispose<List<TrkGeofence>>(
  (ref) => ref.watch(trackingApiProvider).getGeofences(),
);

// ------------------------------------------------------------------ floor plans

final mapSettingsProvider = FutureProvider.autoDispose<MapSettings>(
  (ref) => ref.watch(adminMapsApiProvider).getSettings(),
);

/// The published floor plan of a Master Data location id, or null.
final publishedPlanProvider = FutureProvider.autoDispose.family<MapFloorPlan?, String>(
  (ref, locationId) => ref.watch(adminMapsApiProvider).getPublishedPlan(locationId),
);

final planZonesProvider = FutureProvider.autoDispose.family<List<MapZone>, String>(
  (ref, planId) => ref.watch(adminMapsApiProvider).getZones(planId),
);

/// Plots of a plan; refetched after every live round so a moved plot moves on screen.
final planPlotsProvider = FutureProvider.autoDispose.family<List<MapPlot>, String>((ref, planId) {
  ref.watch(trkLiveProvider.select((s) => s.tick));
  return ref.watch(adminMapsApiProvider).getPlots(planId);
});

final planLayersProvider = FutureProvider.autoDispose.family<List<MapLayer>, String>(
  (ref, planId) => ref.watch(adminMapsApiProvider).getLayers(planId),
);

/// Every zone flagged as a geofence on every PUBLISHED floor plan, with its plan.
final geofenceZonesProvider = FutureProvider.autoDispose<List<({MapFloorPlan plan, MapZone zone})>>((ref) async {
  final api = ref.watch(adminMapsApiProvider);
  final plans = await api.getFloorPlans(status: 'Published');
  final out = <({MapFloorPlan plan, MapZone zone})>[];
  // 4 at a time: one request per plan.
  for (var i = 0; i < plans.length; i += 4) {
    final chunk = plans.skip(i).take(4).toList();
    final zones = await Future.wait(chunk.map((p) => api.getZones(p.floorPlanId)));
    for (final (j, list) in zones.indexed) {
      for (final z in list) {
        if (z.geofence) out.add((plan: chunk[j], zone: z));
      }
    }
  }
  return out;
});

/// A decoded plan drawing: bytes + pixel size (for the aspect ratio when the plan has no size in
/// metres).
class PlanImage {
  const PlanImage(this.bytes, this.width, this.height);

  final Uint8List bytes;
  final int width, height;

  double get aspect => height == 0 ? 1 : width / height;
}

/// Floor-plan drawings, cached in memory for the session and on disk (app cache directory) across
/// restarts, keyed by URL. A new plan version has a new file URL, so a cached file never goes stale.
class FloorPlanImageCache {
  FloorPlanImageCache(this._dio, {Future<(int, int)> Function(Uint8List bytes)? sizeOf}) : _sizeOf = sizeOf ?? _size;

  final Dio _dio;

  /// Pixel size of a drawing (tests stub it: decoding needs the engine).
  final Future<(int, int)> Function(Uint8List bytes) _sizeOf;
  final _memory = <String, PlanImage>{};
  final _pending = <String, Future<PlanImage>>{};

  /// Tests count downloads.
  @visibleForTesting
  int downloads = 0;

  /// Tests skip the disk.
  @visibleForTesting
  static bool useDisk = !kIsWeb;

  PlanImage? peek(String url) => _memory[url];

  Future<PlanImage> load(String url) {
    final hit = _memory[url];
    if (hit != null) return Future.value(hit);
    // A block body: returning the removed future would make whenComplete wait for itself.
    return _pending[url] ??= _load(url).whenComplete(() {
      _pending.remove(url);
    });
  }

  Future<PlanImage> _load(String url) async {
    final file = await _file(url);
    Uint8List? bytes;
    if (file != null && await file.exists()) bytes = await file.readAsBytes();
    if (bytes == null) {
      downloads++;
      try {
        final r = await _dio.get<List<int>>(url, options: Options(responseType: ResponseType.bytes));
        bytes = Uint8List.fromList(r.data ?? const []);
      } on DioException catch (e) {
        throw ApiException.fromDio(e);
      }
      if (file != null) unawaited(file.writeAsBytes(bytes, flush: true).catchError((_) => file));
    }
    final size = await _sizeOf(bytes);
    return _memory[url] = PlanImage(bytes, size.$1, size.$2);
  }

  static Future<(int, int)> _size(Uint8List bytes) async {
    try {
      final buffer = await ui.ImmutableBuffer.fromUint8List(bytes);
      final d = await ui.ImageDescriptor.encoded(buffer);
      final s = (d.width, d.height);
      d.dispose();
      buffer.dispose();
      return s;
    } catch (_) {
      return (0, 0);
    }
  }

  /// A stable file name for [url] (64-bit FNV-1a of its UTF-16 code units, hex).
  @visibleForTesting
  static String fileKey(String url) {
    var h = 0xcbf29ce484222325;
    for (final c in url.codeUnits) {
      h ^= c;
      h = (h * 0x100000001b3) & 0xFFFFFFFFFFFFFFFF;
    }
    String hex32(int v) => (v & 0xFFFFFFFF).toRadixString(16).padLeft(8, '0');
    return '${hex32(h >> 32)}${hex32(h)}';
  }

  /// Deletes the drawings saved on disk (server switch: they belong to the old server).
  static Future<void> clearDisk() async {
    if (!useDisk) return;
    try {
      final dir = Directory('${(await getApplicationCacheDirectory()).path}/floor_plans');
      if (await dir.exists()) await dir.delete(recursive: true);
    } catch (_) {}
  }

  static Future<File?> _file(String url) async {
    if (!useDisk) return null;
    try {
      final dir = await getApplicationCacheDirectory();
      return File('${dir.path}/floor_plans/${fileKey(url)}')..parent.createSync(recursive: true);
    } catch (_) {
      return null;
    }
  }
}

final floorPlanImageCacheProvider = Provider<FloorPlanImageCache>(
  (ref) => FloorPlanImageCache(ref.watch(mediaDioProvider)),
);

/// The drawing of a plan ([MapFloorPlan.fileUrl], made loadable with [resolveMediaUrl]).
final planImageProvider = FutureProvider.autoDispose.family<PlanImage, String>(
  (ref, fileUrl) => ref.watch(floorPlanImageCacheProvider).load(resolveMediaUrl(fileUrl)),
);

/// Everything `refresh` reloads.
void refreshTracking(WidgetRef ref) {
  unawaited(ref.read(trkLiveProvider.notifier).refresh());
  ref
    ..invalidate(trkSensorsProvider)
    ..invalidate(trkGeofencesProvider)
    ..invalidate(geofenceZonesProvider)
    ..invalidate(trkAlertsProvider)
    ..invalidate(trackingSnapshotOnceProvider);
}
