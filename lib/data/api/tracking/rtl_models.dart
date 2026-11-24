import 'package:asset360_mobile/data/api/assets/json_read.dart';
import 'package:asset360_mobile/data/api/tracking/tracking_models.dart' show jUtc;

/// Typed replies of the Real-Time Location page and the Locate (LED / buzzer) endpoints of the
/// Tracking service. Contract: docs/api/MOBILE-API-HANDOFF.md sections 1.2–1.4 and 2, records at the
/// bottom of docs/backend_ref/Services/Tracking/Api/A360.Tracking.Api/Telemetry/RealTimeLocationService.cs
/// (`RtlOverview`, `RtlAsset`, `RtlTrail`, `RtlEventPage`) and `GatewayDto` / `AssetLocationDto` in
/// TelemetryOverviewService.cs. Parsing never throws; missing values stay null / empty.

String _s(Map<String, dynamic> j, String key) => jStr(j[key])?.trim() ?? '';
String? _o(Map<String, dynamic> j, String key) => jStr(j[key])?.trim();

List<T> _list<T>(Object? body, T Function(Map<String, dynamic>) parse) => [
  for (final e in jItems(body))
    if (e is Map) parse(jMap(e)),
];

// ------------------------------------------------------------------ overview

/// GET /api/real-time-location/overview.
class RtlOverview {
  const RtlOverview({this.generatedAt, this.kpis = const RtlKpis(), this.assets = const [], this.gateways = const []});

  final DateTime? generatedAt;
  final RtlKpis kpis;
  final List<RtlAsset> assets;
  final List<RtlGateway> gateways;

  bool get isEmpty => assets.isEmpty;

  /// The asset with registration id [assetId] (case-insensitive), or null.
  RtlAsset? asset(String? assetId) {
    final id = assetId?.trim().toLowerCase();
    if (id == null || id.isEmpty) return null;
    return assets.where((a) => a.assetId.toLowerCase() == id).firstOrNull;
  }

  /// The gateway with device id [id] (case-insensitive), or null.
  RtlGateway? gateway(String? id) {
    final k = id?.trim().toLowerCase();
    if (k == null || k.isEmpty) return null;
    return gateways.where((g) => g.deviceId.toLowerCase() == k).firstOrNull;
  }

  factory RtlOverview.fromJson(Object? body) {
    final j = jMap(body);
    return RtlOverview(
      generatedAt: jUtc(j['generatedAt']),
      kpis: RtlKpis.fromJson(j['kpis']),
      assets: _list(j['assets'], RtlAsset.fromJson).where((a) => a.assetId.isNotEmpty).toList(),
      gateways: _list(j['gateways'], RtlGateway.fromJson).where((g) => g.deviceId.isNotEmpty).toList(),
    );
  }
}

/// `kpis`: In view = moving + stationary + unknown; [locationAlerts] = open geofence / location
/// alerts of the assets in view.
class RtlKpis {
  const RtlKpis({this.assetsInView = 0, this.moving = 0, this.stationary = 0, this.unknown = 0, this.locationAlerts = 0});

  final int assetsInView, moving, stationary, unknown, locationAlerts;

  factory RtlKpis.fromJson(Object? v) {
    final j = jMap(v);
    return RtlKpis(
      assetsInView: jInt(j['assetsInView']) ?? 0,
      moving: jInt(j['moving']) ?? 0,
      stationary: jInt(j['stationary']) ?? 0,
      unknown: jInt(j['unknown']) ?? 0,
      locationAlerts: jInt(j['locationAlerts']) ?? 0,
    );
  }
}

/// `AssetLocationDto`: the zone of the gateway that hears the tag strongest (BLE proximity, no x/y).
class RtlLocation {
  const RtlLocation({
    required this.gatewayId,
    this.gatewayName,
    this.zoneId,
    this.zoneName,
    this.floorName,
    this.buildingName,
    this.siteId,
    this.siteName,
    this.floorPlanId,
    this.locationPath,
    this.method = '',
    this.basis = '',
    this.accuracy = '',
    this.at,
  });

