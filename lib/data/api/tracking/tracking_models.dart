import 'package:asset360_mobile/data/api/assets/json_read.dart';

/// Typed replies of the Tracking service (TRACKING_BASE_URL, port 5490). Field names follow the
/// response records in docs/backend_ref/Services/Tracking/Api/A360.Tracking.Api/Contracts/*.cs
/// (camelCase on the wire). Swagger (docs/api/tracking.json) documents no response bodies, so
/// every field here comes from those contracts; unknown / missing fields parse to null.

/// A server timestamp. The services store UTC; a value without an offset ("2026-11-11T08:00:00")
/// is read as UTC too, so it is never shifted by the phone's time zone.
DateTime? jUtc(Object? v) {
  final s = jStr(v)?.trim();
  if (s == null) return null;
  final hasZone = s.endsWith('Z') || RegExp(r'[+-]\d\d:?\d\d$').hasMatch(s);
  return DateTime.tryParse(hasZone ? s : '${s}Z');
}

String _s(Map<String, dynamic> j, String key) => jStr(j[key])?.trim() ?? '';
String? _o(Map<String, dynamic> j, String key) => jStr(j[key])?.trim();

List<T> _list<T>(Object? body, T Function(Map<String, dynamic>) parse) => [
  for (final e in jItems(body))
    if (e is Map) parse(jMap(e)),
];

// ------------------------------------------------------------------ location events

/// GET /api/location-events (`LocationEventResponse`): one ZONE event of a tracked asset.
/// There is NO position: no latitude / longitude, no floor-plan x / y, no zone id and no floor id —
/// only the zone's NAME. [assetId] is free text ("the asset as shown on the map, e.g. FL-008"),
/// not the asset registration id.
class TrkLocationEvent {
  const TrkLocationEvent({
    required this.eventId,
    required this.eventTime,
    required this.assetId,
    this.assetName = '',
    this.eventType = '',
    this.zoneName = '',
    this.details,
  });

  final String eventId;
  final DateTime eventTime;
  final String assetId;
  final String assetName;

  /// One of [TrkEventType.all].
  final String eventType;
  final String zoneName;

  /// e.g. "Departed Charging Zone (dwell time 18 min)".
  final String? details;

  /// Entered Zone / Dwell Exceeded / Geofence Alert put the asset IN [zoneName]; Departed Zone
  /// takes it out (its zone is then unknown until the next event).
  bool get placesAssetInZone => eventType != TrkEventType.departedZone;

  static TrkLocationEvent? fromJson(Map<String, dynamic> j) {
    final at = jUtc(j['eventTime']);
    final asset = _s(j, 'assetId');
    if (at == null || asset.isEmpty) return null;
    return TrkLocationEvent(
      eventId: _s(j, 'eventId'),
      eventTime: at,
      assetId: asset,
      assetName: _s(j, 'assetName'),
      eventType: _s(j, 'eventType'),
      zoneName: _s(j, 'zoneName'),
      details: _o(j, 'details'),
    );
  }

  static List<TrkLocationEvent> parseList(Object? body) => [
    for (final e in jItems(body))
      if (e is Map) ?fromJson(jMap(e)),
  ];
}

/// `LocationEventType.All`.
abstract final class TrkEventType {
  static const enteredZone = 'Entered Zone';
  static const departedZone = 'Departed Zone';
  static const geofenceAlert = 'Geofence Alert';
  static const dwellExceeded = 'Dwell Exceeded';
  static const all = [enteredZone, departedZone, geofenceAlert, dwellExceeded];
}

// ------------------------------------------------------------------ devices, tags, health

/// GET /api/iot-devices (`IoTDeviceResponse`). [health] is the server's `health` (Good / Warning /
/// Critical). [batteryLevel] is set only for battery-powered devices.
class TrkDevice {
  const TrkDevice({
    required this.deviceId,
    this.deviceName = '',
    this.deviceType = '',
    this.model = '',
    this.connectivity = '',
    this.siteId,
    this.siteName,
    this.assetRegistrationId,
    this.assetCode,
    this.assetName,
    this.gateway,
    this.powerSource = '',
    this.batteryLevel,
    this.lastSeenAt,
    this.health,
    this.firmwareVersion,
    this.status,
  });

  final String deviceId, deviceName, deviceType, model, connectivity, powerSource;
  final String? siteId, siteName, assetRegistrationId, assetCode, assetName, gateway, health, firmwareVersion;

