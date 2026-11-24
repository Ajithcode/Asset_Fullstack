import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

import 'package:asset360_mobile/core/platform/app_launcher.dart';
import 'package:asset360_mobile/data/alerts/alert_feed_engine.dart';
import 'package:asset360_mobile/data/alerts/alert_platform.dart';
import 'package:asset360_mobile/data/alerts/alert_service_loop.dart';
import 'package:asset360_mobile/data/api/alerts/alerts_api.dart';

/// The Android side of in-app alerts (flutter_local_notifications + flutter_foreground_task). Nothing here runs in
/// tests or on other platforms ([alertPlatformProvider]); the logic it carries lives in alert_feed_engine.dart and
/// alert_service_loop.dart.

const _icon = '@mipmap/ic_launcher';
const _serviceId = 7360;
const _keyBase = 'alerts.service.baseUrl';
const _keyToken = 'alerts.service.token';

/// "Asset360 is monitoring alerts": the low-priority persistent notification of the foreground service.
const alertServiceTitle = 'Asset360 is monitoring alerts';
const alertServiceText = 'New alerts appear here and in the Alerts list.';

/// flutter_local_notifications in one place for the app and the service isolate.
class _Notifier {
  final plugin = FlutterLocalNotificationsPlugin();

  Future<void> init({DidReceiveNotificationResponseCallback? onTap}) async {
    await plugin.initialize(
      settings: const InitializationSettings(android: AndroidInitializationSettings(_icon)),
      onDidReceiveNotificationResponse: onTap,
    );
    final android = plugin.resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();
    for (final c in AlertChannel.values) {
      await android?.createNotificationChannel(
        AndroidNotificationChannel(
          c.id,
          c.title,
          description: c.description,
          importance: Importance.high,
          playSound: c == AlertChannel.sound,
          enableVibration: c == AlertChannel.sound,
        ),
      );
    }
  }

  Future<void> show(FeedAlert a) {
    final ch = alertChannelOf(a);
    final body = alertNotificationBody(a);
    final detail = [a.message, a.siteName].where((s) => s != null && s.isNotEmpty).join(' · ');
    return plugin.show(
      id: alertNotificationId(a.alertId),
      title: alertNotificationTitle(a),
      body: body,
      payload: a.alertId,
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          ch.id,
          ch.title,
          channelDescription: ch.description,
          icon: _icon,
          importance: Importance.high,
          priority: Priority.high,
          playSound: a.sound,
          enableVibration: a.sound,
          category: AndroidNotificationCategory.alarm,
          when: a.raisedAt?.millisecondsSinceEpoch,
          styleInformation: BigTextStyleInformation(detail.isEmpty ? body : '$body\n$detail'),
        ),
      ),
    );
  }
}

/// The real [AlertPlatform] on Android.
class AndroidAlertPlatform implements AlertPlatform {
  final _notifier = _Notifier();
  final _taps = StreamController<String>.broadcast();
  final _fromService = StreamController<Map<String, Object?>>.broadcast();
  bool _initialized = false;

  @override
  bool get supportsService => true;

  @override
  Future<void> init() async {
    if (_initialized) return;
    _initialized = true;
    await _notifier.init(
      onTap: (r) {
        final id = r.payload;
        if (id != null && id.isNotEmpty) _taps.add(id);
      },
    );
    FlutterForegroundTask.init(
      androidNotificationOptions: AndroidNotificationOptions(
        channelId: 'alerts_monitor',
        channelName: 'Alert monitoring',
        channelDescription: 'Shown while Asset360 watches for new alerts.',
        channelImportance: NotificationChannelImportance.LOW,
        priority: NotificationPriority.LOW,
        onlyAlertOnce: true,
      ),
      iosNotificationOptions: const IOSNotificationOptions(showNotification: false, playSound: false),
      foregroundTaskOptions: ForegroundTaskOptions(
        eventAction: ForegroundTaskEventAction.repeat(10000),
        autoRunOnBoot: false,
        allowWakeLock: true,
        allowWifiLock: true,
      ),
    );
    FlutterForegroundTask.addTaskDataCallback((data) {
      if (data is Map) _fromService.add(Map<String, Object?>.from(data));
    });
  }

