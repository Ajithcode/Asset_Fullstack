import 'dart:convert';
import 'dart:typed_data';

import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/api/media_api.dart';
import 'package:asset360_mobile/data/repositories/connection_repository.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Answers every request with [status] / [body] (or throws [error]) and remembers the request.
class _FakeAdapter implements HttpClientAdapter {
  _FakeAdapter({this.status = 200, this.body, this.error});

  final int status;
  final Object? body;
  final DioExceptionType? error;
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    last = options;
    if (error != null) throw DioException(requestOptions: options, type: error!);
    return ResponseBody.fromString(
      body == null ? '' : jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/problem+json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Dio _dio(_FakeAdapter adapter, {String token = 'test-token'}) => Dio(BaseOptions(baseUrl: 'http://backend.test:5296'))
  ..httpClientAdapter = adapter
  ..interceptors.add(AuthInterceptor(token: () async => token));

Future<ApiException> _fail(_FakeAdapter adapter) async {
  try {
    await ApiClient(_dio(adapter), checkConfig: false).get<Object?>('/api/asset-categories');
  } on ApiException catch (e) {
    return e;
  }
  fail('expected an ApiException');
}

void main() {
  test('every request carries the Bearer token', () async {
    final adapter = _FakeAdapter(body: []);
    await ApiClient(_dio(adapter), checkConfig: false).get<Object?>('/api/asset-categories');
    expect(adapter.last!.headers['Authorization'], 'Bearer test-token');
    expect(adapter.last!.uri.toString(), 'http://backend.test:5296/api/asset-categories');
  });

  test('400 ValidationProblem → field errors keyed by the backend field path', () async {
    final e = await _fail(
      _FakeAdapter(
        status: 400,
        body: {
          'type': 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
          'title': 'One or more validation errors occurred.',
          'status': 400,
          'errors': {
            'Purchase.PurchaseCost': ['Purchase Cost must be 0 or more.', 'second message'],
            'AssetTag': ["An asset with tag 'GEN-00045' is already registered."],
          },
        },
      ),
    );
    expect(e.kind, ApiErrorKind.validation);
    expect(e.statusCode, 400);
    expect(e.message, 'One or more validation errors occurred.');
    expect(e.fieldErrors, {
      'Purchase.PurchaseCost': 'Purchase Cost must be 0 or more.',
      'AssetTag': "An asset with tag 'GEN-00045' is already registered.",
    });
  });

  test('401 / 403 / 404 / 409 / 500 have clear messages', () async {
    expect(
      (await _fail(_FakeAdapter(status: 401))).message,
      'Token expired or invalid – ask backend for a new dev token',
    );
    expect((await _fail(_FakeAdapter(status: 403))).kind, ApiErrorKind.forbidden);
    expect((await _fail(_FakeAdapter(status: 404))).message, 'Not found on the server.');
    final conflict = await _fail(_FakeAdapter(status: 409, body: {'title': 'Conflict', 'detail': 'Tag already used.'}));
    expect(conflict.kind, ApiErrorKind.conflict);
    expect(conflict.message, 'Tag already used.');
    expect((await _fail(_FakeAdapter(status: 503))).kind, ApiErrorKind.server);
  });

  test('403 from the access guard says "no permission" with the server reason; its 503 = permission check', () async {
    final denied = await _fail(
      _FakeAdapter(
        status: 403,
        body: {'message': "Your role doesn't allow you to create in Assets. Ask an administrator for access."},
      ),
    );
    expect(denied.kind, ApiErrorKind.forbidden);
    expect(denied.isForbidden, isTrue);
    expect(
      denied.message,
      "You don't have permission for this. Your role doesn't allow you to create in Assets. Ask an administrator for access.",
    );
    expect(denied.message, isNot(contains('expired')));
    expect((await _fail(_FakeAdapter(status: 403))).message, startsWith("You don't have permission for this."));
    final unchecked = await _fail(
      _FakeAdapter(status: 503, body: {'message': 'Your permissions could not be checked. Try again in a moment.'}),
    );
    expect(unchecked.kind, ApiErrorKind.permissionCheck);
    expect(unchecked.message, "Permissions couldn't be checked — try again shortly");
    final down = await _fail(_FakeAdapter(status: 503, body: {'message': 'Maintenance window'}));
    expect((down.kind, down.message), (ApiErrorKind.server, 'Maintenance window'));
  });

  test('a 403 never refreshes the token or retries; a 401 refreshes once and retries', () async {
    var refreshes = 0;
    var current = 'old';
    Dio make(_FakeAdapter a) {
      final dio = Dio(BaseOptions(baseUrl: 'http://backend.test:5401'))..httpClientAdapter = a;
      dio.interceptors.add(
        AuthInterceptor(
          token: () async => current,
          onUnauthorized: () async {
            refreshes++;
            return current = 'new';
          },
          retry: dio.fetch,
        ),
      );
      return dio;
    }

    final forbidden = _FakeAdapter(status: 403, body: {'message': 'No.'});
    await expectLater(
      ApiClient(make(forbidden), checkConfig: false).post<Object?>('/api/asset-assignments'),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.forbidden)),
    );
    expect(refreshes, 0);

    final unauthorized = _FakeAdapter(status: 401);
    await expectLater(
      ApiClient(make(unauthorized), checkConfig: false).get<Object?>('/api/asset-registrations'),
      throwsA(isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.unauthorized)),
    );
    expect(refreshes, 1);
    expect(unauthorized.last!.headers['Authorization'], 'Bearer new');
  });

  test('timeouts and no network', () async {
    expect((await _fail(_FakeAdapter(error: DioExceptionType.connectionTimeout))).kind, ApiErrorKind.timeout);
    expect((await _fail(_FakeAdapter(error: DioExceptionType.receiveTimeout))).kind, ApiErrorKind.timeout);
    final offline = await _fail(_FakeAdapter(error: DioExceptionType.connectionError));
    expect(offline.kind, ApiErrorKind.noNetwork);
    expect(offline.message, contains('http://backend.test:5296'));
  });

  test('without dart-defines every service defaults to the demo cloud (no config problem)', () {
    expect(kUseApi, isTrue, reason: 'USE_API defaults to true');
    for (final s in BackendService.values) {
      expect(s.buildUrl, kDemoCloudUrl, reason: s.define);
      expect(s.baseUrl, 'https://a360demo.purpleiq.ai', reason: s.define);
      expect(s.configProblem, isNull, reason: s.define);
    }
    expect(usesSingleGateway, isTrue);
    expect(httpsOnlyBuild, isTrue, reason: 'plain build = https only, no switcher');
  });

  test('an empty or scheme-less service URL is a config problem that names the define', () {
    expect(serviceUrlProblem('API_BASE_URL', '', '5401'), allOf(contains('API_BASE_URL'), contains('5401')));
    expect(serviceUrlProblem('MEDIA_BASE_URL', '', '5300'), allOf(contains('MEDIA_BASE_URL'), contains('5300')));
    expect(serviceUrlProblem('AUTH_BASE_URL', 'a360demo.purpleiq.ai', '5018'), contains('must start with http'));
    expect(serviceUrlProblem('AUTH_BASE_URL', kDemoCloudUrl, '5018'), isNull);
  });

  test('a missing token does not block the call (no Authorization header)', () async {
    for (final token in <Future<String?> Function()>[() async => null, () async => throw StateError('no session')]) {
      final adapter = _FakeAdapter(body: []);
      final dio = Dio(BaseOptions(baseUrl: 'http://asset.test:5401'))
        ..httpClientAdapter = adapter
        ..interceptors.add(AuthInterceptor(token: token));
      final res = await ApiClient(dio, checkConfig: false).get<Object?>('/api/asset-categories');
      expect(res.statusCode, 200);
      expect(adapter.last!.headers.containsKey('Authorization'), isFalse);
    }
  });

  test('upload posts multipart to /api/media/upload with the token and rewrites a localhost URL', () async {
    final adapter = _FakeAdapter(body: {'url': 'http://localhost:5300/uploads/2026/a.jpg'});
    final dio = Dio(BaseOptions(baseUrl: 'http://media.test:5300'))
      ..httpClientAdapter = adapter
      ..interceptors.add(AuthInterceptor(token: () async => 'test-token'));
    final api = MediaApi(ApiClient(dio, checkConfig: false, service: BackendService.media));
    final up = await api.upload(fileName: 'a.jpg', category: 'asset-images', bytes: [1, 2, 3]);
    expect(adapter.last!.uri.toString(), 'http://media.test:5300/api/media/upload');
    final form = adapter.last!.data as FormData;
    expect({for (final f in form.fields) f.key: f.value}, {'category': 'asset-images'});
    expect(form.files.single.key, 'file');
    expect(adapter.last!.headers['Authorization'], 'Bearer test-token');
    // Tests run without MEDIA_BASE_URL = the demo cloud default: localhost moves to the gateway.
    expect(up.url, 'https://a360demo.purpleiq.ai/uploads/2026/a.jpg');
  });

  test('resolveMediaUrl: localhost / 127.0.0.1 / relative → MEDIA_BASE_URL + same path', () {
    const media = 'http://172.16.100.26:5300';
    expect(
      resolveMediaUrl('http://localhost:5300/uploads/a.jpg?v=2', mediaBase: media),
      'http://172.16.100.26:5300/uploads/a.jpg?v=2',
    );
    expect(
      resolveMediaUrl('https://127.0.0.1:7000/uploads/x/b.png', mediaBase: media),
      'http://172.16.100.26:5300/uploads/x/b.png',
    );
    expect(resolveMediaUrl('/uploads/c.pdf', mediaBase: media), 'http://172.16.100.26:5300/uploads/c.pdf');
    expect(
      resolveMediaUrl('http://cdn.example.com/uploads/d.jpg', mediaBase: media),
      'http://cdn.example.com/uploads/d.jpg',
    );
    expect(resolveMediaUrl('http://localhost/uploads/e.jpg', mediaBase: ''), 'http://localhost/uploads/e.jpg');
    // Media now returns its configured BaseUrl; the phone may reach the same service under another host.
    expect(
      resolveMediaUrl('http://172.16.100.26:5300/uploads/asset-images/g.jpg', mediaBase: 'http://10.0.2.2:5300'),
      'http://10.0.2.2:5300/uploads/asset-images/g.jpg',
    );
    expect(
      resolveMediaUrl('http://other.host:8080/uploads/h.jpg', mediaBase: media),
      'http://other.host:8080/uploads/h.jpg',
    );
    // Cloud gateway: https media URLs are kept as returned; old office-IP / localhost ones move to the gateway.
    const cloud = 'https://a360demo.purpleiq.ai';
    expect(
      resolveMediaUrl('https://a360demo.purpleiq.ai/uploads/asset-images/k.jpg', mediaBase: cloud),
      'https://a360demo.purpleiq.ai/uploads/asset-images/k.jpg',
    );
    expect(
      resolveMediaUrl('https://a360demo.purpleiq.ai/uploads/k.jpg', mediaBase: media),
      'https://a360demo.purpleiq.ai/uploads/k.jpg',
      reason: 'an https URL is never rewritten',
    );
    expect(
      resolveMediaUrl('http://172.16.100.29:5300/uploads/old.jpg', mediaBase: cloud),
      'https://a360demo.purpleiq.ai/uploads/old.jpg',
    );
    expect(
      resolveMediaUrl('http://localhost:5300/uploads/l.jpg', mediaBase: cloud),
      'https://a360demo.purpleiq.ai/uploads/l.jpg',
    );
    expect(resolveMediaUrl('/uploads/r.jpg', mediaBase: cloud), 'https://a360demo.purpleiq.ai/uploads/r.jpg');
    expect(
      MediaUpload.fromJson({
        'data': {'filePath': '/uploads/f.jpg'},
      }).url,
      'https://a360demo.purpleiq.ai/uploads/f.jpg',
      reason: 'a bare path joins MEDIA_BASE_URL (demo cloud by default)',
    );
  });

  test('Test connection: GET /health on all 7 services (… DASHBOARD, ADMIN, TRACKING), one result each', () async {
    final adapter = _HealthAdapter();
    final c = ProviderContainer(
      overrides: [
        healthDioProvider.overrideWithValue(Dio()..httpClientAdapter = adapter),
        serviceTargetsProvider.overrideWithValue({
          BackendService.auth: (baseUrl: 'http://h.test:5018', problem: null),
          BackendService.asset: (baseUrl: 'http://h.test:5401/', problem: null),
          BackendService.media: (baseUrl: '', problem: 'MEDIA_BASE_URL is not set.'),
          BackendService.masterData: (baseUrl: 'http://h.test:5500', problem: null),
          BackendService.dashboard: (baseUrl: 'http://h.test:5470', problem: null),
          BackendService.admin: (baseUrl: 'http://h.test:5460', problem: null),
          BackendService.tracking: (baseUrl: 'http://h.test:5490', problem: null),
        }),
      ],
    );
    addTearDown(c.dispose);
    final future = c.read(connectionTestProvider.notifier).run();
    expect(c.read(connectionTestProvider).isLoading, isTrue);
    await future;
    final r = c.read(connectionTestProvider).value!;
    expect(r.map((e) => e.service), BackendService.values);
    expect(r[0].ok, isTrue);
    expect(r[0].url, 'http://h.test:5018/health');
    expect(r[0].status, 'Healthy');
    expect(r[1].ok, isFalse, reason: 'the fake answers 503 for 5401');
    expect(r[1].url, 'http://h.test:5401/health');
    expect(r[1].error, contains('503'));
    expect(r[2].ok, isFalse);
    expect(r[2].error, contains('MEDIA_BASE_URL'));
    expect(r[3].ok, isTrue);
    expect(r[3].url, 'http://h.test:5500/health');
    expect(r[4].ok, isTrue);
    expect(r[4].url, 'http://h.test:5470/health');
    expect(r, hasLength(7));
    expect(r[5].ok, isTrue);
    expect(r[5].url, 'http://h.test:5460/health');
    expect(r[6].ok, isTrue);
    expect(r[6].url, 'http://h.test:5490/health');
    expect(adapter.calls, [
      'http://h.test:5018/health',
      'http://h.test:5401/health',
      'http://h.test:5500/health',
      'http://h.test:5470/health',
      'http://h.test:5460/health',
      'http://h.test:5490/health',
    ]);
  });

  test('Test connection through the cloud gateway: own health path, else "via gateway" + a cheap GET', () async {
    final adapter = _GatewayAdapter();
    final dio = Dio()..httpClientAdapter = adapter;
    const base = 'https://a360demo.purpleiq.ai';
    final c = ProviderContainer(
      overrides: [
        healthDioProvider.overrideWithValue(dio),
        serviceDioProvider.overrideWith((ref, s) => dio),
        serviceTargetsProvider.overrideWithValue({
          for (final s in BackendService.values) s: (baseUrl: base, problem: null),
        }),
      ],
    );
    addTearDown(c.dispose);
    await c.read(connectionTestProvider.notifier).run();
    final gw = c.read(connectionTestProvider.notifier).gateway!;
    expect(gw.ok, isTrue, reason: 'the host answered, only with the web app');
    expect(gw.detail, contains('not exposed'));
    expect(adapter.calls.take(2), ['/health', '/gateway/health']);
    final r = {for (final h in c.read(connectionTestProvider).value!) h.service: h};
    expect(r[BackendService.dashboard]!.ok, isTrue);
    expect(r[BackendService.dashboard]!.viaGateway, isFalse);
    expect(r[BackendService.dashboard]!.url, '$base/dashboard/health');
    expect(r[BackendService.asset]!.viaGateway, isTrue);
    expect(r[BackendService.asset]!.ok, isTrue);
    expect(r[BackendService.asset]!.url, '$base/api/asset-registrations?pageSize=1');
    expect(r[BackendService.auth]!.url, '$base/api/auth/roles');
    expect(r[BackendService.media]!.ok, isTrue, reason: '405 on the POST-only route = Media answered');
    expect(r[BackendService.media]!.status, 'Reachable');
    expect(r[BackendService.tracking]!.ok, isFalse);
    expect(r[BackendService.tracking]!.viaGateway, isTrue);
    expect(r[BackendService.tracking]!.error, contains('502'));
    expect(adapter.calls, contains('/tracking/health'));
  });
}