  /// Online / Offline / Active … as the server sends it (the .26 / .29 servers).
  final String? status;
  final int? batteryLevel;
  final DateTime? lastSeenAt;

  bool get isGateway => deviceType.toLowerCase() == 'gateway';

  factory TrkDevice.fromJson(Map<String, dynamic> j) => TrkDevice(
    deviceId: _s(j, 'deviceId'),
    deviceName: _s(j, 'deviceName'),
    deviceType: _s(j, 'deviceType'),
    model: _s(j, 'model'),
    connectivity: _s(j, 'connectivity'),
    siteId: _o(j, 'siteId'),
    siteName: _o(j, 'siteName'),
    assetRegistrationId: _o(j, 'assetRegistrationId'),
    assetCode: _o(j, 'assetCode'),
    assetName: _o(j, 'assetName'),
    gateway: _o(j, 'gateway'),
    powerSource: _s(j, 'powerSource'),
    // The .29 server sends `batteryPercent`; the contract says `batteryLevel`.
    batteryLevel: jInt(j['batteryLevel']) ?? jInt(j['batteryPercent']),
    lastSeenAt: jUtc(j['lastSeenAt']),
    health: _o(j, 'health'),
    status: _o(j, 'status'),
    firmwareVersion: _o(j, 'firmwareVersion'),
  );

  static List<TrkDevice> parseList(Object? body) =>
      _list(body, TrkDevice.fromJson).where((d) => d.deviceId.isNotEmpty).toList();
}

/// GET /api/device-health (`DeviceHealthResponse`): signal / battery / last seen per device.
/// [health] = Healthy / Attention / Critical / Maintenance; [signalQuality] = Strong / Good / Fair / Poor.
class TrkDeviceHealth {
  const TrkDeviceHealth({
    required this.deviceId,
    this.deviceModel = '',
    this.assetRegistrationId,
    this.assetName,
    this.siteId,
    this.siteName,
    this.gateway,
    this.connectivity = '',
    this.signalStrengthDbm,
    this.snrDb,
    this.signalQuality,
    this.powerSource = '',
    this.batteryLevel,
    this.lastSeenAt,
    this.health,
  });

  final String deviceId, deviceModel, connectivity, powerSource;
  final String? assetRegistrationId, assetName, siteId, siteName, gateway, signalQuality, health;
  final int? signalStrengthDbm, batteryLevel;
  final double? snrDb;
  final DateTime? lastSeenAt;

  factory TrkDeviceHealth.fromJson(Map<String, dynamic> j) => TrkDeviceHealth(
    deviceId: _s(j, 'deviceId'),
    deviceModel: _s(j, 'deviceModel'),
    assetRegistrationId: _o(j, 'assetRegistrationId'),
    assetName: _o(j, 'assetName'),
    siteId: _o(j, 'siteId'),
    siteName: _o(j, 'siteName'),
    gateway: _o(j, 'gateway'),
    connectivity: _s(j, 'connectivity'),
    signalStrengthDbm: jInt(j['signalStrengthDbm']),
    snrDb: jNum(j['snrDb']),
    signalQuality: _o(j, 'signalQuality'),
    powerSource: _s(j, 'powerSource'),
    batteryLevel: jInt(j['batteryLevel']),
    lastSeenAt: jUtc(j['lastSeenAt']),
    health: _o(j, 'health'),
  );

  static List<TrkDeviceHealth> parseList(Object? body) =>
      _list(body, TrkDeviceHealth.fromJson).where((d) => d.deviceId.isNotEmpty).toList();
}

/// GET /api/tag-ids (`TrackingTagResponse`). [status] = Available / Assigned / Inactive;
/// [locationName] is free text.
class TrkTag {
  const TrkTag({
    required this.tagId,
    this.tagType = '',
    this.technology = '',
    this.assetRegistrationId,
    this.assetName,
    this.siteId,
    this.siteName,
    this.locationName,
    this.status,
    this.lastSeenAt,
  });

  final String tagId, tagType, technology;
  final String? assetRegistrationId, assetName, siteId, siteName, locationName, status;
  final DateTime? lastSeenAt;

  factory TrkTag.fromJson(Map<String, dynamic> j) => TrkTag(
    tagId: _s(j, 'tagId'),
    tagType: _s(j, 'tagType'),
    technology: _s(j, 'technology'),
    assetRegistrationId: _o(j, 'assetRegistrationId'),
    assetName: _o(j, 'assetName'),
    siteId: _o(j, 'siteId'),
    siteName: _o(j, 'siteName'),
    locationName: _o(j, 'locationName'),
    status: _o(j, 'status'),
    lastSeenAt: jUtc(j['lastSeenAt']),
  );

