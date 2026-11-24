import 'dart:async';
import 'dart:convert';

import 'package:asset360_mobile/data/api/assets/json_read.dart';
import 'package:asset360_mobile/data/api/dashboard/network_status_live.dart';
import 'package:asset360_mobile/data/api/tracking/rtl_models.dart';
import 'package:asset360_mobile/data/api/tracking/tracking_models.dart' show jUtc;

/// Live tag locations of the Tracking service: GET /api/real-time-location/tags and its WebSocket
/// `/api/real-time-location/tags/live` (`TagLocation` / `TagSeen` in TagLocationService.cs and
/// RealTimeLocationEndpoints.cs). Parsing never throws.

/// Tag ids compare ignoring case, ':' and '-' (`C3:00:00:AA:BB:01` = `c30000aabb01`).
String tagKey(String id) => id.replaceAll(':', '').replaceAll('-', '').replaceAll(' ', '').trim().toLowerCase();

/// One tag with the gateway / zone that hears it (reading fields included).
class TagLocation {
  const TagLocation({
    required this.tagId,
    this.gatewayId = '',
    this.gatewayName,
    this.timestamp,
    this.status,
    this.connection = 'Offline',
    this.rssi,
    this.battery,
    this.plotted = false,
    this.zoneId,
    this.zoneName,
    this.floorPlanId,
    this.floorName,
    this.buildingName,
    this.siteId,
    this.siteName,
    this.locationPath,
    this.x,
    this.y,
    this.assetId,
  });

  final String tagId, gatewayId;
  final String? gatewayName;
  final DateTime? timestamp;
  final String? status;

  /// Online | Idle | Offline.
  final String connection;
  final double? rssi, battery;

  /// The gateway's plot on the floor plan, in % of the plan (null when the gateway is not plotted).
  final double? x, y;
  final bool plotted;
  final String? zoneId, zoneName, floorPlanId, floorName, buildingName, siteId, siteName, locationPath, assetId;

  String get key => tagKey(tagId);

  /// The placement (where the tag is), without the reading: a change = the tag moved.
  String get place => '${gatewayId.toLowerCase()}|${(zoneId ?? '').toLowerCase()}';

  /// The same row with the reading fields of [s] (`seen`).
  TagLocation withSeen(TagSeen s) => TagLocation(
    tagId: tagId,
    gatewayId: gatewayId,
    gatewayName: gatewayName,
    timestamp: s.timestamp ?? timestamp,
    status: s.status,
    connection: s.connection,
    rssi: s.rssi,
    battery: s.battery,
    plotted: plotted,
    zoneId: zoneId,
    zoneName: zoneName,
    floorPlanId: floorPlanId,
    floorName: floorName,
    buildingName: buildingName,
    siteId: siteId,
    siteName: siteName,
    locationPath: locationPath,
    x: x,
    y: y,
    assetId: assetId,
  );

  static TagLocation? tryParse(Object? v) {
    if (v is! Map) return null;
    final j = jMap(v);
    final id = jStr(j['tagId'])?.trim() ?? '';
    if (id.isEmpty) return null;
    final asset = j['asset'];
    return TagLocation(
      tagId: id,
      gatewayId: jStr(j['gatewayId'])?.trim() ?? '',
      gatewayName: jStr(j['gatewayName'])?.trim(),
      timestamp: jUtc(j['timestamp']),
      status: jStr(j['status']),
      connection: jStr(j['connection']) ?? 'Offline',
      rssi: jNum(j['rssi']),
      battery: jNum(j['battery']),
      plotted: j['plotted'] == true,
      zoneId: jStr(j['zoneId']),
      zoneName: jStr(j['zoneName']),
      floorPlanId: jStr(j['floorPlanId']),
      floorName: jStr(j['floorName']),
      buildingName: jStr(j['buildingName']),
      siteId: jStr(j['siteId']),
      siteName: jStr(j['siteName']),
      locationPath: jStr(j['locationPath']),
      x: jNum(j['x']),
      y: jNum(j['y']),
      assetId: asset is Map ? jStr(jMap(asset)['assetRegistrationId']) : null,
    );
  }

  /// `tags` of a snapshot / GET /tags reply (an object with `tags`, or a bare list).
  static List<TagLocation> parseList(Object? body) => [
    for (final e in jItems(body is Map ? jMap(body)['tags'] : body)) ?tryParse(e),
  ];
}

/// `seen`: the same tag + gateway heard again; only the reading fields.
class TagSeen {
  const TagSeen({required this.tagId, this.timestamp, this.status, this.connection = 'Offline', this.rssi, this.battery});

