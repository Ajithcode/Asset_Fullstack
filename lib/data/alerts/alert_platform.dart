import 'dart:async';
import 'dart:io' show Platform;

import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/alerts/alert_android.dart';
import 'package:asset360_mobile/data/api/alerts/alerts_models.dart';

/// What the foreground service needs to poll on its own (it runs in its own isolate, without the app's providers).
class AlertServiceConfig {
  const AlertServiceConfig({required this.baseUrl, required this.token, this.interval = const Duration(seconds: 10)});

  /// The Tracking service root (the gateway), e.g. https://a360demo.purpleiq.ai.
  final String baseUrl;

  /// The signed-in user's access token (kept only in the app's private storage while the service runs; never logged).
  final String token;
  final Duration interval;
}

/// Messages between the app and the foreground service (plain JSON maps).
abstract final class AlertMsg {
  /// service → app: the list after a poll `{type, snapshot}`.
  static const snapshot = 'snapshot';

  /// service → app: a poll failed `{type, message, status?}`.
  static const error = 'error';

  /// app → service: a refreshed access token `{type, token}`.
  static const token = 'token';

  /// app → service: poll now (after an acknowledge, when the app comes to the front).
  static const pollNow = 'pollNow';

  /// app → service: the user acknowledged one `{type, alertId}` / all alerts here.
  static const ackOne = 'ackOne';
  static const ackAll = 'ackAll';
}

/// The Android-only parts behind an interface (tests use a fake): local notifications, the foreground service and the
/// notification permission. Other platforms get [NoAlertPlatform] (the in-app list and badge still work).
abstract class AlertPlatform {
  /// True when polling can run in a foreground service (Android).
  bool get supportsService;

  /// Creates the notification channels and listens for taps. Called once at app start.
  Future<void> init();

  Future<bool> notificationsAllowed();

  /// Android 13+: the system permission dialog. True when granted.
  Future<bool> requestNotifications();

  /// Opens the app's system settings (to allow notifications after a refusal).
  Future<void> openNotificationSettings();

  /// A local notification for [alert]: sound channel only when `alert.sound` is true.
  Future<void> show(FeedAlert alert);

  /// Starts the foreground service ("Asset360 is monitoring alerts"): it polls every [AlertServiceConfig.interval].
  Future<bool> startService(AlertServiceConfig config);

  Future<void> stopService();

  /// A message for the running service.
  void sendToService(Map<String, Object?> message);

  /// Messages from the service ([AlertMsg]).
  Stream<Map<String, Object?>> get fromService;

  /// Alert ids of notifications the user tapped while the app was running.
  Stream<String> get taps;

  /// The alert whose notification started the app (a tap while it was closed), once.
  Future<String?> launchedFromAlert();
}

/// No notifications, no service: the app polls itself and shows the list and the badge.
class NoAlertPlatform implements AlertPlatform {
  @override
  bool get supportsService => false;

  @override
  Future<void> init() async {}

  @override
  Future<bool> notificationsAllowed() async => false;

  @override
  Future<bool> requestNotifications() async => false;

  @override
  Future<void> openNotificationSettings() async {}

  @override
  Future<void> show(FeedAlert alert) async {}

  @override
  Future<bool> startService(AlertServiceConfig config) async => false;

  @override
  Future<void> stopService() async {}

  @override
  void sendToService(Map<String, Object?> message) {}

  @override
  Stream<Map<String, Object?>> get fromService => const Stream.empty();

  @override
  Stream<String> get taps => const Stream.empty();

  @override
  Future<String?> launchedFromAlert() async => null;
}

/// Android gets the real platform; everything else (and `flutter test` on a desktop host) the no-op one.
final alertPlatformProvider = Provider<AlertPlatform>((ref) => Platform.isAndroid ? AndroidAlertPlatform() : NoAlertPlatform());