  static List<TrkTag> parseList(Object? body) => _list(body, TrkTag.fromJson).where((t) => t.tagId.isNotEmpty).toList();
}

// ------------------------------------------------------------------ sensors + telemetry

/// GET /api/sensors and /api/sensors/{sensorId} (`SensorResponse`). [liveReading] is the latest
/// value in [unit] (null = "No data"); [status] = Active / Offline / Attention / Inactive.
class TrkSensor {
  const TrkSensor({
    required this.sensorId,
    this.name = '',
    this.sensorType = '',
    this.measuredParameter = '',
    this.unit,
    this.serialNumber,
    this.deviceId,
    this.deviceModel,
    this.channel,
    this.samplingIntervalSeconds,
    this.lowAlarm,
    this.highAlarm,
    this.siteId,
    this.siteName,
    this.assetRegistrationId,
    this.assetCode,
    this.assetName,
    this.measurementPoint,
    this.liveReading,
    this.calibrationDueDate,
    this.status,
    this.updatedAt,
  });

  final String sensorId, name, sensorType, measuredParameter;
  final String? unit, serialNumber, deviceId, deviceModel, siteId, siteName;
  final String? assetRegistrationId, assetCode, assetName, measurementPoint, status;
  final int? channel, samplingIntervalSeconds;
  final double? lowAlarm, highAlarm, liveReading;
  final DateTime? calibrationDueDate, updatedAt;

  String get title => name.isNotEmpty ? name : sensorType;

  /// Live reading outside [lowAlarm] / [highAlarm].
  bool get outOfRange {
    final v = liveReading;
    if (v == null) return false;
    return (lowAlarm != null && v < lowAlarm!) || (highAlarm != null && v > highAlarm!);
  }

  factory TrkSensor.fromJson(Map<String, dynamic> j) => TrkSensor(
    sensorId: _s(j, 'sensorId'),
    name: _s(j, 'name'),
    sensorType: _s(j, 'sensorType'),
    measuredParameter: _s(j, 'measuredParameter'),
    unit: _o(j, 'unit'),
    serialNumber: _o(j, 'serialNumber'),
    deviceId: _o(j, 'deviceId'),
    deviceModel: _o(j, 'deviceModel'),
    channel: jInt(j['channel']),
    samplingIntervalSeconds: jInt(j['samplingIntervalSeconds']),
    lowAlarm: jNum(j['lowAlarm']),
    highAlarm: jNum(j['highAlarm']),
    siteId: _o(j, 'siteId'),
    siteName: _o(j, 'siteName'),
    assetRegistrationId: _o(j, 'assetRegistrationId'),
    assetCode: _o(j, 'assetCode'),
    assetName: _o(j, 'assetName'),
    measurementPoint: _o(j, 'measurementPoint'),
    liveReading: jNum(j['liveReading']),
    calibrationDueDate: jUtc(j['calibrationDueDate']),
    status: _o(j, 'status'),
    updatedAt: jUtc(j['updatedAt']),
  );

  static List<TrkSensor> parseList(Object? body) =>
      _list(body, TrkSensor.fromJson).where((s) => s.sensorId.isNotEmpty).toList();
}

/// `SensorStatus.All`.
abstract final class TrkSensorStatus {
  static const active = 'Active';
  static const offline = 'Offline';
  static const attention = 'Attention';
  static const inactive = 'Inactive';
  static const all = [active, offline, attention, inactive];
}

/// GET /api/sensors/types (`SensorTypeOption`).
class TrkSensorType {
  const TrkSensorType({required this.sensorType, this.measuredParameter = '', this.unit = ''});

  final String sensorType, measuredParameter, unit;

  static List<TrkSensorType> parseList(Object? body) => [
    for (final e in jItems(body))
      if (e is Map && jStr(e['sensorType']) != null)
        TrkSensorType(
          sensorType: jStr(e['sensorType'])!.trim(),
          measuredParameter: jStr(e['measuredParameter'])?.trim() ?? '',
          unit: jStr(e['unit'])?.trim() ?? '',
        ),
  ];
}

/// GET /api/telemetry-readings (`TelemetryReadingResponse`): one value of one parameter.
class TrkReading {
  const TrkReading({
    required this.recordedAt,
    required this.value,
    this.readingId = '',
    this.deviceId = '',
    this.sensorId,
    this.assetRegistrationId,
    this.parameter = '',
    this.unit,
  });

