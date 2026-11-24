import 'fake_asset_backend.dart';

/// Contract-shaped Real-Time Location / Locate samples (docs/api/MOBILE-API-HANDOFF.md 1.2–1.5, 2.1),
/// made-up values. PUMP-01 is heard by gateway AC233FC0FFEE (plot itemId lower case), FORK-02 by
/// ac233fc0beef (matches the plot's CODE), CART-03 by a gateway that is not plotted.

const rtlOverviewJson = {
  'generatedAt': '2026-11-19T10:15:00Z',
  'kpis': {'assetsInView': 3, 'moving': 1, 'stationary': 1, 'unknown': 1, 'locationAlerts': 1},
  'assets': [
    {
      'assetId': 'AR-000123',
      'assetTag': 'PUMP-01',
      'name': 'Cooling Pump 01',
      'assetType': 'Pump',
      'operationalStatus': 'Active',
      'technology': 'BLE',
      'tracker': 'C30000AABB01',
      'tags': ['C30000AABB01', 'C3:00:00:AA:BB:02'],
      'location': {
        'gatewayId': 'AC233FC0FFEE',
        'gatewayName': 'GW Hall A',
        'zoneId': 'zone-a1',
        'zoneName': 'Hall A',
        'floorName': 'Ground Floor',
        'buildingName': 'Block 1',
        'siteId': 'site-1',
        'siteName': 'Head Office',
        'floorPlanId': 'fp-001',
        'locationPath': 'Head Office › Block 1 › Ground Floor › Hall A',
        'method': 'Estimated',
        'basis': 'Strongest gateway',
        'accuracy': 'Zone level',
        'at': '2026-11-19T10:14:52Z',
      },
      'freshness': 'Live',
      'movement': 'Moving',
      'lastZoneChangeAt': '2026-11-19T10:12:30Z',
      'lastValidLocationAt': '2026-11-19T10:14:52Z',
      'locationSource': 'BLE Proximity',
      'accuracy': 'Zone level',
      'confidence': 'High',
      'confidenceBasis': 'Strongest gateway 9 dB above the next of 2 hearing it',
      'gatewaysHearing': 2,
      'signalMarginDb': 9.0,
      'rssi': -61,
      'activeLocationAlerts': 1,
      'expected': {
        'zoneId': 'zone-a1',
        'zoneName': 'Hall A',
        'floorId': 'fl-1',
        'floorName': 'Ground Floor',
        'buildingName': 'Block 1',
        'siteName': 'Head Office',
        'locationPath': 'Head Office › Block 1 › Ground Floor › Hall A',
      },
      'comparisonStatus': 'Match',
      'comparisonNote': 'Detected in its registered zone',
    },
    {
      'assetId': 'AR-000124',
      'assetTag': 'FORK-02',
      'name': 'Forklift 02',
      'assetType': 'Vehicle',
      'technology': 'BLE',
      'tracker': 'C30000AABB03',
      'tags': ['C30000AABB03'],
      'location': {
        'gatewayId': 'ac233fc0beef',
        'zoneName': 'Store Room',
        'floorPlanId': 'fp-001',
        'method': 'Estimated',
        'basis': 'Strongest gateway',
        'accuracy': 'Gateway range',
        'at': '2026-11-19T10:14:40Z',
      },
      'freshness': 'Live',
      'movement': 'Stationary',
      'locationSource': 'BLE Proximity',
      'accuracy': 'Gateway range',
      'confidence': 'Medium',
      'confidenceBasis': 'Strongest gateway 4 dB above the next',
      'gatewaysHearing': 2,
      'rssi': -70,
      'activeLocationAlerts': 0,
      'expected': null,
      'comparisonStatus': 'Not set',
      'comparisonNote': 'No registered location',
    },
    {
      'assetId': 'AR-000125',
      'assetTag': 'CART-03',
      'name': 'Tool Cart 03',
      'assetType': 'Cart',
      'technology': 'BLE',
      'tracker': null,
      'tags': <String>[],
      'location': {
        'gatewayId': 'AC233FC0DEAD',
        'zoneName': 'Yard',
        'method': 'Estimated',
        'basis': 'Strongest gateway',
        'accuracy': 'Gateway range',
        'at': '2026-11-19T10:08:00Z',
      },
      'freshness': 'Stale',
      'movement': 'Unknown',
      'locationSource': 'BLE Proximity',
      'accuracy': 'Gateway range',
      'confidence': 'Unknown',
      'confidenceBasis': 'Not live',
      'gatewaysHearing': 0,
      'rssi': null,
      'activeLocationAlerts': 0,
      'comparisonStatus': 'Mismatch',
      'comparisonNote': 'Detected outside its registered zone',
    },
  ],
  'gateways': [
    {
      'deviceId': 'AC233FC0FFEE',
      'name': 'GW-01',
      'type': 'Gateway',
      'connectivity': 'Online',
      'lastSeenAt': '2026-11-19T10:14:55Z',
      'tagsHeard': 4,
      'floorPlanId': 'fp-001',
      'plotted': true,
      'siteId': 'site-1',
    },
    {
      'deviceId': 'AC233FC0BEEF',
      'name': 'GW-02',
      'connectivity': 'Idle',
      'tagsHeard': 1,
      'floorPlanId': 'fp-001',
      'plotted': true,
    },
  ],
};

