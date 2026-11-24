import 'package:asset360_mobile/data/api/alerts/alerts_models.dart';
import 'package:asset360_mobile/data/api/assets/json_read.dart';

export 'package:asset360_mobile/data/api/alerts/alerts_models.dart';

/// How [AlertsApi] talks HTTP: the app's Tracking client in the app (token refresh, errors), a plain Dio in the
/// Android foreground service ([alert_service.dart]).
abstract interface class AlertsTransport {
  Future<Object?> get(String path, {Map<String, Object?>? query});
  Future<Object?> post(String path);
}

/// The alert feed endpoints of the Tracking service (through the gateway): docs/api/MOBILE-ALERTS.md.
class AlertsApi {
  const AlertsApi(this.transport);

  final AlertsTransport transport;

  static const base = '/api/alerts';
  static const feedPath = '$base/feed';
  static const acknowledgeAllPath = '$base/acknowledge-all';
  static String acknowledgePath(String alertId) => '$base/${Uri.encodeComponent(alertId)}/acknowledge';

  /// [since] = the previous response's `serverTime`, passed on exactly as received; [status] and [limit] (1–200,
  /// server default 20) only when given.
  Future<AlertFeed> feed({String? since, String? status, int? limit}) async => AlertFeed.fromJson(
    await transport.get(feedPath, query: {'since': ?since, 'status': ?status, 'limit': ?limit?.clamp(1, 200)}),
  );

  /// No body; the server stores the signed-in user. 404 = unknown id; acknowledging twice is harmless.
  Future<AlertAck> acknowledge(String alertId) async =>
      AlertAck.fromJson(await transport.post(acknowledgePath(alertId)), alertId);

  /// Acknowledges every Open alert: `{acknowledged: count}`.
  Future<int> acknowledgeAll() async => jInt(jMap(await transport.post(acknowledgeAllPath))['acknowledged']) ?? 0;
}