  final String readingId, deviceId, parameter;
  final String? sensorId, assetRegistrationId, unit;
  final DateTime recordedAt;
  final double value;

  static TrkReading? fromJson(Map<String, dynamic> j) {
    final at = jUtc(j['recordedAt']);
    final v = jNum(j['value']);
    if (at == null || v == null) return null;
    return TrkReading(
      recordedAt: at,
      value: v,
      readingId: _s(j, 'readingId'),
      deviceId: _s(j, 'deviceId'),
      sensorId: _o(j, 'sensorId'),
      assetRegistrationId: _o(j, 'assetRegistrationId'),
      parameter: _s(j, 'parameter'),
      unit: _o(j, 'unit'),
    );
  }

  /// Newest first, as the server sends them.
  static List<TrkReading> parseList(Object? body) => [
    for (final e in jItems(body))
      if (e is Map) ?fromJson(jMap(e)),
  ];
}

/// GET /api/iot-events/timeline?days= (`IoTEventsTimelineResponse`): ORG-WIDE daily counts
/// (no asset / site filter on the server).
class TrkTimeline {
  const TrkTimeline({this.days = 0, this.totals = const TrkTimelinePoint(date: ''), this.points = const []});

  final int days;
  final TrkTimelinePoint totals;
  final List<TrkTimelinePoint> points;

  factory TrkTimeline.fromJson(Object? body) {
    final j = jMap(body);
    return TrkTimeline(
      days: jInt(j['days']) ?? 0,
      totals: TrkTimelinePoint.fromJson(jMap(j['totals'])),
      points: [
        for (final p in jList(j['points']))
          if (p is Map) TrkTimelinePoint.fromJson(jMap(p)),
      ],
    );
  }
}

class TrkTimelinePoint {
  const TrkTimelinePoint({
    required this.date,
    this.telemetryData = 0,
    this.locationUpdates = 0,
    this.geofenceEvents = 0,
    this.alerts = 0,
  });

  /// UTC day, yyyy-MM-dd.
  final String date;
  final int telemetryData, locationUpdates, geofenceEvents, alerts;

  factory TrkTimelinePoint.fromJson(Map<String, dynamic> j) => TrkTimelinePoint(
    date: _s(j, 'date'),
    telemetryData: jInt(j['telemetryData']) ?? 0,
    locationUpdates: jInt(j['locationUpdates']) ?? 0,
    geofenceEvents: jInt(j['geofenceEvents']) ?? 0,
    alerts: jInt(j['alerts']) ?? 0,
  );
}

// ------------------------------------------------------------------ geofences (GPS)

/// GET /api/geofences (`GeofenceResponse`): an OUTDOOR geofence in WGS84 degrees.
/// [shape] = Polygon / Rectangle / Circle; a circle has one centre point + [radiusMeters].
class TrkGeofence {
  const TrkGeofence({
    required this.id,
    required this.name,
    this.geofenceType = '',
    this.siteId,
    this.siteName = '',
    this.monitoredAssetCount = 0,
    this.ruleCount = 0,
    this.status,
    this.lastEventAt,
    this.mapColour,
    this.assetGroups = const [],
    this.schedule = '',
    this.dwellAlertMinutes,
    this.monitorEntry = false,
    this.monitorExit = false,
    this.shape = '',
    this.coordinates = const [],
    this.radiusMeters,
  });

  final String id, name, geofenceType, siteName, schedule, shape;
  final String? siteId, status, mapColour;
  final int monitoredAssetCount, ruleCount;
  final int? dwellAlertMinutes;
  final DateTime? lastEventAt;
  final List<String> assetGroups;
  final bool monitorEntry, monitorExit;
  final List<({double latitude, double longitude})> coordinates;
  final double? radiusMeters;

  bool get isCircle => shape.toLowerCase() == 'circle';
  bool get isActive => (status ?? 'Active').toLowerCase() == 'active';
  bool get dwellAlert => (dwellAlertMinutes ?? 0) > 0;

