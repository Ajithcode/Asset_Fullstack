/// Backend settings, passed at run time only (never hard-coded). The app calls each service
/// DIRECTLY (the API Gateway on 5296 is not used for now):
///
/// ```sh
/// flutter run --dart-define=USE_API=true \
///   --dart-define=API_BASE_URL=http://<backend-ip>:5401 \
///   --dart-define=MEDIA_BASE_URL=http://<backend-ip>:5300 \
///   --dart-define=AUTH_BASE_URL=http://<backend-ip>:5018 \
///   --dart-define=MASTER_DATA_BASE_URL=http://<backend-ip>:5500 \
///   --dart-define=DASHBOARD_BASE_URL=http://<backend-ip>:5470 \
///   --dart-define=ADMIN_BASE_URL=http://<backend-ip>:5460 \
///   --dart-define=TRACKING_BASE_URL=http://<backend-ip>:5490
/// ```
///
/// See README → "Connecting to the backend".
library;

import 'package:flutter/foundation.dart';

/// True: sign-in and repositories use the backend; false (default): the mock data in lib/data/mock/
/// and the demo login (rex.kumar / Asset@360 / 123456). Code reads [apiMode], which tests can override.
const bool kUseApi = bool.fromEnvironment('USE_API');

/// Asset service root, e.g. http://192.168.1.20:5401 (no trailing /api): every /api/asset-* call.
const String kApiBaseUrl = String.fromEnvironment('API_BASE_URL');

/// Media service root, e.g. http://192.168.1.20:5300: POST /api/media/upload and the /uploads/... files.
const String kMediaBaseUrl = String.fromEnvironment('MEDIA_BASE_URL');

/// UserAccount (login) service root, e.g. http://192.168.1.20:5018.
const String kAuthBaseUrl = String.fromEnvironment('AUTH_BASE_URL');

/// Master Data Management service root (dropdown lists, locations, employees).
///
/// DEV default: the shared test server, so dropdowns work without the extra define. Override it
/// with --dart-define=MASTER_DATA_BASE_URL=... (env/api_dev.json sets it) before any release build.
const String kMasterDataBaseUrl = String.fromEnvironment(
  'MASTER_DATA_BASE_URL',
  defaultValue: 'http://172.16.100.29:5500',
);

/// Dashboard service root (Home dashboard, GET /api/dashboards/mobile/*, network status + its WebSocket).
///
/// DEV default: the shared test server, like [kMasterDataBaseUrl]. Override it with
/// --dart-define=DASHBOARD_BASE_URL=... (env/api_dev.json sets it) before any release build.
const String kDashboardBaseUrl = String.fromEnvironment(
  'DASHBOARD_BASE_URL',
  defaultValue: 'http://172.16.100.29:5470',
);

/// Administration service root (map system: published floor plans, zones, plots, layers,
/// GET /api/administration/maps/*; the Tracking Live Map reads it).
///
/// DEV default: the shared test server, like [kMasterDataBaseUrl]. Override it with
/// --dart-define=ADMIN_BASE_URL=... (env/api_dev.json sets it) before any release build.
const String kAdminBaseUrl = String.fromEnvironment('ADMIN_BASE_URL', defaultValue: 'http://172.16.100.29:5460');

/// Tracking service root (IoT devices, tags, sensors, telemetry readings, location events,
/// geofences, IoT alerts; the Tracking & IoT module and the asset IoT tab read it).
///
/// DEV default: the shared test server, like [kMasterDataBaseUrl]. Override it with
/// --dart-define=TRACKING_BASE_URL=... (env/api_dev.json sets it) before any release build.
const String kTrackingBaseUrl = String.fromEnvironment('TRACKING_BASE_URL', defaultValue: 'http://172.16.100.29:5490');

/// Optional debug override: a Bearer token for the Asset / Media services, used only while nobody is signed in.
const String kDevToken = String.fromEnvironment('DEV_TOKEN');

/// Tests set this to run the API sign-in flow without --dart-define.
@visibleForTesting
bool? debugApiMode;

