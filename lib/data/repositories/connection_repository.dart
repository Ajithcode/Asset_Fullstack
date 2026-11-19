import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/repositories/server_repository.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';

/// One line of Administration → Backend API → Test connection.
class ServiceHealth {
  const ServiceHealth({
    required this.service,
    required this.url,
    this.statusCode,
    this.elapsed,
    this.status,
    this.error,
    this.viaGateway = false,
  });

  final BackendService service;

  /// The URL called ("http://…:5401/health"), or empty when the base URL is not set.
  final String url;
  final int? statusCode;
  final Duration? elapsed;

  /// The reply's `status` ("Healthy"), or the plain-text body.
  final String? status;

  /// Why the check failed (not configured, no network, HTTP error, unhealthy); null = OK.
  final String? error;

  /// Cloud gateway: the service's own health path was not reachable, so [url] is a cheap authenticated GET
  /// (or a public one) on the service through the gateway.
  final bool viaGateway;

  bool get ok => error == null;

  ServiceHealth _via() => ServiceHealth(
    service: service,
    url: url,
    statusCode: statusCode,
    elapsed: elapsed,
    status: status,
    error: error,
    viaGateway: true,
  );
}

/// A plain Dio for the health checks (no token: /health is public). Tests override it.
final healthDioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      connectTimeout: const Duration(seconds: 8),
      receiveTimeout: const Duration(seconds: 8),
      headers: {'Accept': 'application/json'},
    ),
  );
  ref.onDispose(dio.close);
  return dio;
});

/// The Dio a "via gateway" check uses for [BackendService] (token + refresh, like the app's own calls).
/// Tests override it.
final serviceDioProvider = Provider.family<Dio, BackendService>(
  (ref, s) => switch (s) {
    BackendService.auth => ref.watch(healthDioProvider), // GET /api/auth/roles is public
    BackendService.asset => ref.watch(dioProvider),
    BackendService.media => ref.watch(mediaDioProvider),
    BackendService.masterData => ref.watch(masterDataDioProvider),
    BackendService.dashboard => ref.watch(dashboardDioProvider),
    BackendService.admin => ref.watch(adminDioProvider),
    BackendService.tracking => ref.watch(trackingDioProvider),
  },
);

/// Through the cloud gateway (docs/backend_ref/ApiGateway appsettings.json): each service's own health path
/// (`/<prefix>/health`, public) and a cheap GET that proves the service answers.
const gatewayHealthPaths = {
  BackendService.auth: '/user-account/health',
  BackendService.asset: '/asset/health',
  BackendService.media: '/media/health',
  BackendService.masterData: '/master-data-management/health',
  BackendService.dashboard: '/dashboard/health',
  BackendService.admin: '/administration/health',
  BackendService.tracking: '/tracking/health',
};

/// Cheap GETs per service ("via gateway"). Media has no GET API: GET /api/media/upload answering 405
/// (POST only) proves the Media service is behind the route.
const gatewayProbePaths = {
  BackendService.auth: '/api/auth/roles',
  BackendService.asset: '/api/asset-registrations?pageSize=1',
  BackendService.media: '/api/media/upload',
  BackendService.masterData: '/api/master-data/sites?pageSize=1',
  BackendService.dashboard: '/api/dashboards/network-status',
  BackendService.admin: '/api/administration/maps/summary',
  BackendService.tracking: '/api/iot-devices?pageSize=1',
};

/// Base URL and config problem of each service (tests override it).
final serviceTargetsProvider = Provider<Map<BackendService, ({String baseUrl, String? problem})>>((ref) {
  ref.watch(serverProvider);
  return {for (final s in BackendService.values) s: (baseUrl: s.baseUrl, problem: s.configProblem)};
});

/// Test connection, one result per service (in parallel). State: null = not tested yet; loading while the calls
/// run. Never an error: failures are per service.
/// - Office IPs (each service on its own port): GET `<base>/health`.
/// - Cloud gateway (one https base for all, [isGatewayTargets]): the gateway first (GET /health, then
///   /gateway/health: [gateway]); per service its own health path through the gateway ([gatewayHealthPaths]);
///   when that is not reachable the result is marked "via gateway" and a cheap GET ([gatewayProbePaths]) tests it.
class ConnectionTestNotifier extends AsyncNotifier<List<ServiceHealth>?> {
  static const healthPath = '/health';
  static const gatewayHealthPath = '/gateway/health';

  /// A health URL that returned an HTML page (the web app) instead of the service.
  static const webAppAnswered = 'Not reachable: the web app answered (HTML), not the service';

  /// Cloud gateway check of the last run: the URL that answered (/health or /gateway/health) or why none did.
  ({String url, bool ok, String detail})? gateway;

  @override
  List<ServiceHealth>? build() => null;

