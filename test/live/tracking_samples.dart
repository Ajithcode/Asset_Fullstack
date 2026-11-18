import 'fake_asset_backend.dart';

/// Contract-shaped JSON of the Tracking service (docs/backend_ref/Services/Tracking/Api/
/// A360.Tracking.Api/Contracts) and the Administration map system (MapContracts.cs), with times
/// relative to [now]. Field names are the camelCase of the C# response records.

String _t(DateTime now, Duration ago) => now.subtract(ago).toUtc().toIso8601String();

List<Map<String, Object?>> trkDevicesJson(DateTime now) => [
  {
    'id': 'm1',
    'deviceId': 'TS-80214',
    'deviceName': 'Chiller tracker',
    'deviceType': 'Asset Tracker',
    'model': 'E8',
    'serialNumber': 'SN-1',
    'connectivity': 'BLE',
    'siteId': 'site-1',
    'siteName': 'Dubai Factory',
    'assetRegistrationId': 'AR000001',
    'assetCode': 'CHL-30002',
    'assetName': 'Chiller Unit 2',
    'gateway': 'GW-DXB-01',
    'powerSource': 'Battery',
    'batteryLevel': 78,
    'lastSeenAt': _t(now, const Duration(minutes: 2)),
    'health': 'Good',
    'firmwareVersion': '1.4.2',
  },
  {
    'id': 'm2',
    'deviceId': 'GW-DXB-01',
    'deviceName': 'Gateway 1',
    'deviceType': 'Gateway',
    'model': 'G1',
    'connectivity': 'Ethernet',
    'powerSource': 'PoE',
    'lastSeenAt': _t(now, const Duration(seconds: 30)),
  },
  {
    'id': 'm3',
    'deviceId': 'BT-1001',
    'deviceName': 'Pump tag',
    'deviceType': 'Asset Tracker',
    'model': 'E8',
    'connectivity': 'BLE',
    'siteName': 'Dubai Factory',
    'assetRegistrationId': 'AR000002',
    'assetCode': 'PMP-10001',
    'assetName': 'Pump 1',
    'powerSource': 'Battery',
    'batteryLevel': 12,
    'lastSeenAt': _t(now, const Duration(minutes: 30)),
    'health': 'Warning',
  },
];

List<Map<String, Object?>> trkHealthJson(DateTime now) => [
  {
    'id': 'h1',
    'deviceId': 'TS-80214',
    'deviceModel': 'E8',
    'assetRegistrationId': 'AR000001',
    'assetName': 'Chiller Unit 2',
    'connectivity': 'BLE',
    'signalStrengthDbm': -62,
    'snrDb': 7.5,
    'signalQuality': 'Good',
    'powerSource': 'Battery',
    'batteryLevel': 76,
    'lastSeenAt': _t(now, const Duration(minutes: 1)),
    'health': 'Healthy',
  },
];

List<Map<String, Object?>> trkTagsJson(DateTime now) => [
  {
    'id': 't1',
    'tagId': 'BLE-0001',
    'tagType': 'Beacon',
    'technology': 'BLE',
    'assetRegistrationId': 'AR000001',
    'assetName': 'Chiller Unit 2',
    'siteId': 'site-1',
    'siteName': 'Dubai Factory',
    'locationName': 'Warehouse A',
    'status': 'Assigned',
    'lastSeenAt': _t(now, const Duration(minutes: 3)),
  },
];