  final String tagId;
  final DateTime? timestamp;
  final String? status;
  final String connection;
  final double? rssi, battery;

  static TagSeen? tryParse(Object? v) {
    if (v is! Map) return null;
    final j = jMap(v);
    final id = jStr(j['tagId'])?.trim() ?? '';
    if (id.isEmpty) return null;
    return TagSeen(
      tagId: id,
      timestamp: jUtc(j['timestamp']),
      status: jStr(j['status']),
      connection: jStr(j['connection']) ?? 'Offline',
      rssi: jNum(j['rssi']),
      battery: jNum(j['battery']),
    );
  }
}

/// A frame of the tags/live socket.
sealed class TagLiveMessage {
  const TagLiveMessage();

  /// Null for frames of another type or text that is not JSON.
  static TagLiveMessage? parse(Object? data) {
    try {
      final body = data is String ? jsonDecode(data) : (data is List<int> ? jsonDecode(utf8.decode(data)) : data);
      if (body is! Map) return null;
      final j = jMap(body);
      switch (j['type']) {
        case 'snapshot':
          return TagSnapshot(TagLocation.parseList(j));
        case 'update':
          return TagUpdate(
            changed: TagLocation.parseList(j['changed']),
            seen: [for (final e in jItems(j['seen'])) ?TagSeen.tryParse(e)],
            removed: [for (final e in jItems(j['removed'])) if (e is String) e],
          );
      }
    } catch (_) {
      // Not JSON: ignore.
    }
    return null;
  }
}

/// First frame: replaces the list.
class TagSnapshot extends TagLiveMessage {
  const TagSnapshot(this.tags);
  final List<TagLocation> tags;
}

/// `changed` = full rows (new tag / other gateway), `seen` = reading fields, `removed` = tag ids to drop.
class TagUpdate extends TagLiveMessage {
  const TagUpdate({this.changed = const [], this.seen = const [], this.removed = const []});
  final List<TagLocation> changed;
  final List<TagSeen> seen;
  final List<String> removed;
}

/// The tag rows the screen has, by [tagKey], and which of them moved (another gateway / zone than
/// the row they replace).
class TagLiveList {
  final _rows = <String, TagLocation>{};

  Iterable<TagLocation> get rows => _rows.values;
  TagLocation? operator [](String tagId) => _rows[tagKey(tagId)];
  int get length => _rows.length;

  /// Replaces every row (snapshot / poll); returns the keys of tags that changed place.
  Set<String> replace(Iterable<TagLocation> next) {
    final before = Map.of(_rows);
    _rows.clear();
    final moved = <String>{};
    for (final t in next) {
      _rows[t.key] = t;
      final old = before[t.key];
      if (old != null && old.place != t.place) moved.add(t.key);
    }
    return moved;
  }

  /// Applies an update frame; returns the keys of tags that changed place.
  Set<String> apply(TagUpdate u) {
    final moved = <String>{};
    for (final t in u.changed) {
      final old = _rows[t.key];
      if (old != null && old.place != t.place) moved.add(t.key);
      _rows[t.key] = t;
    }
    for (final s in u.seen) {
      final old = _rows[tagKey(s.tagId)];
      if (old != null) _rows[old.key] = old.withSeen(s);
    }
    for (final id in u.removed) {
      _rows.remove(tagKey(id));
    }
    return moved;
  }

  void clear() => _rows.clear();
}

// ------------------------------------------------------------------ overlay on the overview

/// A tag heard within this long ago (and Online) counts as Live; Moving = it changed gateway / zone within it.
const movingWithin = Duration(seconds: 60);

