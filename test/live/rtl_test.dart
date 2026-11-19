import 'dart:async';
import 'dart:convert';
import 'dart:io' show WebSocketException;
import 'dart:typed_data';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/licence/licence.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart' show ActionTile;
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/api/auth_models.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_models.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart' show masterDataRepositoryProvider;
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/network_repository.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/data/tracking/locate_commands.dart';
import 'package:asset360_mobile/data/tracking/locate_session.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/locate_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_asset_backend.dart';
import 'rtl_samples.dart';
import 'tracking_samples.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

/// Signed in with the tenant of the verify-totp sample (handoff 0.1).
class _AuthIn extends AuthNotifier {
  @override
  AuthState build() => AuthSignedIn(
    user: const CurrentUser(fullName: 'Demo User', clientId: 'CLT-1001', tenantId: 'TENANT-DEFAULT'),
    tokens: TokenPair(accessToken: 'tok-1', refreshToken: 'r-1', expiresAt: DateTime.now().add(const Duration(hours: 1))),
    rememberMe: false,
  );
}

/// Timers the pollers / locate session schedule, fired by hand.
class _Timers {
  final pending = <(Duration, void Function(), _FakeTimer)>[];

  Timer schedule(Duration d, void Function() cb) {
    final t = _FakeTimer();
    pending.add((d, cb, t));
    return t;
  }

  /// Fires every live timer of [d] (all when null).
  void fire([Duration? d]) {
    final due = [for (final p in pending) if (!p.$3.cancelled && (d == null || p.$1 == d)) p];
    pending.removeWhere(due.contains);
    for (final p in due) {
      p.$3.cancelled = true;
      p.$2();
    }
  }

  int live(Duration d) => pending.where((p) => !p.$3.cancelled && p.$1 == d).length;
}

class _FakeTimer implements Timer {
  bool cancelled = false;

  @override
  void cancel() => cancelled = true;

  @override
  bool get isActive => !cancelled;

  @override
  int get tick => 0;
}

/// A /ws/locate connection the test drives.
class _FakeChannel implements LocateChannel {
  _FakeChannel(this.uri, this.bearer, {this.fail});

  final Uri uri;
  final String? bearer;
  final Object? fail;
  final _in = StreamController<Object?>();
  final out = <Map<String, dynamic>>[];
  bool closed = false;

  @override
  Future<void> get ready => fail == null ? Future.value() : Future.error(fail!);

  @override
  Stream<Object?> get messages => _in.stream;

  @override
  void send(String text) => out.add(Map<String, dynamic>.from(jsonDecode(text) as Map));

  void reply(Map<String, Object?> frame) => _in.add(jsonEncode(frame));

  @override
  Future<void> close() async {
    closed = true;
    await _in.close();
  }
}

Map<String, dynamic> _subscribed({bool mqtt = true}) => {
  'type': 'subscribed',
  'tagid': 'c30000aabb01',
  'gatewayid': 'ac233fc0ffee',
  'lastSeen': '2026-11-19T10:14:52Z',
  'rssi': -61,
  'mqttConnected': mqtt,
  'topics': {'action': '/gw/ac233fc0ffee/action', 'response': '/gw/ac233fc0ffee/response'},
};