List<Map<String, Object?>> trkSensorsJson(DateTime now) => [
  {
    'id': 's1',
    'sensorId': 'SEN-T-80900',
    'sensorNumber': '80900',
    'name': 'Supply air probe',
    'sensorType': 'Temperature Probe',
    'measuredParameter': 'Temperature',
    'unit': '°C',
    'deviceId': 'TS-80214',
    'channel': 1,
    'samplingIntervalSeconds': 60,
    'lowAlarm': 2,
    'highAlarm': 40,
    'siteName': 'Dubai Factory',
    'assetRegistrationId': 'AR000001',
    'assetCode': 'CHL-30002',
    'assetName': 'Chiller Unit 2',
    'measurementPoint': 'Supply Air',
    'liveReading': 42.1,
    'calibrationDueDate': '2027-01-31T00:00:00Z',
    'status': 'Attention',
  },
  {
    'id': 's2',
    'sensorId': 'SEN-V-1',
    'name': 'Pump DE',
    'sensorType': 'Vibration Sensor',
    'measuredParameter': 'Vibration',
    'unit': 'mm/s',
    'deviceId': 'BT-1001',
    'assetRegistrationId': 'AR000002',
    'assetCode': 'PMP-10001',
    'liveReading': null,
    'status': 'Active',
  },
];

/// Newest first, as GET /api/location-events sends them; the last one is 10 days old.
List<Map<String, Object?>> trkEventsJson(DateTime now) => [
  {
    'id': 'e3',
    'eventId': 'LE000004',
    'eventTime': _t(now, const Duration(minutes: 5)),
    'assetId': 'CHL-30002',
    'assetName': 'Chiller Unit 2',
    'eventType': 'Entered Zone',
    'zoneName': 'Zone B',
  },
  {
    'id': 'e2',
    'eventId': 'LE000003',
    'eventTime': _t(now, const Duration(minutes: 20)),
    'assetId': 'CHL-30002',
    'assetName': 'Chiller Unit 2',
    'eventType': 'Departed Zone',
    'zoneName': 'Receiving',
    'details': 'Departed Receiving (dwell time 18 min)',
  },
  {
    'id': 'e1',
    'eventId': 'LE000002',
    'eventTime': _t(now, const Duration(hours: 2)),
    'assetId': 'FL-008',
    'assetName': 'Forklift 8',
    'eventType': 'Entered Zone',
    'zoneName': 'Charging Zone',
  },
  {
    'id': 'e0',
    'eventId': 'LE000001',
    'eventTime': _t(now, const Duration(days: 10)),
    'assetId': 'FL-008',
    'assetName': 'Forklift 8',
    'eventType': 'Entered Zone',
    'zoneName': 'Old Yard',
  },
];

/// Newest first; one every 10 minutes for 3 hours.
List<Map<String, Object?>> trkReadingsJson(DateTime now) => [
  for (var i = 0; i < 18; i++)
    {
      'id': 'r$i',
      'readingId': 'TR${(1000 - i).toString().padLeft(6, '0')}',
      'recordedAt': _t(now, Duration(minutes: 10 * i)),
      'deviceId': 'TS-80214',
      'sensorId': 'SEN-T-80900',
      'assetRegistrationId': 'AR000001',
      'parameter': 'Temperature',
      'value': 38 + i * .25,
      'unit': '°C',
      'createdAt': _t(now, Duration(minutes: 10 * i)),
    },
];

List<Map<String, Object?>> trkGeofencesJson(DateTime now) => [
  {
    'id': 'gf-1',
    'name': 'Factory perimeter',
    'geofenceType': 'Allowed',
    'siteId': 'site-1',
    'siteName': 'Dubai Factory',
    'monitoredAssetCount': 12,
    'ruleCount': 2,
    'status': 'Active',
    'lastEventAt': _t(now, const Duration(hours: 1)),
    'mapColour': '#2563EB',
    'assetGroups': ['Forklifts'],
    'schedule': 'Always active',
    'dwellAlertMinutes': 30,
    'monitorEntry': true,
    'monitorExit': true,
    'shape': 'Polygon',
    'polygonPoints': 4,
    'coordinates': [
      {'latitude': 25.01, 'longitude': 55.01},
      {'latitude': 25.01, 'longitude': 55.02},
      {'latitude': 25.02, 'longitude': 55.02},
      {'latitude': 25.02, 'longitude': 55.01},
    ],
    'radiusMeters': null,
  },
  {
    'id': 'gf-2',
    'name': 'Gate circle',
    'geofenceType': 'Restricted',
    'siteName': 'Dubai Factory',
    'status': 'Disabled',
    'mapColour': '#EF4444',
    'assetGroups': <String>[],
    'schedule': 'During working hours',
    'monitorEntry': true,
    'monitorExit': false,
    'shape': 'Circle',
    'coordinates': [
      {'latitude': 25.015, 'longitude': 55.015},
    ],
    'radiusMeters': 80,
  },
];

