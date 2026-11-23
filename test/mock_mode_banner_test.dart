import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/core/widgets/mock_mode_banner.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/features/auth/login_screen.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

final _banner = find.text(MockModeBanner.text);

Future<ProviderContainer> _start(WidgetTester tester, {bool signedIn = false}) async {
  tester.view.physicalSize = const Size(1170, 2532);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  final c = ProviderContainer(
    overrides: [
      tokenStoreProvider.overrideWithValue(MemoryTokenStore()),
      if (signedIn) sessionProvider.overrideWith(_SignedIn.new),
    ],
  );
  await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const Asset360App()));
  await tester.pump(SplashScreen.duration);
  await tester.pumpAndSettle();
  return c;
}

Future<void> _stop(WidgetTester tester, ProviderContainer c) async {
  await tester.pumpWidget(const SizedBox());
  await tester.pump(const Duration(seconds: 1));
  c.dispose();
}

void main() {
  tearDown(() => debugApiMode = false);

  testWidgets('Mock mode: the orange banner is on Login and on screens after sign-in', (tester) async {
    debugApiMode = false;
    var c = await _start(tester);
    expect(find.byType(LoginScreen), findsOneWidget);
    expect(_banner, findsOneWidget);
    final strip = tester.widget<Material>(find.byKey(const ValueKey('mock-mode-banner')));
    expect(strip.color, isNot(Colors.transparent));
    // The strip sits at the very top, above the screen.
    expect(tester.getTopLeft(_banner).dy, lessThan(tester.getTopLeft(find.byType(LoginScreen)).dy));
    await _stop(tester, c);

    c = await _start(tester, signedIn: true);
    expect(find.byType(LoginScreen), findsNothing);
    expect(_banner, findsOneWidget);
    await _stop(tester, c);
  });

  testWidgets('API mode: no mock banner on Login', (tester) async {
    debugApiMode = true;
    final c = await _start(tester);
    expect(find.byType(LoginScreen), findsOneWidget);
    expect(_banner, findsNothing);
    expect(find.byKey(const ValueKey('mock-mode-banner')), findsNothing);
    await _stop(tester, c);
  });
}
