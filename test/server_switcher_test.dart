import 'dart:convert';
import 'dart:typed_data';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/app_restart.dart';
import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/auth_api.dart';
import 'package:asset360_mobile/data/api/auth_diagnostics.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/server_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/auth/auth_diagnostics_screen.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Records every call and answers 200 {}.
class _Recorder implements HttpClientAdapter {
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    calls.add('${o.method} ${o.uri}');
    return ResponseBody.fromString(
      jsonEncode({}),
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    debugShowServerSwitcher = true;
  });
  tearDown(() {
    applyServerOverride(null);
    debugShowServerSwitcher = null;
    debugApiMode = null;
  });

  test('normalizeServerOrigin keeps scheme + host only', () {
    expect(normalizeServerOrigin('172.16.100.40'), 'http://172.16.100.40');
    expect(normalizeServerOrigin(' http://172.16.100.40:5018/api '), 'http://172.16.100.40');
    expect(normalizeServerOrigin('https://dev.example.com/x'), 'https://dev.example.com');
    expect(normalizeServerOrigin(''), isNull);
    expect(normalizeServerOrigin('not a host!'), isNull);
    expect(normalizeServerOrigin('ftp://x'), isNull);
  });

  test('the chosen host replaces the host of all 7 base URLs; ports stay', () {
    applyServerOverride('http://172.16.100.26');
    final ports = {for (final s in BackendService.values) s: Uri.parse(s.baseUrl).port};
    expect(ports, {
      BackendService.auth: 5018,
      BackendService.asset: 5401,
      BackendService.media: 5300,
      BackendService.masterData: 5500,
      BackendService.dashboard: 5470,
      BackendService.admin: 5460,
      BackendService.tracking: 5490,
    });
    for (final s in BackendService.values) {
      expect(Uri.parse(s.baseUrl).host, '172.16.100.26', reason: s.define);
      expect(s.configProblem, isNull, reason: s.define);
    }
    expect(serverDisplayOf(currentServerOrigin), 'Server .26 (172.16.100.26)');
    expect(serverDisplayOf('http://10.0.0.5'), '10.0.0.5');

    applyServerOverride(null);
    expect(kApiBaseUrl, isEmpty);
    expect(apiBaseUrl, kApiBaseUrl, reason: 'no choice = the build URLs');
    expect(masterDataBaseUrl, kMasterDataBaseUrl);
  });

  test('the choice is saved on the device; the build server or a client build means no override', () async {
    final c = ProviderContainer();
    addTearDown(c.dispose);
    await c.read(serverProvider.notifier).select('172.16.100.26');
    expect(c.read(serverProvider), 'http://172.16.100.26');
    final p = await SharedPreferences.getInstance();
    expect(p.getString(ServerNotifier.storageKey), 'http://172.16.100.26');

    applyServerOverride(null);
    await loadSavedServer();
    expect(serverOverride, 'http://172.16.100.26', reason: 'restored at start-up');

    debugShowServerSwitcher = false;
    await loadSavedServer();
    expect(serverOverride, isNull, reason: 'client builds ignore a saved server');

    debugShowServerSwitcher = true;
    await c.read(serverProvider.notifier).select(buildServerOrigin);
    expect(c.read(serverProvider), isNull);
    expect(p.getString(ServerNotifier.storageKey), isNull);
  });

  test('every service Dio follows the selected server', () async {
    final c = ProviderContainer();
    addTearDown(c.dispose);
    expect(c.read(masterDataDioProvider).options.baseUrl, 'http://172.16.100.29:5500');
    await c.read(serverProvider.notifier).select('http://172.16.100.26');
    expect(c.read(dioProvider).options.baseUrl, 'http://172.16.100.26:5401');
    expect(c.read(masterDataDioProvider).options.baseUrl, 'http://172.16.100.26:5500');
    expect(c.read(trackingDioProvider).options.baseUrl, 'http://172.16.100.26:5490');
    expect(c.read(authDioProvider).options.baseUrl, 'http://172.16.100.26:5018');
  });

  test('server switch sign-out: logout sent to the old server, token and session cleared', () async {
    debugApiMode = true;
    final server = _Recorder();
    final store = MemoryTokenStore('refresh-1');
    final c = ProviderContainer(
      overrides: [
        tokenStoreProvider.overrideWithValue(store),
        authApiProvider.overrideWith(
          (ref) => AuthApi(
            Dio(BaseOptions(baseUrl: 'http://172.16.100.29:5018'))..httpClientAdapter = server,
            ref.read(authDiagnosticsProvider.notifier),
            checkConfig: false,
          ),
        ),
      ],
    );
    addTearDown(c.dispose);
    await c.read(authProvider.notifier).signOutForServerSwitch();
    expect(store.refreshToken, isNull);
    expect(c.read(authProvider), isA<AuthSignedOut>());
    await Future<void>.delayed(const Duration(milliseconds: 50));
    expect(server.calls.single, contains('172.16.100.29:5018/api/auth/logout'));
  });

  testWidgets('Login shows the server; switching saves it, restarts and returns to Login on the new server', (
    tester,
  ) async {
    debugApiMode = true;
    FloorPlanImageCache.useDisk = false;
    addTearDown(() => FloorPlanImageCache.useDisk = true);
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      AppRestartScope(
        overrides: [tokenStoreProvider.overrideWithValue(MemoryTokenStore())],
        child: const Asset360App(),
      ),
    );
    await tester.pump(SplashScreen.duration);
    await tester.pumpAndSettle();
    expect(find.text('Server: Server .29 (172.16.100.29)'), findsOneWidget);

    await tester.ensureVisible(find.byKey(const ValueKey('login-server')));
    await tester.tap(find.byKey(const ValueKey('login-server')));
    await tester.pumpAndSettle();
    expect(find.text('Backend server'), findsOneWidget);
    expect(find.text('http://172.16.100.29 · build default'), findsOneWidget);

    // Custom: a bad host is refused.
    await tester.tap(find.text('Custom'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).last, 'bad host!');
    await tester.tap(find.text('Use this server'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Enter an IP address or host name'), findsOneWidget);

    await tester.tap(find.text('Server .26'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Use this server'));
    await tester.pumpAndSettle();
    expect(find.text('Switch to Server .26?'), findsOneWidget);
    await tester.tap(find.text('Switch'));
    // Clearing the drift cache and saving the choice is real I/O.
    for (var i = 0; i < 5; i++) {
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pump();
    }
    await tester.pump(SplashScreen.duration);
    await tester.pumpAndSettle();

    expect(find.text('Server: Server .26 (172.16.100.26)'), findsOneWidget);
    expect(serverOverride, 'http://172.16.100.26');
    expect((await SharedPreferences.getInstance()).getString(ServerNotifier.storageKey), 'http://172.16.100.26');
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('Diagnostics shows the server; the switcher button only with SHOW_SERVER_SWITCHER', (tester) async {
    applyServerOverride('172.16.100.26');
    Future<void> pump() async {
      await tester.pumpWidget(const ProviderScope(child: MaterialApp(home: AuthDiagnosticsScreen())));
      await tester.pumpAndSettle();
    }

    await pump();
    expect(find.text('Server .26 (172.16.100.26)'), findsOneWidget);
    expect(find.text('http://172.16.100.26:5018'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Backend server'), findsOneWidget);

    debugShowServerSwitcher = false;
    await tester.pumpWidget(const SizedBox());
    await pump();
    expect(find.widgetWithText(OutlinedButton, 'Backend server'), findsNothing);
  });
}