List<Map<String, Object?>> trkAlertsJson(DateTime now) => [
  {
    'id': 'a1',
    'alertId': 'IA000002',
    'raisedAt': _t(now, const Duration(minutes: 15)),
    'assetRegistrationId': 'AR000001',
    'assetName': 'Chiller Unit 2',
    'deviceId': 'TS-80214',
    'siteName': 'Dubai Factory',
    'eventType': 'Temperature High',
    'severity': 'Critical',
    'status': 'Open',
    'message': 'Supply air 42.1 °C above 40 °C',
  },
  {
    'id': 'a2',
    'alertId': 'IA000001',
    'raisedAt': _t(now, const Duration(days: 2)),
    'assetName': 'Forklift 8',
    'eventType': 'Geofence Exit',
    'severity': 'Warning',
    'status': 'Resolved',
    'resolvedBy': 'admin',
    'resolvedAt': _t(now, const Duration(days: 1)),
  },
];

const trkAlertCountJson = {
  'days': 7,
  'from': '2026-11-05T00:00:00Z',
  'to': '2026-11-12T00:00:00Z',
  'total': 2,
  'previousPeriodTotal': 4,
  'changePercent': -50.0,
  'byStatus': {'Open': 1, 'Acknowledged': 0, 'In Progress': 0, 'Resolved': 1},
  'bySeverity': {'Critical': 1, 'Warning': 1, 'Info': 0},
};

const trkTimelineJson = {
  'days': 7,
  'from': '2026-11-05T00:00:00Z',
  'to': '2026-11-12T00:00:00Z',
  'totals': {'date': '', 'telemetryData': 120, 'locationUpdates': 9, 'geofenceEvents': 2, 'alerts': 2},
  'points': [
    {'date': '2026-11-10', 'telemetryData': 60, 'locationUpdates': 4, 'geofenceEvents': 1, 'alerts': 1},
    {'date': '2026-11-11', 'telemetryData': 60, 'locationUpdates': 5, 'geofenceEvents': 1, 'alerts': 1},
  ],
};

// ------------------------------------------------------------------ Administration maps

/// FP-0001 on floor flr-1 (PDF, so no drawing is downloaded in widget tests).
const mapFloorPlansJson = [
  {
    'floorPlanId': 'FP-0001',
    'locationId': 'flr-1',
    'locationType': 'floor',
    'locationPath': 'Dubai Factory > Building A > Ground Floor',
    'name': 'Ground Floor',
    'type': 'Floor Plan',
    'format': 'PDF',
    'fileUrl': 'http://localhost:5300/uploads/maps/fp1.pdf',
    'fileName': 'fp1.pdf',
    'sizeBytes': 2048,
    'widthMeters': 120.0,
    'heightMeters': 60.0,
    'scale': '1:100',
    'currentVersion': 'v1.0',
    'status': 'Published',
    'publishedAt': '2026-11-01T08:00:00Z',
    'publishedBy': 'admin',
    'zoneCount': 3,
    'plotCount': 3,
  },
];

