import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/network_repository.dart';
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/outdoor_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';

import 'fake_asset_backend.dart';
import 'tracking_samples.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

/// A timer the test fires by hand.
class _FakeTimer implements Timer {
  _FakeTimer(this.delay, this.callback);

  final Duration delay;
  final void Function() callback;
  bool cancelled = false;

  @override
  void cancel() => cancelled = true;

  @override
  bool get isActive => !cancelled;

  @override
  int get tick => 0;
}

final _now = DateTime.utc(2026, 11, 11, 10);

TrackingSnapshot _snapshot([DateTime? at]) {
  final now = at ?? _now;
  return TrackingSnapshot(
    devices: TrkDevice.parseList(trkDevicesJson(now)),
    health: TrkDeviceHealth.parseList(trkHealthJson(now)),
    tags: TrkTag.parseList(trkTagsJson(now)),
    sensors: TrkSensor.parseList(trkSensorsJson(now)),
    events: TrkLocationEvent.parseList(trkEventsJson(now)),
    fetchedAt: now,
  );
}

void main() {
  // ------------------------------------------------------------------ parsing

  group('parsing contract-shaped JSON', () {
    test('location events: zone NAME only, UTC times, newest first', () {
      final e = TrkLocationEvent.parseList(trkEventsJson(_now));
      expect(e, hasLength(4));
      expect(e.first.eventId, 'LE000004');
      expect(e.first.assetId, 'CHL-30002');
      expect(e.first.zoneName, 'Zone B');
      expect(e.first.eventTime, _now.subtract(const Duration(minutes: 5)));
      expect(e.first.eventTime.isUtc, isTrue);
      expect(e[1].placesAssetInZone, isFalse, reason: 'Departed Zone');
      expect(e[1].details, contains('dwell'));
      // A row without asset or time is skipped, not invented.
      expect(
        TrkLocationEvent.parseList([
          {'eventId': 'X'},
        ]),
        isEmpty,
      );
    });

    test('a timestamp without an offset is UTC', () {
      expect(jUtc('2026-11-11T08:00:00'), DateTime.utc(2026, 11, 11, 8));
      expect(jUtc('2026-11-11T08:00:00Z'), DateTime.utc(2026, 11, 11, 8));
      expect(jUtc('2026-11-11T12:00:00+04:00'), DateTime.utc(2026, 11, 11, 8));
      expect(jUtc(null), isNull);
    });

    test('devices, health, tags and sensors', () {
      final d = TrkDevice.parseList(trkDevicesJson(_now));
      expect(d.map((x) => x.deviceId), ['TS-80214', 'GW-DXB-01', 'BT-1001']);
      expect(d.first.assetRegistrationId, 'AR000001');
      expect(d.first.assetCode, 'CHL-30002');
      expect(d.first.batteryLevel, 78);
      expect(d[1].isGateway, isTrue);
      final h = TrkDeviceHealth.parseList(trkHealthJson(_now)).single;
      expect(h.signalStrengthDbm, -62);
      expect(h.snrDb, 7.5);
      expect(h.signalQuality, 'Good');
      final t = TrkTag.parseList(trkTagsJson(_now)).single;
      expect(t.technology, 'BLE');
      expect(t.locationName, 'Warehouse A');
      final s = TrkSensor.parseList(trkSensorsJson(_now));
      expect(s.first.sensorId, 'SEN-T-80900');
      expect(s.first.unit, '°C');
      expect(s.first.liveReading, 42.1);
      expect(s.first.highAlarm, 40);
      expect(s.first.outOfRange, isTrue);
      expect(s.first.calibrationDueDate, DateTime.utc(2027, 1, 31));
      expect(s[1].liveReading, isNull);
      expect(readingText(s[1].liveReading, s[1].unit), 'No data');
    });

    test('telemetry readings: trimmed to the range, oldest first', () {
      final rows = TrkReading.parseList(trkReadingsJson(_now));
      expect(rows, hasLength(18));
      expect(rows.first.recordedAt, _now, reason: 'newest first from the server');
      final hour = readingsIn(rows, TrackRange.hour, _now);
      expect(hour.map((r) => r.recordedAt.isBefore(hour.last.recordedAt) || r == hour.last), everyElement(isTrue));
      expect(hour, hasLength(7), reason: '0, 10 … 60 minutes ago');
      expect(hour.first.recordedAt, _now.subtract(const Duration(hours: 1)));
      expect(readingsIn(rows, TrackRange.day, _now), hasLength(18));
      // Whole UTC days on the server: one extra day is asked.
      expect(TrackRange.hour.serverDays, 2);
      expect(TrackRange.day.serverDays, 2);
      expect(TrackRange.week.serverDays, 8);
    });

    test('GPS geofences: polygon points, circle centre + radius, rectangle from 2 corners', () {
      final g = TrkGeofence.parseList(trkGeofencesJson(_now));
      expect(g.first.coordinates, hasLength(4));
      expect(g.first.monitorEntry && g.first.monitorExit, isTrue);
      expect(g.first.dwellAlert, isTrue);
      expect(g[1].isCircle, isTrue);
      expect(g[1].radiusMeters, 80);
      expect(g[1].isActive, isFalse);
      final rect = TrkGeofence.fromJson({
        'id': 'r',
        'name': 'R',
        'shape': 'Rectangle',
        'coordinates': [
          {'latitude': 1.0, 'longitude': 2.0},
          {'latitude': 3.0, 'longitude': 4.0},
        ],
      });
      expect(geofenceOutline(rect), [const LatLng(1, 2), const LatLng(1, 4), const LatLng(3, 4), const LatLng(3, 2)]);
    });

    test('IoT alerts, alert count and the events timeline', () {
      final a = TrkAlert.parseList(trkAlertsJson(_now));
      expect(a.first.alertId, 'IA000002');
      expect(a.first.isOpen, isTrue);
      expect(a[1].isOpen, isFalse);
      final c = TrkAlertCount.fromJson(trkAlertCountJson);
      expect(c.total, 2);
      expect(c.unresolved, 1);
      expect(c.changePercent, -50);
      expect(c.bySeverity['Critical'], 1);
      final t = TrkTimeline.fromJson(trkTimelineJson);
      expect(t.totals.telemetryData, 120);
      expect(t.points.last.locationUpdates, 5);
    });

    test('floor plans, zones, plots and layer defaults', () {
      final p = MapFloorPlan.parseList(mapFloorPlansJson).single;
      expect(p.locationId, 'flr-1');
      expect(p.isPublished, isTrue);
      expect(p.isImage, isFalse, reason: 'PDF');
      expect(p.widthMeters, 120);
      expect(MapFloorPlan.fromJson({'floorPlanId': 'x', 'format': 'PNG', 'fileUrl': '/a.png'}).isImage, isTrue);
      final z = MapZone.parseList(mapZonesJson);
      expect(z.map((x) => x.shape), ['Rectangle', 'Polygon', 'Circle']);
      expect(z[1].points, hasLength(3));
      expect(z[1].geofence && z[1].entryAlert && z[1].dwellAlert, isTrue);
      expect(z[1].dwellMinutes, 15);
      expect(z[2].radius, 5);
      final plots = MapPlot.parseList(mapPlotsJson);
      expect(plots.first.isAsset, isTrue);
      expect(plots.first.itemId, 'AR000001');
      expect(plots[1].isDevice, isTrue);
      final layers = MapLayerToggles.fromServer(MapLayer.parseList(mapLayersJson));
      expect(layers, const MapLayerToggles(devices: false));
      expect(MapLayerToggles.fromServer(const []), const MapLayerToggles());
    });
  });

  // ------------------------------------------------------------------ % → map

  group('plan percent → map coordinates', () {
    test('landscape plan: corners, centre and the inverse', () {
      final f = PlanFrame.forAspect(2);
      expect(f.width, PlanFrame.span);
      expect(f.height, PlanFrame.span / 2);
      expect(f.toLatLng(0, 0), LatLng(f.height, 0), reason: 'top-left');
      expect(f.toLatLng(100, 100), LatLng(0, f.width), reason: 'bottom-right');
      expect(f.toLatLng(50, 50), LatLng(f.height / 2, f.width / 2));
      final back = f.toPercent(f.toLatLng(25, 75));
      expect(back.x, closeTo(25, 1e-9));
      expect(back.y, closeTo(75, 1e-9));
      // Outside 0–100 is clamped onto the plan.
      expect(f.toLatLng(-10, 130), const LatLng(0, 0));
    });

    test('portrait plan keeps the aspect, latitude stays valid', () {
      final f = PlanFrame.forAspect(.5);
      expect(f.height, PlanFrame.span);
      expect(f.width, PlanFrame.span / 2);
      expect(f.topLeft.latitude, lessThanOrEqualTo(90));
    });

    test('plan size: the image\'s real pixels first (zones are % of the image), else metres', () {
      final p = MapFloorPlan.parseList(mapFloorPlansJson).single;
      expect(frameFor(p, null).aspect, closeTo(2, 1e-9), reason: '120 x 60 m, no image');
      expect(frameFor(p, PlanImage(Uint8List(0), 1600, 1200)).aspect, closeTo(4 / 3, 1e-9));
      const noSize = MapFloorPlan(floorPlanId: 'x', locationId: 'y');
      expect(frameFor(noSize, PlanImage(Uint8List(0), 300, 100)).aspect, closeTo(3, 1e-9));
      expect(frameFor(noSize, null).aspect, 1);
    });

    test('zone outlines: rectangle corners, polygon points, round circle', () {
      final z = MapZone.parseList(mapZonesJson);
      expect(z[0].outline(), [(x: 10.0, y: 20.0), (x: 40.0, y: 20.0), (x: 40.0, y: 60.0), (x: 10.0, y: 60.0)]);
      expect(z[1].outline(), z[1].points);
      final f = PlanFrame.forAspect(2);
      final c = f.point(z[2].center);
      expect(z[2].center, (x: 70.0, y: 50.0), reason: 'zero-size box: x / y is the centre');
      // Radius = 5 % of the plan WIDTH, equal in every direction in map units.
      for (final p in f.zoneOutline(z[2])) {
        final d =
            (p.latitude - c.latitude) * (p.latitude - c.latitude) +
            (p.longitude - c.longitude) * (p.longitude - c.longitude);
        expect(d, closeTo((f.width * .05) * (f.width * .05), 1e-6));
      }
    });
  });

  // ------------------------------------------------------------------ joining

  group('tracked assets', () {
    test('one row per asset; events join by asset code; gateways and old events left out', () {
      final s = _snapshot();
      expect(s.assets.map((a) => a.key), unorderedEquals(['AR000001', 'AR000002', 'FL-008']));
      expect(s.events.map((e) => e.eventId), isNot(contains('LE000001')), reason: 'older than 7 days');
      final chiller = s.byKey('AR000001')!;
      expect(chiller.assetRegistrationId, 'AR000001');
      expect(chiller.code, 'CHL-30002');
      expect(chiller.zone?.zoneName, 'Zone B');
      expect(chiller.lastSeen, _now.subtract(const Duration(minutes: 1)), reason: 'health row');
      expect(chiller.battery, 76, reason: 'most recently seen battery');
      expect(chiller.signal?.signalStrengthDbm, -62);
      expect(chiller.sensors.map((x) => x.sensorId), ['SEN-T-80900']);
      expect(s.byKey('CHL-30002'), same(chiller));
      final forklift = s.byKey('FL-008')!;
      expect(forklift.assetRegistrationId, isNull, reason: 'known only from location events');
      expect(forklift.zone?.zoneName, 'Charging Zone');
      expect(s.inZone(name: 'Zone B', code: 'ZB').map((a) => a.key), ['AR000001']);
      expect(s.inZone(name: 'zone b').length, 1, reason: 'case-insensitive');
      expect(s.eventsOf(chiller, window: const Duration(hours: 1), now: _now), hasLength(2));
      expect(s.eventsOf(chiller, window: const Duration(minutes: 10), now: _now), hasLength(1));
    });

    test('a Departed Zone as the latest event leaves the zone unknown', () {
      final assets = buildTrackedAssets(
        devices: const [],
        health: const [],
        tags: const [],
        sensors: const [],
        events: [
          TrkLocationEvent(
            eventId: '2',
            eventTime: _now,
            assetId: 'X-1',
            eventType: TrkEventType.departedZone,
            zoneName: 'A',
          ),
          TrkLocationEvent(
            eventId: '1',
            eventTime: _now.subtract(const Duration(hours: 1)),
            assetId: 'X-1',
            eventType: TrkEventType.enteredZone,
            zoneName: 'A',
          ),
        ],
      );
      expect(assets.single.zone, isNull);
      expect(assets.single.lastEventAt, _now);
    });

    test('counters come from the same list', () {
      final s = _snapshot();
      expect(s.online(_now), 1, reason: 'only AR000001 seen in the last 10 min');
      expect(s.offline(_now), 2);
      expect(s.lowBattery, 1, reason: 'AR000002 at 12 %');
      expect(s.online(_now) + s.offline(_now), s.assets.length);
    });
  });

  // ------------------------------------------------------------------ stale

  group('stale markers', () {
    test('10 minutes without a signal = stale; never seen = unknown', () {
      expect(freshnessOf(null, _now), Freshness.unknown);
      expect(freshnessOf(_now.subtract(const Duration(minutes: 9, seconds: 59)), _now), Freshness.live);
      expect(freshnessOf(_now.subtract(staleAfter), _now), Freshness.live);
      expect(freshnessOf(_now.subtract(const Duration(minutes: 10, seconds: 1)), _now), Freshness.stale);
      expect(isStale(_now.subtract(const Duration(hours: 1)), _now), isTrue);
    });

    test('plot colours: live violet / blue, stale or unknown grey', () {
      final s = _snapshot();
      final plots = MapPlot.parseList(mapPlotsJson);
      expect(plotColor(plots[0], s, _now), AppColors.primary, reason: 'AR000001 seen 1 min ago');
      expect(plotColor(plots[1], s, _now), AppColors.iconBlue, reason: 'device seen 2 min ago');
      expect(plotColor(plots[2], s, _now), AppColors.grey, reason: 'AR000002 seen 30 min ago');
      expect(plotColor(plots[0], s, _now.add(const Duration(minutes: 15))), AppColors.grey, reason: 'goes stale');
      expect(plotColor(plots[0], null, _now), AppColors.grey);
      expect(plotColor(const MapPlot(kind: 'device', itemId: 'NOPE', x: 0, y: 0), s, _now), AppColors.grey);
    });
  });

  // ------------------------------------------------------------------ polling

  group('polling', () {
    late FakeAssetBackend backend;
    late List<_FakeTimer> timers;
    late ProviderContainer c;

    setUp(() {
      backend = FakeAssetBackend()..seedTracking(DateTime.now);
      timers = [];
      TrkLiveNotifier.debugSchedule = (d, cb) => (timers..add(_FakeTimer(d, cb))).last;
      c = ProviderContainer(overrides: backend.overrides);
    });

    tearDown(() {
      TrkLiveNotifier.debugSchedule = null;
      c.dispose();
    });

    Future<void> settle() async {
      for (var i = 0; i < 20; i++) {
        await Future<void>.delayed(Duration.zero);
      }
    }

    int calls(String path) => backend.calls('GET', path).length;

    test('starts on attach, every 5 s, location events every 6th round, stops on detach', () async {
      final live = c.read(trkLiveProvider.notifier);
      final owner = Object();
      expect(c.read(trkLiveProvider).polling, isFalse);
      live.attach(owner);
      for (var i = 0; i < 5 && c.read(trkLiveProvider).snapshot == null; i++) {
        await settle();
      }
      expect(c.read(trkLiveProvider).polling, isTrue);
      expect(c.read(trkLiveProvider).snapshot?.assets, hasLength(3));
      expect(c.read(trkLiveProvider).alertCount?.unresolved, 1);
      expect(calls('/api/location-events'), 1);
      expect(timers.single.delay, TrkLiveNotifier.interval);

      // Rounds 2–6: devices + health + count only.
      for (var round = 2; round <= 6; round++) {
        timers.last.callback();
        await settle();
        await settle();
      }
      expect(calls('/api/iot-devices'), 6);
      expect(calls('/api/location-events'), 1);
      timers.last.callback(); // round 7 = the 6th after the first: full again
      await settle();
      await settle();
      expect(calls('/api/location-events'), 2);
      expect(c.read(trkLiveProvider).tick, 7);

      final before = timers.length;
      live.detach(owner);
      expect(c.read(trkLiveProvider).polling, isFalse);
      expect(timers.last.cancelled, isTrue);
      expect(timers.length, before, reason: 'nothing scheduled after detach');
    });

    test('stays on while any screen is attached; a failed round keeps the data', () async {
      final live = c.read(trkLiveProvider.notifier);
      final a = Object(), b = Object();
      live
        ..attach(a)
        ..attach(b);
      for (var i = 0; i < 5 && c.read(trkLiveProvider).snapshot == null; i++) {
        await settle();
      }
      live.detach(a);
      expect(c.read(trkLiveProvider).polling, isTrue);
      expect(live.listeners, 1);
      backend.failNext['GET /api/iot-devices'] = const FakeReply(500, {'message': 'boom'});
      timers.last.callback();
      await settle();
      await settle();
      expect(c.read(trkLiveProvider).error, isNotNull);
      expect(c.read(trkLiveProvider).snapshot, isNotNull);
      live.detach(b);
      expect(c.read(trkLiveProvider).polling, isFalse);
    });

    testWidgets('LivePollingScope polls only while its route / tab is visible', (tester) async {
      final container = ProviderContainer(overrides: backend.overrides);
      addTearDown(container.dispose);
      Widget scope(bool visible) => UncontrolledProviderScope(
        container: container,
        child: MaterialApp(
          home: TickerMode(
            enabled: visible,
            child: const LivePollingScope(child: SizedBox()),
          ),
        ),
      );
      await tester.pumpWidget(scope(false));
      await tester.pumpAndSettle();
      expect(container.read(trkLiveProvider).polling, isFalse);
      await tester.pumpWidget(scope(true));
      await tester.pumpAndSettle();
      expect(container.read(trkLiveProvider).polling, isTrue);
      await tester.pumpWidget(scope(false));
      await tester.pumpAndSettle();
      expect(container.read(trkLiveProvider).polling, isFalse);
      await tester.pumpWidget(scope(true));
      await tester.pumpAndSettle();
      await tester.pumpWidget(const SizedBox());
      await tester.pumpAndSettle();
      expect(container.read(trkLiveProvider).polling, isFalse, reason: 'disposed');
    });
  });

  // ------------------------------------------------------------------ query parameters

  group('only the parameters each endpoint reads', () {
    late FakeAssetBackend backend;
    late TrackingApi api;
    late AdminMapsApi maps;

    setUp(() {
      backend = FakeAssetBackend()..seedTracking(DateTime.now);
      Dio dio(String base) => Dio(BaseOptions(baseUrl: base))..httpClientAdapter = backend;
      api = TrackingApi(ApiClient(dio('http://trk.test:5490'), checkConfig: false, service: BackendService.tracking));
      maps = AdminMapsApi(ApiClient(dio('http://admin.test:5460'), checkConfig: false, service: BackendService.admin));
    });

    Map<String, String> q(String path) => backend.calls('GET', path).last.uri.queryParameters;

    test('telemetry, alerts, location events, floor plans', () async {
      await api.getReadings(days: 2, sensorId: 'SEN-T-80900', limit: TrackingApi.maxReadings);
      expect(q('/api/telemetry-readings'), {'days': '2', 'sensorId': 'SEN-T-80900', 'limit': '5000'});
      await api.getAlerts(days: 7);
      expect(q('/api/iot-alerts'), {'days': '7'});
      await api.getAlertCount();
      expect(q('/api/iot-alerts/count'), isEmpty);
      await api.getLocationEvents();
      expect(q('/api/location-events'), isEmpty, reason: 'the endpoint takes none');
      final plan = await maps.getPublishedPlan('flr-1');
      expect(q('/api/administration/maps/floor-plans'), {'locationId': 'flr-1', 'status': 'Published'});
      expect(plan?.floorPlanId, 'FP-0001');
      expect(await maps.getPublishedPlan('flr-2'), isNull);
      await maps.getPlots('FP-0001');
      expect(q('/api/administration/maps/floor-plans/FP-0001/plots'), isEmpty);
    });

    test('readings provider: sensor id wins over device id, trimmed to the range', () async {
      final c = ProviderContainer(overrides: backend.overrides);
      addTearDown(c.dispose);
      final rows = await c.read(
        trkReadingsProvider((sensorId: 'SEN-T-80900', deviceId: 'TS-80214', range: TrackRange.hour)).future,
      );
      expect(q('/api/telemetry-readings'), {'days': '2', 'sensorId': 'SEN-T-80900', 'limit': '5000'});
      expect(rows.length, inInclusiveRange(6, 7));
      expect(rows.first.recordedAt.isBefore(rows.last.recordedAt), isTrue);
    });
  });

  // ------------------------------------------------------------------ image cache

  group('floor-plan image cache', () {
    test('one download per URL; file names are stable', () async {
      FloorPlanImageCache.useDisk = false;
      addTearDown(() => FloorPlanImageCache.useDisk = true);
      final adapter = _BytesAdapter(base64Decode(_png1x1));
      final cache = FloorPlanImageCache(Dio()..httpClientAdapter = adapter, sizeOf: (_) async => (300, 100));
      final results = await Future.wait([
        cache.load('http://media.test/uploads/fp.png'),
        cache.load('http://media.test/uploads/fp.png'),
      ]);
      expect(identical(results[0], results[1]), isTrue, reason: 'concurrent loads share one request');
      expect(results[0].aspect, 3);
      expect(identical(await cache.load('http://media.test/uploads/fp.png'), results[0]), isTrue);
      expect(cache.downloads, 1);
      expect(adapter.calls, 1);
      expect(cache.peek('http://media.test/uploads/fp.png'), isNotNull);
      expect(FloorPlanImageCache.fileKey('a'), FloorPlanImageCache.fileKey('a'));
      expect(FloorPlanImageCache.fileKey('a'), isNot(FloorPlanImageCache.fileKey('b')));
      expect(FloorPlanImageCache.fileKey('a'), hasLength(16));
    });
  });

  // ------------------------------------------------------------------ screens

  group('screens (API mode)', () {
    late FakeAssetBackend backend;

    setUp(() {
      backend = FakeAssetBackend()..seedTracking(DateTime.now);
      debugApiMode = true;
      NetworkNotifier.listenToDevice = false;
      TrkLiveNotifier.debugSchedule = (d, cb) => _FakeTimer(d, cb);
    });

    tearDown(() {
      debugApiMode = null;
      NetworkNotifier.listenToDevice = true;
      TrkLiveNotifier.debugSchedule = null;
    });

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

    Future<void> see(WidgetTester tester, Finder f, {String? reason}) async {
      for (var i = 0; i < 40 && f.evaluate().isEmpty; i++) {
        await tester.drag(page(), const Offset(0, -250));
        await tester.pump();
      }
      expect(f, findsWidgets, reason: reason);
    }

    testWidgets('floor plan: live zone counts, moved asset, device cluster, legend; polling while visible', (
      tester,
    ) async {
      final c = await open(tester, '/more/tracking?tab=map&view=floor&location=flr-1');
      expect(c.read(trkLiveProvider).polling, isTrue);
      expect(c.read(liveLocProvider).polling, isTrue);
      expect(find.byType(FloorPlanMap), findsOneWidget);
      // LIVE counts (KEY RULE): the Bay reader heard one tag in Zone B, the Store reader CHL-30002.
      expect(find.bySemanticsLabel('Zone B, 1 asset'), findsOneWidget);
      // AR000001 has an open IoT alert → "1 asset · 1 alert".
      expect(find.bySemanticsLabel('Restricted Store, 1 asset · 1 alert, geofence'), findsOneWidget);
      expect(find.text('1 asset · 1 alert'), findsOneWidget);
      expect(find.bySemanticsLabel('Charging Zone, 0 assets'), findsOneWidget);
      // Server layer defaults: devices hidden → 2 asset markers; AR000001 was plotted in Zone B
      // but heard in Restricted Store → "moved".
      expect(find.bySemanticsLabel('Asset Chiller Unit 2, moved'), findsOneWidget);
      expect(find.bySemanticsLabel('Asset Pump 1'), findsOneWidget);
      expect(find.bySemanticsLabel(RegExp('^Device ')), findsNothing);
      expect(find.text('Ground Floor'), findsWidgets);
      expect(find.textContaining('PDF'), findsOneWidget, reason: 'non-image plan note');
      expect(find.textContaining(RegExp(r'^Updated \d+ s ago')), findsOneWidget);
      expect(find.text('Moved'), findsOneWidget, reason: 'legend');
      // Devices on: the Bay reader carries a cluster badge (1 unplotted live tag); tap → list.
      await tester.tap(find.byTooltip('Layers'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Devices'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Apply'));
      await tester.pumpAndSettle();
      final bay = find.byKey(const ValueKey('plot-device:C851FBF764C0'));
      expect(bay, findsOneWidget);
      expect(find.byKey(const ValueKey('plot-device:STORE-RDR-2')), findsOneWidget);
      expect(
        find.descendant(of: bay, matching: find.text('1')),
        findsOneWidget,
        reason: 'cluster badge',
      );
      // Lift the map above the bottom bar (drag in the page gutter: a drag on the map pans it).
      await tester.dragFrom(const Offset(4, 400), const Offset(0, -250));
      await tester.pumpAndSettle();
      await tester.tap(bay);
      await tester.pumpAndSettle();
      expect(find.text('Near Bay reader'), findsOneWidget);
      expect(find.text('Tag E2801170000002089FFF'), findsOneWidget);
      await tester.tap(find.text('Close'));
      await tester.pumpAndSettle();

      await tester.tap(find.bySemanticsLabel('Asset Chiller Unit 2, moved'));
      await tester.pumpAndSettle();
      await see(tester, find.text('View Details'));
      expect(find.text('Now near Store reader in Restricted Store'), findsOneWidget);
      expect(find.text('Plotted position (Administration → Maps)'), findsOneWidget);
      await see(tester, find.text('Live assets by zone (2)'), reason: 'Zone B + Restricted Store');

      // Zone sheet: location path, live assets, plotted devices.
      // (Its map label sits under the Store reader marker: open it from the live zone list.)
      final row = find.widgetWithText(ListTile, 'Restricted Store');
      await see(tester, row);
      await tester.ensureVisible(row);
      await tester.pumpAndSettle();
      await tester.tap(row);
      await tester.pumpAndSettle();
      expect(find.text('Live assets (1)'), findsOneWidget);
      expect(find.text('Plotted devices (1)'), findsOneWidget);
      expect(find.text('Store reader'), findsOneWidget);
      await tester.tap(find.text('Close'));
      await tester.pumpAndSettle();

      // Leaving the screen stops both pollers.
      c.read(routerProvider).go('/more');
      await tester.pumpAndSettle();
      expect(c.read(trkLiveProvider).polling, isFalse);
      expect(c.read(liveLocProvider).polling, isFalse);
    });

    testWidgets('a floor without a published plan says so', (tester) async {
      await open(tester, '/more/tracking?tab=map&view=floor&location=flr-2');
      expect(find.text('No floor plan published for this floor'), findsOneWidget);
    });

    testWidgets('outdoor: site pin with its tracked count and the GPS geofences', (tester) async {
      await open(tester, '/more/tracking?tab=map&view=outdoor');
      expect(find.byType(OutdoorMap), findsOneWidget);
      expect(find.bySemanticsLabel('Site Dubai Factory, 3 tracked'), findsOneWidget, reason: 'live count');
      expect(find.text('Factory perimeter'), findsOneWidget);
    });

    testWidgets('asset list tab: every Asset service asset + its tracking facts; unregistered records counted', (
      tester,
    ) async {
      await open(tester, '/more/tracking?tab=list');
      // The Asset service list (GET /api/asset-registrations), not the Tracking service's references.
      expect(backend.calls('GET', '/api/asset-registrations'), isNotEmpty);
      expect(find.text('TAG-0001'), findsOneWidget);
      expect(find.text('Diesel Generator 500 kVA'), findsOneWidget);
      expect(find.text('TAG-0002'), findsOneWidget);
      // Both registered assets have a tracking record (by registration id): zone + online state.
      expect(find.text('Zone: Zone B'), findsOneWidget, reason: 'AR000001 entered Zone B');
      expect(find.text('Not tracked'), findsOneWidget, reason: 'the filter chip only, no row');
      // FL-008 (location events only) matches no registered asset: counted, not listed.
      expect(find.text('Forklift 8'), findsNothing);
      await see(tester, find.text('Showing 2 of 2 assets'));
      await see(tester, find.text('Unregistered tracking records: 1'));
      await tester.tap(find.text('Unregistered tracking records: 1'));
      await tester.pumpAndSettle();
      expect(find.text('Forklift 8'), findsOneWidget);
      await tester.tap(find.text('Close'));
      await tester.pumpAndSettle();

      // Not tracked filter: none; Tracked: both.
      await tester.ensureVisible(find.text('Not tracked'));
      await tester.tap(find.text('Not tracked'));
      await tester.pumpAndSettle();
      expect(find.text('No loaded assets match'), findsOneWidget);
      await tester.ensureVisible(find.text('Tracked'));
      await tester.tap(find.text('Tracked'));
      await tester.pumpAndSettle();
      expect(find.text('TAG-0001'), findsOneWidget);
    });

    testWidgets('asset list tab: an asset without a tracking record says "Not tracked"', (tester) async {
      backend
        ..on('GET', '/api/iot-devices', (_) => const FakeReply(200, []))
        ..on('GET', '/api/device-health', (_) => const FakeReply(200, []))
        ..on('GET', '/api/tag-ids', (_) => const FakeReply(200, []))
        ..on('GET', '/api/location-events', (_) => const FakeReply(200, []));
      await open(tester, '/more/tracking?tab=list');
      expect(find.text('Not tracked'), findsNWidgets(3), reason: 'filter chip + 2 rows');
      expect(find.textContaining('Unregistered tracking records'), findsNothing);
    });

    testWidgets('sensors tab → sensor detail', (tester) async {
      await open(tester, '/more/tracking?tab=sensors');
      expect(find.textContaining('SEN-T-80900'), findsOneWidget);
      expect(find.text('42.1 °C'), findsOneWidget);
      await tester.tap(find.textContaining('SEN-T-80900'));
      await tester.pumpAndSettle();
      expect(find.text('Sensor History'), findsOneWidget);
      expect(find.text('Out of range'), findsOneWidget);
    });

    testWidgets('geofencing tab: the live map for now (no zone drawing)', (tester) async {
      await open(tester, '/more/tracking?tab=zones&view=locations');
      expect(find.byType(OutdoorMap), findsOneWidget);
      expect(find.text('Factory perimeter'), findsOneWidget);
      expect(find.text('Add Zone'), findsNothing);
    });

    testWidgets('alerts: open list matches the badge', (tester) async {
      await open(tester, '/more/tracking/alerts');
      expect(find.text('Temperature High'), findsNothing, reason: 'title shows asset · event');
      expect(find.text('Chiller Unit 2 · Temperature High'), findsOneWidget);
      expect(find.text('Forklift 8 · Geofence Exit'), findsNothing, reason: 'resolved');
    });

    testWidgets('asset tracking: current zone, trail and history replay', (tester) async {
      await open(tester, '/more/tracking/asset/AR000001');
      expect(find.text('In Zone B'), findsOneWidget);
      await see(tester, find.text('Route (2 zone events, last 24 h)'));
      Future<void> up(Finder f) => tester.scrollUntilVisible(f, -250, scrollable: page());
      await tester.tap(find.text('View History'));
      await tester.pumpAndSettle();
      await up(find.byType(Slider));
      expect(find.textContaining('2 of 2 ·'), findsOneWidget, reason: 'replay starts at the newest event');
      await tester.drag(find.byType(Slider), const Offset(-400, 0));
      await tester.pumpAndSettle();
      expect(find.textContaining('1 of 2 ·'), findsOneWidget);
      await up(find.text('Telemetry'));
      await tester.tap(find.text('Telemetry'));
      await tester.pumpAndSettle();
      expect(find.text('42.1 °C'), findsOneWidget);
    });

    testWidgets('asset detail IoT tab: readings, last zone, "No sensors" when none', (tester) async {
      await open(tester, '/assets/detail/AR000001?tab=iot');
      await see(tester, find.text('Last known position'));
      expect(find.text('Zone B'), findsWidgets);
      await see(tester, find.text('Latest readings'));

      backend.on('GET', '/api/sensors', (_) => const FakeReply(200, <Object>[]));
      await open(tester, '/assets/detail/AR000001?tab=iot');
      await see(tester, find.text('No sensors linked to this asset'));
    });
  });
}

const _png1x1 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';

class _BytesAdapter implements HttpClientAdapter {
  _BytesAdapter(this.bytes);

  final List<int> bytes;
  int calls = 0;

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    calls++;
    return ResponseBody.fromBytes(bytes, 200);
  }

  @override
  void close({bool force = false}) {}
}
