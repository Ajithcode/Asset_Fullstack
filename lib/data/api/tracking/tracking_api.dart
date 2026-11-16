import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/tracking/tracking_models.dart';

export 'package:asset360_mobile/data/api/tracking/tracking_models.dart';

/// The READ-ONLY endpoints of the Tracking service (TRACKING_BASE_URL, port 5490) the app uses.
/// Paths and query parameters: docs/api/tracking.json (live OpenAPI, which wins) — identical to
/// docs/backend_ref/Services/Tracking/Api/A360.Tracking.Api/Endpoints/*.cs. Only parameters an
/// endpoint reads are sent. The app never creates or changes Tracking data.
class TrackingApi {
  const TrackingApi(this.client);

  final ApiClient client;

  static const locationEvents = '/api/location-events';
  static const iotDevices = '/api/iot-devices';
  static const deviceHealth = '/api/device-health';
  static const tagIds = '/api/tag-ids';
  static const sensors = '/api/sensors';
  static const sensorTypes = '/api/sensors/types';
  static const sensorStatuses = '/api/sensors/statuses';
  static const telemetryReadings = '/api/telemetry-readings';
  static const iotEventsTimeline = '/api/iot-events/timeline';
  static const geofences = '/api/geofences';
  static const iotAlerts = '/api/iot-alerts';
  static const iotAlertCount = '/api/iot-alerts/count';

  /// Server limits: `days` 1–365 (DashboardPeriod.MaxDays), telemetry `limit` 1–5000 (default 500).
  static const maxDays = 365;
  static const maxReadings = 5000;

  Future<Object?> _get(String path, [Map<String, Object?> query = const {}]) async {
    final q = <String, dynamic>{
      for (final e in query.entries)
        if (e.value != null && !(e.value is String && (e.value as String).trim().isEmpty)) e.key: e.value,
    };
    return (await client.get<Object?>(path, query: q.isEmpty ? null : q)).data;
  }

  /// EVERY location event, newest first. The endpoint takes NO parameters (no asset, time range,
  /// latest-only or paging) — callers keep only what they need (see TrackingSnapshot).
  Future<List<TrkLocationEvent>> getLocationEvents() async => TrkLocationEvent.parseList(await _get(locationEvents));

  /// Every IoT device (no parameters = the full list; `search` / `limit` switch the endpoint to the
  /// dropdown search, which the app does not use).
  Future<List<TrkDevice>> getDevices() async => TrkDevice.parseList(await _get(iotDevices));

  /// Every device-health row (no parameters).
  Future<List<TrkDeviceHealth>> getDeviceHealth() async => TrkDeviceHealth.parseList(await _get(deviceHealth));

  /// Every tracking tag (no parameters).
  Future<List<TrkTag>> getTags() async => TrkTag.parseList(await _get(tagIds));

  /// Every sensor (no parameters).
  Future<List<TrkSensor>> getSensors() async => TrkSensor.parseList(await _get(sensors));

  /// One sensor by its sensor id (e.g. SEN-T-80900).
  Future<TrkSensor> getSensor(String sensorId) async =>
      TrkSensor.fromJson(Map<String, dynamic>.from(await _get('$sensors/${Uri.encodeComponent(sensorId)}') as Map));

  Future<List<TrkSensorType>> getSensorTypes() async => TrkSensorType.parseList(await _get(sensorTypes));

  Future<List<String>> getSensorStatuses() async => [
    for (final s in (await _get(sensorStatuses) as List? ?? const []))
      if (s is String && s.trim().isNotEmpty) s.trim(),
  ];

  /// Readings of the last [days] WHOLE UTC days ending today (default 7 on the server), newest
  /// first, at most [limit] (server default 500, max 5000). Filter by [sensorId] and / or
  /// [deviceId]; there is no asset filter and no from / to.
  Future<List<TrkReading>> getReadings({int? days, String? sensorId, String? deviceId, int? limit}) async =>
      TrkReading.parseList(
        await _get(telemetryReadings, {'days': days, 'sensorId': sensorId, 'deviceId': deviceId, 'limit': limit}),
      );

  /// Org-wide daily counts (telemetry, location updates, geofence events, alerts) for [days].
  Future<TrkTimeline> getTimeline({int? days}) async =>
      TrkTimeline.fromJson(await _get(iotEventsTimeline, {'days': days}));

  /// Every GPS geofence (no parameters).
  Future<List<TrkGeofence>> getGeofences() async => TrkGeofence.parseList(await _get(geofences));

  /// Alerts newest first. `days` null = all time.
  Future<List<TrkAlert>> getAlerts({
    int? days,
    String? status,
    String? severity,
    String? siteName,
    String? assetRegistrationId,
    String? search,
    int? limit,
  }) async => TrkAlert.parseList(
    await _get(iotAlerts, {
      'days': days,
      'status': status,
      'severity': severity,
      'siteName': siteName,
      'assetRegistrationId': assetRegistrationId,
      'search': search,
      'limit': limit,
    }),
  );

  /// The alerts KPI: alerts raised in the last [days] (server default 7).
  Future<TrkAlertCount> getAlertCount({int? days, String? siteName}) async =>
      TrkAlertCount.fromJson(await _get(iotAlertCount, {'days': days, 'siteName': siteName}));
}
