import 'dart:async';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_models.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart';
import 'package:asset360_mobile/data/repositories/network_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:asset360_mobile/features/home/dash_map.dart' show DashAssetPin;
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_tracking_hub.dart';
import 'package:asset360_mobile/features/more/tracking/live/open_asset_on_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/outdoor_map.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';

import 'fake_asset_backend.dart';
import 'dashboard_samples.dart';
import 'tracking_samples.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

class _Timer implements Timer {
  _Timer(this.delay);

  final Duration delay;
  bool cancelled = false;

  @override
  void cancel() => cancelled = true;

  @override
  bool get isActive => !cancelled;

  @override
  int get tick => 0;
}

final _now = DateTime.utc(2026, 11, 18, 10);

TagLocationUpdate _u(String tag, String? gw, {Duration ago = const Duration(minutes: 1)}) =>
    TagLocationUpdate(tagId: tag, lastToGatewayId: gw, lastChangedAt: _now.subtract(ago));

const _gateways = [
  TrkGateway(deviceId: 'GW-1', deviceName: 'Gate 1', siteId: 'S1', siteName: 'Plant'),
  TrkGateway(deviceId: 'GW-2', deviceName: 'Gate 2', siteId: 'S2', siteName: 'Depot'),
  TrkGateway(deviceId: 'GW-NOSITE', deviceName: 'Loose gateway'),
];

const _links = {
  't1': TagLink(tagId: 'T1', assetRegistrationId: 'AR1', assetName: 'Pump 1', battery: 80),
  't2': TagLink(tagId: 'T2', assetRegistrationId: 'AR2', assetName: 'Chiller 2'),
};

