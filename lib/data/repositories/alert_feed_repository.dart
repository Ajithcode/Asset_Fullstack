import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:asset360_mobile/data/alerts/alert_feed_engine.dart';
import 'package:asset360_mobile/data/alerts/alert_platform.dart';
import 'package:asset360_mobile/data/api/alerts/alerts_api.dart';
import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart' show apiMode, trackingBaseUrl;
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';

export 'package:asset360_mobile/data/alerts/alert_feed_engine.dart' show AlertSnapshot;
export 'package:asset360_mobile/data/api/alerts/alerts_models.dart' show FeedAlert, alertStatusOpen;

/// In-app alerts (docs/api/MOBILE-ALERTS.md): the Tracking alert feed polled every 10 s while the user is signed in.
/// - On Android the poll runs in a foreground service ("Asset360 is monitoring alerts", [AlertPlatform]); it shows the
///   local notifications and sends the list here. Elsewhere (and when the service cannot start) this notifier polls
///   itself and shows the notifications.
/// - The bell badge is the feed's `unacknowledged` ([unreadNotificationCountProvider]); the list is the Alerts screen.
/// - The last response is cached (shared_preferences) for offline viewing; the cache is erased at logout.

/// The loaded alerts.
class AlertFeedState {
  const AlertFeedState({
    this.items = const [],
    this.unacknowledged = 0,
    this.serverTime,
    this.updatedAt,
    this.loaded = false,
    this.fromCache = false,
    this.error,
    this.running = false,
    this.serviceMode = false,
    this.notificationsAllowed,
    this.focusAlertId,
    this.busy = false,
  });

  /// The Open alerts, newest first.
  final List<FeedAlert> items;

  /// The number on the bell: the server's count of unacknowledged alerts.
  final int unacknowledged;

  /// The last `serverTime` (the next call's `since`).
  final String? serverTime;

  /// When this phone last got a good answer.
  final DateTime? updatedAt;

  /// A response (or the cache) has been read at least once.
  final bool loaded;

  /// The list is the cached one: no successful poll yet (offline / starting).
  final bool fromCache;

  /// The last poll failed (the list stays); null after a good poll.
  final Object? error;

  /// Polling is on (signed in).
  final bool running;

  /// The poll runs in the Android foreground service.
  final bool serviceMode;

  /// The notification permission: null = not checked yet.
  final bool? notificationsAllowed;

  /// An alert the user asked to see (a notification tap): the Alerts screen scrolls to it, then [AlertFeedNotifier.consumeFocus].
  final String? focusAlertId;

  /// An acknowledge call is running.
  final bool busy;

  AlertFeedState copyWith({
    List<FeedAlert>? items,
    int? unacknowledged,
    String? serverTime,
    DateTime? updatedAt,
    bool? loaded,
    bool? fromCache,
    Object? error,
    bool clearError = false,
    bool? running,
    bool? serviceMode,
    bool? notificationsAllowed,
    String? focusAlertId,
    bool clearFocus = false,
    bool? busy,
  }) => AlertFeedState(
    items: items ?? this.items,
    unacknowledged: unacknowledged ?? this.unacknowledged,
    serverTime: serverTime ?? this.serverTime,
    updatedAt: updatedAt ?? this.updatedAt,
    loaded: loaded ?? this.loaded,
    fromCache: fromCache ?? this.fromCache,
    error: clearError ? null : (error ?? this.error),
    running: running ?? this.running,
    serviceMode: serviceMode ?? this.serviceMode,
    notificationsAllowed: notificationsAllowed ?? this.notificationsAllowed,
    focusAlertId: clearFocus ? null : (focusAlertId ?? this.focusAlertId),
    busy: busy ?? this.busy,
  );
}

/// [AlertsTransport] over the app's Tracking client (access token, one refresh on 401, readable errors).
class ClientAlertsTransport implements AlertsTransport {
  const ClientAlertsTransport(this.client);