  final String gatewayId;
  final String? gatewayName, zoneId, zoneName, floorName, buildingName, siteId, siteName, floorPlanId, locationPath;

  /// Estimated (BLE gateway zone) | Measured (GPS).
  final String method;

  /// "Strongest gateway" (shown under the location).
  final String basis;
  final String accuracy;
  final DateTime? at;

  /// [locationPath], else the parts that are known.
  String get path =>
      locationPath ?? [siteName, buildingName, floorName, zoneName].nonNulls.where((s) => s.isNotEmpty).join(' › ');

  static RtlLocation? fromJson(Object? v) {
    if (v is! Map) return null;
    final j = jMap(v);
    final gw = _s(j, 'gatewayId');
    if (gw.isEmpty && _o(j, 'zoneName') == null && _o(j, 'locationPath') == null) return null;
    return RtlLocation(
      gatewayId: gw,
      gatewayName: _o(j, 'gatewayName'),
      zoneId: _o(j, 'zoneId'),
      zoneName: _o(j, 'zoneName'),
      floorName: _o(j, 'floorName'),
      buildingName: _o(j, 'buildingName'),
      siteId: _o(j, 'siteId'),
      siteName: _o(j, 'siteName'),
      floorPlanId: _o(j, 'floorPlanId'),
      locationPath: _o(j, 'locationPath'),
      method: _s(j, 'method'),
      basis: _s(j, 'basis'),
      accuracy: _s(j, 'accuracy'),
      at: jUtc(j['at']),
    );
  }
}

/// `ExpectedLocation`: where the asset is registered.
class RtlExpected {
  const RtlExpected({this.zoneId, this.zoneName, this.floorId, this.floorName, this.buildingName, this.siteName, this.locationPath});

  final String? zoneId, zoneName, floorId, floorName, buildingName, siteName, locationPath;

  String get path =>
      locationPath ?? [siteName, buildingName, floorName, zoneName].nonNulls.where((s) => s.isNotEmpty).join(' › ');

  static RtlExpected? fromJson(Object? v) {
    if (v is! Map) return null;
    final j = jMap(v);
    return RtlExpected(
      zoneId: _o(j, 'zoneId'),
      zoneName: _o(j, 'zoneName'),
      floorId: _o(j, 'floorId'),
      floorName: _o(j, 'floorName'),
      buildingName: _o(j, 'buildingName'),
      siteName: _o(j, 'siteName'),
      locationPath: _o(j, 'locationPath'),
    );
  }
}

/// One `RtlAsset` of the overview.
class RtlAsset {
  const RtlAsset({
    required this.assetId,
    this.assetTag,
    this.name = '',
    this.assetType,
    this.operationalStatus,
    this.technology = '',
    this.tracker,
    this.tags = const [],
    this.location,
    this.freshness = 'Unknown',
    this.movement = 'Unknown',
    this.lastZoneChangeAt,
    this.lastValidLocationAt,
    this.locationSource = '',
    this.accuracy = 'Unknown',
    this.confidence = 'Unknown',
    this.confidenceBasis = '',
    this.gatewaysHearing = 0,
    this.signalMarginDb,
    this.rssi,
    this.battery,
    this.x,
    this.y,
    this.positionAt,
    this.activeLocationAlerts = 0,
    this.expected,
    this.comparisonStatus = 'Unknown',
    this.comparisonNote = '',
  });

  /// Asset registration id (trail / events filter / asset detail).
  final String assetId;
  final String? assetTag, assetType, operationalStatus, tracker;
  final String name, technology;

  /// Every linked tag id (upper case as sent); Locate needs one.
  final List<String> tags;
  final RtlLocation? location;

  /// Live | Stale | Offline | Unknown.
  final String freshness;

  /// Moving | Stationary | Unknown.
  final String movement;
  final DateTime? lastZoneChangeAt, lastValidLocationAt;
  final String locationSource;

  /// Zone level | Gateway range | Unknown.
  final String accuracy;

  /// High | Medium | Low | Unknown.
  final String confidence;
  final String confidenceBasis;
  final int gatewaysHearing;
  final double? signalMarginDb, rssi;

  /// Tag battery, only from the live tags socket (the overview does not carry it).
  final double? battery;