/// [base] (GET /overview) with the live tag rows laid over each asset that has a tag in [tags]:
/// location (gateway, zone, floor, time), position (x / y), freshness, rssi, battery and movement. A row older than
/// the overview's own location time is ignored (the newer reading wins). [movedAt] = when a tag
/// was seen changing place (by [tagKey]). Moving = changed within [movingWithin], else Stationary while
/// Live, else Unknown. The counters are recomputed; alerts, confidence, expected location and
/// gateways stay from [base]. Assets without a live tag row are left as they are.
RtlOverview overlayTags(RtlOverview base, TagLiveList tags, Map<String, DateTime> movedAt, DateTime now) {
  if (tags.length == 0) return base;
  var touched = false;
  final assets = <RtlAsset>[];
  for (final a in base.assets) {
    final rows = [for (final t in a.tags) ?tags[t]];
    if (rows.isEmpty) {
      assets.add(a);
      continue;
    }
    rows.sort((x, y) => (y.timestamp ?? DateTime.utc(0)).compareTo(x.timestamp ?? DateTime.utc(0)));
    final t = rows.first;
    // The newer reading wins: an overview (refreshed every 30 s) that already knows a later location than
    // the socket's row never moves the marker back to the older one.
    final baseAt = a.location?.at ?? a.lastValidLocationAt;
    if (t.timestamp != null && baseAt != null && baseAt.isAfter(t.timestamp!)) {
      assets.add(a);
      continue;
    }
    touched = true;
    final freshness = switch (t.connection) {
      'Online' => 'Live',
      'Idle' => 'Stale',
      _ => 'Offline',
    };
    DateTime? moved;
    for (final r in rows) {
      final m = movedAt[r.key];
      if (m != null && (moved == null || m.isAfter(moved))) moved = m;
    }
    final movement = freshness != 'Live'
        ? 'Unknown'
        : moved != null && now.difference(moved) <= movingWithin
        ? 'Moving'
        : 'Stationary';
    final old = a.location;
    final location = RtlLocation(
      gatewayId: t.gatewayId.isEmpty ? (old?.gatewayId ?? '') : t.gatewayId,
      gatewayName: t.gatewayName ?? old?.gatewayName,
      zoneId: t.zoneId ?? old?.zoneId,
      zoneName: t.zoneName ?? old?.zoneName,
      floorName: t.floorName ?? old?.floorName,
      buildingName: t.buildingName ?? old?.buildingName,
      siteId: t.siteId ?? old?.siteId,
      siteName: t.siteName ?? old?.siteName,
      floorPlanId: t.floorPlanId ?? old?.floorPlanId,
      locationPath: t.locationPath ?? old?.locationPath,
      method: old?.method ?? '',
      basis: old?.basis ?? '',
      accuracy: old?.accuracy ?? '',
      at: t.timestamp ?? old?.at,
    );
    assets.add(
      a.copyWith(
        location: location,
        freshness: freshness,
        movement: movement,
        lastZoneChangeAt: moved ?? a.lastZoneChangeAt,
        lastValidLocationAt: t.timestamp ?? a.lastValidLocationAt,
        rssi: t.rssi ?? a.rssi,
        battery: t.battery ?? a.battery,
        x: t.plotted ? t.x : null,
        y: t.plotted ? t.y : null,
        positionAt: t.timestamp,
      ),
    );
  }
  if (!touched) return base;
  int count(String m) => assets.where((a) => a.movement == m).length;
  return RtlOverview(
    generatedAt: base.generatedAt,
    kpis: RtlKpis(
      assetsInView: assets.length,
      moving: count('Moving'),
      stationary: count('Stationary'),
      unknown: count('Unknown'),
      locationAlerts: base.kpis.locationAlerts,
    ),
    assets: assets,
    gateways: base.gateways,
  );
}

// ------------------------------------------------------------------ the socket

/// `wss://<tracking host>/api/real-time-location/tags/live` with the same filters as GET /tags.
Uri tagsLiveUri(
  String trackingBaseUrl, {
  int minutes = 1440,
  String? siteId,
  String? floorPlanId,
  String? zoneId,
  bool zonedOnly = false,
}) {
  final base = Uri.parse(trackingBaseUrl);
  String? v(String? s) => s == null || s.trim().isEmpty ? null : s;
  return Uri(
    scheme: base.scheme == 'https' ? 'wss' : 'ws',
    host: base.host,
    port: base.hasPort ? base.port : null,
    path: '${base.path.endsWith('/') ? base.path.substring(0, base.path.length - 1) : base.path}/api/real-time-location/tags/live',
    queryParameters: {
      'minutes': '$minutes',
      'siteId': ?v(siteId),
      'floorPlanId': ?v(floorPlanId),
      'zoneId': ?v(zoneId),
      if (zonedOnly) 'zonedOnly': 'true',
    },
  );
}

/// [text] with every access token removed: `access_token=…` query values, `Bearer …` headers and the
/// literal [token]. Error messages of a failed upgrade can quote the URL, so they go through this.
String redactTokens(String text, [String? token]) {
  var out = text
      .replaceAll(RegExp(r'access_token=[^&\s\x27")]+', caseSensitive: false), 'access_token=…')
      .replaceAll(RegExp(r'Bearer\s+[A-Za-z0-9._~+/=-]+', caseSensitive: false), 'Bearer …');
  if (token != null && token.length >= 6) out = out.replaceAll(token, '…');
  return out;
}

