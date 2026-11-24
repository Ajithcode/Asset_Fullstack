import 'package:asset360_mobile/data/api/assets/json_read.dart';
import 'package:asset360_mobile/data/api/tracking/tracking_models.dart' show jUtc;

/// Typed replies of the in-app alert feed (docs/api/MOBILE-ALERTS.md; `FeedAlert` / `FeedResponse` in
/// docs/backend_ref/Services/Tracking/Api/A360.Tracking.Api/Endpoints/AlertFeedEndpoints.cs). Parsing never throws;
/// a missing value reads as null / empty. Fields are exactly the spec's, nothing is derived or guessed.

/// Alert status values of the spec. The feed returns `Open` for an unacknowledged alert.
const alertStatusOpen = 'Open';

/// The channel name that means "show it inside the app" (the notification this app raises).
const alertChannelInApp = 'In-app';

/// One alert of GET /api/alerts/feed.
class FeedAlert {
  const FeedAlert({
    required this.alertId,
    this.raisedAt,
    this.severity = '',
    this.eventType = '',
    this.assetId,
    this.assetName = '',
    this.zone,
    this.siteName,
    this.message,
    this.status = alertStatusOpen,
    this.ruleName,
    this.channels = const [],
    this.sound = false,
    this.acknowledgedBy,
    this.acknowledgedAt,
  });

  final String alertId;
  final DateTime? raisedAt;
  final String severity, eventType, assetName, status;
  final String? assetId, zone, siteName, message, ruleName, acknowledgedBy;

  /// The rule's notification channels: In-app, Push, Email.
  final List<String> channels;

  /// Play the alert sound (the rule's "Play sound").
  final bool sound;
  final DateTime? acknowledgedAt;

  /// Unacknowledged (the app's to-do list).
  bool get isOpen => status == alertStatusOpen;

  /// The rule wants this alert shown in the app (a notification here).
  bool get wantsInApp => channels.contains(alertChannelInApp);

  FeedAlert copyWith({String? status}) => FeedAlert(
    alertId: alertId,
    raisedAt: raisedAt,
    severity: severity,
    eventType: eventType,
    assetId: assetId,
    assetName: assetName,
    zone: zone,
    siteName: siteName,
    message: message,
    status: status ?? this.status,
    ruleName: ruleName,
    channels: channels,
    sound: sound,
    acknowledgedBy: acknowledgedBy,
    acknowledgedAt: acknowledgedAt,
  );

  static FeedAlert? tryParse(Object? v) {
    if (v is! Map) return null;
    final j = jMap(v);
    final id = jStr(j['alertId'])?.trim();
    if (id == null) return null;
    return FeedAlert(
      alertId: id,
      raisedAt: jUtc(j['raisedAt']),
      severity: jStr(j['severity'])?.trim() ?? '',
      eventType: jStr(j['eventType'])?.trim() ?? '',
      assetId: jStr(j['assetId']),
      assetName: jStr(j['assetName'])?.trim() ?? '',
      zone: jStr(j['zone']),
      siteName: jStr(j['siteName']),
      message: jStr(j['message']),
      status: jStr(j['status'])?.trim() ?? alertStatusOpen,
      ruleName: jStr(j['ruleName']),
      channels: jStrings(j['channels']),
      sound: jBool(j['sound']),
      acknowledgedBy: jStr(j['acknowledgedBy']),
      acknowledgedAt: jUtc(j['acknowledgedAt']),
    );
  }

  Map<String, Object?> toJson() => {
    'alertId': alertId,
    'raisedAt': raisedAt?.toUtc().toIso8601String(),
    'severity': severity,
    'eventType': eventType,
    'assetId': assetId,
    'assetName': assetName,
    'zone': zone,
    'siteName': siteName,
    'message': message,
    'status': status,
    'ruleName': ruleName,
    'channels': channels,
    'sound': sound,
    'acknowledgedBy': acknowledgedBy,
    'acknowledgedAt': acknowledgedAt?.toUtc().toIso8601String(),
  };
}

/// GET /api/alerts/feed: `{serverTime, unacknowledged, items}`.
class AlertFeed {
  const AlertFeed({this.serverTime, this.unacknowledged = 0, this.items = const []});

  /// The server's clock as it sent it: the next call's `since` (never the phone's clock).
  final String? serverTime;

  /// The number for the bell / app-icon badge.
  final int unacknowledged;
  final List<FeedAlert> items;

  factory AlertFeed.fromJson(Object? body) {
    final j = jMap(body);
    return AlertFeed(
      serverTime: jStr(j['serverTime'])?.trim(),
      unacknowledged: jInt(j['unacknowledged']) ?? 0,
      items: [for (final e in jList(j['items'])) ?FeedAlert.tryParse(e)],
    );
  }
}

/// POST /api/alerts/{alertId}/acknowledge: `{alertId, status, acknowledgedBy, acknowledgedAt}`.
class AlertAck {
  const AlertAck({required this.alertId, this.status, this.acknowledgedBy, this.acknowledgedAt});

  final String alertId;
  final String? status, acknowledgedBy;
  final DateTime? acknowledgedAt;

  factory AlertAck.fromJson(Object? body, String fallbackId) {
    final j = jMap(body);
    return AlertAck(
      alertId: jStr(j['alertId']) ?? fallbackId,
      status: jStr(j['status']),
      acknowledgedBy: jStr(j['acknowledgedBy']),
      acknowledgedAt: jUtc(j['acknowledgedAt']),
    );
  }
}