const mapZonesJson = [
  {
    'zoneId': 'Z-1',
    'floorPlanId': 'FP-0001',
    'code': 'ZB',
    'name': 'Zone B',
    'type': 'Storage',
    'color': '#2563EB',
    'shape': 'Rectangle',
    'x': 10.0,
    'y': 20.0,
    'w': 30.0,
    'h': 40.0,
    'points': <Object>[],
    'radius': null,
    'areaM2': 864.0,
    'active': true,
    'geofence': false,
    'entryAlert': false,
    'exitAlert': false,
    'dwellAlert': false,
    'dwellMinutes': 0,
  },
  {
    'zoneId': 'Z-2',
    'floorPlanId': 'FP-0001',
    'code': 'RS',
    'name': 'Restricted Store',
    'type': 'Restricted',
    'color': '#F97316',
    'shape': 'Polygon',
    'x': 50.0,
    'y': 10.0,
    'w': 20.0,
    'h': 20.0,
    'points': [
      {'x': 50.0, 'y': 10.0},
      {'x': 70.0, 'y': 10.0},
      {'x': 60.0, 'y': 30.0},
    ],
    'accessRule': 'Restricted - Badge Required',
    'active': true,
    'geofence': true,
    'entryAlert': true,
    'exitAlert': false,
    'dwellAlert': true,
    'dwellMinutes': 15,
  },
  {
    'zoneId': 'Z-3',
    'floorPlanId': 'FP-0001',
    'code': 'CZ',
    'name': 'Charging Zone',
    'type': 'Operational',
    'shape': 'Circle',
    'x': 70.0,
    'y': 50.0,
    'w': 0.0,
    'h': 0.0,
    'points': <Object>[],
    'radius': 5.0,
    'active': true,
    'geofence': false,
    'entryAlert': false,
    'exitAlert': false,
    'dwellAlert': false,
    'dwellMinutes': 0,
  },
];

const mapPlotsJson = [
  {
    'code': 'CHL-30002',
    'name': 'Chiller Unit 2',
    'type': 'Chiller',
    'status': 'Active',
    'x': 25.0,
    'y': 40.0,
    'orientation': 0.0,
    'zoneId': 'Z-1',
    'floorPlanId': 'FP-0001',
    'kind': 'asset',
    'itemId': 'AR000001',
    'updatedAt': '2026-11-01T08:00:00Z',
  },
  {
    'code': 'TS-80214',
    'name': 'Chiller tracker',
    'x': 30.0,
    'y': 45.0,
    'orientation': 0.0,
    'floorPlanId': 'FP-0001',
    'kind': 'device',
    'itemId': 'TS-80214',
  },
  {
    'code': 'PMP-10001',
    'name': 'Pump 1',
    'x': 80.0,
    'y': 20.0,
    'orientation': 90.0,
    'floorPlanId': 'FP-0001',
    'kind': 'asset',
    'itemId': 'AR000002',
  },
];

const mapLayersJson = [
  {'key': 'zones', 'name': 'Zones', 'enabled': true, 'visible': true, 'opacity': 80},
  {'key': 'geofences', 'name': 'Geofences', 'enabled': true, 'visible': true, 'opacity': 80},
  {'key': 'assets', 'name': 'Assets', 'enabled': true, 'visible': true, 'opacity': 80},
  {'key': 'devices', 'name': 'Devices', 'enabled': true, 'visible': false, 'opacity': 80},
  {'key': 'heatmap', 'name': 'Heatmap (Utilization)', 'enabled': false, 'visible': true, 'opacity': 80},
];

/// Master Data: one site with coordinates, one building, two floors (flr-2 has no plan).
const mdSitesJson = [
  {
    'id': 'site-1',
    'siteName': 'Dubai Factory',
    'code': 'DXB',
    'latitude': 25.015,
    'longitude': 55.015,
    'status': 'Active',
  },
];
const mdBuildingsJson = [
  {
    'id': 'bld-1',
    'name': 'Building A',
    'code': 'BLD-A',
    'site': {'id': 'site-1', 'name': 'Dubai Factory'},
    'latitude': 25.016,
    'longitude': 55.016,
    'status': 'Active',
  },
];
const mdFloorsJson = [
  {
    'id': 'flr-1',
    'name': 'Ground Floor',
    'code': 'GF',
    'site': {'id': 'site-1', 'name': 'Dubai Factory'},
    'buildingId': 'bld-1',
    'buildingName': 'Building A',
    'status': 'Active',
  },
  {
    'id': 'flr-2',
    'name': 'First Floor',
    'code': 'F1',
    'site': {'id': 'site-1', 'name': 'Dubai Factory'},
    'buildingId': 'bld-1',
    'buildingName': 'Building A',
    'status': 'Active',
  },
];