  final ApiClient client;

  @override
  Future<Object?> get(String path, {Map<String, Object?>? query}) async =>
      (await client.get<Object?>(path, query: query == null ? null : {...query})).data;

  @override
  Future<Object?> post(String path) async => (await client.post<Object?>(path)).data;
}

final alertsApiProvider = Provider<AlertsApi>((ref) => AlertsApi(ClientAlertsTransport(ref.watch(trackingClientProvider))));

const _cacheKey = 'alerts.feed.cache';
const _askedKey = 'alerts.permission.asked';

class AlertFeedNotifier extends Notifier<AlertFeedState> {
  static const interval = Duration(seconds: 10);

  /// Polling follows the session. Tests turn it off (test/flutter_test_config.dart) and on again where they need it.
  static bool autoStart = true;

  /// Tests replace the timer.
  @visibleForTesting
  static Timer Function(Duration delay, void Function() callback)? debugSchedule;

  AlertFeedEngine? _engine;
  Timer? _timer;
  StreamSubscription<Map<String, Object?>>? _fromService;
  StreamSubscription<String>? _taps;
  bool _polling = false;
  int _generation = 0;

  @override
  AlertFeedState build() {
    ref.onDispose(() {
      _generation++;
      _timer?.cancel();
      unawaited(_fromService?.cancel());
      unawaited(_taps?.cancel());
    });
    if (autoStart) {
      ref.listen<bool>(sessionProvider.select((s) => s.loggedIn), (_, on) {
        if (on) {
          unawaited(start());
        } else {
          unawaited(stop());
        }
      });
      ref.listen<AuthState>(authProvider, (_, next) {
        // A refreshed access token goes to the service (it cannot refresh it).
        if (next is AuthSignedIn && state.serviceMode) {
          ref.read(alertPlatformProvider).sendToService({'type': AlertMsg.token, 'token': next.tokens.accessToken});
        }
      });
      if (ref.read(sessionProvider).loggedIn) scheduleMicrotask(() => unawaited(start()));
    }
    return const AlertFeedState();
  }

  AlertPlatform get _platform => ref.read(alertPlatformProvider);

  /// Starts polling (sign-in): the cached list first, then the service (Android) or this notifier's own timer.
  Future<void> start() async {
    if (!apiMode || state.running) return;
    final gen = ++_generation;
    state = state.copyWith(running: true);
    final cache = await _readCache();
    if (!_current(gen)) return;
    if (cache != null) {
      state = state.copyWith(
        items: cache.snapshot.items,
        unacknowledged: cache.snapshot.unacknowledged,
        serverTime: cache.snapshot.serverTime,
        updatedAt: cache.snapshot.at,
        loaded: true,
        fromCache: true,
      );
    }
    final platform = _platform;
    try {
      await platform.init();
      final allowed = await platform.notificationsAllowed();
      if (!_current(gen)) return;
      state = state.copyWith(notificationsAllowed: allowed);
      _taps ??= platform.taps.listen(openAlert);
      unawaited(
        platform.launchedFromAlert().then((id) {
          if (id != null && _current(gen)) openAlert(id);
        }),
      );
    } catch (_) {
      // Notifications are optional: the list and the badge work without them.
    }
    if (!_current(gen)) return;
    var service = false;
    if (platform.supportsService) {
      final token = _token();
      if (token != null) {
        try {
          service = await platform.startService(AlertServiceConfig(baseUrl: trackingBaseUrl, token: token, interval: interval));
        } catch (_) {
          service = false;
        }
      }
    }
    if (!_current(gen)) return;
    if (service) {
      state = state.copyWith(serviceMode: true);
      _fromService = platform.fromService.listen(_onService);
    } else {
      final engine = _engine = AlertFeedEngine(ref.read(alertsApiProvider));
      if (cache != null) engine.restoreAnnounced(cache.announced);
      unawaited(_pollLoop(gen));
    }
  }

