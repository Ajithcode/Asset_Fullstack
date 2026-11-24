import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/licence/licence.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/core/demo_clock.dart';
import 'package:asset360_mobile/core/widgets/cards.dart' show SectionCard;
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/dashboard/network_status_live.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart' show PlanImage, planImageProvider;
import 'package:asset360_mobile/features/home/drilldowns/drilldown_page.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_view.dart' show RealTimeLocationView;
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'fake_asset_backend.dart';
import 'rtl_samples.dart';
import 'tracking_samples.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

/// A tags/live connection the test drives.
class _Socket implements LiveSocket {
  final _in = StreamController<Object?>();
  bool closed = false;

  @override
  Future<void> get ready => Future.value();

  @override
  Stream<Object?> get messages => _in.stream;

  void send(Map<String, Object?> frame) => _in.add(jsonEncode(frame));

  @override
  Future<void> close() async {
    closed = true;
    if (!_in.isClosed) await _in.close();
  }
}

/// [rtlOverviewJson] with the floor plan of each asset set ('' = none).
Map<String, Object?> _overviewOn(Map<String, String> plans) {
  final j = jsonDecode(jsonEncode(rtlOverviewJson)) as Map<String, dynamic>;
  for (final a in (j['assets'] as List).cast<Map<String, dynamic>>()) {
    final plan = plans[a['assetId']];
    if (plan == null) continue;
    final loc = a['location'] as Map<String, dynamic>? ?? (a['location'] = <String, dynamic>{'gatewayId': 'AC233FC0DEAD'});
    if (plan.isEmpty) {
      loc.remove('floorPlanId');
    } else {
      loc['floorPlanId'] = plan;
    }
  }
  return j;
}

