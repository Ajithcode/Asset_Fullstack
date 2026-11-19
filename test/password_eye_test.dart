import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/core/widgets/password_eye_button.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

final _eye = find.byKey(const ValueKey('password-eye'));

/// Every password field's obscure flag.
List<bool> _obscured(WidgetTester tester) => [
  for (final e in tester.widgetList<EditableText>(find.byType(EditableText)))
    if (e.obscureText || e.keyboardType == TextInputType.visiblePassword || e.autofillHints != null) e.obscureText,
];

/// Hidden → crossed-out eye + "Show password"; visible → open eye + "Hide password".
void _expectEye(WidgetTester tester, Finder eye, {required bool hidden}) {
  final b = tester.widget<IconButton>(eye);
  expect((b.icon as Icon).icon, hidden ? Icons.visibility_off_outlined : Icons.visibility_outlined);
  expect(b.tooltip, hidden ? 'Show password' : 'Hide password');
}

ProviderContainer? _container;

/// Unmounts the app and disposes its container (stops the clock / live timers).
Future<void> _stop(WidgetTester tester) async {
  await tester.pumpWidget(const SizedBox());
  await tester.pump(const Duration(seconds: 1));
  _container?.dispose();
  _container = null;
}

Future<ProviderContainer> _start(WidgetTester tester, {required bool signedIn}) async {
  tester.view.physicalSize = const Size(1170, 2532);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  final c = ProviderContainer(overrides: [if (signedIn) sessionProvider.overrideWith(_SignedIn.new)]);
  _container = c;
  await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const Asset360App()));
  await tester.pump(SplashScreen.duration);
  await tester.pumpAndSettle();
  return c;
}

void main() {
  testWidgets('PasswordEyeButton: crossed-out eye while hidden, open eye while visible', (tester) async {
    var hidden = true;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: StatefulBuilder(
            builder: (context, set) =>
                PasswordEyeButton(obscured: hidden, onToggle: () => set(() => hidden = !hidden)),
          ),
        ),
      ),
    );
    _expectEye(tester, _eye, hidden: true);
    await tester.tap(_eye);
    await tester.pump();
    expect(hidden, isFalse);
    _expectEye(tester, _eye, hidden: false);
    await tester.tap(_eye);
    await tester.pump();
    _expectEye(tester, _eye, hidden: true);
  });

  testWidgets('Login: the password starts hidden; the eye toggles it with the right icon', (tester) async {
    await _start(tester, signedIn: false);
    final field = find.byWidgetPredicate((w) => w is EditableText && w.obscureText);
    expect(field, findsOneWidget, reason: 'starts hidden');
    _expectEye(tester, _eye, hidden: true);
    await tester.tap(_eye);
    await tester.pump();
    expect(find.byWidgetPredicate((w) => w is EditableText && w.obscureText), findsNothing);
    _expectEye(tester, _eye, hidden: false);
    await tester.tap(_eye);
    await tester.pump();
    expect(field, findsOneWidget);
    _expectEye(tester, _eye, hidden: true);
    await _stop(tester);
  });

  testWidgets('Profile → Change Password: every field starts hidden; the eye shows / hides them', (tester) async {
    final c = await _start(tester, signedIn: true);
    c.read(routerProvider).go('/home/profile');
    await tester.pumpAndSettle();
    final open = find.text('Change Password');
    await tester.scrollUntilVisible(open, 200, scrollable: find.byType(Scrollable).first);
    await tester.pumpAndSettle();
    await tester.tap(open);
    await tester.pumpAndSettle();
    final hiddenFields = find.byWidgetPredicate((w) => w is EditableText && w.obscureText);
    expect(hiddenFields, findsNWidgets(3));
    expect(_eye, findsNWidgets(3));
    for (final e in _eye.evaluate()) {
      _expectEye(tester, find.byWidget(e.widget), hidden: true);
    }
    await tester.tap(_eye.first);
    await tester.pump();
    expect(hiddenFields, findsNothing);
    for (final e in _eye.evaluate()) {
      _expectEye(tester, find.byWidget(e.widget), hidden: false);
    }
    await tester.tap(_eye.first);
    await tester.pump();
    expect(hiddenFields, findsNWidgets(3));
    expect(_obscured(tester).where((o) => o), hasLength(3));
    await _stop(tester);
  });
}