/// True when the app talks to the backend ([kUseApi], or a test override).
bool get apiMode => debugApiMode ?? kUseApi;

/// Build flag: shows the "Backend server" switcher (Settings → Developer, Login → Diagnostics) and
/// lets a saved choice override the build's host. Off in client release builds; env/api_dev.json sets it.
const bool kShowServerSwitcher = bool.fromEnvironment('SHOW_SERVER_SWITCHER');

/// Tests set this to show the server switcher without --dart-define.
@visibleForTesting
bool? debugShowServerSwitcher;

/// True when this build may switch servers ([kShowServerSwitcher], or a test override).
bool get showServerSwitcher => debugShowServerSwitcher ?? kShowServerSwitcher;

/// A server the switcher offers: [origin] = scheme + host, no port (each service keeps its own port).
class ServerPreset {
  const ServerPreset(this.label, this.origin);

  final String label;
  final String origin;
}

const serverPresets = [
  ServerPreset('Server .29', 'http://172.16.100.29'),
  ServerPreset('Server .26', 'http://172.16.100.26'),
];

String? _serverOverride;

/// The chosen server (scheme + host, e.g. http://172.16.100.26) replacing the host of every base URL,
/// or null = the build's own URLs. Set from shared_preferences at start-up (server_repository.dart).
String? get serverOverride => _serverOverride;

/// Use [origin] (see [normalizeServerOrigin]) for every service from now on; null = the build's URLs.
/// Only server_repository.dart (and tests) call this; the switch itself also signs out and restarts.
void applyServerOverride(String? origin) => _serverOverride = normalizeServerOrigin(origin);

/// "172.16.100.40", "172.16.100.40:5018", "https://dev.example.com/x" → scheme + host only
/// ("http://172.16.100.40", "https://dev.example.com"); null when there is no usable host.
String? normalizeServerOrigin(String? input) {
  final raw = input?.trim() ?? '';
  if (raw.isEmpty) return null;
  final u = Uri.tryParse(raw.contains('://') ? raw : 'http://$raw');
  if (u == null || u.host.isEmpty || !const {'http', 'https'}.contains(u.scheme)) return null;
  if (!RegExp(r'^[A-Za-z0-9.\-]+$').hasMatch(u.host)) return null;
  return Uri(scheme: u.scheme, host: u.host).toString();
}

/// The server of the build's --dart-defines (scheme + host of the first service URL that is set), or null.
String? get buildServerOrigin {
  for (final s in BackendService.values) {
    final o = normalizeServerOrigin(s.buildUrl);
    if (o != null) return o;
  }
  return null;
}

/// The server the app talks to now: the saved choice, else the build's.
String? get currentServerOrigin => _serverOverride ?? buildServerOrigin;

/// "Server .29" for a preset, else the host ("dev.example.com"); "Not set" without a server.
String serverLabelOf(String? origin) {
  if (origin == null) return 'Not set';
  for (final p in serverPresets) {
    if (p.origin == origin) return p.label;
  }
  return Uri.parse(origin).host;
}

/// "Server .29 (172.16.100.29)" for a preset, else the host.
String serverDisplayOf(String? origin) {
  final label = serverLabelOf(origin);
  if (origin == null) return label;
  final host = Uri.parse(origin).host;
  return label == host ? host : '$label ($host)';
}

/// [build] with its host replaced by [serverOverride]; the port (the build's, else [port]) and path stay.
String _withServer(String build, String port) {
  final o = _serverOverride;
  if (o == null) return build;
  final origin = Uri.parse(o);
  final b = Uri.tryParse(build);
  final keepPort = b != null && b.hasAuthority && b.hasPort ? b.port : int.parse(port);
  return origin.replace(port: keepPort, path: b != null && b.hasAuthority ? b.path : '').toString();
}

