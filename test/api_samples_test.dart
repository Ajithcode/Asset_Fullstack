import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/api_samples.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'live/fake_asset_backend.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

void main() {
  group('shortening + redaction', () {
    test('lists keep their first 3 items at every depth', () {
      final out = shortenSample({
        'items': [
          for (var i = 0; i < 5; i++)
            {
              'n': i,
              'tags': [1, 2, 3, 4],
            },
        ],
      });
      final items = (out! as Map)['items'] as List;
      expect(items, hasLength(4));
      expect(items.last, '… 2 more (5 items)');
      expect((items.first as Map)['tags'], [1, 2, 3, '… 1 more (4 items)']);
    });

    test('token / password / secret / otp / qrcode / apiKey / privateKey values: first 6 characters + "…"', () {
      final out =
          shortenSample({
                'accessToken': 'eyJhbGciOiJIUzI1NiJ9.payload',
                'apiKey': 'abcdefghij',
                'Password': 'hunter2!',
                'clientSecret': 'shh-very-secret',
                'qrCodeValue': 'QR-000123-XYZ',
                'otp': 123456,
                'privateKey': 'MIIEvQIBADANBg',
                'key': 'plain-key-value',
                'assetKey': 'AK-000123',
                'keyword': 'pump',
                'name': 'Pump 1',
                'nested': {'refreshToken': 'r-1234567890'},
              })!
              as Map;
      expect(out['accessToken'], 'eyJhbG…');
      expect(out['apiKey'], 'abcdef…');
      expect(out['Password'], 'hunter…');
      expect(out['clientSecret'], 'shh-ve…');
      expect(out['qrCodeValue'], 'QR-000…');
      expect(out['otp'], '******…');
      expect(out['privateKey'], 'MIIEvQ…');
      expect(out['key'], 'plain-key-value', reason: 'plain "key" is not masked');
      expect(out['assetKey'], 'AK-000123');
      expect(out['keyword'], 'pump');
      expect(out['name'], 'Pump 1');
      expect((out['nested'] as Map)['refreshToken'], 'r-1234…');
    });

    test('pretty JSON cut to 4,000 characters; text replies stay text', () {
      final long = prettySample({'text': 'x' * 6000});
      expect(long.length, lessThan(sampleMaxChars + 80));
      expect(long, contains('cut at 4000'));
      expect(prettySample('{"a":[1,2,3,4]}'), contains('"a": ['));
      expect(prettySample('Not JSON'), 'Not JSON');
    });
  });

  group('screen', () {
    late FakeAssetBackend backend;

    setUp(() {
      debugShowServerSwitcher = true;
      backend = FakeAssetBackend();
    });
    tearDown(() => debugShowServerSwitcher = null);

    Future<void> pump(WidgetTester tester) async {
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
      final c = ProviderScope.containerOf(tester.element(find.byType(Scaffold).first));
      c.read(routerProvider).go(Routes.apiSamples);
      await tester.pumpAndSettle();
    }

    Future<void> see(WidgetTester tester, Finder f) async {
      final page = find.byWidgetPredicate((w) => w is Scrollable && w.axisDirection == AxisDirection.down).first;
      for (var i = 0; i < 40 && f.evaluate().isEmpty; i++) {
        await tester.drag(page, const Offset(0, -200));
        await tester.pump();
      }
      await tester.ensureVisible(f);
      await tester.pumpAndSettle();
    }

    testWidgets('every preset is listed; Fetch shows URL, status, time and short JSON; Copy', (tester) async {
      await pump(tester);
      for (final s in apiSamples) {
        await see(tester, find.text(s.label));
        expect(find.text(s.label), findsOneWidget, reason: s.label);
      }
      await see(tester, find.text('Custom'));
      await tester.drag(
        find.byWidgetPredicate((w) => w is Scrollable && w.axisDirection == AxisDirection.down).first,
        const Offset(0, 4000),
      );
      await tester.pumpAndSettle();

      final row = find.text('/api/asset-registrations?pageSize=3');
      await see(tester, row);
      final fetch = find.descendant(
        of: find.ancestor(of: row, matching: find.byType(Row)).first,
        matching: find.text('Fetch'),
      );
      await tester.tap(fetch);
      await tester.pumpAndSettle();
      expect(find.text('HTTP 200'), findsOneWidget);
      expect(find.textContaining('http://asset.test:5401/api/asset-registrations?pageSize=3'), findsOneWidget);
      expect(find.textContaining('"assetRegistrationId": "AR000001"'), findsOneWidget);
      expect(find.textContaining(' ms'), findsWidgets);
      final call = backend.calls('GET', '/api/asset-registrations').single;
      expect(call.uri.queryParameters['pageSize'], '3');

      String? copied;
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, (c) async {
        if (c.method == 'Clipboard.setData') copied = (c.arguments as Map)['text'] as String;
        return null;
      });
      await see(tester, find.text('Copy'));
      await tester.tap(find.text('Copy'));
      await tester.pump();
      expect(copied, startsWith('GET http://asset.test:5401/api/asset-registrations?pageSize=3\nHTTP 200 · '));
      expect(copied, contains('"assetRegistrationId": "AR000001"'));
    });

    testWidgets('Custom: service + path + query; a 404 is shown as a result', (tester) async {
      await pump(tester);
      await see(tester, find.text('GET ASSET'));
      await tester.enterText(find.widgetWithText(TextField, 'Path'), '/api/nothing-here');
      await tester.enterText(find.widgetWithText(TextField, 'Query (optional)'), 'a=1');
      final fetch = find.descendant(
        of: find.ancestor(of: find.text('GET ASSET'), matching: find.byType(Row)).first,
        matching: find.text('Fetch'),
      );
      await tester.ensureVisible(fetch);
      await tester.tap(fetch);
      await tester.pumpAndSettle();
      expect(find.text('HTTP 404'), findsOneWidget);
      expect(backend.calls('GET', '/api/nothing-here').single.uri.queryParameters['a'], '1');
    });

    testWidgets('hidden in builds without the server switcher', (tester) async {
      debugShowServerSwitcher = false;
      await pump(tester);
      expect(find.text('Developer builds only'), findsOneWidget);
      expect(find.text('Fetch'), findsNothing);
    });
  });
}
