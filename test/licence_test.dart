import 'dart:convert';
import 'dart:io';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/licence/licence.dart';
import 'package:asset360_mobile/app/router/app_router.dart';
import 'package:asset360_mobile/app/router/main_shell.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/core/demo_clock.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart' show ActionTile;
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/features/auth/splash_screen.dart';
import 'package:asset360_mobile/features/more/inventory/inventory_screen.dart';
import 'package:asset360_mobile/features/work/maintenance/maintenance_hub_screen.dart';
import 'package:asset360_mobile/features/work/work_hub_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

class _SignedIn extends SessionNotifier {
  @override
  SessionState build() => super.build().copyWith(loggedIn: true, rememberMe: true);
}

const _params = {
  'assetId': 'CNC-00123',
  'woId': 'WO-2026-0045',
  'unitId': 'PCB-A-4587',
  'moId': 'MO-2026-0018',
  'partId': 'BR-6205',
  'action': 'print-tag',
  'sensorId': 'SEN-T-80900',
};

/// Every concrete signed-in path in the router (parameters filled in).
List<String> _allPaths(List<RouteBase> routes, [String parent = '']) {
  final out = <String>[];
  for (final r in routes) {
    if (r is ShellRouteBase) {
      out.addAll(_allPaths(r.routes, parent));
    } else if (r is GoRoute) {
      final raw = r.path.startsWith('/') ? r.path : '${parent == '/' ? '' : parent}/${r.path}';
      final path = raw.replaceAllMapped(RegExp(r':(\w+)'), (m) {
        if (m[1] == 'id' && raw.contains('/reports/')) return 'maintenance';
        if (m[1] == 'id' && raw.contains('/admin/users/')) return 'rex';
        return _params[m[1]] ?? 'X-0001';
      });
      out.add(path);
      out.addAll(_allPaths(r.routes, path));
    }
  }
  return out;
}

/// Locked by default (everything except the default licence and always-open screens).
const _lockedByDefault = {
  AppModule.maintenance,
  AppModule.inspection,
  AppModule.wip,
  AppModule.audit,
  AppModule.inventory,
  AppModule.contracts,
  AppModule.financial,
};

/// Badged tiles in the More sheet with the default licence (the locked modules listed there).
const _moreSheetLocked = 7;

/// Screens that never get locked, whatever the licence.
const _alwaysOpen = [
  Routes.home,
  Routes.work,
  Routes.more,
  Routes.settings,
  Routes.help,
  Routes.reportBug,
  Routes.apiSamples,
  Routes.backendApi,
  Routes.settingsAuthDiagnostics,
  Routes.dataCheck,
  Routes.demoTags,
  Routes.scannerSettings,
  '/home/profile',
  '/work/profile',
  '/more/profile',
  '/home/notifications',
  '/more/notifications/settings',
];

/// A widget test that unmounts the app at the end (pending timers of the shell stop).
void lt(String description, Future<void> Function(WidgetTester tester) body) => testWidgets(description, (t) async {
  await body(t);
  await t.pumpWidget(const SizedBox());
  _container?.dispose();
  _container = null;
});

ProviderContainer? _container;

