import 'dart:async';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/licence/licence.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/core/widgets/app_header.dart' show NotificationBell;
import 'package:asset360_mobile/data/alerts/alert_feed_engine.dart';
import 'package:asset360_mobile/data/alerts/alert_platform.dart';
import 'package:asset360_mobile/data/alerts/alert_service_loop.dart';
import 'package:asset360_mobile/data/api/alerts/alerts_api.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/auth_models.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/alert_feed_repository.dart';
import 'package:asset360_mobile/data/repositories/alerts_repository.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'fake_asset_backend.dart';

// ------------------------------------------------------------------ fakes

Map<String, Object?> _alert(
  String id, {
  String status = 'Open',
  String severity = 'High',
  List<String> channels = const ['In-app', 'Push'],
  bool sound = true,
  String raisedAt = '2026-11-24T07:30:01Z',
  String asset = 'Defibrillator 04',
  String? zone = 'Zone A · floor2',
}) => {
  'alertId': id,
  'raisedAt': raisedAt,
  'severity': severity,
  'eventType': 'Geofence Exit',
  'assetId': 'AR000151',
  'assetName': asset,
  'zone': zone,
  'siteName': 'Seahouse',
  'message': '$asset left $zone',
  'status': status,
  'ruleName': '$asset leaves Zone A',
  'channels': channels,
  'sound': sound,
  'acknowledgedBy': status == 'Open' ? null : 'max.milan',
  'acknowledgedAt': status == 'Open' ? null : '2026-11-24T07:33:00Z',
};

Map<String, Object?> _feed(String serverTime, int unacknowledged, List<Map<String, Object?>> items) => {
  'serverTime': serverTime,
  'unacknowledged': unacknowledged,
  'items': items,
};

/// A scripted transport: each GET /feed answers the next prepared response; every call is kept.
class _Script implements AlertsTransport {
  _Script(this.replies);

  final List<Object> replies; // a feed map, or an Exception to throw
  final calls = <(String, Map<String, Object?>?)>[];
  int posts = 0;

  @override
  Future<Object?> get(String path, {Map<String, Object?>? query}) async {
    calls.add((path, query));
    final r = replies.removeAt(0);
    if (r is Exception) throw r;
    return r;
  }

  @override
  Future<Object?> post(String path) async {
    posts++;
    return {'acknowledged': 0};
  }
}

class _FakePlatform implements AlertPlatform {
  _FakePlatform({this.service = false, this.allowed = true, this.grant = true});

  final bool service;
  bool allowed;
  final bool grant;
  bool running = false;
  int stops = 0, starts = 0;
  AlertServiceConfig? config;
  final shown = <FeedAlert>[];
  final sent = <Map<String, Object?>>[];
  final fromServiceC = StreamController<Map<String, Object?>>.broadcast();
  final tapsC = StreamController<String>.broadcast();
  String? launched;
  int permissionRequests = 0;

  @override
  bool get supportsService => service;

  @override
  Future<void> init() async {}

  @override
  Future<bool> notificationsAllowed() async => allowed;

  @override
  Future<bool> requestNotifications() async {
    permissionRequests++;
    allowed = grant;
    return grant;
  }

  @override
  Future<void> openNotificationSettings() async {}

  @override
  Future<void> show(FeedAlert alert) async => shown.add(alert);

  @override
  Future<bool> startService(AlertServiceConfig c) async {
    config = c;
    starts++;
    return running = true;
  }

  @override
  Future<void> stopService() async {
    running = false;
    stops++;
  }

  @override
  void sendToService(Map<String, Object?> message) => sent.add(message);

  @override
  Stream<Map<String, Object?>> get fromService => fromServiceC.stream;

  @override
  Stream<String> get taps => tapsC.stream;

  @override
  Future<String?> launchedFromAlert() async => launched;
}

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

class _AuthIn extends AuthNotifier {
  @override
  AuthState build() => AuthSignedIn(
    user: const CurrentUser(fullName: 'Max Milan', clientId: 'CLT-1', tenantId: 'T-1'),
    tokens: TokenPair(accessToken: 'tok-1', refreshToken: 'r-1', expiresAt: DateTime.now().add(const Duration(hours: 1))),
    rememberMe: false,
  );