  @override
  Future<bool> notificationsAllowed() async =>
      await FlutterForegroundTask.checkNotificationPermission() == NotificationPermission.granted;

  @override
  Future<bool> requestNotifications() async =>
      await FlutterForegroundTask.requestNotificationPermission() == NotificationPermission.granted;

  @override
  Future<void> openNotificationSettings() async {
    await AppLauncher.openAppSettings();
  }

  @override
  Future<void> show(FeedAlert alert) => _notifier.show(alert);

  @override
  Future<bool> startService(AlertServiceConfig config) async {
    await FlutterForegroundTask.saveData(key: _keyBase, value: config.baseUrl);
    await FlutterForegroundTask.saveData(key: _keyToken, value: config.token);
    if (await FlutterForegroundTask.isRunningService) await FlutterForegroundTask.stopService();
    final r = await FlutterForegroundTask.startService(
      serviceId: _serviceId,
      serviceTypes: [ForegroundServiceTypes.dataSync],
      notificationTitle: alertServiceTitle,
      notificationText: alertServiceText,
      callback: alertServiceCallback,
    );
    return r is ServiceRequestSuccess;
  }

  @override
  Future<void> stopService() async {
    if (await FlutterForegroundTask.isRunningService) await FlutterForegroundTask.stopService();
    await FlutterForegroundTask.removeData(key: _keyToken);
    await FlutterForegroundTask.removeData(key: _keyBase);
  }

  @override
  void sendToService(Map<String, Object?> message) => FlutterForegroundTask.sendDataToTask(message);

  @override
  Stream<Map<String, Object?>> get fromService => _fromService.stream;

  @override
  Stream<String> get taps => _taps.stream;

  @override
  Future<String?> launchedFromAlert() async {
    final d = await _notifier.plugin.getNotificationAppLaunchDetails();
    if (d?.didNotificationLaunchApp != true) return null;
    final id = d?.notificationResponse?.payload;
    return id == null || id.isEmpty ? null : id;
  }
}

// ------------------------------------------------------------------ the foreground service

/// The service's entry point (its own isolate).
@pragma('vm:entry-point')
void alertServiceCallback() => FlutterForegroundTask.setTaskHandler(AlertTaskHandler());

/// Plain Dio for the service isolate: the token goes in a header only (never in the URL, never logged).
class DioAlertsTransport implements AlertsTransport {
  DioAlertsTransport(String baseUrl, this.token)
    : _dio = Dio(BaseOptions(baseUrl: baseUrl, connectTimeout: const Duration(seconds: 10), receiveTimeout: const Duration(seconds: 15)));

  final Dio _dio;
  String token;

  Options get _options => Options(headers: {'Authorization': 'Bearer $token', 'Accept': 'application/json'});

  @override
  Future<Object?> get(String path, {Map<String, Object?>? query}) async =>
      (await _dio.get<Object?>(path, queryParameters: query, options: _options)).data;

  @override
  Future<Object?> post(String path) async => (await _dio.post<Object?>(path, options: _options)).data;
}

/// Polls the alert feed every 10 s while the user is signed in, shows a notification for each new alert and sends the
/// list to the app. The app starts it at sign-in and stops it at logout / server switch.
class AlertTaskHandler extends TaskHandler {
  AlertServiceLoop? _loop;

  @override
  Future<void> onStart(DateTime timestamp, TaskStarter starter) async {
    final base = await FlutterForegroundTask.getData<String>(key: _keyBase);
    final token = await FlutterForegroundTask.getData<String>(key: _keyToken);
    if (base == null || token == null) return;
    final notifier = _Notifier();
    await notifier.init();
    final transport = DioAlertsTransport(base, token);
    _loop = AlertServiceLoop(
      engine: AlertFeedEngine(AlertsApi(transport)),
      show: notifier.show,
      send: FlutterForegroundTask.sendDataToMain,
      setToken: (t) => transport.token = t,
      errorStatusOf: (e) => e is DioException ? e.response?.statusCode : null,
    );
    await _loop!.tick();
  }

  @override
  void onRepeatEvent(DateTime timestamp) => unawaited(_loop?.tick());

  @override
  void onReceiveData(Object data) => unawaited(_loop?.onMessage(data));

  @override
  Future<void> onDestroy(DateTime timestamp, bool isTimeout) async {}
}
