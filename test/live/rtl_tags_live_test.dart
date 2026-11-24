import 'dart:async';
import 'dart:convert';

import 'package:asset360_mobile/data/api/auth_models.dart';
import 'package:asset360_mobile/data/api/dashboard/network_status_live.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_asset_backend.dart';
import 'rtl_samples.dart';

class _AuthIn extends AuthNotifier {
  @override
  AuthState build() => AuthSignedIn(
    user: const CurrentUser(fullName: 'Demo User', clientId: 'CLT-1001', tenantId: 'TENANT-DEFAULT'),
    tokens: TokenPair(accessToken: 'tok-1', refreshToken: 'r-1', expiresAt: DateTime.now().add(const Duration(hours: 1))),
    rememberMe: false,
  );
}

/// Timers the notifier / socket schedule, fired by hand.
class _Timers {
  final pending = <(Duration, void Function(), _FakeTimer)>[];

  Timer schedule(Duration d, void Function() cb) {
    final t = _FakeTimer();
    pending.add((d, cb, t));
    return t;
  }

  void fire(Duration d) {
    final due = [for (final p in pending) if (!p.$3.cancelled && p.$1 == d) p];
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

/// A tags/live connection the test drives.
class _FakeSocket implements LiveSocket {
  _FakeSocket(this.uri, this.bearer, {this.fail});

  final Uri uri;
  final String? bearer;
  final Object? fail;
  final _in = StreamController<Object?>();
  bool closed = false;

  @override
  Future<void> get ready => fail == null ? Future.value() : Future.error(fail!);

  @override
  Stream<Object?> get messages => _in.stream;

  void send(Map<String, Object?> frame) => _in.add(jsonEncode(frame));

  void sendRaw(Object? data) => _in.add(data);

  /// The server / network ends the connection.
  Future<void> drop() => _in.close();

  @override
  Future<void> close() async {
    closed = true;
    if (!_in.isClosed) await _in.close();
  }
}

Map<String, Object?> _snapshot(List<Map<String, Object?>> tags) => {
  'type': 'snapshot',
  'total': tags.length,
  'online': tags.length,
  'plotted': tags.length,
  'withAsset': tags.length,
  'tags': tags,
};

Map<String, Object?> _update({
  List<Map<String, Object?>> changed = const [],
  List<Map<String, Object?>> seen = const [],
  List<String> removed = const [],
}) => {'type': 'update', 'changed': changed, 'seen': seen, 'removed': removed};

Map<String, Object?> _seen(String tagId, String gatewayId, {String connection = 'Online', double rssi = -50, double battery = 70}) => {
  'tagId': tagId,
  'gatewayId': gatewayId,
  'timestamp': '2026-11-19T10:15:30Z',
  'status': 'online',
  'connection': connection,
  'rssi': rssi,
  'battery': battery,
};

void main() {
  // ------------------------------------------------------------------ pure parts

  group('frames and rows', () {
    test('snapshot / update frames parse; other frames and non-JSON are ignored', () {
      final snap = TagLiveMessage.parse(jsonEncode(_snapshot([rtlTagRow('C3:00:00:AA:BB:01', 'ac233fc0ffee', assetId: 'AR-000123')])));
      expect(snap, isA<TagSnapshot>());
      final row = (snap! as TagSnapshot).tags.single;
      expect(row.key, 'c30000aabb01');
      expect(row.assetId, 'AR-000123');
      expect(row.battery, 80);
      expect(row.timestamp, DateTime.utc(2026, 11, 19, 10, 14, 52));
      final up = TagLiveMessage.parse(
        utf8.encode(jsonEncode(_update(changed: [rtlTagRow('a', 'g1')], seen: [_seen('b', 'g2')], removed: ['c']))),
      );
      expect(up, isA<TagUpdate>());
      final u = up! as TagUpdate;
      expect((u.changed.length, u.seen.single.tagId), (1, 'b'));
      expect(u.removed, ['c']);
      expect(TagLiveMessage.parse('{"type":"ping"}'), isNull);
      expect(TagLiveMessage.parse('not json'), isNull);
    });

    test('the list: snapshot replaces, update changes / seen / removed, moves are reported', () {
      final list = TagLiveList();
      expect(list.replace([TagLocation.tryParse(rtlTagRow('T1', 'gw-a'))!, TagLocation.tryParse(rtlTagRow('T2', 'gw-a'))!]), isEmpty);
      expect(list.length, 2);
      final moved = list.apply(
        TagUpdate(
          changed: [TagLocation.tryParse(rtlTagRow('t1', 'gw-b'))!, TagLocation.tryParse(rtlTagRow('T3', 'gw-a'))!],
          seen: [TagSeen.tryParse(_seen('T2', 'gw-a', rssi: -80, battery: 12))!],
          removed: ['nothing'],
        ),
      );
      expect(moved, {'t1'}, reason: 'new tags are not moves');
      expect(list['T1']!.gatewayId, 'gw-b');
      expect(list['T2']!.rssi, -80, reason: 'seen = reading fields only');
      expect(list['T2']!.gatewayId, 'gw-a');
      expect(list['T2']!.zoneName, 'Hall A');
      list.apply(const TagUpdate(removed: ['T2']));
      expect(list['T2'], isNull);
      expect(list.length, 2);
      // A new snapshot replaces everything; a tag on another gateway is a move.
      expect(list.replace([TagLocation.tryParse(rtlTagRow('T1', 'gw-a'))!]), {'t1'});
      expect(list.length, 1);
    });

    test('socket URL: wss for https, same filters as GET /tags, no token in it', () {
      final u = tagsLiveUri('https://a360demo.purpleiq.ai', siteId: 'site-1', floorPlanId: 'fp-001');
      expect((u.scheme, u.host, u.path), ('wss', 'a360demo.purpleiq.ai', '/api/real-time-location/tags/live'));
      expect(u.queryParameters, {'minutes': '1440', 'siteId': 'site-1', 'floorPlanId': 'fp-001'});
      final z = tagsLiveUri('http://172.16.100.29:5490', zoneId: 'z', zonedOnly: true, minutes: 60);
      expect((z.scheme, z.port), ('ws', 5490));
      expect(z.queryParameters, {'minutes': '60', 'zoneId': 'z', 'zonedOnly': 'true'});
      expect(withAccessToken(u, 'tok').queryParameters['access_token'], 'tok');
    });

    test('overlay: location, freshness, rssi, battery; Moving only for a recent move; counters recomputed', () {
      final base = RtlOverview.fromJson(rtlOverviewJson);
      final now = DateTime.utc(2026, 11, 19, 10, 16);
      final list = TagLiveList()
        ..replace([
          TagLocation.tryParse(rtlTagRow('C30000AABB01', 'ac233fc0beef', zoneId: 'z2', zoneName: 'Hall B', rssi: -55, battery: 40))!,
        ]);
      final moving = overlayTags(base, list, {'c30000aabb01': now.subtract(const Duration(seconds: 30))}, now);
      final a = moving.asset('AR-000123')!;
      expect((a.movement, a.freshness), ('Moving', 'Live'));
      expect((a.location!.gatewayId, a.location!.zoneName, a.rssi, a.battery), ('ac233fc0beef', 'Hall B', -55, 40));
      expect(a.confidence, 'High', reason: 'overview fields stay');
      expect(a.activeLocationAlerts, 1);
      expect(moving.asset('AR-000124'), same(base.asset('AR-000124')), reason: 'no tag row → untouched');
      expect((moving.kpis.moving, moving.kpis.stationary, moving.kpis.unknown, moving.kpis.assetsInView), (1, 1, 1, 3));
      expect(moving.kpis.locationAlerts, 1);
      final still = overlayTags(base, list, {'c30000aabb01': now.subtract(const Duration(seconds: 61))}, now);
      expect(still.asset('AR-000123')!.movement, 'Stationary');
      expect((still.kpis.moving, still.kpis.stationary), (0, 2));
      expect(identical(overlayTags(base, TagLiveList(), {}, now), base), isTrue);
    });
  });

  // ------------------------------------------------------------------ the socket client

  group('TagsLiveSocket', () {
    test('token as Bearer header and ?access_token=, backoff 1 → 30 s with a fresh token each time, frames reset it', () async {
      final timers = _Timers();
      final sockets = <_FakeSocket>[];
      var tokens = 0;
      final got = <TagLiveMessage>[];
      final states = <bool>[];
      final s = TagsLiveSocket(
        uri: tagsLiveUri('https://h.example'),
        token: () async => 'tok-${++tokens}',
        onMessage: got.add,
        onConnection: states.add,
        schedule: timers.schedule,
        connect: (uri, token) {
          final f = _FakeSocket(uri, token, fail: sockets.length < 3 ? const FormatException('refused') : null);
          sockets.add(f);
          return f;
        },
      )..start();
      await pumpEventQueue();
      expect(sockets.first.bearer, 'tok-1');
      expect(sockets.first.uri.queryParameters['access_token'], 'tok-1');
      expect(states, isEmpty, reason: 'never connected');
      expect(timers.live(const Duration(seconds: 1)), 1);
      timers.fire(const Duration(seconds: 1));
      await pumpEventQueue();
      expect(sockets[1].bearer, 'tok-2', reason: 'a fresh token for every attempt');
      expect(timers.live(const Duration(seconds: 2)), 1);
      timers.fire(const Duration(seconds: 2));
      await pumpEventQueue();
      expect(timers.live(const Duration(seconds: 4)), 1);
      timers.fire(const Duration(seconds: 4));
      await pumpEventQueue();
      expect(states, [true]);
      expect(s.connected, isTrue);
      sockets.last.send(_snapshot([rtlTagRow('t', 'g')]));
      await pumpEventQueue();
      expect(got.single, isA<TagSnapshot>());
      expect(s.failures, 0);
      await sockets.last.drop();
      await pumpEventQueue();
      expect(states, [true, false]);
      expect(timers.live(const Duration(seconds: 1)), 1, reason: 'backoff restarted after a frame');
      // The ladder tops out at 30 s.
      s.failures = 9;
      expect(s.nextDelay, const Duration(seconds: 30));
      await s.stop();
      expect(timers.live(const Duration(seconds: 1)), 0);
      expect(sockets.last.closed, isTrue);
    });
  });

  // ------------------------------------------------------------------ the notifier

  group('Live Map: socket over the overview', () {
    late FakeAssetBackend backend;
    late _Timers timers;
    late List<_FakeSocket> sockets;
    late DateTime now;

    setUp(() {
      backend = FakeAssetBackend()..seedRtl();
      timers = _Timers();
      sockets = [];
      now = DateTime.utc(2026, 11, 19, 10, 15, 0);
      TrkLiveNotifier.debugSchedule = timers.schedule;
      RtlNotifier.debugNow = () => now;
      RtlNotifier.debugConnector = (uri, token) {
        final s = _FakeSocket(uri, token);
        sockets.add(s);
        return s;
      };
    });

    tearDown(() {
      TrkLiveNotifier.debugSchedule = null;
      RtlNotifier.debugNow = null;
      // flutter_test_config.dart's idle socket again.
      RtlNotifier.debugConnector = (_, _) => _Idle();
    });

    int calls(String path) => backend.requests.where((o) => o.uri.path == path).length;

    Future<(ProviderContainer, RtlNotifier, Object)> attached() async {
      final c = liveContainer(backend, overrides: [authProvider.overrideWith(_AuthIn.new)]);
      addTearDown(c.dispose);
      final n = c.read(rtlProvider.notifier);
      final owner = Object();
      n.attach(owner);
      await pumpEventQueue();
      return (c, n, owner);
    }

    test('connects with the Bearer token + ?access_token= and the map filters; first the /tags list, then Live', () async {
      final (c, n, owner) = await attached();
      expect(calls('/api/real-time-location/tags'), 1, reason: 'initial list from GET /tags before the snapshot');
      expect(sockets, hasLength(1));
      expect(sockets.single.bearer, 'tok-1');
      expect(sockets.single.uri.scheme, 'wss');
      expect(sockets.single.uri.path, '/api/real-time-location/tags/live');
      expect(sockets.single.uri.queryParameters['access_token'], 'tok-1');
      expect(sockets.single.uri.queryParameters['minutes'], '1440');
      expect(c.read(rtlProvider).live, isTrue);
      n.setScope(siteId: 'site-1', floorPlanId: 'fp-001');
      await pumpEventQueue();
      expect(sockets, hasLength(2), reason: 'the scope changed: reconnect with the new filters');
      expect(sockets.first.closed, isTrue);
      final q = sockets.last.uri.queryParameters;
      expect((q['siteId'], q['floorPlanId']), ('site-1', 'fp-001'));
      expect(calls('/api/real-time-location/tags'), 1, reason: 'socket up: no extra poll once connected');
      n.detach(owner);
    });

    test('snapshot replaces the tag list: map data, counters and Moving / Stationary follow it', () async {
      final (c, n, owner) = await attached();
      expect(c.read(rtlProvider).overview!.kpis.moving, 1, reason: 'the overview alone');
      sockets.single.send(
        _snapshot([
          rtlTagRow('C30000AABB01', 'ac233fc0ffee', rssi: -48, battery: 66),
          rtlTagRow('C30000AABB03', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -72),
        ]),
      );
      await pumpEventQueue();
      final o = c.read(rtlProvider).overview!;
      expect(o.asset('AR-000123')!.rssi, -48);
      expect(o.asset('AR-000123')!.battery, 66);
      expect(o.asset('AR-000124')!.location!.zoneName, 'Hall B');
      // No move seen on the socket: Stationary while live (the web's rule), not the server's 5 min Moving.
      expect((o.kpis.moving, o.kpis.stationary, o.kpis.unknown), (0, 2, 1));
      n.detach(owner);
    });

    test('update: changed (full row → Moving for 60 s), seen (reading only), removed (drops back to the overview)', () async {
      final (c, n, owner) = await attached();
      final s = sockets.single;
      s.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee'), rtlTagRow('C30000AABB03', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -72)]));
      await pumpEventQueue();
      // changed: PUMP-01's tag is now heard by the other gateway.
      s.send(_update(changed: [rtlTagRow('C30000AABB01', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -58)]));
      await pumpEventQueue();
      var a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.movement, a.location!.gatewayId, a.location!.zoneName, a.rssi), ('Moving', 'ac233fc0beef', 'Hall B', -58));
      expect(c.read(rtlProvider).overview!.kpis.moving, 1);
      // 61 s later the move expires on its own (no frame needed).
      now = now.add(const Duration(seconds: 61));
      timers.fire(const Duration(seconds: 61));
      a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect(a.movement, 'Stationary');
      expect(c.read(rtlProvider).overview!.kpis.moving, 0);
      // seen: same gateway, only the reading moved on; Idle = Stale, so movement is Unknown.
      s.send(_update(seen: [_seen('C30000AABB03', 'ac233fc0beef', connection: 'Idle', rssi: -80, battery: 9)]));
      await pumpEventQueue();
      final f = c.read(rtlProvider).overview!.asset('AR-000124')!;
      expect((f.freshness, f.movement, f.rssi, f.battery), ('Stale', 'Unknown', -80, 9));
      expect(f.location!.zoneName, 'Hall B', reason: 'seen never changes the place');
      expect(f.location!.at, DateTime.utc(2026, 11, 19, 10, 15, 30));
      // removed: the tag left the window → the asset shows the overview's values again.
      s.send(_update(removed: ['c30000aabb03']));
      await pumpEventQueue();
      final back = c.read(rtlProvider).overview!.asset('AR-000124')!;
      final orig = RtlOverview.fromJson(rtlOverviewJson).asset('AR-000124')!;
      expect((back.freshness, back.movement, back.rssi), ('Live', 'Stationary', orig.rssi));
      expect(back.battery, isNull);
      n.detach(owner);
    });

    test('overview refresh every 30 s keeps alerts / confidence and the live rows over it', () async {
      final (c, n, owner) = await attached();
      sockets.single.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee', rssi: -40)]));
      await pumpEventQueue();
      final before = calls('/api/real-time-location/overview');
      timers.fire(RtlNotifier.interval);
      await pumpEventQueue();
      expect(calls('/api/real-time-location/overview'), before + 1);
      final a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect(a.rssi, -40, reason: 'the live row stays over the fresh overview');
      expect(a.activeLocationAlerts, 1);
      expect(c.read(rtlProvider).overview!.kpis.locationAlerts, 1);
      n.detach(owner);
    });

    test('socket drops → "Polling": /tags every 10 s; reconnect with a fresh token → Live again, polling stops', () async {
      final (c, n, owner) = await attached();
      expect(timers.live(RtlNotifier.tagPollInterval), 0);
      await sockets.single.drop();
      await pumpEventQueue();
      expect(c.read(rtlProvider).socketUp, isFalse);
      expect(c.read(rtlProvider).live, isFalse);
      expect(c.read(rtlProvider).polling, isTrue, reason: 'still attached: "Polling"');
      expect(calls('/api/real-time-location/tags'), 2, reason: 'one /tags round at once');
      expect(timers.live(RtlNotifier.tagPollInterval), 1);
      expect(timers.live(const Duration(seconds: 1)), 1, reason: 'reconnect after 1 s');
      // Polling replaces the rows; a tag on another gateway counts as a move.
      backend.on(
        'GET',
        '/api/real-time-location/tags',
        (_) => FakeReply(200, {
          'tags': [rtlTagRow('c30000aabb01', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -77)],
        }),
      );
      timers.fire(RtlNotifier.tagPollInterval);
      await pumpEventQueue();
      expect(calls('/api/real-time-location/tags'), 3);
      var a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.rssi, a.location!.zoneName, a.movement), (-77, 'Hall B', 'Moving'));
      // The first reconnect fails (refused), the next works.
      RtlNotifier.debugConnector = (uri, token) {
        final s = _FakeSocket(uri, token, fail: sockets.length == 1 ? const FormatException('refused') : null);
        sockets.add(s);
        return s;
      };
      timers.fire(const Duration(seconds: 1));
      await pumpEventQueue();
      expect(sockets, hasLength(2));
      expect(c.read(rtlProvider).live, isFalse);
      expect(timers.live(const Duration(seconds: 2)), 1, reason: 'backoff 1 → 2 s');
      timers.fire(const Duration(seconds: 2));
      await pumpEventQueue();
      expect(sockets, hasLength(3));
      expect(sockets.last.bearer, 'tok-1');
      expect(c.read(rtlProvider).live, isTrue);
      expect(timers.live(RtlNotifier.tagPollInterval), 0, reason: 'polling stops once connected');
      sockets.last.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee', rssi: -45)]));
      await pumpEventQueue();
      a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.rssi, a.location!.zoneName), (-45, 'Hall A'));
      n.detach(owner);
    });

    test('hidden / backgrounded: the socket is closed and nothing polls; back on screen reconnects', () async {
      final (c, n, owner) = await attached();
      n.detach(owner);
      await pumpEventQueue();
      expect(sockets.single.closed, isTrue);
      expect(c.read(rtlProvider).polling, isFalse);
      expect(c.read(rtlProvider).socketUp, isFalse);
      for (final d in [RtlNotifier.interval, RtlNotifier.tagPollInterval, const Duration(seconds: 1)]) {
        expect(timers.live(d), 0);
      }
      final before = backend.requests.length;
      timers.fire(RtlNotifier.tagPollInterval);
      await pumpEventQueue();
      expect(backend.requests.length, before);
      n.attach(Object());
      await pumpEventQueue();
      expect(sockets, hasLength(2));
      expect(c.read(rtlProvider).live, isTrue);
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