/// The cloud gateway as seen on a360demo: every */health path answers the web app's HTML (200); the API routes
/// answer JSON; Media's upload route answers 405 to a GET; Tracking is down (502).
class _GatewayAdapter implements HttpClientAdapter {
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    calls.add(o.uri.path);
    final (int code, String body, String type) = switch (o.uri.path) {
      final p when p.endsWith('/health') && p.startsWith('/dashboard') => (
        200,
        '{"status":"Healthy"}',
        'application/json',
      ),
      final p when p.endsWith('/health') => (200, '<!doctype html><html></html>', 'text/html'),
      '/api/media/upload' => (405, '', 'text/plain'),
      final p when p.startsWith('/api/iot-devices') => (502, '', 'text/plain'),
      _ => (200, '{"items":[]}', 'application/json'),
    };
    return ResponseBody.fromString(
      body,
      code,
      headers: {
        Headers.contentTypeHeader: [type],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

/// /health on port 5018 → Healthy; anything else → 503.
class _HealthAdapter implements HttpClientAdapter {
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    calls.add(o.uri.toString());
    final ok =
        o.uri.port == 5018 || o.uri.port == 5500 || o.uri.port == 5470 || o.uri.port == 5460 || o.uri.port == 5490;
    return ResponseBody.fromString(
      ok ? jsonEncode({'status': 'Healthy', 'service': 'UserAccount'}) : '',
      ok ? 200 : 503,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
