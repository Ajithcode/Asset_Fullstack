import 'dart:convert';
import 'dart:typed_data';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/auth_models.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart' show PlanImage, planImageProvider;
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_map.dart' show rtlLocateColor;
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';

import 'fake_asset_backend.dart';
import 'rtl_samples.dart';
import 'tracking_samples.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

class _AuthIn extends AuthNotifier {
  @override
  AuthState build() => AuthSignedIn(
    user: const CurrentUser(fullName: 'Demo User', clientId: 'CLT-1001', tenantId: 'TENANT-DEFAULT'),
    tokens: TokenPair(accessToken: 'tok-1', refreshToken: 'r-1', expiresAt: DateTime.now().add(const Duration(hours: 1))),
    rememberMe: false,
  );
}

/// [rtlOverviewJson] with [edit] applied to the asset [id].
Map<String, Object?> _overview(String id, void Function(Map<String, dynamic> asset) edit) {
  final j = jsonDecode(jsonEncode(rtlOverviewJson)) as Map<String, dynamic>;
  edit((j['assets'] as List).cast<Map<String, dynamic>>().firstWhere((a) => a['assetId'] == id));
  return j;
}

void main() {
  late FakeAssetBackend backend;
  final frame = PlanFrame.forAspect(1.5); // the test plan picture is 600 × 400
  final zones = MapZone.parseList(rtlZonesJson);
  final hallA = zones.firstWhere((z) => z.zoneId == 'zone-a1');

  setUp(() {
    backend = FakeAssetBackend()
      ..seedTracking(DateTime.now)
      ..seedRtl();
    debugApiMode = true;
  });

  tearDown(() => debugApiMode = false);

  Future<ProviderContainer> open(WidgetTester tester, [String route = Routes.tracking]) async {
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          ...backend.overrides,
          sessionProvider.overrideWith(_SignedIn.new),
          authProvider.overrideWith(_AuthIn.new),
          tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
          planImageProvider.overrideWith((ref, url) async => PlanImage(Uint8List.fromList(base64Decode(rtlPng1x1)), 600, 400)),
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

  MapCamera camera(WidgetTester t) => MapCamera.of(t.element(find.byType(MarkerLayer).first));

  Finder asset(String id) => find.byKey(ValueKey('rtl-asset-$id'));
  Finder pulse([Finder? within]) => within == null
      ? find.byKey(const ValueKey('rtl-pulse'))
      : find.descendant(of: within, matching: find.byKey(const ValueKey('rtl-pulse')));

  Color outlineOf(WidgetTester t, String id) {
    final box = t.widget<AnimatedContainer>(find.descendant(of: asset(id), matching: find.byType(AnimatedContainer)));
    return ((box.decoration! as BoxDecoration).border! as Border).top.color;
  }

  /// Pumps in small steps until [f] is on screen (a pulse lasts 5 s; pumpAndSettle would run it to the end).
  Future<void> pumpUntil(WidgetTester t, Finder f) async {
    for (var i = 0; i < 60 && f.evaluate().isEmpty; i++) {
      await t.pump(const Duration(milliseconds: 50));
    }
  }

  Future<void> locate(WidgetTester t, String id) async {
    await t.ensureVisible(find.byKey(const ValueKey('rtl-locate')));
    await t.pumpAndSettle();
    await t.tap(find.byKey(const ValueKey('rtl-locate')));
    await t.pumpAndSettle();
    await t.tap(find.byKey(ValueKey('locate-target-$id')));
  }

  // ------------------------------------------------------------------ the reasons (pure)

  group('"No location" reasons (the web\'s noLocationReason)', () {
    RtlAsset a({List<String> tags = const ['T1'], String? tracker, RtlLocation? location, String freshness = 'Live'}) =>
        RtlAsset(assetId: 'x', tags: tags, tracker: tracker, location: location, freshness: freshness);

    test('no tag linked / tag never heard / gateway not plotted', () {
      expect(rtlNoLocationReason(a(tags: const [])), 'no tag linked');
      expect(rtlNoLocationReason(a(tags: const [], tracker: ' ')), 'no tag linked');
      expect(rtlNoLocationReason(a(tags: const [], tracker: 'T9', location: const RtlLocation(gatewayId: 'g', zoneId: 'z', floorPlanId: 'p'))), isNull);
      expect(rtlNoLocationReason(a()), 'tag not heard by any gateway yet');
      expect(rtlNoLocationReason(a(freshness: 'Offline')), 'tag offline, not heard by any gateway');
      expect(rtlNoLocationReason(a(location: const RtlLocation(gatewayId: 'GW-9'))), 'gateway GW-9 is not plotted on a floor plan');
      expect(rtlNoLocationReason(a(location: const RtlLocation(gatewayId: 'GW-9', floorPlanId: 'p'))), isNull, reason: 'no zone: drawn at its gateway');
      expect(rtlNoLocationReason(a(location: const RtlLocation(gatewayId: 'g', zoneId: 'z', floorPlanId: 'p'))), isNull);
      expect(rtlNoLocationText(a(tags: const [])), 'No location · no tag linked');
      expect(rtlUnplacedReason(a(location: const RtlLocation(gatewayId: 'GW-1'))), 'gateway GW-1 is not plotted on this floor plan');
      expect(rtlNoFloorPlanReason, 'its floor has no floor plan');
    });
  });

  // ------------------------------------------------------------------ Locate

  group('Locate (target icon)', () {
    testWidgets('fits the ZONE and pulses the ASSET marker red, then keeps a steady red outline; the gateway is not highlighted', (tester) async {
      await open(tester);
      final before = camera(tester);
      expect(outlineOf(tester, 'AR-000123'), isNot(rtlLocateColor));
      await locate(tester, 'AR-000123');
      await pumpUntil(tester, pulse());
      // The red pulse is on the asset's marker, nowhere else (not on a gateway, not on the other asset).
      expect(pulse(), findsOneWidget);
      expect(pulse(asset('AR-000123')), findsOneWidget);
      expect(pulse(find.byKey(const ValueKey('rtl-gw-ac233fc0ffee'))), findsNothing, reason: 'not the gateway');
      expect(pulse(asset('AR-000124')), findsNothing);
      // The camera fits Hall A (its shape with padding), not a fixed zoom on the gateway.
      await tester.pump(const Duration(seconds: 1));
      final cam = camera(tester);
      final g = zoneGeom(hallA, frame);
      expect(cam.visibleBounds.contains(LatLng(g.south, g.west)), isTrue);
      expect(cam.visibleBounds.contains(LatLng(g.north, g.east)), isTrue);
      final fill = (g.east - g.west) / (cam.visibleBounds.east - cam.visibleBounds.west);
      expect(fill, inInclusiveRange(.35, .95), reason: 'the zone fills the view with padding round it');
      expect(cam.zoom, greaterThan(before.zoom));
      expect(cam.zoom, lessThan(2.9), reason: 'not the fixed gateway zoom 3');
      // About 5 s later the ring is gone; the steady red outline stays while the asset is selected.
      await tester.pump(const Duration(seconds: 6));
      expect(pulse(), findsNothing);
      expect(outlineOf(tester, 'AR-000123'), rtlLocateColor);
      expect(find.byKey(const ValueKey('rtl-callout')), findsOneWidget);
      await tester.pumpAndSettle();
    });

    testWidgets('the pulse lasts about 5 s', (tester) async {
      await open(tester);
      await locate(tester, 'AR-000123');
      await pumpUntil(tester, pulse());
      expect(pulse(), findsOneWidget);
      await tester.pump(const Duration(seconds: 3));
      expect(pulse(), findsOneWidget, reason: 'still pulsing 3 s later');
      await tester.pump(const Duration(seconds: 3));
      expect(pulse(), findsNothing, reason: 'over after ~5 s');
      await tester.pumpAndSettle();
    });

    testWidgets('the asset card\'s Show on map: an asset with no zone is zoomed at its marker and pulsed', (tester) async {
      // FORK-02 has no zone in the overview: it is drawn at its gateway's plot (GW-02 at 75 / 60).
      await open(tester);
      await tester.ensureVisible(asset('AR-000124'));
      await tester.pumpAndSettle();
      await tester.tap(asset('AR-000124'));
      await tester.pumpAndSettle();
      final sheet = find.descendant(of: find.byType(BottomSheet).last, matching: find.byType(Scrollable)).first;
      await tester.scrollUntilVisible(find.text('Show on map'), 200, scrollable: sheet);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Show on map'));
      await pumpUntil(tester, pulse());
      expect(pulse(asset('AR-000124')), findsOneWidget);
      expect(pulse(find.byKey(const ValueKey('rtl-gw-gw-02'))), findsNothing, reason: 'the asset, not the gateway');
      await tester.pump(const Duration(seconds: 1));
      final cam = camera(tester);
      final marker = frame.toLatLng(75, 60);
      expect(cam.zoom, closeTo(3, .01));
      expect(cam.center.latitude, closeTo(marker.latitude, 1e-6));
      expect(cam.center.longitude, closeTo(marker.longitude, 1e-6));
      await tester.pumpAndSettle();
      expect(outlineOf(tester, 'AR-000124'), rtlLocateColor);
    });

    testWidgets('View on Map (deep link) fits the zone and pulses the asset too', (tester) async {
      await open(tester, Routes.trackingMap(view: 'rtl', asset: 'AR-000123'));
      // The deep link runs in initState: by now the pulse may be over, the outline is not.
      expect(outlineOf(tester, 'AR-000123'), rtlLocateColor);
      final cam = camera(tester);
      final g = zoneGeom(hallA, frame);
      expect(cam.visibleBounds.contains(LatLng(g.south, g.west)), isTrue);
      expect(cam.visibleBounds.contains(LatLng(g.north, g.east)), isTrue);
    });

    testWidgets('no zone of this plan → zoom to the asset\'s marker (its gateway plot) and pulse it', (tester) async {
      // Its zone is not one of this plan's zones; its gateway is plotted here.
      backend.on('GET', '/api/real-time-location/overview', (_) => FakeReply(200, _overview('AR-000123', (a) => (a['location'] as Map)['zoneId'] = 'zone-elsewhere')));
      await open(tester);
      await locate(tester, 'AR-000123');
      await pumpUntil(tester, pulse());
      expect(pulse(asset('AR-000123')), findsOneWidget);
      await tester.pump(const Duration(seconds: 1));
      final cam = camera(tester);
      final marker = frame.toLatLng(22.5, 30);
      expect(cam.zoom, closeTo(3, .01), reason: 'its marker, not a zone');
      expect(cam.center.latitude, closeTo(marker.latitude, 1e-6));
      expect(cam.center.longitude, closeTo(marker.longitude, 1e-6));
      await tester.pumpAndSettle();
    });
  });

  // ------------------------------------------------------------------ no location

  group('No location: the map stays, a message says why', () {
    Future<void> expectNoLocation(WidgetTester tester, String id, String why) async {
      final before = camera(tester);
      await locate(tester, id);
      await pumpUntil(tester, find.textContaining('No location ('));
      expect(find.textContaining('No location ($why'), findsOneWidget);
      expect(pulse(), findsNothing);
      final after = camera(tester);
      expect((after.zoom, after.center), (before.zoom, before.center), reason: 'nothing moved');
      await tester.pumpAndSettle();
    }

    testWidgets('no tag linked (also shown in the Locate list)', (tester) async {
      await open(tester);
      await tester.tap(find.byKey(const ValueKey('rtl-locate')));
      await tester.pumpAndSettle();
      expect(tester.widget<Text>(find.byKey(const ValueKey('locate-sub-AR-000125'))).data, 'No location · no tag linked');
      expect(tester.widget<Text>(find.byKey(const ValueKey('locate-sub-AR-000123'))).data, isNot(startsWith('No location')));
      await tester.tap(find.byKey(const ValueKey('locate-target-AR-000125')));
      await pumpUntil(tester, find.textContaining('No location ('));
      expect(find.textContaining('No location (no tag linked)'), findsOneWidget);
      expect(pulse(), findsNothing);
      await tester.pumpAndSettle();
    });

    testWidgets('tag never heard', (tester) async {
      backend.on('GET', '/api/real-time-location/overview', (_) => FakeReply(200, _overview('AR-000123', (a) => a['location'] = null)));
      await open(tester);
      await expectNoLocation(tester, 'AR-000123', 'tag not heard by any gateway yet');
    });

    testWidgets('gateway not plotted', (tester) async {
      backend.on(
        'GET',
        '/api/real-time-location/overview',
        (_) => FakeReply(
          200,
          _overview('AR-000123', (a) => a['location'] = {'gatewayId': 'AC233FC0DEAD', 'method': 'Estimated'}),
        ),
      );
      await open(tester);
      await expectNoLocation(tester, 'AR-000123', 'gateway AC233FC0DEAD is not plotted on a floor plan');
    });

    testWidgets('its floor has no floor plan', (tester) async {
      backend.on('GET', '/api/real-time-location/overview', (_) => FakeReply(200, _overview('AR-000123', (a) => (a['location'] as Map)['floorPlanId'] = 'fp-404')));
      await open(tester);
      await expectNoLocation(tester, 'AR-000123', 'its floor has no floor plan');
    });

    testWidgets('located on another plan but not drawn on this floor: the list says the gateway is not plotted here', (tester) async {
      // Its zone and gateway are on this floor plan but neither is drawn: the zones and plots lists are empty.
      backend.on('GET', '/api/administration/maps/floor-plans/fp-001/zones', (_) => const FakeReply(200, <Object>[]));
      backend.on('GET', '/api/administration/maps/floor-plans/fp-001/plots', (_) => const FakeReply(200, <Object>[]));
      await open(tester);
      await tester.ensureVisible(find.byKey(const ValueKey('rtl-not-on-plan')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('rtl-not-on-plan')));
      await tester.pumpAndSettle();
      expect(find.textContaining('No location · gateway AC233FC0FFEE is not plotted on this floor plan'), findsOneWidget);
      await locate(tester, 'AR-000123');
      await pumpUntil(tester, find.textContaining('No location ('));
      expect(find.textContaining('No location (gateway AC233FC0FFEE is not plotted on this floor plan)'), findsOneWidget);
      expect(pulse(), findsNothing);
      await tester.pumpAndSettle();
    });
  });
}