extension TrackingFake on FakeAssetBackend {
  /// Every Tracking / Administration map / Master Data location route the Tracking screens read.
  void seedTracking(DateTime Function() now) {
    on('GET', '/api/iot-devices', (_) => FakeReply(200, trkDevicesJson(now())));
    on('GET', '/api/device-health', (_) => FakeReply(200, trkHealthJson(now())));
    on('GET', '/api/tag-ids', (_) => FakeReply(200, trkTagsJson(now())));
    on('GET', '/api/sensors', (_) => FakeReply(200, trkSensorsJson(now())));
    on('GET', '/api/sensors/SEN-T-80900', (_) => FakeReply(200, trkSensorsJson(now()).first));
    on(
      'GET',
      '/api/sensors/types',
      (_) => const FakeReply(200, [
        {'sensorType': 'Temperature Probe', 'idPrefix': 'SEN-T', 'measuredParameter': 'Temperature', 'unit': '°C'},
        {'sensorType': 'Vibration Sensor', 'idPrefix': 'SEN-V', 'measuredParameter': 'Vibration', 'unit': 'mm/s'},
      ]),
    );
    on('GET', '/api/sensors/statuses', (_) => const FakeReply(200, ['Active', 'Offline', 'Attention', 'Inactive']));
    on('GET', '/api/location-events', (_) => FakeReply(200, trkEventsJson(now())));
    on('GET', '/api/telemetry-readings', (_) => FakeReply(200, trkReadingsJson(now())));
    on('GET', '/api/iot-events/timeline', (_) => const FakeReply(200, trkTimelineJson));
    on('GET', '/api/geofences', (_) => FakeReply(200, trkGeofencesJson(now())));
    on('GET', '/api/iot-alerts', (_) => FakeReply(200, trkAlertsJson(now())));
    on('GET', '/api/iot-alerts/count', (_) => const FakeReply(200, trkAlertCountJson));
    const maps = '/api/administration/maps';
    on('GET', '$maps/floor-plans', (o) {
      final q = o.uri.queryParameters;
      return FakeReply(200, [
        for (final p in mapFloorPlansJson)
          if ((q['locationId'] == null || p['locationId'] == q['locationId']) &&
              (q['status'] == null || p['status'] == q['status']))
            p,
      ]);
    });
    on('GET', '$maps/floor-plans/FP-0001/zones', (_) => const FakeReply(200, mapZonesJson));
    on('GET', '$maps/floor-plans/FP-0001/plots', (_) => const FakeReply(200, mapPlotsJson));
    on('GET', '$maps/floor-plans/FP-0001/layers', (_) => const FakeReply(200, mapLayersJson));
    on('GET', '/api/master-data/sites', (_) => const FakeReply(200, mdSitesJson));
    on('GET', '/api/master-data/buildings', (_) => const FakeReply(200, mdBuildingsJson));
    on('GET', '/api/master-data/floors', (_) => const FakeReply(200, mdFloorsJson));
    // The Asset service knows AR000001 / TAG-0001 and AR000002 / TAG-0002 (sampleAssets); the
    // tracking record "FL-008" (location events only) matches no registered asset.
    on('GET', '/api/asset-registrations/lookup', (o) {
      final code = (o.uri.queryParameters['code'] ?? '').toUpperCase();
      final id = switch (code) {
        'AR000001' || 'TAG-0001' => 'AR000001',
        'AR000002' || 'TAG-0002' => 'AR000002',
        _ => null,
      };
      if (id == null) {
        return FakeReply(404, {'message': "No asset matches the scanned code '${o.uri.queryParameters['code']}'."});
      }
      return FakeReply(200, {
        'code': o.uri.queryParameters['code'],
        'matchedBy': code.startsWith('AR') ? 'assetRegistrationId' : 'assetTag',
        'asset': {...sampleRegistration(), 'assetRegistrationId': id},
      });
    });
  }
}
