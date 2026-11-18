import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart';

/// Developer → API samples (dev builds, SHOW_SERVER_SWITCHER): GET a real endpoint with the
/// signed-in user's token on the current server and show a short, redacted copy of the reply, so
/// the response shapes can be checked (and pasted to the backend team) from the phone.

/// One preset GET endpoint.
class ApiSample {
  const ApiSample(this.service, this.path, [this.query = '']);

  final BackendService service;
  final String path;

  /// Raw query string without "?" (e.g. "pageSize=3").
  final String query;

  String get label => query.isEmpty ? path : '$path?$query';
}

const apiSamples = [
  ApiSample(BackendService.asset, '/api/asset-dashboard/location-updates'),
  ApiSample(BackendService.asset, '/api/asset-dashboard/locations'),
  ApiSample(BackendService.asset, '/api/asset-registrations', 'pageSize=3'),
  ApiSample(BackendService.tracking, '/api/location-events'),
  ApiSample(BackendService.tracking, '/api/tag-ids'),
  ApiSample(BackendService.tracking, '/api/iot-devices'),
  ApiSample(BackendService.tracking, '/api/sensors'),
  ApiSample(BackendService.tracking, '/api/device-health'),
  ApiSample(BackendService.admin, '/api/administration/maps/floor-plans'),
  ApiSample(BackendService.admin, '/api/administration/maps/summary'),
  ApiSample(BackendService.dashboard, '/api/dashboards/mobile/asset-locations'),
];

/// Services the Custom row offers (the ones the app calls with the Bearer token).
const apiSampleServices = [
  BackendService.asset,
  BackendService.tracking,
  BackendService.admin,
  BackendService.dashboard,
  BackendService.masterData,
  BackendService.media,
];

/// The app's Dio of [service]: the current server's base URL + the session token (refresh on 401).
Dio sampleDioFor(Ref ref, BackendService service) => ref.read(switch (service) {
  BackendService.media => mediaClientProvider,
  BackendService.masterData => masterDataClientProvider,
  BackendService.dashboard => dashboardClientProvider,
  BackendService.admin => adminClientProvider,
  BackendService.tracking => trackingClientProvider,
  // The login service is not offered (its Dio carries no session token).
  BackendService.asset || BackendService.auth => apiClientProvider,
}).dio;

/// Lists cut to this many items, at every depth.
const sampleListItems = 3;

/// The pretty JSON is cut to this many characters.
const sampleMaxChars = 4000;

/// Key NAMES that are masked: token, password, secret, otp, qrcode, apiKey, privateKey (plain "key",
/// e.g. "assetKey" / "key", is not).
final _secretKey = RegExp('token|password|secret|otp|qrcode|apikey|privatekey', caseSensitive: false);

String _redact(Object v) {
  final s = '$v';
  return s.length <= 6 ? '${'*' * s.length}…' : '${s.substring(0, 6)}…';
}

/// Every list cut to its first [sampleListItems] items (a note says how many there were); every value
/// whose key name contains token / password / secret / otp / qrcode / apiKey / privateKey = first 6
/// characters + "…".
Object? shortenSample(Object? json) => switch (json) {
  final Map m => {
    for (final MapEntry(:key, :value) in m.entries)
      '$key': _secretKey.hasMatch('$key') && value != null && value is! Map && value is! List
          ? _redact(value)
          : shortenSample(value),
  },
  final List l => [
    for (final v in l.take(sampleListItems)) shortenSample(v),
    if (l.length > sampleListItems) '… ${l.length - sampleListItems} more (${l.length} items)',
  ],
  _ => json,
};

/// Pretty JSON of [shortenSample], cut to [sampleMaxChars]. A body that is not JSON is shown as text.
String prettySample(Object? body) {
  Object? json = body;
  if (body is String) {
    try {
      json = jsonDecode(body);
    } on FormatException {
      json = body;
    }
  }
  final text = json is String ? json : const JsonEncoder.withIndent('  ').convert(shortenSample(json));
  return text.length <= sampleMaxChars
      ? text
      : '${text.substring(0, sampleMaxChars)}\n… cut at $sampleMaxChars of ${text.length} characters';
}

/// One fetch: what was called and the short reply. [status] is null when no reply came (network).
class ApiSampleResult {
  const ApiSampleResult({required this.url, required this.elapsed, this.status, this.body = '', this.error});

  final String url;
  final int? status;
  final Duration elapsed;

  /// [prettySample] of the reply.
  final String body;

  /// Network / timeout / config problem.
  final String? error;

  bool get ok => status != null && status! >= 200 && status! < 300;

  /// URL + status + the shortened JSON (the Copy button).
  String get copyText => [
    'GET $url',
    if (status != null) 'HTTP $status · ${elapsed.inMilliseconds} ms' else 'No reply · ${error ?? ''}',
    if (body.isNotEmpty) ...['', body],
  ].join('\n');
}

/// GET [path]?[query] on [service] with the session token. Any HTTP status is a result (shown as is).
Future<ApiSampleResult> fetchSample(Ref ref, BackendService service, String path, String query) async {
  final p = path.trim().startsWith('/') ? path.trim() : '/${path.trim()}';
  final q = query.trim().startsWith('?') ? query.trim().substring(1) : query.trim();
  final dio = sampleDioFor(ref, service);
  final url = '${dio.options.baseUrl}$p${q.isEmpty ? '' : '?$q'}';
  final problem = dio.options.baseUrl.isEmpty ? (service.configProblem ?? '${service.define} is not set') : null;
  if (problem != null) return ApiSampleResult(url: url, elapsed: Duration.zero, error: problem);
  final watch = Stopwatch()..start();
  try {
    final res = await dio.get<Object?>(
      p,
      queryParameters: q.isEmpty ? null : Uri.splitQueryString(q),
      options: Options(validateStatus: (_) => true),
    );
    return ApiSampleResult(
      url: res.realUri.toString(),
      status: res.statusCode,
      elapsed: watch.elapsed,
      body: res.data == null || res.data == '' ? '' : prettySample(res.data),
    );
  } on DioException catch (e) {
    return ApiSampleResult(
      url: url,
      elapsed: watch.elapsed,
      status: e.response?.statusCode,
      body: e.response?.data == null ? '' : prettySample(e.response!.data),
      error: e.message ?? e.type.name,
    );
  }
}

/// `ref.read(apiSampleFetcherProvider)(service, path, query)`.
final apiSampleFetcherProvider = Provider<Future<ApiSampleResult> Function(BackendService, String, String)>(
  (ref) =>
      (service, path, query) => fetchSample(ref, service, path, query),
);