/// Effective service roots: the build's URL, or the same port on the chosen server ([serverOverride]).
String get apiBaseUrl => _withServer(kApiBaseUrl, '5401');
String get mediaBaseUrl => _withServer(kMediaBaseUrl, '5300');
String get authBaseUrl => _withServer(kAuthBaseUrl, '5018');
String get masterDataBaseUrl => _withServer(kMasterDataBaseUrl, '5500');
String get dashboardBaseUrl => _withServer(kDashboardBaseUrl, '5470');
String get adminBaseUrl => _withServer(kAdminBaseUrl, '5460');
String get trackingBaseUrl => _withServer(kTrackingBaseUrl, '5490');

String? _urlProblem(String name, String value, String port) => switch (value) {
  '' => '$name is not set. Run with --dart-define=$name=http://<backend-ip>:$port',
  _ when Uri.tryParse(value)?.hasScheme != true => '$name "$value" must start with http:// or https://',
  _ => null,
};

/// What is missing to call the Asset service, or null when API_BASE_URL is usable.
String? get apiConfigProblem => _urlProblem('API_BASE_URL', apiBaseUrl, '5401');

/// What is missing to call the Media service, or null when MEDIA_BASE_URL is usable.
String? get mediaConfigProblem => _urlProblem('MEDIA_BASE_URL', mediaBaseUrl, '5300');

/// What is missing to call the login service, or null when AUTH_BASE_URL is usable.
String? get authConfigProblem => _urlProblem('AUTH_BASE_URL', authBaseUrl, '5018');

/// What is missing to call the Master Data service, or null when MASTER_DATA_BASE_URL is usable.
String? get masterDataConfigProblem => _urlProblem('MASTER_DATA_BASE_URL', masterDataBaseUrl, '5500');

/// What is missing to call the Dashboard service, or null when DASHBOARD_BASE_URL is usable.
String? get dashboardConfigProblem => _urlProblem('DASHBOARD_BASE_URL', dashboardBaseUrl, '5470');

/// What is missing to call the Administration service, or null when ADMIN_BASE_URL is usable.
String? get adminConfigProblem => _urlProblem('ADMIN_BASE_URL', adminBaseUrl, '5460');

/// What is missing to call the Tracking service, or null when TRACKING_BASE_URL is usable.
String? get trackingConfigProblem => _urlProblem('TRACKING_BASE_URL', trackingBaseUrl, '5490');

/// The backend services the app calls directly.
enum BackendService {
  auth('AUTH', 'AUTH_BASE_URL', 'Login service'),
  asset('ASSET', 'API_BASE_URL', 'Asset service'),
  media('MEDIA', 'MEDIA_BASE_URL', 'Media service'),
  masterData('MASTER DATA', 'MASTER_DATA_BASE_URL', 'Master Data service'),
  dashboard('DASHBOARD', 'DASHBOARD_BASE_URL', 'Dashboard service'),
  admin('ADMIN', 'ADMIN_BASE_URL', 'Administration service'),
  tracking('TRACKING', 'TRACKING_BASE_URL', 'Tracking service');

  const BackendService(this.code, this.define, this.label);

  /// Short name in the connection test ("ASSET").
  final String code;

  /// The --dart-define that sets [baseUrl].
  final String define;
  final String label;

  /// The URL the app calls: [buildUrl], or the same port on the chosen server ([serverOverride]).
  String get baseUrl => switch (this) {
    auth => authBaseUrl,
    asset => apiBaseUrl,
    media => mediaBaseUrl,
    masterData => masterDataBaseUrl,
    dashboard => dashboardBaseUrl,
    admin => adminBaseUrl,
    tracking => trackingBaseUrl,
  };

  /// The URL from the build's --dart-define.
  String get buildUrl => switch (this) {
    auth => kAuthBaseUrl,
    asset => kApiBaseUrl,
    media => kMediaBaseUrl,
    masterData => kMasterDataBaseUrl,
    dashboard => kDashboardBaseUrl,
    admin => kAdminBaseUrl,
    tracking => kTrackingBaseUrl,
  };

  String? get configProblem => switch (this) {
    auth => authConfigProblem,
    asset => apiConfigProblem,
    media => mediaConfigProblem,
    masterData => masterDataConfigProblem,
    dashboard => dashboardConfigProblem,
    admin => adminConfigProblem,
    tracking => trackingConfigProblem,
  };
}