const rtlEmptyOverviewJson = {
  'generatedAt': '2026-11-19T10:15:00Z',
  'kpis': {'assetsInView': 0, 'moving': 0, 'stationary': 0, 'unknown': 0, 'locationAlerts': 0},
  'assets': <Object>[],
  'gateways': <Object>[],
};

Map<String, Object?> rtlEventsJson({int page = 1, int pageSize = 10, int total = 37}) {
  final pages = (total / pageSize).ceil();
  final first = (page - 1) * pageSize;
  return {
    'items': [
      for (var i = first; i < first + pageSize && i < total; i++)
        {
          'eventId': 'ENT-$i',
          'at': '2026-11-19T10:${(59 - i % 60).toString().padLeft(2, '0')}:00Z',
          'assetId': 'AR-000123',
          'assetName': 'Cooling Pump 01',
          'eventType': i.isEven ? 'Zone Entry' : 'Geofence Entry',
          'zone': 'Hall A',
          'floor': 'Ground Floor',
          'details': 'Entered from Store Room',
          'severity': i.isEven ? 'Info' : 'Critical',
          'source': 'C30000AABB01',
          'status': 'Recorded',
        },
    ],
    'total': total,
    'page': page,
    'pageSize': pageSize,
    'totalPages': pages,
    'rangeLabel': 'Last 24 hours',
    'eventTypes': ['Location Detected', 'Zone Entry', 'Zone Exit', 'Geofence Entry'],
  };
}

const rtlTrailJson = {
  'assetId': 'AR-000123',
  'name': 'Cooling Pump 01',
  'range': '30m',
  'from': '2026-11-19T09:45:00Z',
  'to': '2026-11-19T10:15:00Z',
  'segments': [
    {
      'gatewayId': 'AC233FC0BEEF',
      'zoneName': 'Store Room',
      'floorName': 'Ground Floor',
      'from': '2026-11-19T09:45:00Z',
      'to': '2026-11-19T10:12:30Z',
      'minutes': 27.5,
      'initial': true,
    },
    {
      'gatewayId': 'AC233FC0FFEE',
      'zoneName': 'Hall A',
      'floorName': 'Ground Floor',
      'from': '2026-11-19T10:12:30Z',
      'to': '2026-11-19T10:15:00Z',
      'minutes': 2.5,
      'initial': false,
    },
  ],
  'zoneChanges': 1,
  'timeInZones': [
    {'zone': 'Store Room', 'minutes': 27.5},
    {'zone': 'Hall A', 'minutes': 2.5},
  ],
  'note': 'BLE proximity: zone transitions, not a precise path.',
};