  /// Where the newest live tag row puts the asset on its floor plan (% of the plan, x right, y down: the
  /// plot of the gateway that hears the tag, `TagLocation.X / Y`) and when that row was read. Null until
  /// the tags socket / GET /tags has a row for it; the overview alone has no x / y.
  final double? x, y;
  final DateTime? positionAt;
  final int activeLocationAlerts;
  final RtlExpected? expected;

  /// Match | Mismatch | Unknown | Not set.
  final String comparisonStatus;
  final String comparisonNote;

  /// The same asset with the live fields replaced (the tags socket overlay).
  RtlAsset copyWith({
    RtlLocation? location,
    String? freshness,
    String? movement,
    DateTime? lastZoneChangeAt,
    DateTime? lastValidLocationAt,
    double? rssi,
    double? battery,
    double? x,
    double? y,
    DateTime? positionAt,
  }) => RtlAsset(
    assetId: assetId,
    assetTag: assetTag,
    name: name,
    assetType: assetType,
    operationalStatus: operationalStatus,
    technology: technology,
    tracker: tracker,
    tags: tags,
    location: location ?? this.location,
    freshness: freshness ?? this.freshness,
    movement: movement ?? this.movement,
    lastZoneChangeAt: lastZoneChangeAt ?? this.lastZoneChangeAt,
    lastValidLocationAt: lastValidLocationAt ?? this.lastValidLocationAt,
    locationSource: locationSource,
    accuracy: accuracy,
    confidence: confidence,
    confidenceBasis: confidenceBasis,
    gatewaysHearing: gatewaysHearing,
    signalMarginDb: signalMarginDb,
    rssi: rssi ?? this.rssi,
    battery: battery ?? this.battery,
    x: x ?? this.x,
    y: y ?? this.y,
    positionAt: positionAt ?? this.positionAt,
    activeLocationAlerts: activeLocationAlerts,
    expected: expected,
    comparisonStatus: comparisonStatus,
    comparisonNote: comparisonNote,
  );

  /// "PUMP-01" (else the registration id).
  String get tagLabel => (assetTag?.isNotEmpty ?? false) ? assetTag! : assetId;

  bool get hasTags => tags.isNotEmpty;

  /// When the asset was last located.
  DateTime? get lastSeen => location?.at ?? lastValidLocationAt;

  bool matches(String query) {
    final q = query.trim().toLowerCase();
    if (q.isEmpty) return true;
    return [assetTag, name, assetId, tracker, ...tags].any((v) => v != null && v.toLowerCase().contains(q));
  }

  factory RtlAsset.fromJson(Map<String, dynamic> j) => RtlAsset(
    assetId: _s(j, 'assetId'),
    assetTag: _o(j, 'assetTag'),
    name: _s(j, 'name'),
    assetType: _o(j, 'assetType'),
    operationalStatus: _o(j, 'operationalStatus'),
    technology: _s(j, 'technology'),
    tracker: _o(j, 'tracker'),
    tags: [for (final t in jStrings(j['tags'])) t.trim()],
    location: RtlLocation.fromJson(j['location']),
    freshness: _o(j, 'freshness') ?? 'Unknown',
    movement: _o(j, 'movement') ?? 'Unknown',
    lastZoneChangeAt: jUtc(j['lastZoneChangeAt']),
    lastValidLocationAt: jUtc(j['lastValidLocationAt']),
    locationSource: _s(j, 'locationSource'),
    accuracy: _o(j, 'accuracy') ?? 'Unknown',
    confidence: _o(j, 'confidence') ?? 'Unknown',
    confidenceBasis: _s(j, 'confidenceBasis'),
    gatewaysHearing: jInt(j['gatewaysHearing']) ?? 0,
    signalMarginDb: jNum(j['signalMarginDb']),
    rssi: jNum(j['rssi']),
    activeLocationAlerts: jInt(j['activeLocationAlerts']) ?? 0,
    expected: RtlExpected.fromJson(j['expected']),
    comparisonStatus: _o(j, 'comparisonStatus') ?? 'Unknown',
    comparisonNote: _s(j, 'comparisonNote'),
  );
}

