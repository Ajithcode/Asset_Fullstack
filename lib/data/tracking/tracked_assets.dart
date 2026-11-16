import 'package:asset360_mobile/data/api/tracking/tracking_models.dart';

/// Pure logic of the live Tracking module: joins the Tracking service lists into one row per
/// tracked asset. No Flutter, no I/O, so tests feed it contract-shaped JSON directly.

/// A device / tag / asset not heard from for longer than this is shown grey ("stale").
const staleAfter = Duration(minutes: 10);

/// Battery at or below this percent counts as Low Battery.
const lowBatteryPct = 20;

/// Location events older than this are dropped after each fetch (the endpoint has no time filter;
/// 7 days = the longest trail the app offers).
const keepEventsFor = Duration(days: 7);

/// Live / stale / never seen.
enum Freshness { live, stale, unknown }

Freshness freshnessOf(DateTime? lastSeen, DateTime now) {
  if (lastSeen == null) return Freshness.unknown;
  return now.difference(lastSeen) > staleAfter ? Freshness.stale : Freshness.live;
}

bool isStale(DateTime? lastSeen, DateTime now) => freshnessOf(lastSeen, now) != Freshness.live;

DateTime? _latest(Iterable<DateTime?> times) {
  DateTime? best;
  for (final t in times) {
    if (t != null && (best == null || t.isAfter(best))) best = t;
  }
  return best;
}

String _norm(String? s) => (s ?? '').trim().toLowerCase();

/// Where an asset is, as far as the server knows: the zone NAME of its latest location event
/// (null after a "Departed Zone" event). Never an x / y — the server sends none.
class ZonePosition {
  const ZonePosition({required this.zoneName, required this.since, required this.eventType});

  final String zoneName;
  final DateTime since;
  final String eventType;
}

/// One row of the tracked asset list: an asset with a device or tag linked, or with location
/// events. [key] = [assetRegistrationId] when the server gives one, else the location-event
/// asset text (e.g. "FL-008").
class TrackedAsset {
  const TrackedAsset({
    required this.key,
    this.assetRegistrationId,
    this.code,
    this.name = '',
    this.siteId,
    this.siteName,
    this.locationName,
    this.devices = const [],
    this.tags = const [],
    this.health = const [],
    this.sensors = const [],
    this.zone,
    this.lastEventAt,
  });

  final String key;
  final String? assetRegistrationId, code, siteId, siteName, locationName;
  final String name;
  final List<TrkDevice> devices;
  final List<TrkTag> tags;
  final List<TrkDeviceHealth> health;
  final List<TrkSensor> sensors;
  final ZonePosition? zone;
  final DateTime? lastEventAt;

  String get title => name.isNotEmpty ? name : (code ?? key);
  String get subtitle => code ?? assetRegistrationId ?? key;

  /// Latest of every device / health / tag last-seen and the latest location event.
  DateTime? get lastSeen => _latest([
    for (final d in devices) d.lastSeenAt,
    for (final h in health) h.lastSeenAt,
    for (final t in tags) t.lastSeenAt,
    lastEventAt,
  ]);

  /// Battery of the most recently seen battery device (health rows first).
  int? get battery {
    final rows = [
      for (final h in health)
        if (h.batteryLevel != null) (h.lastSeenAt, h.batteryLevel!),
      for (final d in devices)
        if (d.batteryLevel != null) (d.lastSeenAt, d.batteryLevel!),
    ]..sort((a, b) => (b.$1 ?? DateTime(0)).compareTo(a.$1 ?? DateTime(0)));
    return rows.firstOrNull?.$2;
  }

  /// Signal of the most recently seen health row.
  TrkDeviceHealth? get signal {
    final rows = [
      for (final h in health)
        if (h.signalStrengthDbm != null || h.signalQuality != null) h,
    ]..sort((a, b) => (b.lastSeenAt ?? DateTime(0)).compareTo(a.lastSeenAt ?? DateTime(0)));
    return rows.firstOrNull;
  }

  bool get lowBattery => (battery ?? 100) <= lowBatteryPct;

  Freshness freshness(DateTime now) => freshnessOf(lastSeen, now);
  bool isOnline(DateTime now) => freshness(now) == Freshness.live;