  bool _current(int gen) => ref.mounted && gen == _generation && state.running;

  String? _token() => switch (ref.read(authProvider)) {
    AuthSignedIn(:final tokens) => tokens.accessToken,
    _ => null,
  };

  /// Logout / server switch: stops the poll and the service and erases the list and the cache.
  Future<void> stop() async {
    _generation++;
    _timer?.cancel();
    _timer = null;
    await _fromService?.cancel();
    _fromService = null;
    _engine = null;
    final was = state.running;
    state = const AlertFeedState();
    if (was || _platform.supportsService) {
      try {
        await _platform.stopService();
      } catch (_) {
        // Already stopped.
      }
    }
    await _clearCache();
  }

  // ------------------------------------------------------------------ own polling (no service)

  Future<void> _pollLoop(int gen) async {
    if (!_current(gen)) return;
    await _pollOnce(gen);
    if (!_current(gen)) return;
    final schedule = debugSchedule ?? Timer.new;
    _timer = schedule(interval, () => unawaited(_pollLoop(gen)));
  }

  Future<void> _pollOnce(int gen) async {
    final engine = _engine;
    if (engine == null || _polling) return;
    _polling = true;
    try {
      final poll = await engine.poll();
      if (!_current(gen)) return;
      for (final a in poll.newAlerts) {
        try {
          await _platform.show(a);
        } catch (_) {
          // A notification that cannot be shown must not stop the list.
        }
      }
      _apply(poll.snapshot);
      unawaited(_writeCache(poll.snapshot, engine.announced));
    } catch (e) {
      if (_current(gen)) state = state.copyWith(error: e);
    } finally {
      _polling = false;
    }
  }

  void _apply(AlertSnapshot s) => state = state.copyWith(
    items: s.items,
    unacknowledged: s.unacknowledged,
    serverTime: s.serverTime,
    updatedAt: s.at,
    loaded: true,
    fromCache: false,
    clearError: true,
  );

  // ------------------------------------------------------------------ the service

  void _onService(Map<String, Object?> m) {
    if (!ref.mounted || !state.running) return;
    switch (m['type']) {
      case AlertMsg.snapshot:
        final s = AlertSnapshot.fromJson(m['snapshot']);
        _apply(s);
        unawaited(_writeCache(s, const {}));
      case AlertMsg.error:
        final status = m['status'];
        state = state.copyWith(
          error: ApiException(
            status == 401 ? ApiErrorKind.unauthorized : ApiErrorKind.noNetwork,
            status == 401 ? 'Your session expired. Open the app to sign in again.' : '${m['message'] ?? 'Could not reach the server'}',
            statusCode: status is int ? status : null,
          ),
        );
    }
  }

  // ------------------------------------------------------------------ actions

  /// Poll now (pull to refresh, Retry, the app came to the front, after an acknowledge).
  Future<void> pollNow() async {
    if (!state.running) return;
    if (state.serviceMode) {
      _platform.sendToService({'type': AlertMsg.pollNow});
    } else {
      await _pollOnce(_generation);
    }
  }

  /// POST /api/alerts/{alertId}/acknowledge. Throws the API error (the screen shows it with Retry).
  Future<void> acknowledge(String alertId) async {
    state = state.copyWith(busy: true);
    try {
      final ack = await ref.read(alertsApiProvider).acknowledge(alertId);
      if (!ref.mounted) return;
      if (ack.status != alertStatusOpen) _afterAcknowledge(alertId);
    } finally {
      if (ref.mounted) state = state.copyWith(busy: false);
    }
    await pollNow();
  }

  /// POST /api/alerts/acknowledge-all (the screen asks first). Returns how many the server acknowledged.
  Future<int> acknowledgeAll() async {
    state = state.copyWith(busy: true);
    int n;
    try {
      n = await ref.read(alertsApiProvider).acknowledgeAll();
      if (!ref.mounted) return n;
      _afterAcknowledge(null);
    } finally {
      if (ref.mounted) state = state.copyWith(busy: false);
    }
    await pollNow();
    return n;
  }