/// `GatewayDto`: connectivity Online | Idle | Offline (2 / 10 min rule).
class RtlGateway {
  const RtlGateway({
    required this.deviceId,
    this.name,
    this.type,
    this.model,
    this.connectivity = 'Offline',
    this.lastSeenAt,
    this.tagsHeard = 0,
    this.zoneName,
    this.floorName,
    this.floorPlanId,
    this.plotted = false,
    this.siteId,
  });

  final String deviceId;
  final String? name, type, model, zoneName, floorName, floorPlanId, siteId;
  final String connectivity;
  final DateTime? lastSeenAt;
  final int tagsHeard;
  final bool plotted;

  String get label => (name?.isNotEmpty ?? false) ? name! : deviceId;

  factory RtlGateway.fromJson(Map<String, dynamic> j) => RtlGateway(
    deviceId: _s(j, 'deviceId'),
    name: _o(j, 'name'),
    type: _o(j, 'type'),
    model: _o(j, 'model'),
    connectivity: _o(j, 'connectivity') ?? 'Offline',
    lastSeenAt: jUtc(j['lastSeenAt']),
    tagsHeard: jInt(j['tagsHeard']) ?? 0,
    zoneName: _o(j, 'zoneName'),
    floorName: _o(j, 'floorName'),
    floorPlanId: _o(j, 'floorPlanId'),
    plotted: jBool(j['plotted']),
    siteId: _o(j, 'siteId'),
  );
}

// ------------------------------------------------------------------ events

/// GET /api/real-time-location/events (`RtlEventPage`).
class RtlEventPage {
  const RtlEventPage({
    this.items = const [],
    this.total = 0,
    this.page = 1,
    this.pageSize = 10,
    this.totalPages = 0,
    this.rangeLabel = '',
    this.eventTypes = const [],
  });

  final List<RtlEvent> items;
  final int total, page, pageSize, totalPages;
  final String rangeLabel;

  /// Event types for the filter dropdown.
  final List<String> eventTypes;

  bool get hasMore => page < totalPages;

  factory RtlEventPage.fromJson(Object? body) {
    final j = jMap(body);
    return RtlEventPage(
      items: _list(j['items'], RtlEvent.fromJson),
      total: jInt(j['total']) ?? 0,
      page: jInt(j['page']) ?? 1,
      pageSize: jInt(j['pageSize']) ?? 10,
      totalPages: jInt(j['totalPages']) ?? 0,
      rangeLabel: _s(j, 'rangeLabel'),
      eventTypes: jStrings(j['eventTypes']),
    );
  }
}

/// `RtlEvent`: Zone Entry / Zone Exit / Floor Change / Location Detected / Stale Location /
/// Location Lost / geofence alert types.
class RtlEvent {
  const RtlEvent({
    required this.eventId,
    this.at,
    this.assetId,
    this.assetName,
    this.eventType = '',
    this.zone,
    this.floor,
    this.details,
    this.severity = 'Info',
    this.source,
    this.status = '',
  });

  final String eventId, eventType, severity, status;
  final DateTime? at;
  final String? assetId, assetName, zone, floor, details, source;

  factory RtlEvent.fromJson(Map<String, dynamic> j) => RtlEvent(
    eventId: _s(j, 'eventId'),
    at: jUtc(j['at']),
    assetId: _o(j, 'assetId'),
    assetName: _o(j, 'assetName'),
    eventType: _s(j, 'eventType'),
    zone: _o(j, 'zone'),
    floor: _o(j, 'floor'),
    details: _o(j, 'details'),
    severity: _o(j, 'severity') ?? 'Info',
    source: _o(j, 'source'),
    status: _s(j, 'status'),
  );
}

// ------------------------------------------------------------------ trail

/// GET /api/real-time-location/assets/{assetId}/trail (`RtlTrail`).
class RtlTrail {
  const RtlTrail({
    required this.assetId,
    this.name = '',
    this.range = '30m',
    this.from,
    this.to,
    this.segments = const [],
    this.zoneChanges = 0,
    this.timeInZones = const [],
    this.note = '',
  });