  /// The zone name, else the tag's location, else the site.
  String get whereLabel => zone?.zoneName ?? locationName ?? siteName ?? '';

  /// Codes a location event may use for this asset.
  Set<String> get matchKeys => {
    for (final s in [
      assetRegistrationId,
      code,
      key,
      ...devices.map((d) => d.assetCode),
      ...sensors.map((s) => s.assetCode),
    ])
      if (_norm(s).isNotEmpty) _norm(s),
  };

  bool matches(String query) {
    final q = _norm(query);
    if (q.isEmpty) return true;
    return [
      name,
      code,
      assetRegistrationId,
      key,
      whereLabel,
      siteName,
      ...devices.map((d) => d.deviceId),
      ...tags.map((t) => t.tagId),
    ].any((s) => _norm(s).contains(q));
  }
}

/// Everything the live Tracking screens show, from one round of Tracking service calls.
class TrackingSnapshot {
  TrackingSnapshot({
    required this.devices,
    required this.health,
    required this.tags,
    required this.sensors,
    required List<TrkLocationEvent> events,
    required this.fetchedAt,
  }) : events = [
         for (final e in events)
           if (fetchedAt.difference(e.eventTime) <= keepEventsFor) e,
       ]..sort((a, b) => b.eventTime.compareTo(a.eventTime)) {
    assets = buildTrackedAssets(devices: devices, health: health, tags: tags, sensors: sensors, events: this.events);
  }

  final List<TrkDevice> devices;
  final List<TrkDeviceHealth> health;
  final List<TrkTag> tags;
  final List<TrkSensor> sensors;

  /// The last [keepEventsFor] of location events, newest first.
  final List<TrkLocationEvent> events;
  final DateTime fetchedAt;
  late final List<TrackedAsset> assets;

  /// The same lists with fresher devices / health (the 5 s refresh between full fetches).
  TrackingSnapshot withDevices(List<TrkDevice> devices, List<TrkDeviceHealth> health, DateTime at) =>
      TrackingSnapshot(devices: devices, health: health, tags: tags, sensors: sensors, events: events, fetchedAt: at);

  TrackedAsset? byKey(String key) {
    final k = _norm(key);
    return assets.where((a) => _norm(a.key) == k || a.matchKeys.contains(k)).firstOrNull;
  }

  TrkDevice? device(String deviceId) => devices.where((d) => _norm(d.deviceId) == _norm(deviceId)).firstOrNull;

  /// Events of one asset within [window] of [now], newest first.
  List<TrkLocationEvent> eventsOf(TrackedAsset a, {required Duration window, required DateTime now}) {
    final keys = a.matchKeys;
    return [
      for (final e in events)
        if (keys.contains(_norm(e.assetId)) && now.difference(e.eventTime) <= window) e,
    ];
  }

  /// Assets whose current zone has this name or code (case-insensitive).
  List<TrackedAsset> inZone({required String name, String code = ''}) => [
    for (final a in assets)
      if (a.zone != null && {_norm(name), _norm(code)}.contains(_norm(a.zone!.zoneName))) a,
  ];

  int online(DateTime now) => assets.where((a) => a.isOnline(now)).length;
  int offline(DateTime now) => assets.length - online(now);
  int get lowBattery => assets.where((a) => a.lowBattery).length;
}