  void _afterAcknowledge(String? alertId) {
    if (alertId == null) {
      final s = _engine?.acknowledgedAllLocally();
      _platform.sendToService({'type': AlertMsg.ackAll});
      state = s == null ? state.copyWith(items: const [], unacknowledged: 0) : state.copyWith(items: s.items, unacknowledged: s.unacknowledged);
    } else {
      final s = _engine?.acknowledgedLocally(alertId);
      _platform.sendToService({'type': AlertMsg.ackOne, 'alertId': alertId});
      if (s != null) {
        state = state.copyWith(items: s.items, unacknowledged: s.unacknowledged);
      } else {
        final had = state.items.any((a) => a.alertId == alertId);
        state = state.copyWith(
          items: [for (final a in state.items) if (a.alertId != alertId) a],
          unacknowledged: had && state.unacknowledged > 0 ? state.unacknowledged - 1 : state.unacknowledged,
        );
      }
    }
  }

  /// A notification was tapped: show that alert in the list (the shell opens the Alerts screen).
  void openAlert(String alertId) {
    if (!ref.mounted) return;
    state = state.copyWith(focusAlertId: alertId);
  }

  void consumeFocus() {
    if (state.focusAlertId != null) state = state.copyWith(clearFocus: true);
  }

  // ------------------------------------------------------------------ notification permission

  /// True when the app should explain and ask for the notification permission now: not granted and never asked.
  Future<bool> shouldAskPermission() async {
    if (!state.running) return false;
    final platform = _platform;
    if (!platform.supportsService) return false;
    final allowed = await platform.notificationsAllowed();
    if (ref.mounted) state = state.copyWith(notificationsAllowed: allowed);
    if (allowed) return false;
    final prefs = await SharedPreferences.getInstance();
    return !(prefs.getBool(_askedKey) ?? false);
  }

  /// The system permission dialog (Android 13+). A refusal is remembered; the list and the badge keep working.
  Future<bool> requestPermission() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_askedKey, true);
    final ok = await _platform.requestNotifications();
    if (ref.mounted) state = state.copyWith(notificationsAllowed: ok);
    return ok;
  }

  Future<void> openNotificationSettings() => _platform.openNotificationSettings();

  /// After the user came back from the system settings.
  Future<void> recheckPermission() async {
    final ok = await _platform.notificationsAllowed();
    if (ref.mounted) state = state.copyWith(notificationsAllowed: ok);
  }

  // ------------------------------------------------------------------ cache

  Future<({AlertSnapshot snapshot, Set<String> announced})?> _readCache() async {
    try {
      final raw = (await SharedPreferences.getInstance()).getString(_cacheKey);
      if (raw == null) return null;
      final j = jsonDecode(raw);
      if (j is! Map) return null;
      final announced = j['announced'];
      return (
        snapshot: AlertSnapshot.fromJson(j['snapshot']),
        announced: {if (announced is List) for (final id in announced) if (id is String) id},
      );
    } catch (_) {
      return null;
    }
  }

  Future<void> _writeCache(AlertSnapshot s, Set<String> announced) async {
    try {
      final ids = announced.length > 500 ? announced.skip(announced.length - 500).toList() : announced.toList();
      await (await SharedPreferences.getInstance()).setString(_cacheKey, jsonEncode({'snapshot': s.toJson(), 'announced': ids}));
    } catch (_) {
      // The cache is a convenience.
    }
  }

  Future<void> _clearCache() async {
    try {
      await (await SharedPreferences.getInstance()).remove(_cacheKey);
    } catch (_) {
      // Nothing to clear.
    }
  }
}

final alertFeedProvider = NotifierProvider<AlertFeedNotifier, AlertFeedState>(AlertFeedNotifier.new);