const rtlSitesJson = [
  {'id': 'site-1', 'siteName': 'Head Office', 'code': 'HO', 'status': 'Active'},
  {'id': 'site-old', 'siteName': 'Old Depot', 'code': 'OD', 'status': 'Active', 'isDeleted': true},
];

const rtlBuildingsJson = [
  {
    'id': 'bld-1',
    'name': 'Block 1',
    'code': 'B1',
    'status': 'Active',
    'site': {'id': 'site-1', 'name': 'Head Office'},
  },
];

const rtlFloorsJson = [
  {'id': 'fl-1', 'name': 'Ground Floor', 'code': 'GF', 'status': 'Active', 'buildingId': 'bld-1', 'site': 'site-1'},
  {
    'id': 'fl-2',
    'name': 'First Floor',
    'code': '1F',
    'status': 'Active',
    'buildingId': 'bld-1',
    'site': {'id': 'site-1'},
    'imageUrl': '/uploads/floors/fl-2.png',
  },
  {'id': 'fl-3', 'name': 'Roof', 'code': 'RF', 'status': 'Deleted', 'buildingId': 'bld-1'},
];

const rtlFloorPlansJson = {
  'items': [
    {
      'floorPlanId': 'fp-001',
      'locationId': 'fl-1',
      'name': 'Ground Floor plan',
      'format': 'png',
      'fileUrl': '/uploads/maps/fp-001.png',
      'widthMeters': 60,
      'heightMeters': 40,
      'status': 'Published',
      'updatedAt': '2026-11-18T08:00:00Z',
      'createdAt': '2026-11-10T08:00:00Z',
    },
    {
      'floorPlanId': 'fp-002',
      'locationId': 'fl-1',
      'name': 'Ground Floor draft',
      'format': 'png',
      'fileUrl': '/uploads/maps/fp-002.png',
      'status': 'Draft',
      'updatedAt': '2026-11-19T08:00:00Z',
    },
    {
      'floorPlanId': 'fp-003',
      'locationId': 'fl-2',
      'name': 'First Floor (archived)',
      'format': 'png',
      'fileUrl': '/uploads/maps/fp-003.png',
      'status': 'Archived',
    },
  ],
};

const rtlZonesJson = [
  {
    'zoneId': 'zone-a1',
    'code': 'Z-A1',
    'name': 'Hall A',
    'type': 'Production',
    'color': '#7c3aed',
    'shape': 'Rectangle',
    'x': 10,
    'y': 15,
    'w': 30,
    'h': 25,
    'active': true,
  },
  {
    'zoneId': 'zone-sr',
    'code': 'Z-SR',
    'name': 'Store Room',
    'type': 'Storage',
    'shape': 'Circle',
    'x': 75,
    'y': 60,
    'radius': 10,
    'active': true,
  },
  {
    'zoneId': 'zone-x',
    'code': 'Z-X',
    'name': 'Server Room',
    'type': 'Restricted Area',
    'shape': 'Polygon',
    'points': [
      {'x': 50, 'y': 10},
      {'x': 65, 'y': 10},
      {'x': 65, 'y': 30},
    ],
    'active': true,
  },
];

const rtlPlotsJson = [
  {
    'kind': 'device',
    'itemId': 'ac233fc0ffee',
    'code': 'AC233FC0FFEE',
    'name': 'GW-01',
    'type': 'Gateway',
    'x': 22.5,
    'y': 30.0,
    'zoneId': 'zone-a1',
  },
  {'kind': 'device', 'itemId': 'gw-02', 'code': 'AC233FC0BEEF', 'name': 'GW-02', 'type': 'Gateway', 'x': 75, 'y': 60},
  {'kind': 'asset', 'itemId': 'AR-000999', 'name': 'Plotted asset', 'x': 40, 'y': 40},
];

/// A 1 × 1 PNG (the plan drawing in widget tests).
const rtlPng1x1 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';