  factory TrkGeofence.fromJson(Map<String, dynamic> j) => TrkGeofence(
    id: _s(j, 'id'),
    name: _s(j, 'name'),
    geofenceType: _s(j, 'geofenceType'),
    siteId: _o(j, 'siteId'),
    siteName: _s(j, 'siteName'),
    monitoredAssetCount: jInt(j['monitoredAssetCount']) ?? 0,
    ruleCount: jInt(j['ruleCount']) ?? 0,
    status: _o(j, 'status'),
    lastEventAt: jUtc(j['lastEventAt']),
    mapColour: _o(j, 'mapColour'),
    assetGroups: jStrings(j['assetGroups']),
    schedule: _s(j, 'schedule'),
    dwellAlertMinutes: jInt(j['dwellAlertMinutes']),
    monitorEntry: jBool(j['monitorEntry']),
    monitorExit: jBool(j['monitorExit']),
    shape: _s(j, 'shape'),
    coordinates: [
      for (final p in jList(j['coordinates']))
        if (p is Map && jNum(p['latitude']) != null && jNum(p['longitude']) != null)
          (latitude: jNum(p['latitude'])!, longitude: jNum(p['longitude'])!),
    ],
    radiusMeters: jNum(j['radiusMeters']),
  );

  static List<TrkGeofence> parseList(Object? body) =>
      _list(body, TrkGeofence.fromJson).where((g) => g.id.isNotEmpty).toList();
}

// ------------------------------------------------------------------ IoT alerts

/// GET /api/iot-alerts (`IoTAlertResponse`). [severity] = Critical / Warning / Info;
/// [status] = Open / Acknowledged / In Progress / Resolved.
class TrkAlert {
  const TrkAlert({
    required this.alertId,
    required this.raisedAt,
    this.assetRegistrationId,
    this.assetName = '',
    this.deviceId,
    this.siteId,
    this.siteName,
    this.eventType = '',
    this.severity = '',
    this.status,
    this.message,
    this.acknowledgedBy,
    this.acknowledgedAt,
    this.resolvedBy,
    this.resolvedAt,
  });

  final String alertId, assetName, eventType, severity;
  final DateTime raisedAt;
  final String? assetRegistrationId, deviceId, siteId, siteName, status, message, acknowledgedBy, resolvedBy;
  final DateTime? acknowledgedAt, resolvedAt;

  bool get isOpen => status != 'Resolved';

  static TrkAlert? fromJson(Map<String, dynamic> j) {
    final at = jUtc(j['raisedAt']);
    if (at == null) return null;
    return TrkAlert(
      alertId: _s(j, 'alertId'),
      raisedAt: at,
      assetRegistrationId: _o(j, 'assetRegistrationId'),
      assetName: _s(j, 'assetName'),
      deviceId: _o(j, 'deviceId'),
      siteId: _o(j, 'siteId'),
      siteName: _o(j, 'siteName'),
      eventType: _s(j, 'eventType'),
      severity: _s(j, 'severity'),
      status: _o(j, 'status'),
      message: _o(j, 'message'),
      acknowledgedBy: _o(j, 'acknowledgedBy'),
      acknowledgedAt: jUtc(j['acknowledgedAt']),
      resolvedBy: _o(j, 'resolvedBy'),
      resolvedAt: jUtc(j['resolvedAt']),
    );
  }

  static List<TrkAlert> parseList(Object? body) => [
    for (final e in jItems(body))
      if (e is Map) ?fromJson(jMap(e)),
  ];
}

/// GET /api/iot-alerts/count?days=&siteName= (`IoTAlertCountResponse`): alerts RAISED in the last
/// [days] whole UTC days (default 7), split by status and severity.
class TrkAlertCount {
  const TrkAlertCount({
    this.days = 7,
    this.total = 0,
    this.previousPeriodTotal = 0,
    this.changePercent,
    this.byStatus = const {},
    this.bySeverity = const {},
  });

  final int days, total, previousPeriodTotal;
  final double? changePercent;
  final Map<String, int> byStatus, bySeverity;

  /// Not Resolved (Open + Acknowledged + In Progress).
  int get unresolved => total - (byStatus['Resolved'] ?? 0);

  factory TrkAlertCount.fromJson(Object? body) {
    final j = jMap(body);
    Map<String, int> counts(Object? v) => {
      for (final e in jMap(v).entries)
        if (jInt(e.value) != null) e.key: jInt(e.value)!,
    };
    return TrkAlertCount(
      days: jInt(j['days']) ?? 7,
      total: jInt(j['total']) ?? 0,
      previousPeriodTotal: jInt(j['previousPeriodTotal']) ?? 0,
      changePercent: jNum(j['changePercent']),
      byStatus: counts(j['byStatus']),
      bySeverity: counts(j['bySeverity']),
    );
  }
}
