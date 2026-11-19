import 'dart:async';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/licence/licence.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/network_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_asset_backend.dart';
import 'tracking_samples.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

class _FakeTimer implements Timer {
  _FakeTimer(this.callback);

  final void Function() callback;
  bool cancelled = false;

  @override
  void cancel() => cancelled = true;

  @override
  bool get isActive => !cancelled;

  @override
  int get tick => 0;
}

/// API mode: a locked module's route opens the overlay and never calls that module's service.
void main() {
  late FakeAssetBackend backend;

  setUp(() {
    backend = FakeAssetBackend()..seedTracking(DateTime.now);
    debugApiMode = true;
    NetworkNotifier.listenToDevice = false;
    TrkLiveNotifier.debugSchedule = (d, cb) => _FakeTimer(cb);
  });

  tearDown(() {
    debugApiMode = null;
    NetworkNotifier.listenToDevice = true;
    TrkLiveNotifier.debugSchedule = null;
    debugLicensedModules = AppModule.values.toSet();
  });

  Future<ProviderContainer> boot(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          ...backend.overrides,
          sessionProvider.overrideWith(_SignedIn.new),
          tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
        ],
        child: const Asset360App(),
      ),
    );
    await tester.pump(SplashScreen.duration);
    await tester.pumpAndSettle();
    return ProviderScope.containerOf(tester.element(find.byType(Scaffold).first));
  }

  bool trackingCalled() => backend.requests.any((o) => o.uri.host == 'trk.test' || o.uri.host == 'admin.test');

  testWidgets('Tracking locked by LICENSED_MODULES: overlay, no Tracking / Admin-maps calls', (tester) async {
    debugLicensedModules = {...defaultLicensedModules}..remove(AppModule.tracking);
    final c = await boot(tester);
    backend.requests.clear();
    for (final route in [Routes.tracking, Routes.trackingAlerts, Routes.trackSensor('SEN-T-80900')]) {
      c.read(routerProvider).go(route);
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('licence-lock-tracking')), findsOneWidget, reason: route);
    }
    expect(trackingCalled(), isFalse, reason: backend.requests.map((o) => o.uri).join('\n'));
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('Tracking licensed (default): the hub loads from the Tracking service', (tester) async {
    debugLicensedModules = defaultLicensedModules;
    final c = await boot(tester);
    backend.requests.clear();
    c.read(routerProvider).go(Routes.tracking);
    await tester.pumpAndSettle();
    expect(find.text(LicenceText.title), findsNothing);
    expect(trackingCalled(), isTrue);
    await tester.pumpWidget(const SizedBox());
  });
}