/// One TagLocation row (GET /tags and the tags/live socket) for [tagId] heard by [gatewayId].
Map<String, Object?> rtlTagRow(
  String tagId,
  String gatewayId, {
  String zoneId = 'zone-a1',
  String zoneName = 'Hall A',
  String connection = 'Online',
  double rssi = -61,
  double battery = 80,
  String timestamp = '2026-11-19T10:14:52Z',
  String? assetId,
}) => {
  'tagId': tagId,
  'gatewayId': gatewayId,
  'deviceId': gatewayId,
  'gatewayName': 'GW $zoneName',
  'timestamp': timestamp,
  'status': 'online',
  'connection': connection,
  'rssi': rssi,
  'battery': battery,
  'plotted': true,
  'zoneId': zoneId,
  'zoneName': zoneName,
  'floorPlanId': 'fp-001',
  'floorName': 'Ground Floor',
  'buildingName': 'Block 1',
  'siteId': 'site-1',
  'siteName': 'Head Office',
  'locationPath': 'Head Office › Block 1 › Ground Floor › $zoneName',
  if (assetId != null) 'asset': {'assetRegistrationId': assetId},
  'matchIssues': <String>[],
};

/// What GET /tags answers (and the socket's snapshot carries): PUMP-01's tag and FORK-02's tag.
final rtlTagsJson = {
  'total': 2,
  'online': 2,
  'plotted': 2,
  'withAsset': 2,
  'tags': [
    rtlTagRow('c30000aabb01', 'ac233fc0ffee', assetId: 'AR-000123'),
    rtlTagRow('c30000aabb03', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -70, assetId: 'AR-000124'),
  ],
};

extension RtlSeed on FakeAssetBackend {
  /// Real-Time Location routes (overview / events / trail), Master Data floors and Administration
  /// plans / zones / plots. [overview] replaces the overview reply.
  void seedRtl({Object overview = rtlOverviewJson}) {
    on('GET', '/api/real-time-location/overview', (_) => FakeReply(200, overview));
    on('GET', '/api/real-time-location/tags', (_) => FakeReply(200, rtlTagsJson));
    on('GET', '/api/real-time-location/events', (o) {
      final q = o.uri.queryParameters;
      return FakeReply(
        200,
        rtlEventsJson(page: int.tryParse(q['page'] ?? '') ?? 1, pageSize: int.tryParse(q['pageSize'] ?? '') ?? 10),
      );
    });
    on('GET', '/api/real-time-location/assets/AR-000123/trail', (_) => const FakeReply(200, rtlTrailJson));
    on('GET', '/api/real-time-location/assets/*', (o) => FakeReply(404, {'message': "No tracked tags are linked to '${o.uri.pathSegments[3]}'."}));
    on('GET', '/api/master-data/sites', (_) => const FakeReply(200, rtlSitesJson));
    on('GET', '/api/master-data/buildings', (_) => const FakeReply(200, rtlBuildingsJson));
    on('GET', '/api/master-data/floors', (_) => const FakeReply(200, rtlFloorsJson));
    const maps = '/api/administration/maps';
    on('GET', '$maps/floor-plans', (_) => const FakeReply(200, rtlFloorPlansJson));
    on('GET', '$maps/floor-plans/fp-001/zones', (_) => const FakeReply(200, rtlZonesJson));
    on('GET', '$maps/floor-plans/fp-001/plots', (_) => const FakeReply(200, rtlPlotsJson));
    on('GET', '/api/locating/tags/c30000aabb01/commands', (_) => const FakeReply(200, [
      {
        '_id': {r'$oid': '6555'},
        'reqId': 503,
        'command': 'led',
        'tagid': 'c30000aabb01',
        'gatewayid': 'ac233fc0ffee',
        'status': 'completed',
        'sentAt': {r'$date': '2026-11-19T10:15:01Z'},
        'request': {'key': 'SERVER-SECRET'},
        'responses': <Object>[],
      },
    ]));
  }
}