  final String assetId, name, range, note;
  final DateTime? from, to;
  final List<RtlTrailSegment> segments;
  final int zoneChanges;
  final List<({String zone, double minutes})> timeInZones;

  factory RtlTrail.fromJson(Object? body) {
    final j = jMap(body);
    return RtlTrail(
      assetId: _s(j, 'assetId'),
      name: _s(j, 'name'),
      range: _o(j, 'range') ?? '30m',
      from: jUtc(j['from']),
      to: jUtc(j['to']),
      segments: _list(j['segments'], RtlTrailSegment.fromJson),
      zoneChanges: jInt(j['zoneChanges']) ?? 0,
      timeInZones: [
        for (final z in jList(j['timeInZones']))
          if (z is Map) (zone: jStr(z['zone'])?.trim() ?? 'Unknown zone', minutes: jNum(z['minutes']) ?? 0),
      ],
      note: _s(j, 'note'),
    );
  }
}

/// One stay in a zone. [initial] = it started before the range (the asset was already there).
class RtlTrailSegment {
  const RtlTrailSegment({
    this.gatewayId = '',
    this.zoneName,
    this.floorName,
    this.from,
    this.to,
    this.minutes = 0,
    this.initial = false,
  });

  final String gatewayId;
  final String? zoneName, floorName;
  final DateTime? from, to;
  final double minutes;
  final bool initial;

  factory RtlTrailSegment.fromJson(Map<String, dynamic> j) => RtlTrailSegment(
    gatewayId: _s(j, 'gatewayId'),
    zoneName: _o(j, 'zoneName'),
    floorName: _o(j, 'floorName'),
    from: jUtc(j['from']),
    to: jUtc(j['to']),
    minutes: jNum(j['minutes']) ?? 0,
    initial: jBool(j['initial']),
  );
}

// ------------------------------------------------------------------ locating

/// GET /api/locating/tags/{tagid}/gateway (also the `subscribed` frame of /ws/locate).
class LocateTagGateway {
  const LocateTagGateway({required this.tagId, this.gatewayId, this.lastSeen, this.rssi, this.mqttConnected});

  final String tagId;
  final String? gatewayId;
  final DateTime? lastSeen;
  final double? rssi;

  /// Only in the `subscribed` frame; false = the locating service has no broker connection.
  final bool? mqttConnected;

  factory LocateTagGateway.fromJson(Object? body) {
    final j = jMap(body);
    return LocateTagGateway(
      tagId: _s(j, 'tagid'),
      gatewayId: _o(j, 'gatewayid'),
      lastSeen: jUtc(j['lastSeen']),
      rssi: jNum(j['rssi']),
      mqttConnected: j['mqttConnected'] is bool ? j['mqttConnected'] as bool : null,
    );
  }
}

/// One row of GET /api/locating/tags/{tagid}/commands. The raw gateway `request` is never read
/// (it holds the server-side command key).
class LocateCommandRecord {
  const LocateCommandRecord({this.reqId, this.command = '', this.status = '', this.gatewayId, this.sentAt});

  final int? reqId;

  /// led | buzzer | both.
  final String command;

  /// sent | accepted | completed | failed | timeout.
  final String status;
  final String? gatewayId;
  final DateTime? sentAt;

  /// Mongo relaxed extended JSON: numbers may be `{"$numberInt": "5"}`, dates `{"$date": "…"}`.
  static Object? _plain(Object? v) {
    if (v is Map) {
      for (final k in const [r'$date', r'$numberInt', r'$numberLong', r'$numberDouble']) {
        if (v.containsKey(k)) return _plain(v[k]);
      }
    }
    return v;
  }

  factory LocateCommandRecord.fromJson(Map<String, dynamic> j) => LocateCommandRecord(
    reqId: jInt(_plain(j['reqId'])),
    command: _s(j, 'command'),
    status: _s(j, 'status'),
    gatewayId: _o(j, 'gatewayid'),
    sentAt: jUtc(_plain(j['sentAt'])),
  );

  static List<LocateCommandRecord> parseList(Object? body) => _list(body, LocateCommandRecord.fromJson);
}