  /// True when every configured service has the same base URL without a port (the cloud gateway).
  static bool isGatewayTargets(Iterable<({String baseUrl, String? problem})> targets) {
    final bases = {
      for (final t in targets)
        if (t.problem == null) _trim(t.baseUrl),
    };
    if (bases.length != 1) return false;
    final u = Uri.tryParse(bases.single);
    return u != null && u.hasAuthority && !u.hasPort;
  }

  static String _trim(String base) => base.endsWith('/') ? base.substring(0, base.length - 1) : base;

  Future<void> run() async {
    state = const AsyncLoading();
    final targets = ref.read(serviceTargetsProvider);
    gateway = null;
    if (isGatewayTargets(targets.values)) {
      final base = _trim(targets.values.firstWhere((t) => t.problem == null).baseUrl);
      gateway = await _gatewayCheck(base);
    }
    final results = await Future.wait(BackendService.values.map(check));
    state = AsyncData(results);
  }

  Future<({String url, bool ok, String detail})> _gatewayCheck(String base) async {
    var last = (url: '$base$healthPath', ok: false, detail: '');
    var hostAnswered = false;
    for (final path in const [healthPath, gatewayHealthPath]) {
      final r = await _health(BackendService.auth, '$base$path');
      if (r.ok) return (url: r.url, ok: true, detail: '${r.statusCode}${r.status == null ? '' : ' · ${r.status}'}');
      hostAnswered |= r.error == webAppAnswered;
      last = (url: r.url, ok: false, detail: r.error ?? '');
    }
    if (hostAnswered) {
      return (
        url: '$base$healthPath, $gatewayHealthPath',
        ok: true,
        detail: 'host reachable; health paths not exposed (the web app answered)',
      );
    }
    return last;
  }

  Future<ServiceHealth> check(BackendService service) async {
    final targets = ref.read(serviceTargetsProvider);
    final (:baseUrl, :problem) = targets[service]!;
    if (problem != null) return ServiceHealth(service: service, url: '', error: problem);
    final base = _trim(baseUrl);
    if (!isGatewayTargets(targets.values)) return _health(service, '$base$healthPath');
    final own = await _health(service, '$base${gatewayHealthPaths[service]}');
    if (own.ok) return own;
    return (await _probe(service, '$base${gatewayProbePaths[service]}'))._via();
  }

  /// A cheap GET on [service] through the gateway (the service's Dio: token + refresh).
  Future<ServiceHealth> _probe(BackendService service, String url) async {
    final watch = Stopwatch()..start();
    try {
      final res = await ref.read(serviceDioProvider(service)).get<Object?>(url);
      return ServiceHealth(service: service, url: url, statusCode: res.statusCode, elapsed: watch.elapsed);
    } catch (e) {
      final err = ApiException.from(e);
      // Media: GET on the POST-only upload route → 405 = the service answered.
      if (service == BackendService.media && err.statusCode == 405) {
        return ServiceHealth(service: service, url: url, statusCode: 405, elapsed: watch.elapsed, status: 'Reachable');
      }
      return ServiceHealth(
        service: service,
        url: url,
        statusCode: err.statusCode,
        elapsed: watch.elapsed,
        error: switch (err.statusCode) {
          null => err.message,
          401 => 'HTTP 401: the gateway answered; sign in to test this service',
          final code => 'HTTP $code: ${err.message}',
        },
      );
    }
  }

  Future<ServiceHealth> _health(BackendService service, String url) async {
    final watch = Stopwatch()..start();
    try {
      final res = await ref.read(healthDioProvider).get<Object?>(url);
      final data = res.data;
      // The cloud host answers unknown paths with the web app's index.html (200): not a health endpoint.
      if (data is String && data.trimLeft().startsWith('<')) {
        return ServiceHealth(
          service: service,
          url: url,
          statusCode: res.statusCode,
          elapsed: watch.elapsed,
          error: webAppAnswered,
        );
      }
      final status = data is Map ? '${data['status'] ?? ''}' : (data is String ? data.trim() : '');
      final unhealthy = status.isNotEmpty && status.toLowerCase() != 'healthy';
      return ServiceHealth(
        service: service,
        url: url,
        statusCode: res.statusCode,
        elapsed: watch.elapsed,
        status: status.isEmpty ? null : status,
        error: unhealthy ? 'Service reports "$status"' : null,
      );
    } catch (e) {
      final err = ApiException.from(e);
      return ServiceHealth(
        service: service,
        url: url,
        statusCode: err.statusCode,
        elapsed: watch.elapsed,
        error: err.statusCode == null ? err.message : 'HTTP ${err.statusCode}: ${err.message}',
      );
    }
  }
}

final connectionTestProvider = AsyncNotifierProvider<ConnectionTestNotifier, List<ServiceHealth>?>(
  ConnectionTestNotifier.new,
);