  void refreshTo(String token) => state = AuthSignedIn(
    user: (state as AuthSignedIn).user,
    tokens: TokenPair(accessToken: token, refreshToken: 'r-1', expiresAt: DateTime.now().add(const Duration(hours: 1))),
    rememberMe: false,
  );
}

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

void main() {
  // ------------------------------------------------------------------ the polling rules (docs/api/MOBILE-ALERTS.md)

  group('AlertFeedEngine: since / serverTime, de-duplication, filter, removal', () {
    test('first call without since (status=Open); every next call sends the previous serverTime as since, exactly', () async {
      final t = _Script([
        _feed('2026-11-24T07:34:38Z', 1, [_alert('AL1')]),
        _feed('2026-11-24T07:34:48.1234567Z', 1, const []),
        _feed('2026-11-24T07:34:58Z', 1, const []),
      ]);
      final e = AlertFeedEngine(AlertsApi(t));
      expect(e.since, isNull);
      await e.poll();
      expect(t.calls[0].$1, '/api/alerts/feed');
      expect(t.calls[0].$2, {'status': 'Open', 'limit': 100}, reason: 'the first call has no since');
      expect(e.since, '2026-11-24T07:34:38Z');
      await e.poll();
      expect(t.calls[1].$2, {'since': '2026-11-24T07:34:38Z', 'limit': 200}, reason: 'the server clock, no status filter');
      await e.poll();
      expect(t.calls[2].$2!['since'], '2026-11-24T07:34:48.1234567Z', reason: 'passed on as received, never reformatted');
    });

    test('a failed poll changes nothing: the same since is sent again; a reply without serverTime keeps it', () async {
      final t = _Script([
        _feed('2026-11-24T07:00:00Z', 0, const []),
        Exception('offline'),
        {'unacknowledged': 0, 'items': <Object>[]},
        _feed('2026-11-24T07:01:00Z', 0, const []),
      ]);
      final e = AlertFeedEngine(AlertsApi(t));
      await e.poll();
      await expectLater(e.poll(), throwsException);
      await e.poll();
      await e.poll();
      expect(t.calls[1].$2!['since'], '2026-11-24T07:00:00Z');
      expect(t.calls[2].$2!['since'], '2026-11-24T07:00:00Z', reason: 'the failed call did not move since');
      expect(t.calls[3].$2!['since'], '2026-11-24T07:00:00Z', reason: 'no serverTime in the reply: since stays');
      expect(e.since, '2026-11-24T07:01:00Z');
    });

    test('what was already open at the start is listed, not announced; a new Open In-app alert is announced once', () async {
      final t = _Script([
        _feed('s1', 2, [_alert('AL2', raisedAt: '2026-11-24T07:20:00Z'), _alert('AL1', raisedAt: '2026-11-24T07:10:00Z')]),
        _feed('s2', 3, [_alert('AL3', raisedAt: '2026-11-24T07:31:00Z')]),
        _feed('s3', 3, [_alert('AL3', raisedAt: '2026-11-24T07:31:00Z')]), // seen again: no second notification
        _feed('s4', 4, [_alert('AL4'), _alert('AL3')]),
      ]);
      final e = AlertFeedEngine(AlertsApi(t));
      final p1 = await e.poll();
      expect(p1.newAlerts, isEmpty);
      expect(p1.snapshot.items.map((a) => a.alertId), ['AL2', 'AL1'], reason: 'newest first');
      final p2 = await e.poll();
      expect(p2.newAlerts.map((a) => a.alertId), ['AL3']);
      expect((await e.poll()).newAlerts, isEmpty, reason: 'never notify the same alertId twice');
      expect((await e.poll()).newAlerts.map((a) => a.alertId), ['AL4'], reason: 'only the new one');
      expect(e.announced, containsAll(['AL1', 'AL2', 'AL3', 'AL4']));
    });

    test('In-app filter: only Open alerts whose channels contain "In-app" are announced', () async {
      final t = _Script([
        _feed('s1', 0, const []),
        _feed('s2', 4, [
          _alert('AL1', channels: const ['Push']),
          _alert('AL2', channels: const []),
          _alert('AL3', channels: const ['In-app']),
          _alert('AL4', status: 'Acknowledged', channels: const ['In-app']),
        ]),
      ]);
      final e = AlertFeedEngine(AlertsApi(t));
      await e.poll();
      final p = await e.poll();
      expect(p.newAlerts.map((a) => a.alertId), ['AL3']);
      expect(p.snapshot.items.map((a) => a.alertId).toSet(), {'AL1', 'AL2', 'AL3'}, reason: 'all Open ones are listed');
    });

    test('an item that comes back with another status leaves the list; an updated Open one is replaced', () async {
      final t = _Script([
        _feed('s1', 3, [_alert('AL1'), _alert('AL2'), _alert('AL3')]),
        _feed('s2', 1, [
          _alert('AL1', status: 'Acknowledged'),
          _alert('AL2', status: 'Resolved'),
          _alert('AL3', severity: 'Critical'),
        ]),
      ]);
      final e = AlertFeedEngine(AlertsApi(t));
      await e.poll();
      final p = await e.poll();
      expect(p.snapshot.items.map((a) => a.alertId), ['AL3']);
      expect(p.snapshot.items.single.severity, 'Critical');
      expect(p.snapshot.unacknowledged, 1, reason: 'the badge number is the server\'s');
    });

    test('acknowledged here: removed at once, the count follows; Acknowledge all clears', () async {
      final t = _Script([_feed('s1', 3, [_alert('AL1'), _alert('AL2'), _alert('AL3')])]);
      final e = AlertFeedEngine(AlertsApi(t));
      await e.poll();
      final one = e.acknowledgedLocally('AL2');
      expect((one.items.length, one.unacknowledged), (2, 2));
      final all = e.acknowledgedAllLocally();
      expect(all.items, isEmpty);
      expect(all.unacknowledged, 0);
    });

    test('the sound flag picks the channel; title / body / id', () {
      final loud = FeedAlert.tryParse(_alert('AL1', sound: true))!;
      final quiet = FeedAlert.tryParse(_alert('AL2', sound: false))!;
      expect(alertChannelOf(loud), AlertChannel.sound);
      expect(alertChannelOf(quiet), AlertChannel.silent);
      expect(AlertChannel.sound.id, isNot(AlertChannel.silent.id));
      expect(alertNotificationTitle(loud), 'High · Geofence Exit');
      expect(alertNotificationBody(loud), 'Defibrillator 04 · Zone A · floor2');
      expect(alertNotificationId('AL1'), alertNotificationId('AL1'));
      expect(alertNotificationId('AL1'), isNot(alertNotificationId('AL2')));
      expect(alertNotificationId('AL000074'), greaterThan(0));
    });

    test('tolerant parsing: unknown fields ignored, missing ones empty, rows without an id dropped', () {
      final f = AlertFeed.fromJson({
        'serverTime': '2026-11-24T07:34:38Z',
        'unacknowledged': '5',
        'items': [
          {'alertId': 'AL9', 'surprise': 1},
          {'severity': 'High'},
          'junk',
        ],
      });
      expect(f.unacknowledged, 5);
      expect(f.items.single.alertId, 'AL9');
      expect(f.items.single.channels, isEmpty);
      expect(f.items.single.sound, isFalse);
      expect(f.items.single.status, 'Open', reason: 'a null status reads as Open like the server');
    });
  });

  group('AlertServiceLoop (what runs inside the foreground service)', () {
    test('a tick polls, shows the new alerts, sends the list; errors go to the app; token / ack messages apply', () async {
      final t = _Script([
        _feed('s1', 0, const []),
        _feed('s2', 1, [_alert('AL1', sound: false)]),
        Exception('boom'),
      ]);
      final shown = <FeedAlert>[];
      final sent = <Map<String, Object?>>[];
      String? token;
      final loop = AlertServiceLoop(
        engine: AlertFeedEngine(AlertsApi(t)),
        show: (a) async => shown.add(a),
        send: sent.add,
        setToken: (v) => token = v,
      );
      await loop.tick();
      expect(shown, isEmpty);
      expect(sent.single['type'], AlertMsg.snapshot);
      await loop.tick();
      expect(shown.map((a) => a.alertId), ['AL1']);
      expect(alertChannelOf(shown.single), AlertChannel.silent);
      final snap = AlertSnapshot.fromJson(sent.last['snapshot']);
      expect((snap.items.length, snap.unacknowledged, snap.serverTime), (1, 1, 's2'));
      await loop.tick();
      expect(sent.last['type'], AlertMsg.error);
      await loop.onMessage({'type': AlertMsg.token, 'token': 'new-token'});
      expect(token, 'new-token');
    });
  });

  // ------------------------------------------------------------------ the app: session, badge, list

  group('Alert feed in the app', () {
    late FakeAssetBackend backend;
    late _FakePlatform platform;
    late List<_FakeTimer> timers;

    setUp(() {
      backend = FakeAssetBackend();
      platform = _FakePlatform();
      timers = [];
      debugApiMode = true;
      AlertFeedNotifier.autoStart = true;
      AlertFeedNotifier.debugSchedule = (d, cb) => _FakeTimer(d, cb)..also(timers.add);
      SharedPreferences.setMockInitialValues({});
    });

    tearDown(() {
      debugApiMode = false;
      AlertFeedNotifier.autoStart = false;
      AlertFeedNotifier.debugSchedule = null;
      debugLicensedModules = AppModule.values.toSet();
    });

    void feeds(List<Object> replies) {
      backend.on('GET', '/api/alerts/feed', (_) {
        final r = replies.length > 1 ? replies.removeAt(0) : replies.first;
        return r is FakeReply ? r : FakeReply(200, r);
      });
    }

    ProviderContainer container({_FakePlatform? p}) {
      final c = ProviderContainer(
        retry: (_, _) => null,
        overrides: [
          ...backend.overrides,
          alertPlatformProvider.overrideWithValue(p ?? platform),
          authProvider.overrideWith(_AuthIn.new),
        ],
      );
      addTearDown(c.dispose);
      return c;
    }

    int feedCalls() => backend.calls('GET', '/api/alerts/feed').length;

    Future<void> settle() => pumpEventQueue();

    test('polls every 10 s while signed in: starts at sign-in, stops at logout / server switch (and clears the list)', () async {
      feeds([
        _feed('s1', 2, [_alert('AL1'), _alert('AL2')]),
        _feed('s2', 2, const []),
      ]);
      final c = container();
      c.listen(alertFeedProvider, (_, _) {});
      await settle();
      expect(feedCalls(), 0, reason: 'not signed in: no call');
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect(feedCalls(), 1);
      expect(backend.calls('GET', '/api/alerts/feed').first.uri.queryParameters.containsKey('since'), isFalse);
      expect(c.read(alertFeedProvider).items, hasLength(2));
      expect(timers.where((t) => !t.cancelled).map((t) => t.delay), [const Duration(seconds: 10)]);
      timers.last.callback();
      await settle();
      expect(feedCalls(), 2);
      expect(backend.calls('GET', '/api/alerts/feed').last.uri.queryParameters['since'], 's1');
      // Logout (also what a server switch does first): everything stops.
      c.read(sessionProvider.notifier).apiSignedOut();
      await settle();
      expect(c.read(alertFeedProvider).running, isFalse);
      expect(c.read(alertFeedProvider).items, isEmpty);
      expect(timers.last.cancelled, isTrue, reason: 'the pending 10 s timer is cancelled');
      expect(platform.stops, greaterThanOrEqualTo(0));
      final before = feedCalls();
      timers.last.callback();
      await settle();
      expect(feedCalls(), before, reason: 'no poll after logout');
      expect((await SharedPreferences.getInstance()).getString('alerts.feed.cache'), isNull, reason: 'the cache is erased at logout');
      // Signing in again starts again, from a first call (no since).
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect(backend.calls('GET', '/api/alerts/feed').last.uri.queryParameters.containsKey('since'), isFalse);
    });

    test('notifications: new Open In-app alerts only, once each; the sound flag decides the channel', () async {
      feeds([
        _feed('s1', 1, [_alert('AL1')]), // open at start: listed, no notification
        _feed('s2', 3, [
          _alert('AL2', sound: true),
          _alert('AL3', sound: false),
          _alert('AL4', channels: const ['Push']),
        ]),
        _feed('s3', 3, [_alert('AL2'), _alert('AL3')]), // again: no second notification
      ]);
      final c = container();
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect(platform.shown, isEmpty);
      timers.last.callback();
      await settle();
      // The feed is newest first; notifications go out oldest first (AL3, then AL2).
      expect(platform.shown.map((a) => a.alertId), ['AL3', 'AL2']);
      expect(platform.shown.map(alertChannelOf), [AlertChannel.silent, AlertChannel.sound]);
      timers.last.callback();
      await settle();
      expect(platform.shown.map((a) => a.alertId), ['AL3', 'AL2'], reason: 'never the same alertId twice');
    });

    test('the bell badge is the feed\'s unacknowledged count (not the number of items shown)', () async {
      feeds([_feed('s1', 7, [_alert('AL1'), _alert('AL2')])]);
      final c = container();
      c.listen(alertFeedProvider, (_, _) {});
      expect(c.read(unreadNotificationCountProvider), 0, reason: '0 until the first answer');
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect(c.read(unreadNotificationCountProvider), 7);
      expect(c.read(alertFeedProvider).items, hasLength(2));
    });

    test('acknowledge one: POST /api/alerts/{id}/acknowledge, the item leaves, the badge follows the next poll', () async {
      feeds([
        _feed('s1', 2, [_alert('AL1'), _alert('AL2')]),
        _feed('s2', 1, [_alert('AL1', status: 'Acknowledged')]),
      ]);
      backend.on(
        'POST',
        '/api/alerts/AL1/acknowledge',
        (_) => const FakeReply(200, {'alertId': 'AL1', 'status': 'Acknowledged', 'acknowledgedBy': 'max.milan', 'acknowledgedAt': '2026-11-24T07:33:00Z'}),
      );
      final c = container();
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      await c.read(alertFeedProvider.notifier).acknowledge('AL1');
      expect(backend.calls('POST', '/api/alerts/AL1/acknowledge'), hasLength(1));
      expect(c.read(alertFeedProvider).items.map((a) => a.alertId), ['AL2']);
      expect(c.read(unreadNotificationCountProvider), 1);
      expect(feedCalls(), 2, reason: 'the list is re-read at once');
    });

    test('acknowledge fails → the error reaches the caller (message + Retry), the list stays', () async {
      feeds([_feed('s1', 1, [_alert('AL1')])]);
      backend.on('POST', '/api/alerts/AL1/acknowledge', (_) => const FakeReply(500, {'message': 'boom'}));
      final c = container();
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      await expectLater(c.read(alertFeedProvider.notifier).acknowledge('AL1'), throwsA(anything));
      expect(c.read(alertFeedProvider).items, hasLength(1));
      expect(c.read(alertFeedProvider).busy, isFalse);
    });

    test('Acknowledge all: POST /api/alerts/acknowledge-all, the list empties, the count is 0', () async {
      feeds([
        _feed('s1', 3, [_alert('AL1'), _alert('AL2')]),
        _feed('s2', 0, [_alert('AL1', status: 'Acknowledged'), _alert('AL2', status: 'Acknowledged')]),
      ]);
      backend.on('POST', '/api/alerts/acknowledge-all', (_) => const FakeReply(200, {'acknowledged': 3}));
      final c = container();
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect(await c.read(alertFeedProvider.notifier).acknowledgeAll(), 3);
      expect(backend.calls('POST', '/api/alerts/acknowledge-all'), hasLength(1));
      expect(c.read(alertFeedProvider).items, isEmpty);
      expect(c.read(unreadNotificationCountProvider), 0);
    });

    test('offline: the cached list shows first; a failed poll keeps the list and sets the error; Retry recovers', () async {
      feeds([_feed('s1', 1, [_alert('AL1')])]);
      var c = container();
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect((await SharedPreferences.getInstance()).getString('alerts.feed.cache'), isNotNull);
      c.dispose();
      // The app is opened again with no network.
      timers.clear();
      feeds([const FakeReply(500, {'message': 'down'}), _feed('s2', 1, [_alert('AL1')])]);
      c = container();
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      final s = c.read(alertFeedProvider);
      expect(s.items.map((a) => a.alertId), ['AL1'], reason: 'the cached list is still there');
      expect(s.error, isNotNull);
      expect(s.fromCache, isTrue);
      await c.read(alertFeedProvider.notifier).pollNow();
      expect(c.read(alertFeedProvider).error, isNull);
      expect(c.read(alertFeedProvider).fromCache, isFalse);
    });

    test('foreground service (Android): started at sign-in with the token and server, no own poll; its list and errors arrive; stopped at logout', () async {
      final svc = _FakePlatform(service: true);
      feeds([_feed('x', 0, const [])]);
      final c = container(p: svc);
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect(svc.starts, 1);
      expect(svc.config!.token, 'tok-1');
      expect(svc.config!.baseUrl, trackingBaseUrl);
      expect(svc.config!.interval, const Duration(seconds: 10));
      expect(c.read(alertFeedProvider).serviceMode, isTrue);
      expect(feedCalls(), 0, reason: 'the service polls, not the app');
      // The service sends the list after each poll.
      svc.fromServiceC.add({
        'type': AlertMsg.snapshot,
        'snapshot': AlertSnapshot(serverTime: 's1', unacknowledged: 4, items: [FeedAlert.tryParse(_alert('AL1'))!], at: DateTime.utc(2026, 11, 24, 7, 35)).toJson(),
      });
      await settle();
      expect(c.read(unreadNotificationCountProvider), 4);
      expect(c.read(alertFeedProvider).items.single.alertId, 'AL1');
      // A 401 in the service shows as an error, the list stays.
      svc.fromServiceC.add({'type': AlertMsg.error, 'message': 'x', 'status': 401});
      await settle();
      expect(c.read(alertFeedProvider).error, isNotNull);
      expect(c.read(alertFeedProvider).items, hasLength(1));
      // A refreshed token goes to the service.
      (c.read(authProvider.notifier) as _AuthIn).refreshTo('tok-2');
      await settle();
      expect(svc.sent.last, {'type': AlertMsg.token, 'token': 'tok-2'});
      // Pull to refresh asks the service to poll.
      await c.read(alertFeedProvider.notifier).pollNow();
      expect(svc.sent.last, {'type': AlertMsg.pollNow});
      // Logout stops the service.
      c.read(sessionProvider.notifier).apiSignedOut();
      await settle();
      expect(svc.running, isFalse);
      expect(svc.stops, 1);
    });

    test('a notification tap opens the alert list on that alert (also when it started the app)', () async {
      final p = _FakePlatform()..launched = 'AL7';
      feeds([_feed('s1', 1, [_alert('AL7')])]);
      final c = container(p: p);
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      expect(c.read(alertFeedProvider).focusAlertId, 'AL7', reason: 'cold start from a notification');
      c.read(alertFeedProvider.notifier).consumeFocus();
      p.tapsC.add('AL9');
      await settle();
      expect(c.read(alertFeedProvider).focusAlertId, 'AL9');
    });

    test('notification permission: explained and asked once; a refusal never blocks the list or the badge', () async {
      final svc = _FakePlatform(service: true, allowed: false, grant: false);
      feeds([_feed('s1', 2, [_alert('AL1')])]);
      final c = container(p: svc);
      c.listen(alertFeedProvider, (_, _) {});
      c.read(sessionProvider.notifier).apiSignedIn();
      await settle();
      final n = c.read(alertFeedProvider.notifier);
      expect(c.read(alertFeedProvider).notificationsAllowed, isFalse);
      expect(await n.shouldAskPermission(), isTrue);
      expect(await n.requestPermission(), isFalse);
      expect(svc.permissionRequests, 1);
      expect(await n.shouldAskPermission(), isFalse, reason: 'asked once');
      expect(c.read(alertFeedProvider).notificationsAllowed, isFalse);
      svc.fromServiceC.add({
        'type': AlertMsg.snapshot,
        'snapshot': AlertSnapshot(serverTime: 's1', unacknowledged: 2, items: [FeedAlert.tryParse(_alert('AL1'))!]).toJson(),
      });
      await settle();
      expect(c.read(unreadNotificationCountProvider), 2, reason: 'the badge works without the permission');
      expect(c.read(alertFeedProvider).items, hasLength(1));
    });

    // -------------------------------------------------------------- the Alerts screen

    group('screen', () {
      ProviderContainer? open;

      // Like the other widget tests: unmount, then dispose INSIDE the test, so the 30 s clock timer is gone at the end.
      void screenTest(String name, Future<void> Function(WidgetTester tester) body) => testWidgets(name, (tester) async {
        try {
          await body(tester);
        } finally {
          await tester.pumpWidget(const SizedBox());
          open?.dispose();
          open = null;
        }
      });

      Future<ProviderContainer> pumpApp(WidgetTester tester, {String route = Routes.home}) async {
        tester.view.physicalSize = const Size(1170, 2532);
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);
        final c = ProviderContainer(
          retry: (_, _) => null,
          overrides: [
            ...backend.overrides,
            alertPlatformProvider.overrideWithValue(platform),
            authProvider.overrideWith(_AuthIn.new),
            sessionProvider.overrideWith(_SignedIn.new),
            tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
          ],
        );
        open = c;
        await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const Asset360App()));
        await tester.pump(SplashScreen.duration);
        await tester.pumpAndSettle();
        c.read(routerProvider).go(route);
        await tester.pumpAndSettle();
        return c;
      }

      screenTest('the bell shows the unacknowledged count and opens "Alerts": severity, asset, zone, time ago', (tester) async {
        final raised = DateTime.now().toUtc().subtract(const Duration(minutes: 5)).toIso8601String();
        feeds([
          _feed('s1', 5, [
            _alert('AL1', severity: 'Critical', raisedAt: raised),
            _alert('AL2', severity: 'Low', asset: 'Chiller 2', zone: 'Plant room'),
          ]),
        ]);
        await pumpApp(tester);
        final badge = tester.widget<Badge>(find.descendant(of: find.byType(NotificationBell), matching: find.byType(Badge)));
        expect(badge.isLabelVisible, isTrue);
        expect((badge.label! as Text).data, '5', reason: 'the feed\'s unacknowledged, not 2 items');
        await tester.tap(find.byTooltip('Alerts'));
        await tester.pumpAndSettle();
        expect(find.text('Alerts'), findsWidgets);
        expect(find.text('5 unacknowledged'), findsOneWidget);
        expect(find.byKey(const ValueKey('alert-AL1')), findsOneWidget);
        expect(find.text('Defibrillator 04 · AR000151'), findsOneWidget);
        expect(find.text('Zone A · floor2 · Seahouse'), findsWidgets);
        expect(find.text('Critical'), findsOneWidget);
        expect(find.text('5 min ago'), findsOneWidget);
        expect(find.byIcon(Icons.error_rounded), findsOneWidget, reason: 'Critical icon');
        expect(find.byIcon(Icons.info_rounded), findsOneWidget, reason: 'Low icon');
      });

      screenTest('Acknowledge on one alert: POST, the card leaves; an error shows a message with Retry', (tester) async {
        feeds([
          _feed('s1', 2, [_alert('AL1'), _alert('AL2', asset: 'Chiller 2')]),
          _feed('s2', 1, [_alert('AL2', asset: 'Chiller 2')]),
        ]);
        var fail = true;
        backend.on('POST', '/api/alerts/AL1/acknowledge', (_) {
          if (fail) return const FakeReply(500, {'message': 'boom'});
          return const FakeReply(200, {'alertId': 'AL1', 'status': 'Acknowledged'});
        });
        await pumpApp(tester, route: Routes.notificationsFrom(Routes.home));
        await tester.tap(find.byKey(const ValueKey('alert-ack-AL1')));
        await tester.pumpAndSettle();
        expect(find.textContaining('Could not acknowledge'), findsOneWidget);
        expect(find.byKey(const ValueKey('alert-AL1')), findsOneWidget, reason: 'still there');
        fail = false;
        await tester.tap(find.text('Retry'));
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('alert-AL1')), findsNothing);
        expect(find.byKey(const ValueKey('alert-AL2')), findsOneWidget);
        expect(backend.calls('POST', '/api/alerts/AL1/acknowledge'), hasLength(2));
      });

      screenTest('Acknowledge all asks first: Cancel does nothing, Confirm posts and empties the list', (tester) async {
        feeds([
          _feed('s1', 2, [_alert('AL1'), _alert('AL2')]),
          _feed('s2', 0, const []),
        ]);
        backend.on('POST', '/api/alerts/acknowledge-all', (_) => const FakeReply(200, {'acknowledged': 2}));
        await pumpApp(tester, route: Routes.notificationsFrom(Routes.home));
        await tester.tap(find.byKey(const ValueKey('alerts-ack-all')));
        await tester.pumpAndSettle();
        expect(find.text('Acknowledge all alerts?'), findsOneWidget);
        await tester.tap(find.text('Cancel'));
        await tester.pumpAndSettle();
        expect(backend.calls('POST', '/api/alerts/acknowledge-all'), isEmpty);
        await tester.tap(find.byKey(const ValueKey('alerts-ack-all')));
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'Acknowledge all'));
        await tester.pumpAndSettle();
        expect(backend.calls('POST', '/api/alerts/acknowledge-all'), hasLength(1));
        expect(find.byKey(const ValueKey('alerts-empty')), findsOneWidget);
        expect(find.byKey(const ValueKey('alert-AL1')), findsNothing);
      });

      screenTest('an alert that comes back with another status disappears from the list on the next poll', (tester) async {
        feeds([
          _feed('s1', 2, [_alert('AL1'), _alert('AL2')]),
          _feed('s2', 1, [_alert('AL1', status: 'Acknowledged')]),
        ]);
        await pumpApp(tester, route: Routes.notificationsFrom(Routes.home));
        expect(find.byKey(const ValueKey('alert-AL1')), findsOneWidget);
        timers.last.callback();
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('alert-AL1')), findsNothing);
        expect(find.byKey(const ValueKey('alert-AL2')), findsOneWidget);
        expect(find.text('1 unacknowledged'), findsOneWidget);
      });

      screenTest('a failing feed: message + Retry (no cache); with a list: the list stays with a banner', (tester) async {
        var up = false;
        backend.on('GET', '/api/alerts/feed', (_) => up ? FakeReply(200, _feed('s1', 1, [_alert('AL1')])) : const FakeReply(500, {'message': 'boom'}));
        await pumpApp(tester, route: Routes.notificationsFrom(Routes.home));
        expect(find.text('Retry'), findsOneWidget);
        up = true;
        await tester.tap(find.text('Retry'));
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('alert-AL1')), findsOneWidget);
        up = false;
        timers.last.callback();
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('alert-AL1')), findsOneWidget, reason: 'the list stays');
        expect(find.byKey(const ValueKey('alerts-offline')), findsOneWidget);
        expect(find.text('Retry'), findsOneWidget);
      });

      screenTest('works for any signed-in user: no licence lock on the bell or the list', (tester) async {
        debugLicensedModules = <AppModule>{};
        feeds([_feed('s1', 1, [_alert('AL1')])]);
        await pumpApp(tester);
        await tester.tap(find.byTooltip('Alerts'));
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('alert-AL1')), findsOneWidget);
        expect(find.textContaining('not licensed'), findsNothing);
      });

      screenTest('a notification tap (?alert=) opens the list with that alert marked', (tester) async {
        feeds([_feed('s1', 2, [_alert('AL1'), _alert('AL2', asset: 'Chiller 2')])]);
        final c = await pumpApp(tester);
        c.read(alertFeedProvider.notifier).openAlert('AL2');
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('alert-AL2')), findsOneWidget);
        final box = tester.widget<Container>(find.byKey(const ValueKey('alert-AL2')));
        expect(((box.decoration! as BoxDecoration).border! as Border).top.width, 2, reason: 'marked');
        expect(c.read(alertFeedProvider).focusAlertId, isNull, reason: 'consumed');
      });

      screenTest('notifications refused: a banner explains, the list still works; "Turn on" asks again', (tester) async {
        platform.allowed = false;
        feeds([_feed('s1', 1, [_alert('AL1')])]);
        await pumpApp(tester, route: Routes.notificationsFrom(Routes.home));
        expect(find.byKey(const ValueKey('alerts-notifications-off')), findsOneWidget);
        expect(find.byKey(const ValueKey('alert-AL1')), findsOneWidget);
      });
    });
  });
}

extension on _FakeTimer {
  void also(void Function(_FakeTimer t) f) => f(this);
}