/// Joins the lists into one [TrackedAsset] per asset:
/// * devices, tags, health rows and sensors with an `assetRegistrationId` group under that id;
/// * a location event joins the asset whose registration id or asset code (device / sensor
///   `assetCode`) equals its `assetId` text; otherwise it starts its own row keyed by that text;
/// * the asset's zone = its newest event (null when that event is "Departed Zone").
/// Sorted by last seen, newest first.
List<TrackedAsset> buildTrackedAssets({
  required List<TrkDevice> devices,
  required List<TrkDeviceHealth> health,
  required List<TrkTag> tags,
  required List<TrkSensor> sensors,
  required List<TrkLocationEvent> events,
}) {
  final byId = <String, _Acc>{};
  _Acc acc(String id) => byId.putIfAbsent(_norm(id), () => _Acc(id));

  for (final d in devices) {
    final id = d.assetRegistrationId;
    if (id == null || d.isGateway) continue;
    acc(id)
      ..devices.add(d)
      ..code ??= d.assetCode
      ..name ??= d.assetName
      ..siteId ??= d.siteId
      ..siteName ??= d.siteName;
  }
  for (final t in tags) {
    final id = t.assetRegistrationId;
    if (id == null) continue;
    acc(id)
      ..tags.add(t)
      ..name ??= t.assetName
      ..siteId ??= t.siteId
      ..siteName ??= t.siteName
      ..locationName ??= t.locationName;
  }
  for (final h in health) {
    final id = h.assetRegistrationId;
    if (id == null) continue;
    acc(id)
      ..health.add(h)
      ..name ??= h.assetName
      ..siteId ??= h.siteId
      ..siteName ??= h.siteName;
  }
  // Sensors only enrich assets that already have a device / tag (they carry no position).
  for (final s in sensors) {
    final a = s.assetRegistrationId == null ? null : byId[_norm(s.assetRegistrationId)];
    if (a == null) continue;
    a
      ..sensors.add(s)
      ..code ??= s.assetCode;
  }

  // Event asset text → the asset it names.
  final alias = <String, _Acc>{};
  for (final a in byId.values) {
    for (final k in [a.id, a.code, ...a.devices.map((d) => d.assetCode), ...a.sensors.map((s) => s.assetCode)]) {
      if (_norm(k).isNotEmpty) alias.putIfAbsent(_norm(k), () => a);
    }
  }
  final sorted = [...events]..sort((a, b) => b.eventTime.compareTo(a.eventTime));
  for (final e in sorted) {
    final a = alias[_norm(e.assetId)] ?? alias.putIfAbsent(_norm(e.assetId), () => acc(e.assetId)..code = e.assetId);
    a.name ??= e.assetName.isEmpty ? null : e.assetName;
    if (a.lastEventAt == null) {
      a.lastEventAt = e.eventTime;
      a.zone = e.placesAssetInZone && e.zoneName.isNotEmpty
          ? ZonePosition(zoneName: e.zoneName, since: e.eventTime, eventType: e.eventType)
          : null;
    }
  }

  final out = [for (final a in byId.values) a.build()];
  out.sort((a, b) => (b.lastSeen ?? DateTime(0)).compareTo(a.lastSeen ?? DateTime(0)));
  return out;
}

class _Acc {
  _Acc(this.id);

  final String id;
  String? code, name, siteId, siteName, locationName;
  final devices = <TrkDevice>[];
  final tags = <TrkTag>[];
  final health = <TrkDeviceHealth>[];
  final sensors = <TrkSensor>[];
  ZonePosition? zone;
  DateTime? lastEventAt;

  /// True when the server gave a registration id (a device / tag / health row named it).
  bool get registered => devices.isNotEmpty || tags.isNotEmpty || health.isNotEmpty;

  TrackedAsset build() => TrackedAsset(
    key: id,
    assetRegistrationId: registered ? id : null,
    code: code,
    name: name ?? '',
    siteId: siteId,
    siteName: siteName,
    locationName: locationName,
    devices: devices,
    tags: tags,
    health: health,
    sensors: sensors,
    zone: zone,
    lastEventAt: lastEventAt,
  );
}

/// The window of a trail / chart.
enum TrackRange {
  hour('1 h', Duration(hours: 1)),
  day('24 h', Duration(hours: 24)),
  week('7 d', Duration(days: 7));

  const TrackRange(this.label, this.duration);
  final String label;
  final Duration duration;

  /// `days` to ask GET /telemetry-readings for: the server counts WHOLE UTC days ending today
  /// (days=1 = since 00:00 UTC only), so one extra day is asked (an hour may cross midnight UTC)
  /// and the phone trims to [duration].
  int get serverDays => switch (this) {
    hour || day => 2,
    week => 8,
  };

  static TrackRange? byName(String? n) => values.where((r) => r.name == n).firstOrNull;
}

/// Readings within [range] of [now], OLDEST first (for charts).
List<TrkReading> readingsIn(List<TrkReading> newestFirst, TrackRange range, DateTime now) => [
  for (final r in newestFirst.reversed)
    if (!r.recordedAt.isAfter(now) && now.difference(r.recordedAt) <= range.duration) r,
];
