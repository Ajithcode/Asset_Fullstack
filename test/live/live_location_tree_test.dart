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
import 'package:asset360_mobile/features/more/tracking/live/outdoor_map.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_asset_backend.dart';
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

  group('parsing', () {
    test('location-updates tags, gateways, battery percent', () {
      final u = TagLocationUpdate.parseList(locationUpdatesJson(_now));
      expect(u.map((t) => t.tagId), ['CHL-30002', 'PMP-10001', 'TAG-LOST']);
      expect(u.first.lastToGatewayId, 'GW-DXB-01');
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

    /// Master Data without any coordinates (the .29 server today).
    void noCoordinates() {
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

    testWidgets('no coordinates → the tree: sites, registered building / floor / zone, unplaced, 10 s polling', (
      tester,
    ) async {
      noCoordinates();
      final c = await open(tester, '/more/tracking?tab=map');
      expect(find.byType(OutdoorMap), findsNothing);
      expect(find.text('Map'), findsNothing, reason: 'no toggle without coordinates');
      expect(find.text('Dubai Factory · Site'), findsOneWidget);
      expect(c.read(liveLocProvider).polling, isTrue);
      expect(timers.where((t) => t.delay == LiveLocNotifier.interval && !t.cancelled), isNotEmpty);
      expect(LiveLocNotifier.interval, const Duration(seconds: 10));

      // AR000001 (registered elsewhere) sits at site level, online (2 min ago), near the gateway.
      await see(tester, find.text('Chiller Unit 2'));
      expect(find.textContaining('Tag CHL-30002 · near Dubai Gate 1 · 2 mins ago'), findsOneWidget);
      // AR000002 is registered in this site → Building A › First Floor › Pump Room.
      await see(tester, find.text('Building A · Building'));
      await tester.tap(find.text('Building A · Building'));
      await tester.pumpAndSettle();
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

      // Leaving stops the 10 s polling.
      c.read(routerProvider).go('/more');
      await tester.pumpAndSettle();
      expect(c.read(liveLocProvider).polling, isFalse);
    });

    testWidgets('site counts: 2 assets, 1 online (2 min), 1 offline (30 min)', (tester) async {
      noCoordinates();
      final c = await open(tester, '/more/tracking?tab=zones');
      final site = c.read(locationTreeProvider('')).tree!.sites.single;
      final n = site.counts(DateTime.now().toUtc());
      expect((n.assets, n.online, n.offline), (2, 1, 1));
      expect(find.text('Dubai Factory · Site'), findsOneWidget, reason: 'Geofencing shows the same view');
    });

    testWidgets('coordinates → the map by default with live pin counts; Tree / Map toggle', (tester) async {
      await open(tester, '/more/tracking?tab=map');
      expect(find.byType(OutdoorMap), findsOneWidget);
      expect(find.bySemanticsLabel('Site Dubai Factory, 2 tracked'), findsOneWidget, reason: 'live count');
      await tester.tap(find.text('Tree'));
      await tester.pumpAndSettle();
      expect(find.byType(OutdoorMap), findsNothing);
      expect(find.text('Dubai Factory · Site'), findsOneWidget);
      await tester.tap(find.text('Map'));
      await tester.pumpAndSettle();
      expect(find.byType(OutdoorMap), findsOneWidget);
    });
  });
}