void main() {
  late FakeAssetBackend backend;
  late List<_Socket> sockets;
  late ProviderContainer c;

  setUp(() {
    backend = FakeAssetBackend()
      ..seedTracking(DateTime.now)
      ..seedRtl();
    sockets = [];
    SharedPreferences.setMockInitialValues({});
    debugApiMode = true;
    DemoClock.frozen = true;
    DrilldownPage.refreshDelay = Duration.zero;
    RtlNotifier.debugConnector = (uri, token) {
      final s = _Socket();
      sockets.add(s);
      return s;
    };
  });

  tearDown(() {
    debugApiMode = false;
    debugLicensedModules = AppModule.values.toSet();
    RtlNotifier.debugConnector = (_, _) => _Idle();
  });

  Future<void> pumpApp(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    c = ProviderContainer(
      retry: (_, _) => null,
      overrides: [
        ...backend.overrides,
        sessionProvider.overrideWith(_SignedIn.new),
        tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
        planImageProvider.overrideWith((ref, url) async => PlanImage(Uint8List.fromList(base64Decode(rtlPng1x1)), 600, 400)),
      ],
    );
    await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const Asset360App()));
    await tester.pump(SplashScreen.duration);
    await tester.pumpAndSettle();
    c.read(routerProvider).go(Routes.home);
    await tester.pumpAndSettle();
  }

  Future<void> dispose(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox());
    await tester.pump();
    c.dispose();
  }

  Finder card() => find.byKey(const ValueKey('home-floor-card'));

  /// The point of marker [id] in the preview's animated layer, or null.
  LatLng? pointOf(WidgetTester t, String id) {
    for (final layer in t.widgetList<MarkerLayer>(find.descendant(of: card(), matching: find.byType(MarkerLayer)))) {
      for (final m in layer.markers) {
        if (m.key == ValueKey('rtl-mk-$id')) return m.point;
      }
    }
    return null;
  }

  Map<String, Object?> snapshot(List<Map<String, Object?>> tags) => {'type': 'snapshot', 'total': tags.length, 'tags': tags};

  // ------------------------------------------------------------------ the pure rule

  group('busiestFloorPlan', () {
    final plans = MapFloorPlan.parseList(rtlFloorPlansJson);
    RtlAsset on(String id, String? plan) =>
        RtlAsset(assetId: id, location: plan == null ? null : RtlLocation(gatewayId: 'g', floorPlanId: plan));

    test('the plan with the most located assets wins; assets without a floor plan do not count', () {
      final r = busiestFloorPlan([on('a', 'fp-002'), on('b', 'FP-001'), on('c', 'fp-001'), on('d', null)], plans, dev: true);
      expect(r, isNotNull);
      expect(r!.plan.floorPlanId, 'fp-001');
      expect(r.assets, 2);
    });

    test('only plans the map can draw count: not archived, an image file, Published (Draft only in dev builds)', () {
      final onlyDraft = [on('a', 'fp-002'), on('b', 'fp-002'), on('c', 'fp-001')];
      expect(busiestFloorPlan(onlyDraft, plans, dev: false)!.plan.floorPlanId, 'fp-001', reason: 'the Draft plan is skipped');
      expect(busiestFloorPlan(onlyDraft, plans, dev: true)!.plan.floorPlanId, 'fp-002');
      expect(busiestFloorPlan([on('a', 'unknown-plan')], plans, dev: true), isNull);
      expect(busiestFloorPlan(const [], plans, dev: true), isNull);
    });

    test('a tie goes to the Published plan', () {
      expect(busiestFloorPlan([on('a', 'fp-002'), on('b', 'fp-001')], plans, dev: true)!.plan.floorPlanId, 'fp-001');
    });
  });

  // ------------------------------------------------------------------ the Home card

  group('Home: Asset Location (Live) = indoor floor plan', () {
    testWidgets('shows the busiest floor with live markers in the zone grid; no outdoor map, no GPS call', (tester) async {
      await pumpApp(tester);
      expect(find.text('Asset Location (Live)'), findsOneWidget);
      expect(card(), findsOneWidget);
      expect(find.text('Ground Floor'), findsOneWidget, reason: 'the floor name');
      expect(find.byKey(const ValueKey('home-floor-counts')), findsOneWidget);
      expect(tester.widget<Text>(find.byKey(const ValueKey('home-floor-counts'))).data, '2 assets · 2 online');
      expect(find.byKey(const ValueKey('home-floor-live')), findsOneWidget, reason: 'the tags socket is connected: Live dot');
      // Zones, gateways and the assets of this floor, drawn by the Real-Time Location map widget in preview mode.
      expect(find.descendant(of: card(), matching: find.byType(FlutterMap)), findsOneWidget);
      expect(find.descendant(of: card(), matching: find.byKey(const ValueKey('rtl-gw-ac233fc0ffee'))), findsOneWidget);
      expect(find.descendant(of: card(), matching: find.byKey(const ValueKey('rtl-asset-AR-000123'))), findsOneWidget);
      expect(find.descendant(of: card(), matching: find.byKey(const ValueKey('rtl-asset-AR-000124'))), findsOneWidget);
      expect(find.descendant(of: card(), matching: find.byType(TileLayer)), findsNothing, reason: 'no OpenStreetMap tiles');
      expect(find.byType(TileLayer), findsNothing);
      // Never the GPS-only dashboard coordinates.
      expect(backend.calls('GET', '/api/dashboards/mobile/asset-locations'), isEmpty);
      expect(backend.calls('GET', '/api/real-time-location/overview'), isNotEmpty);
      // The preview is a picture: no zoom / layer buttons, no gestures.
      expect(find.descendant(of: card(), matching: find.byTooltip('Zoom in')), findsNothing);
      await dispose(tester);
    });

    testWidgets('uses the one shared tags socket: live frames move the markers; it stops when Home is hidden', (tester) async {
      await pumpApp(tester);
      expect(sockets, hasLength(1), reason: 'one socket for the floor');
      final before = pointOf(tester, 'AR-000123');
      expect(before, isNotNull);
      // The tag is now heard by the Store Room gateway: PUMP-01 moves to the zone grid of that zone.
      sockets.single.send(
        snapshot([rtlTagRow('C30000AABB01', 'ac233fc0beef', zoneId: 'zone-sr', zoneName: 'Store Room', timestamp: '2099-01-01T00:00:00Z')]),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 600));
      final mid = pointOf(tester, 'AR-000123');
      await tester.pump(const Duration(seconds: 2));
      final after = pointOf(tester, 'AR-000123');
      expect(after, isNot(before), reason: 'the marker went to the other zone');
      expect(mid, isNot(after), reason: 'and glided there (not a jump)');
      expect(sockets, hasLength(1));
      // Another tab: Home is hidden → the socket is closed.
      c.read(routerProvider).go(Routes.assets);
      await tester.pumpAndSettle(const Duration(seconds: 10));
      expect(c.read(rtlProvider).polling, isFalse);
      expect(c.read(rtlProvider.notifier).listeners, 0);
      expect(c.read(rtlProvider.notifier).socket, isNull, reason: 'the socket was stopped and released');
      expect(c.read(rtlProvider).socketUp, isFalse);
      c.read(routerProvider).go(Routes.home);
      await tester.pumpAndSettle();
      expect(sockets.length, 2, reason: 'back on Home: reconnects');
      await dispose(tester);
    });

    testWidgets('tap the card → Real-Time Location opens on that floor (View Map too)', (tester) async {
      // First Floor (fl-2) has the most located assets; Ground Floor is the default floor of the map.
      final plans = jsonDecode(jsonEncode(rtlFloorPlansJson)) as Map<String, dynamic>;
      (plans['items'] as List).add({
        'floorPlanId': 'fp-010',
        'locationId': 'fl-2',
        'name': 'First Floor plan',
        'format': 'png',
        'fileUrl': '/uploads/maps/fp-010.png',
        'status': 'Published',
        'updatedAt': '2026-11-18T08:00:00Z',
      });
      backend.on('GET', '/api/administration/maps/floor-plans', (_) => FakeReply(200, plans));
      backend.on(
        'GET',
        '/api/real-time-location/overview',
        (_) => FakeReply(200, _overviewOn({'AR-000123': 'fp-010', 'AR-000124': 'fp-010', 'AR-000125': 'fp-001'})),
      );
      await pumpApp(tester);
      expect(find.text('First Floor'), findsWidgets);
      expect(find.text('Ground Floor'), findsNothing);
      await tester.ensureVisible(card());
      await tester.pumpAndSettle();
      await tester.tap(card());
      await tester.pumpAndSettle();
      expect(c.read(routerProvider).routerDelegate.currentConfiguration.last.matchedLocation, Routes.tracking);
      expect(find.byKey(const ValueKey('rtl-live')), findsOneWidget, reason: 'Real-Time Location');
      expect(tester.widget<RealTimeLocationView>(find.byType(RealTimeLocationView)).focusFloorPlan, 'fp-010');
      expect(c.read(rtlProvider).floorPlanId, 'fp-010', reason: 'on the First Floor, not the default Ground Floor');
      await dispose(tester);
    });

    testWidgets('View Map opens Real-Time Location on the busiest floor', (tester) async {
      await pumpApp(tester);
      final viewMap = find.descendant(
        of: find.ancestor(of: find.text('Asset Location (Live)'), matching: find.byType(SectionCard)),
        matching: find.text('View Map'),
      );
      await tester.ensureVisible(viewMap);
      await tester.pumpAndSettle();
      await tester.tap(viewMap);
      await tester.pumpAndSettle();
      expect(tester.widget<RealTimeLocationView>(find.byType(RealTimeLocationView)).focusFloorPlan, 'fp-001');
      expect(find.byKey(const ValueKey('rtl-live')), findsOneWidget);
      await dispose(tester);
    });

    testWidgets('nothing located → "No assets on the floor plans yet"', (tester) async {
      backend.on('GET', '/api/real-time-location/overview', (_) => const FakeReply(200, rtlEmptyOverviewJson));
      await pumpApp(tester);
      expect(find.text('No assets on the floor plans yet'), findsOneWidget);
      expect(card(), findsNothing);
      expect(find.byType(TileLayer), findsNothing);
      await dispose(tester);
    });

    testWidgets('assets without any drawable floor plan → the same empty message', (tester) async {
      backend.on('GET', '/api/administration/maps/floor-plans', (_) => const FakeReply(200, {'items': <Object>[]}));
      await pumpApp(tester);
      expect(find.text('No assets on the floor plans yet'), findsOneWidget);
      await dispose(tester);
    });

    testWidgets('Tracking not licensed: no Tracking call, a tap shows the licence sheet', (tester) async {
      debugLicensedModules = AppModule.values.where((m) => m != AppModule.tracking).toSet();
      await pumpApp(tester);
      expect(find.byKey(const ValueKey('home-floor-locked')), findsOneWidget);
      expect(card(), findsNothing);
      expect(backend.requests.where((o) => o.uri.path.startsWith('/api/real-time-location')), isEmpty);
      expect(sockets, isEmpty);
      await tester.ensureVisible(find.byKey(const ValueKey('home-floor-locked')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('home-floor-locked')));
      await tester.pumpAndSettle();
      expect(find.byKey(ValueKey('licence-sheet-${AppModule.tracking.key}')), findsOneWidget);
      expect(backend.requests.where((o) => o.uri.path.startsWith('/api/real-time-location')), isEmpty);
      await dispose(tester);
    });
  });

  // ------------------------------------------------------------------ the drill-down

  group('Home drill-down: Asset Location', () {
    testWidgets('shows the same indoor floor plan instead of the outdoor map; the button opens Real-Time Location', (tester) async {
      await pumpApp(tester);
      c.read(routerProvider).go(Routes.assetLocation);
      await tester.pumpAndSettle();
      expect(card(), findsOneWidget);
      expect(find.text('Ground Floor'), findsOneWidget);
      expect(find.byType(TileLayer), findsNothing);
      expect(backend.calls('GET', '/api/dashboards/mobile/asset-locations'), isEmpty);
      await tester.ensureVisible(find.byKey(const ValueKey('home-floor-open')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('home-floor-open')));
      await tester.pumpAndSettle();
      expect(tester.widget<RealTimeLocationView>(find.byType(RealTimeLocationView)).focusFloorPlan, 'fp-001');
      await dispose(tester);
    });
  });
}

class _Idle implements LiveSocket {
  final _m = StreamController<Object?>();

  @override
  Future<void> get ready => Future.value();

  @override
  Stream<Object?> get messages => _m.stream;

  @override
  Future<void> close() => _m.close();
}
