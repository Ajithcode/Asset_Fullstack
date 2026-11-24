import 'package:asset360_mobile/data/api/alerts/alerts_api.dart';

/// The alert list as the app shows it: the Open alerts (newest first), the server's unacknowledged count, and the
/// server time of the call that produced it.
class AlertSnapshot {
  const AlertSnapshot({this.serverTime, this.unacknowledged = 0, this.items = const [], this.at});

  final String? serverTime;
  final int unacknowledged;
  final List<FeedAlert> items;

  /// When this phone received it (for "updated N s ago" and the offline cache).
  final DateTime? at;

  Map<String, Object?> toJson() => {
    'serverTime': serverTime,
    'unacknowledged': unacknowledged,
    'items': [for (final a in items) a.toJson()],
    'at': at?.toUtc().toIso8601String(),
  };

  factory AlertSnapshot.fromJson(Object? body) {
    final feed = AlertFeed.fromJson(body);
    final at = body is Map ? body['at'] : null;
    return AlertSnapshot(
      serverTime: feed.serverTime,
      unacknowledged: feed.unacknowledged,
      items: feed.items,
      at: at is String ? DateTime.tryParse(at) : null,
    );
  }
}

/// One poll: the list after it and the alerts to notify.
class AlertPoll {
  const AlertPoll({required this.snapshot, this.newAlerts = const []});

  final AlertSnapshot snapshot;

  /// Alerts seen for the first time that are Open and have the In-app channel (oldest first). Empty on the first
  /// poll: what was already open when the app started is listed, not announced.
  final List<FeedAlert> newAlerts;
}

/// The polling logic of docs/api/MOBILE-ALERTS.md, with no platform in it (runs in the app and in the Android
/// foreground service; tests drive it with a fake [AlertsApi]):
/// - the FIRST call has no `since` (the latest Open alerts: `status=Open`);
/// - every next call sends the previous response's `serverTime` as `since`, exactly as received (the server's
///   clock, never the phone's) and no `status`, so an alert acknowledged elsewhere comes back with another status;
/// - an item that is Open is kept / updated; any other status drops it from the list;
/// - an alertId is announced at most once.
class AlertFeedEngine {
  AlertFeedEngine(this.api, {this.firstLimit = 100, this.nextLimit = 200, this.maxItems = 200});

  final AlertsApi api;

  /// Page size of the first call / of the `since` calls (the spec allows 1–200, default 20).
  final int firstLimit, nextLimit;
  final int maxItems;

  String? _since;
  bool _first = true;
  final _items = <String, FeedAlert>{};
  final _announced = <String>{};
  int _unacknowledged = 0;
  AlertSnapshot _snapshot = const AlertSnapshot();

  /// The `since` the next call will send (null before the first response).
  String? get since => _since;
  AlertSnapshot get snapshot => _snapshot;

  /// Alert ids already announced (persisted by the app so a restart does not repeat them).
  Set<String> get announced => Set.unmodifiable(_announced);

  /// Marks [ids] as announced (restored from the previous run).
  void restoreAnnounced(Iterable<String> ids) => _announced.addAll(ids);

  /// Forget everything (logout / another server).
  void reset() {
    _since = null;
    _first = true;
    _items.clear();
    _announced.clear();
    _unacknowledged = 0;
    _snapshot = const AlertSnapshot();
  }

  /// One poll. Errors are the caller's: nothing changes when the call fails (the same `since` is sent next time).
  Future<AlertPoll> poll({DateTime? now}) async {
    final firstCall = _since == null;
    final feed = firstCall
        ? await api.feed(status: alertStatusOpen, limit: firstLimit)
        : await api.feed(since: _since, limit: nextLimit);
    // Only a server time ever becomes `since`; without one the previous value stays.
    if (feed.serverTime != null) _since = feed.serverTime;
    final announce = <FeedAlert>[];
    if (firstCall) _items.clear();
    for (final a in feed.items.reversed) {
      if (a.isOpen) {
        _items[a.alertId] = a;
        if (_announced.add(a.alertId) && !_first && a.wantsInApp) announce.add(a);
      } else {
        _items.remove(a.alertId);
      }
    }
    _first = false;
    _unacknowledged = feed.unacknowledged;
    return AlertPoll(snapshot: _build(now), newAlerts: announce);
  }

  /// The list right after the user acknowledged [alertId] here (the next poll confirms it).
  AlertSnapshot acknowledgedLocally(String alertId, {DateTime? now}) {
    if (_items.remove(alertId) != null && _unacknowledged > 0) _unacknowledged--;
    return _build(now);
  }

  /// The list right after "Acknowledge all".
  AlertSnapshot acknowledgedAllLocally({DateTime? now}) {
    _items.clear();
    _unacknowledged = 0;
    return _build(now);
  }

  AlertSnapshot _build(DateTime? now) {
    final list = _items.values.toList()
      ..sort((a, b) => (b.raisedAt ?? DateTime.utc(0)).compareTo(a.raisedAt ?? DateTime.utc(0)));
    if (list.length > maxItems) list.removeRange(maxItems, list.length);
    return _snapshot = AlertSnapshot(serverTime: _since, unacknowledged: _unacknowledged, items: list, at: now ?? DateTime.now().toUtc());
  }
}

// ------------------------------------------------------------------ notifications

/// Android notification channels: one with sound, one silent (the sound is a property of a channel on Android 8+).
enum AlertChannel {
  sound('alerts_sound', 'Alerts (with sound)', 'New alerts whose rule plays a sound.'),
  silent('alerts_silent', 'Alerts (silent)', 'New alerts whose rule does not play a sound.');

  const AlertChannel(this.id, this.title, this.description);

  final String id, title, description;
}

/// The channel of [a]'s notification: the one with sound only when the alert's `sound` is true.
AlertChannel alertChannelOf(FeedAlert a) => a.sound ? AlertChannel.sound : AlertChannel.silent;

/// Notification title: severity and what happened ("High · Geofence Exit").
String alertNotificationTitle(FeedAlert a) => [a.severity, a.eventType].where((s) => s.isNotEmpty).join(' · ');

/// Notification text: the asset and its zone ("Defibrillator 04 · Zone A · floor2"), else the message.
String alertNotificationBody(FeedAlert a) {
  final where = [a.assetName, a.zone].where((s) => s != null && s.isNotEmpty).join(' · ');
  return where.isNotEmpty ? where : (a.message ?? a.alertId);
}

/// A stable positive notification id for an alertId (a re-shown alert replaces its notification).
int alertNotificationId(String alertId) {
  var h = 17;
  for (final c in alertId.codeUnits) {
    h = (h * 31 + c) & 0x3fffffff;
  }
  return h;
}
