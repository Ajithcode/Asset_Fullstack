import 'dart:convert';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/api/assets/json_read.dart';
import 'package:asset360_mobile/data/api/token_store.dart';
import 'package:asset360_mobile/data/repositories/network_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/features/assets/live/live_assignment_screens.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_asset_backend.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

const _assignments = '/api/asset-assignments';
const _saraId = '6650a1b2c3d4e5f600000e01';
const _return = '/api/asset-assignments/asg-db-1/return';

/// API mode Assign Custodian / Check Out (POST /api/asset-assignments with the Master Data
/// employee) and Move Asset (Master Data location pickers, no move endpoint yet).
void main() {
  late FakeAssetBackend backend;

  setUp(() {
    backend = FakeAssetBackend()
      ..on(
        'POST',
        _assignments,
        (o) => FakeReply(201, {
          'id': 'asg-db-1',
          'assignmentNumber': 'ASG-000001',
          'status': 'Active',
          'assignedToName': (o.data as Map)['employee']?['employeeName'],
        }),
      );
    debugApiMode = true;
    NetworkNotifier.listenToDevice = false;
  });

  tearDown(() {
    debugApiMode = null;
    NetworkNotifier.listenToDevice = true;
  });

  Future<void> open(WidgetTester tester, String route) async {
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
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
    // Start on the asset, then open the form on top (so it can pop back after saving).
    c.read(routerProvider).go('/assets/detail/AR000001');
    await tester.pumpAndSettle();
    c.read(routerProvider).push(route);
    await tester.pumpAndSettle();
  }

  Finder button(String label) =>
      find.ancestor(of: find.text(label), matching: find.byWidgetPredicate((w) => w is FilledButton)).last;

  Future<void> tap(WidgetTester tester, Finder f) async {
    await tester.ensureVisible(f);
    await tester.pumpAndSettle();
    await tester.tap(f);
    await tester.pumpAndSettle();
  }

  Future<void> pickSara(WidgetTester tester) async {
    await tap(tester, find.text('Search by name or employee ID'));
    expect(find.textContaining('EMP-0042'), findsOneWidget, reason: 'Master Data employees');
    await tester.tap(find.text('Sara Ali').last);
    await tester.pumpAndSettle();
  }

  Map<String, dynamic> sent() =>
      jsonDecode(jsonEncode(backend.calls('POST', _assignments).single.data)) as Map<String, dynamic>;

  testWidgets('Assign Custodian: employee required; posts an Employee assignment with the Master Data employee', (
    tester,
  ) async {
    await open(tester, Routes.moreActionFrom('/assets', 'assign-custodian', 'AR000001'));
    expect(find.text('Assign Custodian'), findsWidgets);
    await tap(tester, button('Assign Custodian'));
    expect(find.text('Choose the custodian'), findsOneWidget);
    expect(backend.calls('POST', _assignments), isEmpty);

    await pickSara(tester);
    expect(find.text('Employee Details'), findsOneWidget);
    expect(find.text('Facilities Technician'), findsOneWidget);
    await tap(tester, button('Assign Custodian'));
    final body = sent();
    expect(body['assetRegistrationIds'], ['AR000001']);
    expect([body['selectionMode'], body['assignTo'], body['assignmentType']], ['Single', 'Employee', 'Custodian']);
    expect(body['isOpenEnded'], isTrue);
    expect(body['toDate'], isNull);
    expect(body['fromDate'], isNotNull);
    expect(body['employee'], {
      'employeeId': _saraId,
      'employeeCode': 'EMP-0042',
      'employeeName': 'Sara Ali',
      'designation': 'Facilities Technician',
      'departmentId': 'md-dep-1',
      'departmentName': 'Facilities Management',
      'manager': null,
    });
    expect(find.textContaining('custodian set to Sara Ali (ASG-000001)'), findsOneWidget);
    expect(find.text('Assign Custodian'), findsNothing, reason: 'back on the asset');
  });

  testWidgets('Check Out: posts a "Check Out" assignment with the return date; Check In without an Active '
      'assignment explains why; a server error stays on the form', (tester) async {
    await open(tester, Routes.moreActionFrom('/assets', 'check-in-out', 'AR000001'));
    expect(find.text('Expected Return Date'), findsOneWidget, reason: 'not assigned: opens on Check Out');
    await tap(tester, find.descendant(of: find.byType(SegmentTabs), matching: find.text('Check In')));
    expect(find.text(notAssignedMessage), findsOneWidget);
    await tap(tester, button('Check In'));
    expect(backend.requests.where((r) => r.uri.path.endsWith('/return')), isEmpty);
    await tap(tester, find.descendant(of: find.byType(SegmentTabs), matching: find.text('Check Out')));

    await pickSara(tester);
    backend.on(
      'POST',
      _assignments,
      (_) => const FakeReply(400, {
        'title': 'One or more validation errors occurred.',
        'status': 400,
        'errors': {
          'AssetRegistrationIds': ['AR000001 is already assigned'],
        },
      }),
    );
    await tap(tester, button('Check Out'));
    expect(find.byType(SegmentTabs), findsOneWidget, reason: 'the form stays open');
    expect(backend.calls('POST', _assignments), hasLength(1));

    backend.on('POST', _assignments, (o) => const FakeReply(201, {'id': 'x', 'assignmentNumber': 'ASG-000002'}));
    await tap(tester, button('Check Out'));
    final body = jsonDecode(jsonEncode(backend.calls('POST', _assignments).last.data)) as Map<String, dynamic>;
    expect(body['assignmentType'], 'Check Out');
    expect((body['employee'] as Map)['employeeId'], _saraId);
    expect(body['isOpenEnded'], isTrue, reason: 'no return date chosen');
    expect(find.textContaining('checked out to Sara Ali'), findsOneWidget);
  });

  testWidgets('AssignTo comes from /api/asset-assignments/options; without it nothing is sent', (tester) async {
    backend.on('GET', '/api/asset-assignments/options', (_) => const FakeReply(503, {'message': 'down'}));
    await open(tester, Routes.moreActionFrom('/assets', 'assign-custodian', 'AR000001'));
    expect(find.textContaining('Could not load the assignment options'), findsOneWidget);
    await pickSara(tester);
    await tap(tester, button('Assign Custodian'));
    expect(backend.calls('POST', _assignments), isEmpty);

    // The server's own spelling is sent back.
    backend.on(
      'GET',
      '/api/asset-assignments/options',
      (_) => const FakeReply(200, {
        'assignTo': ['EMPLOYEE', 'Department'],
      }),
    );
    await tap(tester, find.text('Retry'));
    expect(find.textContaining('Could not load the assignment options'), findsNothing);
    await tap(tester, button('Assign Custodian'));
    expect(sent()['assignTo'], 'EMPLOYEE');
  });

  Map<String, dynamic> returned() =>
      jsonDecode(jsonEncode(backend.calls('POST', _return).single.data)) as Map<String, dynamic>;

  /// A status chip inside the Assignment History card.
  Finder chip(String label) => find.descendant(
    of: find.ancestor(of: find.text('Assignment History'), matching: find.byType(SectionCard)),
    matching: find.byWidgetPredicate((w) => w is StatusChip && w.label == label),
  );

  testWidgets('Check In: opens on Check In for an Active assignment; condition required; POST /return with the '
      'asset, today and condition → remarks; asset + history refreshed', (tester) async {
    backend.addAssignment();
    await open(tester, Routes.moreActionFrom('/assets', 'check-in-out', 'AR000001'));
    expect(find.text('Current Assignment'), findsOneWidget, reason: 'Active assignment: opens on Check In');
    expect(find.text('ASG-000001'), findsOneWidget);
    expect(find.text('Sara Ali'), findsOneWidget);
    expect(
      backend
          .calls('GET', _assignments)
          .any(
            (r) =>
                r.uri.queryParameters['assetRegistrationId'] == 'AR000001' &&
                r.uri.queryParameters['status'] == 'Active',
          ),
      isTrue,
    );

    await tap(tester, button('Check In'));
    expect(find.text('Choose the condition', skipOffstage: false), findsOneWidget);
    expect(backend.calls('POST', _return), isEmpty);

    await tester.scrollUntilVisible(
      find.text('Good'),
      200,
      scrollable: find
          .byWidgetPredicate((w) => w is Scrollable && w.axisDirection == AxisDirection.down)
          .hitTestable()
          .first,
    );
    await tap(tester, find.text('Good'));
    await tester.enterText(find.widgetWithText(TextField, 'Anything missing or damaged?'), 'Lid scratched');
    await tester.pumpAndSettle();
    final assetLoads = backend.calls('GET', '/api/asset-registrations/AR000001').length;
    await tap(tester, button('Check In'));
    final body = returned();
    expect(body['assetRegistrationIds'], ['AR000001']);
    expect(body['returnDate'], jDay(DateTime.now()));
    expect(body['remarks'], 'Condition at return: Good. Lid scratched');
    expect(find.textContaining('checked in from Sara Ali (ASG-000001 returned)'), findsOneWidget);
    expect(find.text('Current Assignment'), findsNothing, reason: 'back on the asset');
    expect(backend.calls('GET', '/api/asset-registrations/AR000001').length, greaterThan(assetLoads));

    await _scrollTo(tester, find.text('Assignment History'));
    expect(chip('Returned'), findsOneWidget);
    expect(chip('Active'), findsNothing);
  });

  testWidgets('Return Asset: no Active assignment → "isn\'t assigned", nothing sent; a permission-check 503 '
      'offers Retry, which returns it', (tester) async {
    await open(tester, Routes.withAsset(Routes.returnAsset, 'AR000001'));
    expect(find.text(notAssignedMessage), findsOneWidget);
    await tap(tester, button('Return Asset'));
    expect(backend.requests.where((r) => r.uri.path.endsWith('/return')), isEmpty);

    backend.addAssignment();
    final c = ProviderScope.containerOf(tester.element(find.byType(Scaffold).first));
    c.read(routerProvider).pop();
    await tester.pumpAndSettle();
    c.read(routerProvider).push(Routes.withAsset(Routes.returnAsset, 'AR000001'));
    await tester.pumpAndSettle();
    expect(find.text('ASG-000001'), findsOneWidget);
    await tap(tester, find.text('Fair'));
    backend.failNext['POST $_return'] = const FakeReply(503, {
      'message': 'Your permissions could not be checked. Try again in a moment.',
    });
    await tap(tester, button('Return Asset'));
    expect(find.text(ApiException.permissionCheckMessage), findsOneWidget);
    expect(find.text('Current Assignment'), findsOneWidget, reason: 'the form stays open');
    await tap(tester, find.widgetWithText(SnackBarAction, 'Retry'));
    expect(backend.calls('POST', _return), hasLength(2));
    expect(find.textContaining('returned from Sara Ali'), findsOneWidget);
    expect(backend.assignments.single['status'], 'Returned');
  });

  testWidgets('Asset detail: Assignment History lists Draft / Active / Returned, newest first; Check In opens the '
      'Check In tab', (tester) async {
    backend
      ..addAssignment(id: 'asg-old', number: 'ASG-000001', status: 'Returned', createdAt: '2026-10-01T08:00:00Z')
      ..addAssignment(id: 'asg-draft', number: 'ASG-000002', status: 'Draft', createdAt: '2026-10-15T08:00:00Z')
      ..addAssignment(number: 'ASG-000003', assignedToName: 'Omar Khan', createdAt: '2026-11-01T08:00:00Z');
    await open(tester, Routes.assetDetail('AR000001'));
    await _scrollTo(tester, find.text('Assignment History'));
    expect(chip('Active'), findsOneWidget);
    expect(chip('Draft'), findsOneWidget);
    expect(chip('Returned'), findsOneWidget);
    final y = [
      for (final n in ['ASG-000003', 'ASG-000002', 'ASG-000001']) tester.getTopLeft(find.textContaining(n).last).dy,
    ];
    expect(y, orderedEquals([...y]..sort()), reason: 'newest first');

    await tap(tester, find.text('Check In').last);
    expect(find.text('Current Assignment'), findsOneWidget);
    expect(find.text('Omar Khan'), findsWidgets);
  });
}

/// Scrolls the visible page until [f] is built and on screen (the asset detail is a lazy list).
Future<void> _scrollTo(WidgetTester tester, Finder f) async {
  await tester.scrollUntilVisible(
    f,
    200,
    scrollable: find.byWidgetPredicate((w) => w is Scrollable && w.axisDirection == AxisDirection.down).hitTestable().first,
  );
  await tester.pumpAndSettle();
}