void main() {
  group('placement: tag → gateway → site', () {
    test('the site is the gateway\'s site; registered building / floor / zone only in that same site', () {
      final tree = buildLocationTree(
        updates: [_u('T1', 'GW-1'), _u('T2', 'GW-2')],
        gateways: _gateways,
        links: _links,
        registered: const {
          // AR1 registered in the live site S1 → placed down to its zone.
          'ar1': RegisteredPlace(
            siteId: 'S1',
            buildingId: 'B1',
            buildingName: 'Block A',
            floorId: 'F1',
            floorName: 'Ground',
            zoneId: 'Z1',
            zoneName: 'Bay 3',
          ),
          // AR2 registered at S1 but heard at S2 → stays at site level of S2, not "registered".
          'ar2': RegisteredPlace(siteId: 'S1', buildingId: 'B1', buildingName: 'Block A'),
        },
        md: const LocationMasterData(
          sites: [MdSite(id: 'S1', name: 'Main Plant')],
        ),
      );
      expect(tree.sites.map((s) => s.name), ['Depot', 'Main Plant'], reason: 'Master Data name wins');
      final plant = tree.sites.firstWhere((s) => s.id == 'S1');
      final zone = plant.find('Z1')!;
      expect(zone.level, LocLevel.zone);
      expect(zone.name, 'Bay 3');
      expect(zone.assets.single.title, 'Pump 1');
      expect(zone.assets.single.registered, isNotNull);
      expect(zone.assets.single.nearLabel, 'near Gate 1');
      expect(plant.find('B1')!.counts(_now).assets, 1);
      final depot = tree.sites.firstWhere((s) => s.id == 'S2');
      expect(depot.children, isEmpty);
      expect(depot.assets.single.title, 'Chiller 2');
      expect(depot.assets.single.registered, isNull);
      expect(tree.siteCounts, {'S2': 1, 'S1': 1});
    });

    test('unknown gateways, no gateway and a gateway without site are "Unplaced"', () {
      final tree = buildLocationTree(
        updates: [_u('T1', 'GW-404'), _u('T2', null), _u('T3', 'GW-NOSITE')],
        gateways: _gateways,
        links: _links,
      );
      expect(tree.sites, isEmpty);
      expect(tree.unplaced, hasLength(3));
      expect(tree.unknownGateways, {'GW-404'});
      final byTag = {for (final a in tree.unplaced) a.tagId: a};
      expect(byTag['T1']!.nearLabel, 'Unknown gateway GW-404');
      expect(byTag['T1']!.unplaced, UnplacedReason.unknownGateway);
      expect(byTag['T2']!.unplaced, UnplacedReason.noGateway);
      expect(byTag['T3']!.unplaced, UnplacedReason.gatewayWithoutSite);
      expect(byTag['T3']!.title, 'Tag T3', reason: 'no asset linked');
    });

    test('the newest update of a repeated tag wins', () {
      final tree = buildLocationTree(
        updates: [
          _u('T1', 'GW-2', ago: const Duration(hours: 1)),
          _u('T1', 'GW-1'),
        ],
        gateways: _gateways,
        links: _links,
      );
      expect(tree.sites.single.id, 'S1');
    });

    test('tag links: device assetCode / deviceId, then Dashboard network-status tags', () {
      final links = tagLinks(
        [
          const TrkDevice(deviceId: 'BT-1', assetCode: 'TAG-9', assetRegistrationId: 'AR9', batteryLevel: 55),
          const TrkDevice(deviceId: 'GW-1', deviceType: 'Gateway', assetRegistrationId: 'ARX'),
        ],
        [(tagId: 'TAG-7', assetRegistrationId: 'AR7', assetName: 'Fan 7')],
      );
      expect(links['tag-9']!.assetRegistrationId, 'AR9');
      expect(links['tag-9']!.battery, 55);
      expect(links['bt-1']!.assetRegistrationId, 'AR9');
      expect(links['tag-7']!.assetName, 'Fan 7');
      expect(links.containsKey('gw-1'), isFalse, reason: 'gateways are not tags');
    });
  });

  group('online / offline timing', () {
    test('online = lastChangedAt within 10 minutes; counts and alerts per level', () {
      final tree = buildLocationTree(
        updates: [
          _u('T1', 'GW-1', ago: const Duration(minutes: 10)),
          _u('T2', 'GW-1', ago: const Duration(minutes: 10, seconds: 1)),
        ],
        gateways: _gateways,
        links: _links,
        alerts: [
          TrkAlert(
            alertId: 'a',
            assetName: '',
            eventType: '',
            severity: '',
            raisedAt: _now,
            assetRegistrationId: 'AR2',
          ),
          TrkAlert(
            alertId: 'b',
            assetName: '',
            eventType: '',
            severity: '',
            raisedAt: _now,
            assetRegistrationId: 'AR2',
            status: 'Resolved',
          ),
        ],
      );
      final c = tree.sites.single.counts(_now);
      expect((c.assets, c.online, c.offline, c.alerts), (2, 1, 1, 1));
      final t2 = tree.sites.single.assets.firstWhere((a) => a.tagId == 'T2');
      expect(t2.isOnline(_now), isFalse);
      expect(const LiveTagAsset(tagId: 'X').isOnline(_now), isFalse, reason: 'never changed = offline');
    });
  });

  group('device plots (KEY RULE)', () {
    const plan = MapFloorPlan(
      floorPlanId: 'FP-26',
      locationId: 'F1',
      locationType: 'floor',
      status: 'Published',
      name: 'Ground',
    );
    const zoneA = MapZone(
      zoneId: 'MZ-A',
      name: 'Dock',
      shape: 'Rectangle',
      x: 0,
      y: 0,
      w: 50,
      h: 50,
      location: MapLocationRef(
        siteId: 'S1',
        siteName: 'Plant',
        buildingId: 'B1',
        buildingName: 'Block A',
        floorId: 'F1',
        floorName: 'Ground',
        zoneId: 'MDZ-A',
        zoneName: 'Dock (MD)',
      ),
    );
    const zoneB = MapZone(zoneId: 'MZ-B', name: 'Store', shape: 'Rectangle', x: 50, y: 0, w: 50, h: 50);
    const reader = MapPlot(
      kind: 'device',
      itemId: 'C851FBF764C0',
      deviceId: 'C851FBF764C0',
      code: 'RDR-1',
      name: 'Dock reader',
      x: 20,
      y: 20,
      zoneId: 'MZ-A',
    );
    const storeReader = MapPlot(
      kind: 'device',
      itemId: 'rdr-store',
      name: 'Store reader',
      x: 70,
      y: 20,
      zoneId: 'MZ-B',
    );
    const pumpPlot = MapPlot(kind: 'asset', itemId: 'AR1', name: 'Pump 1', x: 25, y: 25, zoneId: 'MZ-A');
    const chillerPlot = MapPlot(kind: 'asset', itemId: 'AR2', name: 'Chiller 2', x: 30, y: 30, zoneId: 'MZ-A');
    const md = LocationMasterData(
      floors: [
        MdSpace(
          id: 'F1',
          name: 'Ground',
          buildingId: 'B1',
          site: MdRef(id: 'S1', name: 'Plant'),
        ),
      ],
      buildings: [
        MdSpace(
          id: 'B1',
          name: 'Block A',
          site: MdRef(id: 'S1', name: 'Plant'),
        ),
      ],
    );
    final places = devicePlaces([
      (plan: plan, zones: const [zoneA, zoneB], plots: const [reader, storeReader, pumpPlot, chillerPlot]),
    ], md);

    test('device ids match case-insensitively (itemId, deviceId and code)', () {
      expect(places.keys, containsAll(['c851fbf764c0', 'rdr-1', 'rdr-store']));
      expect(places['c851fbf764c0']!.deviceLabel, 'Dock reader');
      final tree = buildLocationTree(
        updates: [_u('T1', 'c851FBF764c0')],
        gateways: const [],
        links: _links,
        devicePlaces: places,
      );
      expect(tree.unplaced, isEmpty, reason: '.26: the gateways list is empty, the device plot places it');
      expect(tree.sites.single.allAssets.single.nearLabel, 'near Dock reader');
    });

    test('tag → device → zone / floor / site; the device plot wins over the gateways list', () {
      final tree = buildLocationTree(
        updates: [_u('T1', 'C851FBF764C0'), _u('T2', 'RDR-STORE')],
        // The same id in the gateways list at ANOTHER site: the plot is preferred.
        gateways: const [TrkGateway(deviceId: 'c851fbf764c0', siteId: 'S2', siteName: 'Depot')],
        links: _links,
        devicePlaces: places,
      );
      expect(tree.sites.map((s) => s.id), ['S1']);
      final dock = tree.find('MDZ-A')!;
      expect(dock.name, 'Dock', reason: 'map zone label');
      expect(dock.assets.single.title, 'Pump 1');
      expect(tree.find('F1')!.level, LocLevel.floor);
      // Store has no `location`: floor → building → site come from the plan's Master Data floor.
      final store = tree.find('map:MZ-B')!;
      expect(store.assets.single.title, 'Chiller 2');
      expect(tree.find('B1')!.counts(_now).assets, 2);
    });

    test('moved assets and device clusters on one floor plan', () {
      final tree = buildLocationTree(
        updates: [
          _u('T1', 'rdr-1'), // AR1 plotted in Dock, heard in Dock → not moved
          _u('T2', 'rdr-store'), // AR2 plotted in Dock, heard in Store → moved
          _u('T9', 'C851FBF764C0'), // no asset plot → clustered on the Dock reader
        ],
        gateways: const [],
        links: _links,
        devicePlaces: places,
      );
      final live = buildFloorLive(
        plan: plan,
        plots: const [reader, storeReader, pumpPlot, chillerPlot],
        assets: tree.all,
      );
      expect(live.moved, {'asset:AR2'});
      expect(live.plotted.keys, containsAll(['asset:AR1', 'asset:AR2']));
      expect(live.clusters.keys, ['device:C851FBF764C0']);
      expect(live.clusters['device:C851FBF764C0']!.single.tagId, 'T9');
      expect(live.byZone['MZ-A']!.map((a) => a.tagId), unorderedEquals(['T1', 'T9']));
      expect(live.byZone['MZ-B']!.single.tagId, 'T2');
      // Heard on ANOTHER plan → moved as well.
      const other = MapFloorPlan(floorPlanId: 'FP-OTHER', locationId: 'F9');
      expect(buildFloorLive(plan: other, plots: const [pumpPlot], assets: tree.all).moved, {'asset:AR1'});
    });

    test('Draft plans only in developer builds; Archived never; Published preferred', () {
      const draft = MapFloorPlan(floorPlanId: 'D', locationId: 'F1', status: 'Draft');
      const archived = MapFloorPlan(floorPlanId: 'A', locationId: 'F1', status: 'Archived');
      expect(planVisible(plan, dev: false), isTrue);
      expect(planVisible(draft, dev: false), isFalse);
      expect(planVisible(draft, dev: true), isTrue);
      expect(planVisible(archived, dev: true), isFalse);
      expect(planForLocation(const [draft, archived], 'F1', dev: false), isNull);
      expect(planForLocation(const [draft, archived], 'F1', dev: true)!.floorPlanId, 'D');
      expect(planForLocation(const [draft, plan], 'F1', dev: true)!.floorPlanId, 'FP-26');
    });

    test('% → map: polygon points, rectangle corners, circle round about x / y', () {
      final f = PlanFrame.forAspect(1600 / 1200);
      const poly = MapZone(zoneId: 'p', shape: 'Polygon', points: [(x: 0, y: 0), (x: 100, y: 0), (x: 50, y: 100)]);
      expect(f.zoneOutline(poly), [f.topLeft, LatLng(f.height, f.width), LatLng(0, f.width / 2)]);
      const rect = MapZone(zoneId: 'r', shape: 'Rectangle', x: 10, y: 20, w: 30, h: 40);
      expect(f.zoneOutline(rect).first, f.toLatLng(10, 20));
      expect(f.zoneOutline(rect)[2], f.toLatLng(40, 60));
      // y grows downwards: 20 % from the top is above 60 %.
      expect(f.toLatLng(10, 20).latitude, greaterThan(f.toLatLng(10, 60).latitude));
      // A circle's centre is x / y even when the server also sends a box.
      const circle = MapZone(zoneId: 'c', shape: 'Circle', x: 40, y: 50, w: 20, h: 20, radius: 10);
      expect(circle.center, (x: 40.0, y: 50.0));
      final c = f.toLatLng(40, 50);
      for (final p in f.zoneOutline(circle)) {
        final d =
            (p.latitude - c.latitude) * (p.latitude - c.latitude) +
            (p.longitude - c.longitude) * (p.longitude - c.longitude);
        expect(d, closeTo((f.width * .1) * (f.width * .1), 1e-9));
      }
    });

    test('device online: server status, else last seen within 10 minutes', () {
      expect(deviceOnline(const TrkDevice(deviceId: 'd', status: 'Online'), _now), isTrue);
      expect(deviceOnline(TrkDevice(deviceId: 'd', status: 'Offline', lastSeenAt: _now), _now), isFalse);
      expect(
        deviceOnline(TrkDevice(deviceId: 'd', lastSeenAt: _now.subtract(const Duration(minutes: 9))), _now),
        isTrue,
      );
      expect(
        deviceOnline(TrkDevice(deviceId: 'd', lastSeenAt: _now.subtract(const Duration(minutes: 11))), _now),
        isFalse,
      );
      expect(deviceOnline(null, _now), isFalse);
      expect(TrkDevice.fromJson({'deviceId': 'd', 'status': 'Online'}).status, 'Online');
    });
  });

  group('location polling', () {
    test('starts on attach (10 s, days=1), stops on detach', () async {
      final backend = FakeAssetBackend()..seedTracking(DateTime.now);
      final timers = <_Timer>[];
      TrkLiveNotifier.debugSchedule = (d, cb) {
        final t = _Timer(d);
        timers.add(t);
        return t;
      };
      addTearDown(() => TrkLiveNotifier.debugSchedule = null);
      final c = ProviderContainer(overrides: backend.overrides);
      addTearDown(c.dispose);
      final n = c.read(liveLocProvider.notifier);
      final owner = Object();
      n.attach(owner);
      await pumpEventQueue();
      expect(c.read(liveLocProvider).polling, isTrue);
      expect(c.read(liveLocProvider).updates, hasLength(4));
      expect(timers.last.delay, const Duration(seconds: 10));
      expect(backend.calls('GET', '/api/asset-dashboard/location-updates').single.uri.queryParameters['days'], '1');
      n.detach(owner);
      expect(c.read(liveLocProvider).polling, isFalse);
      expect(timers.last.cancelled, isTrue);
    });
  });

  group('parsing', () {
    test('location-updates tags, gateways, battery percent', () {
      final u = TagLocationUpdate.parseList(locationUpdatesJson(_now));
      expect(u.map((t) => t.tagId), ['CHL-30002', 'PMP-10001', 'E2801170000002089FFF', 'TAG-LOST']);
      expect(u.first.lastToGatewayId, 'store-rdr-2');
      expect(u.first.lastFromGatewayId, 'GW-OLD');
      expect(u.first.count, 4);
      expect(u.first.lastToLatitude, isNull);
      expect(TrkGateway.parseList(trkGatewaysJson).single.label, 'Dubai Gate 1');
      expect(TrkDevice.fromJson({'deviceId': 'd', 'batteryPercent': 41}).batteryLevel, 41);
    });

    test('map mode needs real coordinates on a site, building or floor', () {
      expect(hasMapCoordinates(const LocationMasterData()), isFalse);
      expect(
        hasMapCoordinates(
          const LocationMasterData(
            sites: [MdSite(id: 's', name: 's', latitude: 0, longitude: 0)],
          ),
        ),
        isFalse,
      );
      expect(
        hasMapCoordinates(
          const LocationMasterData(
            floors: [MdSpace(id: 'f', name: 'f', latitude: 25.1, longitude: 55.2)],
          ),
        ),
        isTrue,
      );
    });
  });

  group('screens (API mode)', () {
    late FakeAssetBackend backend;
    late List<_Timer> timers;

    setUp(() {
      backend = FakeAssetBackend()..seedTracking(DateTime.now);
      timers = [];
      debugApiMode = true;
      NetworkNotifier.listenToDevice = false;
      TrkLiveNotifier.debugSchedule = (d, cb) {
        final t = _Timer(d);
        timers.add(t);
        return t;
      };
    });

    tearDown(() {
      debugApiMode = null;
      NetworkNotifier.listenToDevice = true;
      TrkLiveNotifier.debugSchedule = null;
    });

    /// No coordinates anywhere: Master Data, Dashboard asset-locations (the .29 server today).
    void noCoordinates({bool dashboard = false}) {
      if (!dashboard) {
        backend.on(
          'GET',
          '/api/dashboards/mobile/asset-locations',
          (_) => const FakeReply(200, dashLocationsEmptyJson),
        );
      }
      backend
        ..on(
          'GET',
          '/api/master-data/sites',
          (_) => const FakeReply(200, [
            {'id': 'site-1', 'siteName': 'Dubai Factory', 'code': 'DXB', 'latitude': null, 'longitude': null},
          ]),
        )
        ..on(
          'GET',
          '/api/master-data/buildings',
          (_) => const FakeReply(200, [
            {
              'id': 'bld-1',
              'name': 'Building A',
              'site': {'id': 'site-1', 'name': 'Dubai Factory'},
            },
          ]),
        );
    }

    Future<ProviderContainer> open(WidgetTester tester, String route) async {
      tester.view.physicalSize = const Size(1170, 2532);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            ...backend.overrides,
            sessionProvider.overrideWith(_SignedIn.new),
            tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
          ],
          child: const Asset360App(),
        ),
      );
      await tester.pump(SplashScreen.duration);
      await tester.pumpAndSettle();
      final c = ProviderScope.containerOf(tester.element(find.byType(Scaffold).first));
      c.read(routerProvider).go(route);
      await tester.pumpAndSettle();
      return c;
    }

    Finder page() => find.byWidgetPredicate((w) => w is Scrollable && w.axisDirection == AxisDirection.down).first;

    Future<void> see(WidgetTester tester, Finder f) async {
      for (var i = 0; i < 40 && f.evaluate().isEmpty; i++) {
        await tester.drag(page(), const Offset(0, -250));
        await tester.pump();
      }
      expect(f, findsWidgets);
      await tester.ensureVisible(f.first);
      await tester.pumpAndSettle();
    }

    testWidgets('no coordinates → the tree: device-plot placement, registered location, unplaced, 10 s polling', (
      tester,
    ) async {
      noCoordinates();
      final c = await open(tester, '/more/tracking?tab=map');
      expect(find.byType(OutdoorMap), findsNothing);
      expect(find.text('Map'), findsNothing, reason: 'no outdoor map without coordinates');
      expect(find.text('Floor plans'), findsOneWidget, reason: 'the floor navigation instead');
      expect(find.text('Dubai Factory · Site'), findsOneWidget);
      expect(c.read(liveLocProvider).polling, isTrue);
      expect(timers.where((t) => t.delay == LiveLocNotifier.interval && !t.cancelled), isNotEmpty);
      expect(LiveLocNotifier.interval, const Duration(seconds: 10));
      final q = backend.calls('GET', '/api/asset-dashboard/location-updates').last.uri.queryParameters;
      expect(q['days'], '1');

      // AR000001: its tag was last heard by the PLOTTED Store reader → Building A › Ground Floor ›
      // Restricted Store (KEY RULE; the gateways list does not know that reader).
      await see(tester, find.text('Building A · Building'));
      await tester.tap(find.text('Building A · Building'));
      await tester.pumpAndSettle();
      await see(tester, find.text('Ground Floor · Floor'));
      await tester.tap(find.text('Ground Floor · Floor'));
      await tester.pumpAndSettle();
      await see(tester, find.text('Restricted Store · Zone'));
      await tester.tap(find.text('Restricted Store · Zone'));
      await tester.pumpAndSettle();
      await see(tester, find.text('Chiller Unit 2'));
      expect(find.textContaining('Tag CHL-30002 · near Store reader · 2 mins ago'), findsOneWidget);
      // The Bay reader's unlinked tag sits in Zone B (Master Data zone of the map zone).
      expect(find.text('Zone B · Zone'), findsOneWidget);

      // AR000002 is heard by the gateway list's Dubai Gate 1 and registered in this site.
      await see(tester, find.text('First Floor · Floor'));
      await tester.tap(find.text('First Floor · Floor'));
      await tester.pumpAndSettle();
      await see(tester, find.text('Pump Room · Zone'));
      await tester.tap(find.text('Pump Room · Zone'));
      await tester.pumpAndSettle();
      await see(tester, find.text('Registered location'));
      expect(find.textContaining('Tag PMP-10001 · near Dubai Gate 1 · 30 mins ago'), findsOneWidget);

      // Unknown gateway → Unplaced.
      await see(tester, find.text('Unplaced (1)'));
      expect(find.textContaining('Unknown gateway GW-UNKNOWN-9'), findsWidgets);

      // Ground Floor has an Administration plan → Floor plan opens the live floor-plan view.
      final planButton = find.text('Floor plan');
      expect(planButton, findsOneWidget, reason: 'only Ground Floor (FP-0001) has a plan');
      await see(tester, planButton);
      await tester.tap(planButton);
      await tester.pumpAndSettle();
      expect(find.byType(FloorPlanMap), findsOneWidget);
      expect(find.bySemanticsLabel('Zone B, 1 asset'), findsOneWidget);

      // Leaving stops the 10 s polling.
      c.read(routerProvider).go('/more');
      await tester.pumpAndSettle();
      expect(c.read(liveLocProvider).polling, isFalse);
    });

    testWidgets('site counts: 3 assets, 2 online (2 / 3 min), 1 offline (30 min)', (tester) async {
      noCoordinates();
      final c = await open(tester, '/more/tracking?tab=zones');
      final site = c.read(locationTreeProvider('')).tree!.sites.single;
      final n = site.counts(DateTime.now().toUtc());
      expect((n.assets, n.online, n.offline), (3, 2, 1));
      expect(find.text('Dubai Factory · Site'), findsOneWidget, reason: 'Geofencing shows the same view');
    });

    testWidgets('floor picker: floors with a plan carry a map icon', (tester) async {
      noCoordinates();
      await open(tester, '/more/tracking?tab=map');
      await tester.tap(find.text('Floor plans'));
      await tester.pumpAndSettle();
      expect(find.byType(OutdoorMap), findsNothing);
      await tester.tap(find.text('Site').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Dubai Factory').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Building').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Building A').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Floor').first);
      await tester.pumpAndSettle();
      expect(find.byTooltip('Has a floor plan'), findsOneWidget, reason: 'Ground Floor only');
      await tester.tap(find.text('Ground Floor').last);
      await tester.pumpAndSettle();
      expect(find.byType(FloorPlanMap), findsOneWidget);
    });

    testWidgets('coordinates → the map by default with live pin counts; Tree / Map toggle', (tester) async {
      await open(tester, '/more/tracking?tab=map');
      expect(find.byType(OutdoorMap), findsOneWidget);
      expect(find.bySemanticsLabel('Site Dubai Factory, 3 tracked'), findsOneWidget, reason: 'live count');
      await tester.tap(find.text('Tree'));
      await tester.pumpAndSettle();
      expect(find.byType(OutdoorMap), findsNothing);
      expect(find.text('Dubai Factory · Site'), findsOneWidget);
      await tester.tap(find.text('Map'));
      await tester.pumpAndSettle();
      expect(find.byType(OutdoorMap), findsOneWidget);
    });

    testWidgets('outdoor map with ONLY Dashboard coordinates; toggle order Map, Floor plans, Tree', (tester) async {
      noCoordinates(dashboard: true); // Master Data has none; Dashboard asset-locations has AR000001
      await open(tester, '/more/tracking?tab=map');
      expect(find.byType(OutdoorMap), findsOneWidget, reason: 'no waiting for Master Data coordinates');
      final map = tester.getTopLeft(find.text('Map')).dx;
      final plans = tester.getTopLeft(find.text('Floor plans')).dx;
      final tree = tester.getTopLeft(find.text('Tree')).dx;
      expect(map < plans && plans < tree, isTrue);
      // The Home Asset Location pin; tap → card with Floor plan (AR000001 is on FP-0001).
      final pin = find.bySemanticsLabel('Asset Diesel Generator 500 kVA');
      expect(pin, findsOneWidget);
      expect(find.bySemanticsLabel(RegExp('^Site ')), findsNothing, reason: 'no site coordinates');
      // (The fit also covers the seeded geofences, which can put the pin under a map button.)
      tester.widget<DashAssetPin>(find.byType(DashAssetPin)).onTap!();
      await tester.pumpAndSettle();
      expect(find.text('Block A - Ground'), findsOneWidget);
      // Scroll the card above the bottom bar (drag in the page gutter, not on the map).
      await tester.dragFrom(const Offset(4, 600), const Offset(0, -400));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Floor plan'));
      await tester.pumpAndSettle();
      expect(find.byType(FloorPlanMap), findsOneWidget);
      expect(find.text('Now near Store reader in Restricted Store'), findsOneWidget, reason: 'its plot card');
    });
  });

  group('View on Map (one shared function)', () {
    late FakeAssetBackend backend;

    setUp(() {
      backend = FakeAssetBackend()..seedTracking(DateTime.now);
      debugApiMode = true;
      NetworkNotifier.listenToDevice = false;
      TrkLiveNotifier.debugSchedule = (d, cb) => _Timer(d);
      // Asset GET /api/asset-dashboard/locations: AR000077 has coordinates only here.
      backend.on(
        'GET',
        '/api/asset-dashboard/locations',
        (_) => const FakeReply(200, {
          'pins': [
            {
              'assetRegistrationId': 'AR000077',
              'assetTag': 'TAG-0077',
              'assetName': 'Forklift 77',
              'siteName': 'Dubai Factory',
              'latitude': 25.1,
              'longitude': 55.2,
              'status': 'Online',
              'lastZone': 'Yard',
            },
          ],
          'legend': {'Online': 1},
          'assetsWithoutCoordinates': 0,
          'topSites': <Object>[],
          'totalSites': 1,
        }),
      );
    });

    tearDown(() {
      debugApiMode = null;
      NetworkNotifier.listenToDevice = true;
      TrkLiveNotifier.debugSchedule = null;
    });

    Future<GoRouter> app(WidgetTester tester, String route) async {
      tester.view.physicalSize = const Size(1170, 2532);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            ...backend.overrides,
            sessionProvider.overrideWith(_SignedIn.new),
            tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
          ],
          child: const Asset360App(),
        ),
      );
      await tester.pump(SplashScreen.duration);
      await tester.pumpAndSettle();
      final c = ProviderScope.containerOf(tester.element(find.byType(Scaffold).first));
      final router = c.read(routerProvider);
      router.go(route);
      await tester.pumpAndSettle();
      return router;
    }

    /// Starts the shared action and pumps until it finished (its requests run on the test clock).
    Future<void> viewOnMap(WidgetTester tester, String id) async {
      var done = false;
      unawaited(openAssetOnMap(tester.element(find.byType(Navigator).first), id).whenComplete(() => done = true));
      for (var i = 0; i < 50 && !done; i++) {
        await tester.pump(const Duration(milliseconds: 50));
      }
      expect(done, isTrue, reason: 'View on Map resolved');
      await tester.pumpAndSettle();
    }

    testWidgets('on a floor plan → that plan, its plot selected, live zone highlighted', (tester) async {
      await app(tester, '/assets');
      await viewOnMap(tester, 'AR000001');
      // (A push into the More tab: read the opened hub's deep link.)
      final hub = tester.widget<LiveTrackingHubScreen>(find.byType(LiveTrackingHubScreen));
      expect(hub.view, 'floor');
      expect(hub.location, 'flr-1');
      expect(hub.zone, 'Z-2', reason: 'live zone via the tag (Store reader)');
      expect(hub.asset, 'AR000001');
      expect(hub.plot, 'asset:AR000001');
      expect(find.byType(FloorPlanMap), findsOneWidget);
      expect(tester.widget<FloorPlanMap>(find.byType(FloorPlanMap)).focus, isNotNull, reason: 'zoomed');
      expect(tester.widget<FloorPlanMap>(find.byType(FloorPlanMap)).highlightZones, contains('Z-2'));
      expect(find.text('Now near Store reader in Restricted Store'), findsOneWidget, reason: 'card open');
    });

    testWidgets('coordinates only → outdoor map centred on its marker, card open', (tester) async {
      await app(tester, '/assets');
      await viewOnMap(tester, 'AR000077');
      final hub = tester.widget<LiveTrackingHubScreen>(find.byType(LiveTrackingHubScreen));
      expect(hub.view, 'outdoor');
      expect(hub.asset, 'AR000077');
      expect(find.byType(OutdoorMap), findsOneWidget);
      expect(tester.widget<OutdoorMap>(find.byType(OutdoorMap)).focusAssetId, 'AR000077');
      expect(find.bySemanticsLabel('Asset Forklift 77'), findsOneWidget);
      expect(find.text('Yard'), findsOneWidget, reason: 'card open');
      expect(find.text('View Details'), findsOneWidget);
    });

    testWidgets('no map location → SnackBar', (tester) async {
      await app(tester, '/assets');
      await viewOnMap(tester, 'AR999999');
      expect(find.text('This asset has no map location yet'), findsOneWidget);
      expect(find.byType(LiveTrackingHubScreen), findsNothing);
    });

    testWidgets('asset detail grid: View on Map uses it', (tester) async {
      await app(tester, '/assets/detail/AR000001');
      final grid = find.text('View on Map');
      await tester.ensureVisible(grid.first);
      await tester.pumpAndSettle();
      await tester.tap(grid.first);
      for (var i = 0; i < 40 && find.byType(LiveTrackingHubScreen).evaluate().isEmpty; i++) {
        await tester.pump(const Duration(milliseconds: 50));
      }
      await tester.pumpAndSettle();
      expect(tester.widget<LiveTrackingHubScreen>(find.byType(LiveTrackingHubScreen)).location, 'flr-1');
      expect(find.byType(FloorPlanMap), findsOneWidget);
    });
  });
}