void main() {
  // ------------------------------------------------------------------ parsing

  group('parsing (handoff 1.2–1.4)', () {
    test('overview: KPIs, assets, location, expected, gateways', () {
      final o = RtlOverview.fromJson(rtlOverviewJson);
      expect(o.kpis.assetsInView, 3);
      expect(o.kpis.moving, 1);
      expect(o.kpis.stationary, 1);
      expect(o.kpis.unknown, 1);
      expect(o.kpis.locationAlerts, 1);
      final a = o.asset('ar-000123')!; // case-insensitive
      expect(a.tagLabel, 'PUMP-01');
      expect(a.tags, ['C30000AABB01', 'C3:00:00:AA:BB:02']);
      expect(a.location!.gatewayId, 'AC233FC0FFEE');
      expect(a.location!.path, 'Head Office › Block 1 › Ground Floor › Hall A');
      expect(a.location!.at, DateTime.utc(2026, 11, 19, 10, 14, 52));
      expect(a.confidence, 'High');
      expect(a.rssi, -61);
      expect(a.activeLocationAlerts, 1);
      expect(a.expected!.zoneName, 'Hall A');
      expect(a.comparisonStatus, 'Match');
      expect(o.asset('AR-000124')!.expected, isNull);
      expect(o.gateway('ac233fc0ffee')!.label, 'GW-01');
      expect(gatewayStateOf(o.gateway('AC233FC0BEEF')), RtlGatewayState.idle);
      expect(gatewayStateOf(null), RtlGatewayState.offline);
      expect(RtlOverview.fromJson(rtlEmptyOverviewJson).isEmpty, isTrue);
      expect(RtlOverview.fromJson(null).kpis.assetsInView, 0);
    });

    test('marker status: green Live+Moving, blue Live+Stationary, orange Stale, grey low confidence, red Offline', () {
      final o = RtlOverview.fromJson(rtlOverviewJson);
      expect(rtlStatusOf(o.asset('AR-000123')!), RtlStatus.liveMoving);
      expect(rtlStatusOf(o.asset('AR-000124')!), RtlStatus.liveStationary);
      expect(rtlStatusOf(o.asset('AR-000125')!), RtlStatus.stale);
      expect(rtlStatusOf(const RtlAsset(assetId: 'x', freshness: 'Offline')), RtlStatus.offline);
      expect(rtlStatusOf(const RtlAsset(assetId: 'x', freshness: 'Unknown')), RtlStatus.lowConfidence);
      expect(
        rtlStatusOf(const RtlAsset(assetId: 'x', freshness: 'Live', movement: 'Moving', confidence: 'Low')),
        RtlStatus.lowConfidence,
      );
    });

    test('events page and trail (incl. initial segment and time in zones)', () {
      final p = RtlEventPage.fromJson(rtlEventsJson(page: 2));
      expect(p.items, hasLength(10));
      expect(p.page, 2);
      expect(p.totalPages, 4);
      expect(p.hasMore, isTrue);
      expect(p.eventTypes, contains('Geofence Entry'));
      expect(p.items.first.eventId, 'ENT-10');
      final t = RtlTrail.fromJson(rtlTrailJson);
      expect(t.segments, hasLength(2));
      expect(t.segments.first.initial, isTrue);
      expect(t.segments.first.minutes, 27.5);
      expect(t.zoneChanges, 1);
      expect(t.timeInZones.map((z) => z.zone), ['Store Room', 'Hall A']);
      expect(t.note, startsWith('BLE proximity'));
    });

    test('locate command history: Mongo relaxed JSON, the raw request is never read', () {
      final rows = LocateCommandRecord.parseList([
        {
          'reqId': {r'$numberInt': '503'},
          'command': 'led',
          'status': 'completed',
          'gatewayid': 'ac233fc0ffee',
          'sentAt': {r'$date': '2026-11-19T10:15:01Z'},
          'request': {'key': 'SECRET'},
        },
      ]);
      expect(rows.single.reqId, 503);
      expect(rows.single.sentAt, DateTime.utc(2026, 11, 19, 10, 15, 1));
      expect(rows.single.status, 'completed');
    });
  });

  // ------------------------------------------------------------------ placement / zones / floors

  group('map rules', () {
    final plots = MapPlot.parseList(rtlPlotsJson);
    final o = RtlOverview.fromJson(rtlOverviewJson);

    test('assets sit on their gateway plot (itemId or code, case-insensitive); unplotted → Not on this floor plan', () {
      final p = placeAssets(o.assets, plots);
      expect(p.byGateway['ac233fc0ffee']!.map((a) => a.assetTag), ['PUMP-01']);
      expect(p.byGateway['gw-02']!.map((a) => a.assetTag), ['FORK-02'], reason: 'matched on the plot code');
      expect(p.notOnPlan.map((a) => a.assetTag), ['CART-03']);
      expect(p.plotOf(o.asset('AR-000124')!)!.itemId, 'gw-02');
      expect(p.gatewayPlots.keys, isNot(contains('AR-000999')), reason: 'asset plots are not gateways');
    });

    test('several assets on one gateway fan out round it; more than the limit → a count bubble', () {
      final many = [
        for (var i = 0; i < 9; i++)
          RtlAsset(assetId: 'A$i', location: const RtlLocation(gatewayId: 'AC233FC0FFEE')),
      ];
      expect(placeAssets(many, plots).byGateway['ac233fc0ffee'], hasLength(9));
      expect(fanOffsets(1).single, (dx: 0.0, dy: 0.0));
      final three = fanOffsets(3);
      expect(three.toSet(), hasLength(3), reason: 'distinct positions');
      for (final o in three) {
        expect((o.dx * o.dx + o.dy * o.dy), closeTo(22 * 22, 0.001));
      }
      expect(fanOffsets(rtlFanLimit + 1).every((o) => o.dx == 0 && o.dy == 0), isTrue);
    });

    test('zone shapes: rectangle, polygon, circle radius = % of plan WIDTH; restricted by type / access rule', () {
      final zones = MapZone.parseList(rtlZonesJson);
      final rect = zones[0].outline();
      expect(rect.first, (x: 10.0, y: 15.0));
      expect(rect[2], (x: 40.0, y: 40.0));
      // Plan 60 × 40 m → aspect 1.5: a radius of 10 % of the width is 15 % of the height.
      final circle = zones[1].outline(aspect: 1.5);
      final xs = circle.map((p) => p.x), ys = circle.map((p) => p.y);
      expect(xs.reduce((a, b) => a > b ? a : b), closeTo(85, 0.01));
      expect(xs.reduce((a, b) => a < b ? a : b), closeTo(65, 0.01));
      expect(ys.reduce((a, b) => a > b ? a : b), closeTo(75, 0.1));
      expect(zones[1].center, (x: 75.0, y: 60.0));
      expect(zones[2].outline(), hasLength(3));
      expect(isRestrictedZone(zones[2]), isTrue);
      expect(isRestrictedZone(zones[0]), isFalse);
      expect(isRestrictedZone(const MapZone(zoneId: 'z', type: 'Office', accessRule: 'Restricted access')), isTrue);
    });

    test('floor-plan choice: Published > newest non-archived with a file (Draft in dev only) > floor picture > none', () {
      final plans = MapFloorPlan.parseList(rtlFloorPlansJson);
      const gf = MdSpace(id: 'fl-1', name: 'Ground Floor');
      const ff = MdSpace(id: 'fl-2', name: 'First Floor', imageUrl: '/uploads/floors/fl-2.png');
      const roof = MdSpace(id: 'fl-9', name: 'Roof', images: ['/uploads/floors/roof.png']);
      const empty = MdSpace(id: 'fl-0', name: 'Basement');
      expect((chooseFloorPlan(plans, gf, dev: false) as RtlPlanChoice).plan.floorPlanId, 'fp-001');
      expect((chooseFloorPlan(plans, gf, dev: true) as RtlPlanChoice).plan.floorPlanId, 'fp-001', reason: 'Published wins');
      final noPublished = plans.where((p) => p.floorPlanId != 'fp-001').toList();
      final draft = chooseFloorPlan(noPublished, gf, dev: true) as RtlPlanChoice;
      expect(draft.plan.floorPlanId, 'fp-002');
      expect(draft.draft, isTrue);
      expect(chooseFloorPlan(noPublished, gf, dev: false), isA<RtlNoPlan>(), reason: 'no Draft outside dev builds');
      // Archived plan skipped → the Master Data floor picture.
      expect((chooseFloorPlan(plans, ff, dev: true) as RtlPictureChoice).url, '/uploads/floors/fl-2.png');
      expect((chooseFloorPlan(plans, roof, dev: false) as RtlPictureChoice).url, '/uploads/floors/roof.png');
      expect(chooseFloorPlan(plans, empty, dev: true), isA<RtlNoPlan>());
      // Newest of two non-published; a PDF plan has no image → the next one.
      final two = [
        const MapFloorPlan(floorPlanId: 'old', locationId: 'fl-1', fileUrl: '/a.png', status: 'Draft'),
        MapFloorPlan(
          floorPlanId: 'new',
          locationId: 'fl-1',
          fileUrl: '/b.png',
          status: 'Draft',
          updatedAt: DateTime.utc(2026, 11, 19),
        ),
        MapFloorPlan(
          floorPlanId: 'pdf',
          locationId: 'fl-1',
          fileUrl: '/c.pdf',
          format: 'pdf',
          status: 'Published',
          updatedAt: DateTime.utc(2026, 11, 20),
        ),
      ];
      expect((chooseFloorPlan(two, gf, dev: true) as RtlPlanChoice).plan.floorPlanId, 'new');
    });

    test('floor picker: Site › Building › Floor; deleted rows skipped', () {
      final sites = mdParseList(rtlSitesJson, MdSite.fromJson);
      final buildings = mdParseList(rtlBuildingsJson, MdSpace.fromJson);
      final floors = mdParseList(rtlFloorsJson, MdSpace.fromJson);
      expect(sites.map((s) => s.name), ['Head Office']);
      final options = rtlFloorOptions(sites, buildings, floors);
      expect(options.map((f) => f.label), ['Head Office › Block 1 › First Floor', 'Head Office › Block 1 › Ground Floor']);
      expect(options.every((f) => f.siteId == 'site-1'), isTrue);
    });
  });

  // ------------------------------------------------------------------ locate rules

  group('locate commands (handoff 2.2)', () {
    test('tag ids: lower case, ":" and "-" removed', () {
      expect(normalizeTagId('C3:00:00:AA:BB:01'), 'c30000aabb01');
      expect(normalizeTagId(' C3-00-00-AA-BB-01 '), 'c30000aabb01');
    });

    test('ranges and messages', () {
      expect(LocateLimits.ledOn.check('0'), 'Enter a value from 1 to 65535');
      expect(LocateLimits.ledOn.check('65535'), isNull);
      expect(LocateLimits.ledOn.check('65536'), isNotNull);
      expect(LocateLimits.ledCycles.check('abc'), isNotNull);
      expect(LocateLimits.brightness.check('101'), 'Enter a value from 1 to 100');
      expect(LocateLimits.beep.check('19'), 'Enter a value from 20 to 65535');
      expect(LocateLimits.pause.check('20'), isNull);
      expect(LocateLimits.beepCycles.check('0'), isNull, reason: '0 = off');
      expect(LocateLimits.tone.check('6'), 'Enter a value from 0 to 5');
      expect(LocateLimits.tone.hint, 'Min 0, max 5');
    });

    test('exact payloads: led / buzzer flat, both nested, Turn off = buzzer cycles 0', () {
      const led = LedOptions(onMs: 200, offMs: 200, cycles: 25);
      const bz = BuzzerOptions(beepMs: 200, pauseMs: 200, cycles: 25);
      expect(jsonEncode(locateCommand(LocateMode.led, 'C3:00:00:AA:BB:01')), jsonEncode({
        'type': 'led',
        'tagid': 'c30000aabb01',
        'color': 3,
        'onMs': 300,
        'offMs': 300,
        'cycles': 10,
        'brightness': 100,
      }));
      expect(jsonEncode(locateCommand(LocateMode.buzzer, 'c30000aabb01')), jsonEncode({
        'type': 'buzzer',
        'tagid': 'c30000aabb01',
        'beepMs': 300,
        'pauseMs': 300,
        'cycles': 5,
        'level': 0,
      }));
      expect(jsonEncode(locateCommand(LocateMode.both, 'c30000aabb01', led: led, buzzer: bz)), jsonEncode({
        'type': 'both',
        'tagid': 'c30000aabb01',
        'led': {'color': 3, 'onMs': 200, 'offMs': 200, 'cycles': 25, 'brightness': 100},
        'buzzer': {'beepMs': 200, 'pauseMs': 200, 'cycles': 25, 'level': 0},
      }));
      expect(turnOffCommand('C30000AABB01'), {'type': 'buzzer', 'tagid': 'c30000aabb01', 'cycles': 0});
      expect(subscribeFrame('C30000AABB01'), {'type': 'subscribe', 'tagid': 'c30000aabb01'});
      expect(runTimeLine(LocateMode.both, const LedOptions(), const BuzzerOptions()), 'LED runs 6.0 s · buzzer runs 3.0 s');
      expect(runTimeLine(LocateMode.led, const LedOptions(onMs: 60000, offMs: 1, cycles: 5), const BuzzerOptions()),
          'LED runs 5.0 min');
    });

    test('progress follows reqId: sent → accepted → completed; other reqIds ignored; failed / timeout / error', () {
      var p = const LocateProgress(LocateStage.sending);
      p = applyLocateFrame(p, {'type': 'commandSent', 'reqId': 503, 'command': 'led', 'gatewayid': 'ac233fc0ffee'});
      expect((p.stage, p.reqId, p.message), (LocateStage.sent, 503, 'Sent to gateway ac233fc0ffee'));
      expect(applyLocateFrame(p, {'type': 'commandStatus', 'reqId': 999, 'status': 'completed'}).stage, LocateStage.sent);
      p = applyLocateFrame(p, {'type': 'commandStatus', 'reqId': 503, 'command': 'led', 'status': 'accepted', 'code': 1});
      expect((p.stage, p.message), (LocateStage.accepted, 'Gateway accepted'));
      final done = applyLocateFrame(p, {'type': 'commandStatus', 'reqId': 503, 'command': 'led', 'status': 'completed'});
      expect((done.stage, done.message, done.success), (LocateStage.completed, 'Tag is blinking', true));
      final beep = applyLocateFrame(p, {'type': 'commandStatus', 'reqId': 503, 'command': 'buzzer', 'status': 'completed'});
      expect(beep.message, 'Tag is beeping');
      final failed = applyLocateFrame(p, {
        'type': 'commandStatus',
        'reqId': 503,
        'status': 'failed',
        'message': 'Tag did not respond (error 4).',
      });
      expect((failed.stage, failed.message, failed.problem), (LocateStage.failed, 'Tag did not respond (error 4).', true));
      final timeout = applyLocateFrame(p, {'type': 'commandStatus', 'reqId': 503, 'status': 'timeout'});
      expect((timeout.stage, timeout.message), (LocateStage.timeout, 'No answer from the gateway in time'));
      final error = applyLocateFrame(const LocateProgress(LocateStage.sending), {
        'type': 'error',
        'message': 'Blink count must be between 1 and 65535 (got 0).',
      });
      expect((error.stage, error.message), (LocateStage.error, 'Blink count must be between 1 and 65535 (got 0).'));
    });

    test('socket URL: wss for https bases, ws for http (port kept)', () {
      expect(locateSocketUri('https://a360demo.purpleiq.ai').toString(), 'wss://a360demo.purpleiq.ai/ws/locate');
      expect(locateSocketUri('http://172.16.100.26:5490').toString(), 'ws://172.16.100.26:5490/ws/locate');
    });
  });

  group('LocateSession (/ws/locate)', () {
    late _Timers timers;
    late List<_FakeChannel> channels;
    setUp(() {
      timers = _Timers();
      channels = [];
    });

    LocateSession session({List<Object?> failures = const [], Future<String?> Function()? refresh}) {
      var i = 0;
      return LocateSession(
        uri: Uri.parse('wss://a360demo.purpleiq.ai/ws/locate'),
        token: () async => 'tok-1',
        refreshToken: refresh ?? () async => 'tok-2',
        schedule: timers.schedule,
        connect: (uri, bearer) {
          final ch = _FakeChannel(uri, bearer, fail: i < failures.length ? failures[i] : null);
          i++;
          channels.add(ch);
          return ch;
        },
      );
    }

    test('Bearer header (no token in the URL), subscribe with the normalised tag, commands + progress', () async {
      final s = session();
      await s.open('C3:00:00:AA:BB:01');
      final ch = channels.single;
      expect(ch.bearer, 'tok-1');
      expect(ch.uri.queryParameters, isEmpty, reason: 'token only in the header');
      expect(ch.out.single, {'type': 'subscribe', 'tagid': 'c30000aabb01'});
      ch.reply(_subscribed());
      await pumpEventQueue();
      expect(s.connection, LocateConnection.subscribed);
      expect(s.gateway!.gatewayId, 'ac233fc0ffee');
      expect(s.notice, isNull);
      s.sendCommand(locateCommand(LocateMode.led, 'c30000aabb01'));
      expect(ch.out.last['type'], 'led');
      expect(s.canSend, isFalse, reason: 'one command at a time');
      ch
        ..reply({'type': 'commandSent', 'reqId': 7, 'command': 'led', 'gatewayid': 'ac233fc0ffee'})
        ..reply({'type': 'commandStatus', 'reqId': 7, 'command': 'led', 'status': 'accepted', 'code': 1})
        ..reply({'type': 'commandStatus', 'reqId': 7, 'command': 'led', 'status': 'completed', 'code': 2, 'error': 0});
      await pumpEventQueue();
      expect(s.progress.message, 'Tag is blinking');
      expect(s.canSend, isTrue);
      // Turn off: buzzer cycles 0, "Buzzer off" when done.
      s.sendCommand(turnOffCommand('c30000aabb01'), turnOff: true);
      expect(ch.out.last, {'type': 'buzzer', 'tagid': 'c30000aabb01', 'cycles': 0});
      ch
        ..reply({'type': 'commandSent', 'reqId': 8, 'command': 'buzzer', 'gatewayid': 'ac233fc0ffee'})
        ..reply({'type': 'commandStatus', 'reqId': 8, 'command': 'buzzer', 'status': 'completed'});
      await pumpEventQueue();
      expect(s.progress.message, 'Buzzer off');
      await s.close();
      expect(ch.closed, isTrue);
      s.dispose();
    });

    test('mqttConnected false → "Locating service is offline"; error frames show their message', () async {
      final s = session();
      await s.open('c30000aabb01');
      channels.single.reply(_subscribed(mqtt: false));
      await pumpEventQueue();
      expect(s.offline, isTrue);
      expect(s.notice, 'Locating service is offline');
      channels.single.reply({'type': 'error', 'message': "No NetworkTimeSeries data with a gatewayid for tag 'x'."});
      await pumpEventQueue();
      expect(s.notice, startsWith('No NetworkTimeSeries data'));
      s.dispose();
    });

    test('no answer → local timeout "No answer from the gateway in time"', () async {
      final s = session();
      await s.open('c30000aabb01');
      channels.single.reply(_subscribed());
      await pumpEventQueue();
      s.sendCommand(locateCommand(LocateMode.buzzer, 'c30000aabb01'));
      channels.single.reply({'type': 'commandSent', 'reqId': 9, 'command': 'buzzer', 'gatewayid': 'gw'});
      await pumpEventQueue();
      timers.fire();
      expect(s.progress.stage, LocateStage.timeout);
      expect(s.progress.message, 'No answer from the gateway in time');
      s.dispose();
    });

    test('401 on the upgrade → reconnect with a fresh token, then with ?access_token= as a last resort', () async {
      const unauthorized = WebSocketException('Connection was not upgraded to websocket, HTTP status code: 401', 401);
      final s = session(failures: [unauthorized, unauthorized]);
      await s.open('c30000aabb01');
      await pumpEventQueue();
      expect(channels, hasLength(3));
      expect(channels[0].bearer, 'tok-1');
      expect(channels[1].bearer, 'tok-2', reason: 'fresh token');
      expect(channels[2].bearer, isNull);
      expect(channels[2].uri.queryParameters['access_token'], 'tok-2');
      expect(channels[2].out.single['type'], 'subscribe');
      s.dispose();
    });
  });

  // ------------------------------------------------------------------ polling

  group('overview polling', () {
    late FakeAssetBackend backend;
    late _Timers timers;
    setUp(() {
      backend = FakeAssetBackend()..seedRtl();
      timers = _Timers();
      TrkLiveNotifier.debugSchedule = timers.schedule;
    });
    tearDown(() => TrkLiveNotifier.debugSchedule = null);

    int overviewCalls() => backend.requests.where((o) => o.uri.path == RtlApi.overviewPath).length;

    test('polls every 10 s while attached, stops when detached; scope sent as siteId / floorPlanId', () async {
      final c = liveContainer(backend);
      addTearDown(c.dispose);
      final n = c.read(rtlProvider.notifier);
      final owner = Object();
      n.attach(owner);
      await pumpEventQueue();
      expect(overviewCalls(), 1);
      expect(c.read(rtlProvider).overview!.kpis.assetsInView, 3);
      expect(c.read(rtlProvider).live, isTrue);
      expect(timers.live(RtlNotifier.interval), 1);
      timers.fire(RtlNotifier.interval);
      await pumpEventQueue();
      expect(overviewCalls(), 2);
      n.setScope(siteId: 'site-1', floorPlanId: 'fp-001');
      await pumpEventQueue();
      final q = backend.requests.last.uri.queryParameters;
      expect((q['siteId'], q['floorPlanId']), ('site-1', 'fp-001'));
      n.detach(owner);
      await pumpEventQueue();
      expect(c.read(rtlProvider).polling, isFalse);
      expect(timers.live(RtlNotifier.interval), 0, reason: 'timer cancelled');
      final before = overviewCalls();
      timers.fire();
      await pumpEventQueue();
      expect(overviewCalls(), before);
    });

    test('a failed round → Paused (error kept), the last overview stays', () async {
      final c = liveContainer(backend);
      addTearDown(c.dispose);
      final owner = Object();
      final n = c.read(rtlProvider.notifier)..attach(owner);
      await pumpEventQueue();
      backend.failNext['GET ${RtlApi.overviewPath}'] = const FakeReply(500, {'message': 'boom'});
      timers.fire(RtlNotifier.interval);
      await pumpEventQueue();
      expect(c.read(rtlProvider).live, isFalse);
      expect(c.read(rtlProvider).overview, isNotNull);
      n.detach(owner);
    });

    test('events paging / filter and trail 404 through the API', () async {
      final c = liveContainer(backend);
      addTearDown(c.dispose);
      final api = c.read(rtlApiProvider);
      final p = await api.getEvents(range: '7d', page: 2, pageSize: 25, eventType: 'Zone Entry');
      final q = backend.requests.last.uri.queryParameters;
      expect(q, containsPair('range', '7d'));
      expect(q, containsPair('page', '2'));
      expect(q, containsPair('pageSize', '25'));
      expect(q, containsPair('eventType', 'Zone Entry'));
      expect(p.items.first.eventId, 'ENT-25');
      expect(await api.getEvents(pageSize: 500).then((_) => backend.requests.last.uri.queryParameters['pageSize']), '100');
      final t = await api.getTrail('AR-000123', range: '1h');
      expect(backend.requests.last.uri.queryParameters['range'], '1h');
      expect(t.segments, hasLength(2));
      await expectLater(
        api.getTrail('AR-NOTAG'),
        throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.notFound)),
      );
    });
  });

  // ------------------------------------------------------------------ tenant params

  group('clientId / tenantId', () {
    test('from the sign-in reply user (verify-totp) and /me', () {
      final t = TokenPair.tryParse({
        'accessToken': 'eyJ.a.b',
        'refreshToken': 'r-1',
        'accessTokenExpiresAt': '2026-11-19T12:00:00Z',
        'user': {'userName': 'demo.user', 'clientId': 'CLT-1001', 'tenantId': 'TENANT-DEFAULT'},
      })!;
      expect((t.clientId, t.tenantId), ('CLT-1001', 'TENANT-DEFAULT'));
      final me = CurrentUser.fromJson({'userName': 'demo.user', 'clientId': 'CLT-9', 'tenantId': 'T-9'});
      expect((me.clientId, me.tenantId), ('CLT-9', 'T-9'));
      expect(const CurrentUser(fullName: 'x').withTenant('C', 'T').clientId, 'C');
      expect(me.withTenant('C', 'T').clientId, 'CLT-9', reason: '/me wins');
    });

    test('sent on Tracking + Administration calls only, never when unknown', () async {
      final backend = FakeAssetBackend()..seedRtl();
      final c = liveContainer(backend, overrides: [authProvider.overrideWith(_AuthIn.new)]);
      addTearDown(c.dispose);
      await c.read(rtlApiProvider).getOverview(floorPlanId: 'fp-001');
      await c.read(adminMapsApiProvider).getFloorPlans();
      await c.read(masterDataRepositoryProvider).list('/api/master-data/sites', MdSite.fromJson);
      Map<String, String> q(String host) => backend.requests.lastWhere((o) => o.uri.host == host).uri.queryParameters;
      expect(q('trk.test'), allOf(containsPair('clientId', 'CLT-1001'), containsPair('tenantId', 'TENANT-DEFAULT')));
      expect(q('trk.test'), containsPair('floorPlanId', 'fp-001'));
      expect(q('admin.test'), allOf(containsPair('clientId', 'CLT-1001'), containsPair('tenantId', 'TENANT-DEFAULT')));
      expect(q('md.test').keys, isNot(contains('clientId')));
      // Signed out: no tenant parameters at all.
      final out = liveContainer(backend);
      addTearDown(out.dispose);
      await out.read(rtlApiProvider).getOverview();
      expect(backend.requests.last.uri.queryParameters.keys, isNot(contains('clientId')));
    });
  });

  // ------------------------------------------------------------------ screens

  group('screens (API mode)', () {
    late FakeAssetBackend backend;
    late _Timers timers;
    late List<_FakeChannel> channels;

    setUp(() {
      backend = FakeAssetBackend()
        ..seedTracking(DateTime.now)
        ..seedRtl();
      timers = _Timers();
      channels = [];
      debugApiMode = true;
      NetworkNotifier.listenToDevice = false;
      TrkLiveNotifier.debugSchedule = timers.schedule;
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
      final png = base64Decode(rtlPng1x1);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            ...backend.overrides,
            sessionProvider.overrideWith(_SignedIn.new),
            authProvider.overrideWith(_AuthIn.new),
            tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
            planImageProvider.overrideWith((ref, url) async => PlanImage(Uint8List.fromList(png), 600, 400)),
            locateConnectorProvider.overrideWithValue((uri, bearer) {
              final ch = _FakeChannel(uri, bearer);
              channels.add(ch);
              return ch;
            }),
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
      await tester.scrollUntilVisible(f, 300, scrollable: page());
      await tester.pumpAndSettle();
    }

    Future<void> top(WidgetTester tester) async {
      await tester.fling(page(), const Offset(0, 3000), 3000);
      await tester.pumpAndSettle();
    }

    /// Scrolls the open bottom sheet until [f] is built and visible.
    Future<void> inSheet(WidgetTester tester, Finder f, {double delta = 200}) async {
      final sheet = find.descendant(of: find.byType(BottomSheet).last, matching: find.byType(Scrollable)).first;
      await tester.scrollUntilVisible(f, delta, scrollable: sheet);
      await tester.pumpAndSettle();
    }

    testWidgets('Live Map = Real-Time Location: counters, gateways, markers on plots, restricted zone, not on plan',
        (tester) async {
      final c = await open(tester, Routes.tracking);
      expect(find.byKey(const ValueKey('rtl-live')), findsOneWidget);
      expect(find.text('LIVE'), findsOneWidget);
      expect(find.text('Assets in View'), findsOneWidget);
      expect(find.text('Location Alerts'), findsOneWidget);
      // The Published plan of the default floor (Ground Floor, the first with a plan) is polled.
      final q = backend.requests.lastWhere((o) => o.uri.path == RtlApi.overviewPath).uri.queryParameters;
      expect(q['floorPlanId'], 'fp-001');
      expect(c.read(rtlProvider).polling, isTrue);
      expect(find.byKey(const ValueKey('rtl-gw-ac233fc0ffee')), findsOneWidget);
      expect(find.byKey(const ValueKey('rtl-asset-AR-000123')), findsOneWidget);
      expect(find.byKey(const ValueKey('rtl-asset-AR-000124')), findsOneWidget, reason: 'matched on the plot code');
      expect(find.byKey(const ValueKey('rtl-asset-AR-000125')), findsNothing, reason: 'gateway not plotted');
      expect(find.text('Restricted Zone'), findsOneWidget);
      await see(tester, find.byKey(const ValueKey('rtl-not-on-plan')));
      expect(find.text('Not on this floor plan (1)'), findsOneWidget);
      // Tap a marker → card with comparison + trail.
      await top(tester);
      await tester.ensureVisible(find.byKey(const ValueKey('rtl-asset-AR-000123')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('rtl-asset-AR-000123')));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('rtl-callout')), findsOneWidget);
      await inSheet(tester, find.text('Location Comparison'));
      expect(find.text('Match'), findsOneWidget);
      await inSheet(tester, find.text('Hall A · Ground Floor'));
      expect(find.textContaining('already there'), findsOneWidget);
      // Leaving the screen stops polling.
      c.read(routerProvider).go(Routes.home);
      await tester.pumpAndSettle();
      expect(c.read(rtlProvider).polling, isFalse);
    });

    testWidgets('counters filter the list; search; trail 404 says "no tracked tag"', (tester) async {
      await open(tester, Routes.tracking);
      await see(tester, find.byKey(const ValueKey('rtl-row-AR-000125')));
      expect(find.text('Showing 3 of 3'), findsOneWidget);
      await top(tester);
      await tester.tap(find.text('Moving'));
      await tester.pumpAndSettle();
      await see(tester, find.text('Showing 1 of 3'));
      expect(find.byKey(const ValueKey('rtl-row-AR-000123')), findsOneWidget);
      expect(find.byKey(const ValueKey('rtl-row-AR-000124')), findsNothing);
      await top(tester);
      await tester.tap(find.text('Moving'));
      await tester.pumpAndSettle();
      await see(tester, find.byKey(const ValueKey('rtl-row-AR-000125')));
      await tester.tap(find.byKey(const ValueKey('rtl-row-AR-000125')));
      await tester.pumpAndSettle();
      await inSheet(tester, find.text('Mismatch'));
      await inSheet(tester, find.text('This asset has no tracked tag'));
    });

    testWidgets('empty overview → "No assets in view yet"; no floor plan → message', (tester) async {
      backend
        ..seedRtl(overview: rtlEmptyOverviewJson)
        ..on('GET', '/api/administration/maps/floor-plans', (_) => const FakeReply(200, <Object>[]));
      // No floor picture either.
      backend.on('GET', '/api/master-data/floors', (_) => FakeReply(200, [rtlFloorsJson.first]));
      await open(tester, Routes.tracking);
      expect(find.text('No floor plan for this floor'), findsOneWidget);
      await see(tester, find.text('No assets in view yet'));
    });

    testWidgets('events: rows, type filter, View all pages with Load more', (tester) async {
      final c = await open(tester, Routes.tracking);
      await see(tester, find.byKey(const ValueKey('rtl-event-ENT-0')));
      expect(find.text('Showing 10 of 37'), findsOneWidget);
      final q = backend.requests.lastWhere((o) => o.uri.path == RtlApi.eventsPath).uri.queryParameters;
      expect((q['range'], q['pageSize'], q['page']), ('24h', '10', '1'));
      c.read(routerProvider).push(Routes.trackingEvents(range: '7d', type: 'Zone Entry'));
      await tester.pumpAndSettle();
      var last = backend.requests.lastWhere((o) => o.uri.path == RtlApi.eventsPath).uri.queryParameters;
      expect((last['range'], last['eventType'], last['pageSize']), ('7d', 'Zone Entry', '25'));
      await see(tester, find.byKey(const ValueKey('events-more')));
      await tester.tap(find.byKey(const ValueKey('events-more')));
      await tester.pumpAndSettle();
      last = backend.requests.lastWhere((o) => o.uri.path == RtlApi.eventsPath).uri.queryParameters;
      expect(last['page'], '2');
      await see(tester, find.text('Showing 37 of 37'));
    });

    testWidgets('Locate Asset sheet → LED panel: validation, Send, progress, Turn off, History', (tester) async {
      await open(tester, Routes.tracking);
      await tester.tap(find.byKey(const ValueKey('rtl-locate')));
      await tester.pumpAndSettle();
      expect(find.text('Locate Asset'), findsWidgets);
      // CART-03 has no tag: the indicator is disabled.
      final cart = tester.widget<IconButton>(find.byKey(const ValueKey('locate-led-AR-000125')));
      expect(cart.onPressed, isNull);
      await tester.tap(find.byKey(const ValueKey('locate-led-AR-000123')));
      await tester.pumpAndSettle();
      final ch = channels.single;
      expect(ch.bearer, 'tok-1');
      expect(ch.out.single, {'type': 'subscribe', 'tagid': 'c30000aabb01'});
      ch.reply(_subscribed());
      await tester.pumpAndSettle();
      expect(find.text('Tag c30000aabb01 · Nearest gateway ac233fc0ffee · -61 dBm'), findsOneWidget);
      expect(find.byKey(const ValueKey('locate-tag-picker')), findsOneWidget, reason: 'two tags');
      await inSheet(tester, find.text('LED runs 6.0 s'));
      // Invalid blink count → message, Send disabled.
      await inSheet(tester, find.byKey(const ValueKey('led-cycles')));
      await tester.enterText(find.byKey(const ValueKey('led-cycles')), '0');
      await tester.pumpAndSettle();
      expect(find.text('Enter a value from 1 to 65535'), findsOneWidget);
      expect(tester.widget<FilledButton>(find.byKey(const ValueKey('locate-send'))).onPressed, isNull);
      await inSheet(tester, find.byKey(const ValueKey('pattern-Fast blink')), delta: -200);
      await tester.tap(find.byKey(const ValueKey('pattern-Fast blink')));
      await tester.pumpAndSettle();
      await inSheet(tester, find.text('LED runs 10.0 s'));
      await inSheet(tester, find.byKey(const ValueKey('locate-send')));
      await tester.tap(find.byKey(const ValueKey('locate-send')));
      await tester.pump();
      expect(ch.out.last, {
        'type': 'led',
        'tagid': 'c30000aabb01',
        'color': 3,
        'onMs': 200,
        'offMs': 200,
        'cycles': 25,
        'brightness': 100,
      });
      ch.reply({'type': 'commandSent', 'reqId': 11, 'command': 'led', 'gatewayid': 'ac233fc0ffee'});
      await tester.pump();
      expect(find.text('Sent to gateway ac233fc0ffee'), findsOneWidget);
      ch.reply({'type': 'commandStatus', 'reqId': 11, 'command': 'led', 'status': 'completed'});
      await tester.pumpAndSettle();
      expect(find.text('Tag is blinking'), findsOneWidget);
      await tester.ensureVisible(find.byKey(const ValueKey('locate-off')));
      await tester.tap(find.byKey(const ValueKey('locate-off')));
      await tester.pump();
      expect(ch.out.last, {'type': 'buzzer', 'tagid': 'c30000aabb01', 'cycles': 0});
      expect(find.text('The LED stops by itself after 10.0 s'), findsOneWidget);
      ch
        ..reply({'type': 'commandSent', 'reqId': 12, 'command': 'buzzer', 'gatewayid': 'ac233fc0ffee'})
        ..reply({'type': 'error', 'message': 'No readings for this tag yet'});
      await tester.pumpAndSettle();
      expect(find.text('No readings for this tag yet'), findsOneWidget);
      // Both: nested payload.
      await inSheet(tester, find.text('Both'), delta: -200);
      await tester.tap(find.text('Both'));
      await tester.pumpAndSettle();
      await inSheet(tester, find.byKey(const ValueKey('locate-send')));
      await tester.tap(find.byKey(const ValueKey('locate-send')));
      await tester.pump();
      expect(ch.out.last['type'], 'both');
      expect(ch.out.last['led'], isA<Map<String, dynamic>>());
      expect(ch.out.last['buzzer'], {'beepMs': 300, 'pauseMs': 300, 'cycles': 5, 'level': 0});
      ch.reply({'type': 'commandSent', 'reqId': 13, 'command': 'both', 'gatewayid': 'gw'});
      ch.reply({'type': 'commandStatus', 'reqId': 13, 'command': 'both', 'status': 'timeout'});
      await tester.pumpAndSettle();
      expect(find.text('No answer from the gateway in time'), findsOneWidget);
      // History.
      await inSheet(tester, find.byKey(const ValueKey('locate-history')));
      await tester.drag(
        find.descendant(of: find.byType(BottomSheet).last, matching: find.byType(Scrollable)).first,
        const Offset(0, -400),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('locate-history')));
      await tester.pumpAndSettle();
      expect(find.text('Locate history'), findsOneWidget);
      expect(find.text('completed'), findsOneWidget);
      expect(find.textContaining('SERVER-SECRET'), findsNothing);
      // Closing the panel closes the socket.
      await tester.tapAt(const Offset(20, 20));
      await tester.pumpAndSettle();
      await tester.tapAt(const Offset(20, 20));
      await tester.pumpAndSettle();
      expect(find.byType(LocatePanel), findsNothing);
      expect(ch.closed, isTrue);
    });

    testWidgets('Locate target: closes the sheet, selects the asset and opens its card', (tester) async {
      await open(tester, Routes.tracking);
      await tester.tap(find.byKey(const ValueKey('rtl-locate')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const ValueKey('locate-search')), 'fork');
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('locate-row-AR-000123')), findsNothing);
      await tester.tap(find.byKey(const ValueKey('locate-target-AR-000124')));
      await tester.pumpAndSettle();
      expect(find.text('FORK-02'), findsWidgets);
      await inSheet(tester, find.text('Location Comparison'));
      expect(find.text('Not set'), findsWidgets);
    });

    testWidgets('View on Map deep link (view=rtl) selects the asset on the Real-Time Location map', (tester) async {
      await open(tester, Routes.trackingMap(view: 'rtl', asset: 'AR-000123'));
      expect(find.byKey(const ValueKey('rtl-live')), findsOneWidget);
      expect(find.byKey(const ValueKey('rtl-callout')), findsOneWidget);
    });

    /// The overview with AR000001 (the fake Asset service's asset) as PUMP-01, with or without tags.
    Map<String, Object?> overviewWithDetailAsset({required bool tags}) {
      final o = Map<String, Object?>.from(rtlOverviewJson);
      final first = Map<String, Object?>.from((o['assets']! as List).first as Map)
        ..['assetId'] = 'AR000001'
        ..['tags'] = tags ? ['C30000AABB01'] : <String>[]
        ..['tracker'] = tags ? 'C30000AABB01' : null;
      o['assets'] = [first];
      return o;
    }

    Future<void> seeTile(WidgetTester tester) async {
      await see(tester, find.byKey(const ValueKey('action-locate')));
    }

    testWidgets('asset detail: Locate tile opens the panel when the asset has a tag; back / × close it', (tester) async {
      backend.seedRtl(overview: overviewWithDetailAsset(tags: true));
      await open(tester, Routes.assetDetail('AR000001'));
      await seeTile(tester);
      final tile = tester.widget<ActionTile>(find.byKey(const ValueKey('action-locate')));
      expect(tile.label, 'Locate');
      expect(tile.onTap, isNotNull);
      await tester.tap(find.byKey(const ValueKey('action-locate')));
      await tester.pumpAndSettle();
      expect(find.byType(LocatePanel), findsOneWidget);
      expect(find.byKey(const ValueKey('locate-back')), findsNothing, reason: 'not opened from the asset list');
      expect(find.text('LED settings'), findsOneWidget);
      expect(channels.single.out.single, {'type': 'subscribe', 'tagid': 'c30000aabb01'});
      await tester.tap(find.byKey(const ValueKey('locate-close')));
      await tester.pumpAndSettle();
      expect(find.byType(LocatePanel), findsNothing);
      expect(channels.single.closed, isTrue);
    });

    testWidgets('asset detail: no tag → Locate tile disabled with "No tag linked"', (tester) async {
      backend.seedRtl(overview: overviewWithDetailAsset(tags: false));
      await open(tester, Routes.assetDetail('AR000001'));
      await seeTile(tester);
      final tile = tester.widget<ActionTile>(find.byKey(const ValueKey('action-locate')));
      expect(tile.label, 'No tag linked');
      expect(tile.onTap, isNull);
      await tester.tap(find.byKey(const ValueKey('action-locate')), warnIfMissed: false);
      await tester.pumpAndSettle();
      expect(find.byType(LocatePanel), findsNothing);
    });

    testWidgets('asset detail: Tracking locked → Locate shows the licence sheet, no Tracking call', (tester) async {
      debugLicensedModules = {...defaultLicensedModules}..remove(AppModule.tracking);
      addTearDown(() => debugLicensedModules = AppModule.values.toSet());
      await open(tester, Routes.assetDetail('AR000001'));
      await seeTile(tester);
      await tester.tap(find.byKey(const ValueKey('action-locate')));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('licence-sheet-tracking')), findsOneWidget);
      expect(backend.requests.any((o) => o.uri.path == RtlApi.overviewPath), isFalse);
    });

    testWidgets('Locate Asset list → panel: ← returns to the list, × closes both', (tester) async {
      await open(tester, Routes.tracking);
      await tester.tap(find.byKey(const ValueKey('rtl-locate')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('locate-led-AR-000123')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('locate-back')));
      await tester.pumpAndSettle();
      expect(find.byType(LocatePanel), findsNothing);
      expect(find.byKey(const ValueKey('locate-search')), findsOneWidget, reason: 'back on the asset list');
      await tester.tap(find.byKey(const ValueKey('locate-led-AR-000123')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('locate-close')));
      await tester.pumpAndSettle();
      expect(find.byType(LocatePanel), findsNothing);
      expect(find.byKey(const ValueKey('locate-search')), findsNothing);
    });

    testWidgets('Locate panel: a quick pattern in Buzzer mode switches to LED and fills the LED fields', (tester) async {
      await open(tester, Routes.tracking);
      await tester.tap(find.byKey(const ValueKey('rtl-locate')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('locate-led-AR-000123')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Buzzer'));
      await tester.pumpAndSettle();
      SegmentedButton<LocateMode> mode() => tester.widget(find.byKey(const ValueKey('locate-mode')));
      expect(mode().selected, {LocateMode.buzzer});
      await tester.tap(find.byKey(const ValueKey('pattern-Slow blink')));
      await tester.pumpAndSettle();
      expect(mode().selected, {LocateMode.led});
      String text(String key) => tester.widget<TextField>(find.byKey(ValueKey(key))).controller!.text;
      await inSheet(tester, find.byKey(const ValueKey('led-cycles')));
      expect((text('led-on'), text('led-off'), text('led-cycles')), ('1000', '1000', '15'));
    });

    testWidgets('Locations toggle keeps the location tree / floor-plan views', (tester) async {
      await open(tester, Routes.tracking);
      await tester.tap(find.text('Locations'));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('rtl-live')), findsNothing);
    });
  });
}