void main() {
  setUp(() {
    DemoClock.frozen = true;
    debugLicensedModules = defaultLicensedModules;
  });
  tearDown(() => debugLicensedModules = AppModule.values.toSet());

  Future<(ProviderContainer, GoRouter)> boot(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    final c = ProviderContainer(overrides: [sessionProvider.overrideWith(_SignedIn.new)]);
    _container = c;
    await tester.pumpWidget(UncontrolledProviderScope(container: c, child: const Asset360App()));
    await tester.pump(SplashScreen.duration);
    await tester.pumpAndSettle();
    return (c, c.read(routerProvider));
  }

  String loc(GoRouter r) => r.state.uri.path;
  Finder lockOverlay(AppModule m) => find.byKey(ValueKey('licence-lock-${m.key}'));
  Finder lockSheet(AppModule m) => find.byKey(ValueKey('licence-sheet-${m.key}'));
  Finder inBar(String label) => find.descendant(of: find.byType(AppBottomBar), matching: find.text(label));

  group('licence source', () {
    test('default list when LICENSED_MODULES is empty or names nothing known', () {
      expect(licenceFromBuildSetting('').licensed, defaultLicensedModules);
      expect(licenceFromBuildSetting('').source, LicenceSource.defaults);
      expect(licenceFromBuildSetting(' , bogus ').licensed, defaultLicensedModules);
      expect(defaultLicensedModules.map((m) => m.key).toSet(), {
        'dashboard',
        'assets',
        'scan',
        'movement',
        'tracking',
        'reports',
        'administration',
        'issues',
        'alerts',
        'documents',
        'account',
      });
      expect(defaultLicensedModules.union(_lockedByDefault), AppModule.values.toSet());
      expect(defaultLicensedModules.intersection(_lockedByDefault), isEmpty);
    });

    test('the env files list exactly the default modules ("admin" = Administration)', () {
      expect(AppModule.fromKey('admin'), AppModule.administration);
      expect(AppModule.fromKey('Administration'), AppModule.administration);
      for (final f in ['env/api_dev.json', 'env/api_old_26.json', 'env/api_demo.json', 'env/api_live.json']) {
        final raw = (jsonDecode(File(f).readAsStringSync()) as Map)['LICENSED_MODULES'] as String;
        expect(parseLicensedModules(raw), defaultLicensedModules, reason: f);
      }
    });

    test('LICENSED_MODULES overrides the default (case / spaces ignored, unknown keys dropped)', () {
      final l = licenceFromBuildSetting('assets, Maintenance ,scan,unknown');
      expect(l.source, LicenceSource.buildSetting);
      expect(l.licensed, {AppModule.assets, AppModule.maintenance, AppModule.scan});
      expect(l.isLicensed(AppModule.maintenance), isTrue);
      expect(l.isLicensed(AppModule.tracking), isFalse, reason: 'not in the list → locked');
    });

    test('the service uses the build setting / defaults until a backend licence exists', () {
      debugLicensedModules = null;
      final c = ProviderContainer();
      addTearDown(c.dispose);
      expect(c.read(licenceApiProvider), isNull, reason: 'TODO(licence-api)');
      final l = c.read(licenceProvider);
      expect(l.licensed, defaultLicensedModules);
      expect(c.read(licenceProvider.notifier).isLicensed(AppModule.maintenance), isFalse);
    });

    test('route → module map', () {
      expect(moduleForLocation(Routes.home), isNull);
      expect(moduleForLocation(Routes.work), isNull);
      expect(moduleForLocation(Routes.more), isNull);
      expect(moduleForLocation(Routes.assets), AppModule.assets);
      expect(moduleForLocation(Routes.assetDetail('X')), AppModule.assets);
      expect(moduleForLocation(Routes.newRegistration), AppModule.assets);
      expect(moduleForLocation('/work/more-actions/condition?asset=X'), AppModule.assets);
      expect(moduleForLocation('/more/scan/history'), AppModule.scan);
      expect(moduleForLocation(Routes.checkOut), AppModule.movement);
      expect(moduleForLocation(Routes.trackingAlerts), AppModule.tracking);
      expect(moduleForLocation(Routes.trackSensor('S')), AppModule.tracking);
      expect(moduleForLocation(Routes.maintenancePerformance), AppModule.maintenance);
      expect(moduleForLocation(Routes.recentAlerts), AppModule.alerts);
      expect(moduleForLocation(Routes.assetOverview), AppModule.dashboard);
      expect(moduleForLocation(Routes.createWorkOrder(assetId: 'X')), AppModule.maintenance);
      expect(moduleForLocation(Routes.reportIssue), AppModule.issues);
      expect(moduleForLocation(Routes.productionOrder('MO')), AppModule.wip);
      expect(moduleForLocation(Routes.compliance), AppModule.audit);
      expect(moduleForLocation(Routes.partDetail('P')), AppModule.inventory);
      expect(moduleForLocation(Routes.documentDetail('D')), AppModule.documents);
      expect(moduleForLocation(Routes.alertDetail('A')), AppModule.alerts);
      expect(moduleForLocation(Routes.admin), AppModule.administration);
      for (final r in [..._alwaysOpen, ...Routes.authRoutes]) {
        expect(moduleForLocation(r), isNull, reason: r);
      }
    });
  });

  lt('every route: licensed modules open normally, locked ones show the overlay', (tester) async {
    final (_, router) = await boot(tester);
    final paths = _allPaths(router.configuration.routes).where((p) => !Routes.authRoutes.contains(p)).toList();
    expect(paths.length, greaterThan(60));
    final lockedSeen = <AppModule>{};
    for (final p in paths) {
      router.go(p);
      await tester.pumpAndSettle();
      final m = moduleForLocation(p);
      expect(find.byType(AppBottomBar), findsOneWidget, reason: p);
      expect(tester.takeException(), isNull, reason: p);
      if (m != null && _lockedByDefault.contains(m)) {
        lockedSeen.add(m);
        expect(lockOverlay(m), findsOneWidget, reason: p);
        expect(find.text(LicenceText.title), findsOneWidget, reason: p);
        expect(find.text(LicenceText.backToDashboard), findsOneWidget, reason: p);
        expect(
          find.descendant(of: lockOverlay(m), matching: find.bySubtype<ButtonStyleButton>()),
          findsOneWidget,
          reason: '$p: one button only',
        );
      } else {
        expect(find.text(LicenceText.title), findsNothing, reason: p);
      }
    }
    expect(lockedSeen, _lockedByDefault, reason: 'every locked module has a route');
  });

  lt('a locked module never builds its screen (no data / API loading behind the blur)', (tester) async {
    final (c, router) = await boot(tester);
    router.go(Routes.maintenance);
    await tester.pumpAndSettle();
    expect(lockOverlay(AppModule.maintenance), findsOneWidget);
    expect(find.byType(MaintenanceHubScreen, skipOffstage: false), findsNothing);
    expect(find.byType(BackdropFilter), findsOneWidget);
    router.go(Routes.inventory);
    await tester.pumpAndSettle();
    expect(find.byType(InventoryScreen, skipOffstage: false), findsNothing);
    // Licensing it later shows the real screen in place.
    debugLicensedModules = {...defaultLicensedModules, AppModule.inventory};
    c.invalidate(licenceProvider);
    await tester.pumpAndSettle();
    expect(find.byType(InventoryScreen), findsOneWidget);
    expect(find.text(LicenceText.title), findsNothing);
  });

  lt('"Back to Dashboard" goes Home and clears the locked screen; Work opens the hub', (tester) async {
    final (_, router) = await boot(tester);
    await tester.tap(inBar('Work'));
    await tester.pumpAndSettle();
    expect(find.byType(WorkHubScreen), findsOneWidget);
    await tester.tap(find.text('Maintenance').first);
    await tester.pumpAndSettle();
    expect(loc(router), Routes.maintenance);
    expect(lockOverlay(AppModule.maintenance), findsOneWidget);
    // Nothing behind the card reacts.
    expect(find.byType(ModalBarrier), findsWidgets);
    await tester.tap(find.text(LicenceText.backToDashboard));
    await tester.pumpAndSettle();
    expect(loc(router), Routes.home);
    expect(find.text(LicenceText.title), findsNothing);
    await tester.tap(inBar('Work'));
    await tester.pumpAndSettle();
    expect(loc(router), Routes.work);
    expect(find.byType(WorkHubScreen), findsOneWidget);
  });

  lt('Android back on a locked screen returns Home', (tester) async {
    final (_, router) = await boot(tester);
    for (final route in [Routes.inventory, Routes.partDetail('BR-6205'), Routes.maintenancePerformance]) {
      router.go(route);
      await tester.pumpAndSettle();
      expect(find.text(LicenceText.title), findsOneWidget, reason: route);
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(loc(router), Routes.home, reason: route);
      expect(find.text(LicenceText.title), findsNothing, reason: route);
    }
  });

  lt('More sheet and Work hub: locked tiles faded with a lock badge, still open the overlay', (tester) async {
    final (_, router) = await boot(tester);
    await tester.tap(inBar('More'));
    await tester.pumpAndSettle();
    final badges = find.byKey(const ValueKey('licence-lock-badge'));
    // Exactly the locked modules' tiles carry a badge; Issues / Alerts / Documents / Reports / Admin are open.
    for (final open in ['Issues', 'Alerts', 'Documents', 'Reports', 'Administration', 'Tracking']) {
      final tile = find.textContaining(open);
      if (tile.evaluate().isEmpty) continue;
      expect(
        find.descendant(of: find.ancestor(of: tile.first, matching: find.byType(LicenceTileLook)), matching: badges),
        findsNothing,
        reason: open,
      );
    }
    expect(badges, findsNWidgets(_moreSheetLocked), reason: 'Inventory, Contracts & Warranty, Financial, Maintenance, Inspection, WIP, Audit');
    await tester.tap(find.text('Inventory'));
    await tester.pumpAndSettle();
    expect(loc(router), Routes.inventory);
    expect(lockOverlay(AppModule.inventory), findsOneWidget);

    router.go(Routes.work);
    await tester.pumpAndSettle();
    expect(
      find.byKey(const ValueKey('licence-lock-badge')),
      findsNWidgets(4),
      reason: 'Maintenance, Inspection, WIP, Audit (Issues and Movement are open)',
    );
    await tester.tap(find.text('Movement & Custody'));
    await tester.pumpAndSettle();
    expect(loc(router), Routes.movement);
    expect(find.text(LicenceText.title), findsNothing, reason: 'licensed');
  });

  lt('cross-links show the licence sheet; OK closes it without navigating', (tester) async {
    final (_, router) = await boot(tester);
    final asset = Routes.assetDetail('CNC-00123');
    router.go(asset);
    await tester.pumpAndSettle();
    for (final (label, module) in [
      ('Create Work Order', AppModule.maintenance),
      ('Create Inspection', AppModule.inspection),
    ]) {
      await tester.tap(find.text(label).first);
      await tester.pumpAndSettle();
      expect(lockSheet(module), findsOneWidget, reason: label);
      expect(find.text(LicenceText.sheetBody), findsOneWidget);
      await tester.tap(find.text('OK'));
      await tester.pumpAndSettle();
      expect(lockSheet(module), findsNothing, reason: label);
      expect(loc(router), Routes.assetDetail('CNC-00123'), reason: label);
    }
    // Licensed cross-links navigate: Report Issue (Issues is open now) and Move Asset.
    await tester.tap(find.widgetWithText(ActionTile, 'Report Issue'));
    await tester.pumpAndSettle();
    expect(lockSheet(AppModule.issues), findsNothing);
    expect(loc(router), Routes.reportIssue);
    expect(find.text(LicenceText.title), findsNothing);
    router.go(asset);
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ActionTile, 'Move Asset'));
    await tester.pumpAndSettle();
    expect(loc(router), Routes.transferAsset);

    // Home cards that drill into locked modules.
    router.go(Routes.home);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Work Orders').first);
    await tester.pumpAndSettle();
    expect(lockSheet(AppModule.maintenance), findsOneWidget);
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();
    expect(loc(router), Routes.home);
    // Alerts is open now: the Critical Alert tile drills in.
    await tester.tap(find.text('Critical Alert'));
    await tester.pumpAndSettle();
    expect(lockSheet(AppModule.alerts), findsNothing);
    expect(loc(router), isNot(Routes.home));
    expect(find.text(LicenceText.title), findsNothing);
  });

  lt('newly open modules and Movement & Custody open normally (no overlay, badge or sheet)', (tester) async {
    final (_, router) = await boot(tester);
    for (final route in [
      Routes.reports,
      Routes.admin,
      Routes.issues,
      Routes.reportIssue,
      Routes.alerts,
      Routes.alertDetail('ALT-0001'),
      Routes.recentAlerts,
      Routes.documents,
      Routes.documentDetail('DOC-0001'),
      '/home/notifications',
      Routes.movement,
      Routes.transferAsset,
      Routes.checkOut,
      Routes.checkIn,
      Routes.assignCustodian,
      Routes.returnAsset,
    ]) {
      router.go(route);
      await tester.pumpAndSettle();
      expect(find.text(LicenceText.title), findsNothing, reason: route);
      expect(moduleForLocation(route) == null || defaultLicensedModules.contains(moduleForLocation(route)), isTrue,
          reason: route);
    }
  });

  lt('asset detail: tabs of locked modules show the lock card without Back to Dashboard', (tester) async {
    final (_, router) = await boot(tester);
    for (final (tab, module) in [
      ('maintenance', AppModule.maintenance),
      ('inspection', AppModule.inspection),
      ('financial', AppModule.financial),
    ]) {
      router.go(Routes.assets); // a fresh detail screen per tab
      await tester.pumpAndSettle();
      router.go(Routes.assetDetail('CNC-00123', tab: tab));
      await tester.pumpAndSettle();
      expect(lockOverlay(module), findsOneWidget, reason: tab);
      expect(find.text(LicenceText.backToDashboard), findsNothing, reason: tab);
    }
    router.go(Routes.assets);
    await tester.pumpAndSettle();
    router.go(Routes.assetDetail('CNC-00123', tab: 'documents'));
    await tester.pumpAndSettle();
    expect(find.text(LicenceText.title), findsNothing, reason: 'asset documents belong to Assets');
  });

  lt('always-open screens are never locked, even with no module licensed', (tester) async {
    debugLicensedModules = {};
    final (_, router) = await boot(tester);
    for (final r in _alwaysOpen) {
      router.go(r);
      await tester.pumpAndSettle();
      expect(find.text(LicenceText.title), findsNothing, reason: r);
      expect(tester.takeException(), isNull, reason: r);
    }
    // …while a module screen is.
    router.go(Routes.assets);
    await tester.pumpAndSettle();
    expect(lockOverlay(AppModule.assets), findsOneWidget);
  });
}