/// [uri] without its `access_token` (safe to show / copy).
Uri withoutAccessToken(Uri uri) {
  final q = {...uri.queryParameters}..remove('access_token');
  return Uri(
    scheme: uri.scheme,
    host: uri.host,
    port: uri.hasPort ? uri.port : null,
    path: uri.path,
    queryParameters: q.isEmpty ? null : q,
  );
}

/// The tags/live socket: connects with a fresh token every attempt (Bearer header and `?access_token=`,
/// never logged), reports frames, and after a drop reconnects after 1 s, 2 s, 4 s … up to 30 s. A frame
/// resets the backoff. [start] / [stop] follow the screen's visibility and the app lifecycle.
class TagsLiveSocket {
  TagsLiveSocket({
    required this.uri,
    required this.token,
    required this.onMessage,
    required this.onConnection,
    this.onClosed,
    this.onError,
    LiveSocketConnector? connect,
    LiveScheduler? schedule,
  }) : _connect = connect ?? ioLiveSocket,
       _schedule = schedule ?? Timer.new;

  static const backoff = [
    Duration(seconds: 1),
    Duration(seconds: 2),
    Duration(seconds: 4),
    Duration(seconds: 8),
    Duration(seconds: 16),
    Duration(seconds: 30),
  ];

  final Uri uri;

  /// A valid (refreshed when about to expire) access token for the upgrade request.
  final Future<String?> Function() token;
  final void Function(TagLiveMessage message) onMessage;

  /// true = open, false = closed / failed / stopped.
  final void Function(bool connected) onConnection;

  /// The connection ended: the WebSocket close code / reason when the platform reports them.
  final void Function(int? code, String? reason)? onClosed;

  /// A failed attempt or a broken stream, as text with every token removed ([redactTokens]).
  final void Function(String message)? onError;
  final LiveSocketConnector _connect;
  final LiveScheduler _schedule;

  LiveSocket? _socket;
  StreamSubscription<Object?>? _sub;
  Timer? _retry;
  String? _token;
  bool _running = false;
  bool _connected = false;

  /// Failed attempts since the last frame.
  int failures = 0;

  bool get running => _running;
  bool get connected => _connected;

  /// How long the next reconnect waits.
  Duration get nextDelay => backoff[(failures - 1).clamp(0, backoff.length - 1)];

  void start() {
    if (_running) return;
    _running = true;
    failures = 0;
    unawaited(_open());
  }

  Future<void> stop() async {
    _running = false;
    _retry?.cancel();
    _retry = null;
    await _closeSocket();
    _set(false);
  }

  void _set(bool c) {
    if (_connected == c) return;
    _connected = c;
    onConnection(c);
  }

  Future<void> _open() async {
    if (!_running) return;
    String? t;
    try {
      t = await token();
    } catch (_) {
      t = null;
    }
    if (!_running) return;
    _token = t;
    final LiveSocket socket;
    try {
      socket = _connect(withAccessToken(uri, t), t);
    } catch (e) {
      _dropped(null, e);
      return;
    }
    _socket = socket;
    _sub = socket.messages.listen(
      _onData,
      onError: (Object e) => _dropped(socket, e),
      onDone: () => _dropped(socket),
      cancelOnError: true,
    );
    try {
      await socket.ready;
      if (_running && identical(_socket, socket)) _set(true);
    } catch (e) {
      _dropped(socket, e);
    }
  }

  void _onData(Object? data) {
    final m = TagLiveMessage.parse(data);
    if (m == null) return;
    failures = 0;
    _set(true);
    onMessage(m);
  }

  /// The socket closed or failed: schedule the next attempt (once per socket).
  void _dropped([LiveSocket? socket, Object? error]) {
    if (socket != null && !identical(_socket, socket)) return;
    if (error != null) onError?.call(redactTokens('$error', _token));
    final info = socket is LiveSocketCloseInfo ? socket as LiveSocketCloseInfo : null;
    onClosed?.call(info?.closeCode, info?.closeReason);
    unawaited(_closeSocket());
    if (!_running) return;
    failures++;
    _set(false);
    _retry?.cancel();
    _retry = _schedule(nextDelay, () {
      _retry = null;
      unawaited(_open());
    });
  }

  Future<void> _closeSocket() async {
    final sub = _sub, socket = _socket;
    _sub = null;
    _socket = null;
    await sub?.cancel();
    try {
      await socket?.close();
    } catch (_) {
      // Already closed.
    }
  }
}
