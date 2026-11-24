import 'dart:async';

import 'package:asset360_mobile/data/alerts/alert_feed_engine.dart';
import 'package:asset360_mobile/data/alerts/alert_platform.dart';
import 'package:asset360_mobile/data/api/alerts/alerts_models.dart';

/// What runs inside the foreground service, with the platform pieces passed in (tests drive it directly; the real
/// `TaskHandler` in alert_android.dart wires Dio, flutter_local_notifications and the port to the app).
/// Every tick = one [AlertFeedEngine.poll]: new alerts are shown through [show], the list goes to the app through
/// [send]; a failed poll goes to the app as an error and the next tick tries again (same `since`).
class AlertServiceLoop {
  AlertServiceLoop({
    required this.engine,
    required this.show,
    required this.send,
    required this.setToken,
    this.errorStatusOf,
  });

  final AlertFeedEngine engine;
  final Future<void> Function(FeedAlert alert) show;
  final void Function(Map<String, Object?> message) send;
  final void Function(String token) setToken;

  /// The HTTP status of a failure, for the app's message (null = not an HTTP error).
  final int? Function(Object error)? errorStatusOf;

  bool _busy = false;
  bool _again = false;

  /// One poll now; a call while one runs is run once more right after it.
  Future<void> tick() async {
    if (_busy) {
      _again = true;
      return;
    }
    _busy = true;
    try {
      final poll = await engine.poll();
      for (final a in poll.newAlerts) {
        try {
          await show(a);
        } catch (_) {
          // A notification that cannot be shown must not stop the list.
        }
      }
      send({'type': AlertMsg.snapshot, 'snapshot': poll.snapshot.toJson()});
    } catch (e) {
      send({'type': AlertMsg.error, 'message': '$e', 'status': ?errorStatusOf?.call(e)});
    } finally {
      _busy = false;
    }
    if (_again) {
      _again = false;
      await tick();
    }
  }

  /// A message from the app.
  Future<void> onMessage(Object? data) async {
    if (data is! Map) return;
    switch (data['type']) {
      case AlertMsg.token:
        final t = data['token'];
        if (t is String && t.isNotEmpty) setToken(t);
      case AlertMsg.pollNow:
        await tick();
      case AlertMsg.ackOne:
        final id = data['alertId'];
        if (id is String) engine.acknowledgedLocally(id);
      case AlertMsg.ackAll:
        engine.acknowledgedAllLocally();
    }
  }
}
