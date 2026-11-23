import 'package:asset360_mobile/app/theme/app_theme.dart';
import 'package:asset360_mobile/core/widgets/master_location_field.dart';
import 'package:asset360_mobile/core/widgets/person_picker.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/models/asset_registration.dart';
import 'package:asset360_mobile/data/validation/asset_registration_validator.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_asset_backend.dart';

/// Registration pickers use the web's sources: the location tree built from /sites, /buildings, /floors,
/// /zones and /locations (ids preserved), Sub Category always required, employees filtered by department.
void main() {
  late FakeAssetBackend backend;
  late ProviderContainer c;

  setUp(() {
    debugApiMode = true;
    backend = FakeAssetBackend()..seedLocationLists();
    c = ProviderContainer(retry: (_, _) => null, overrides: backend.overrides);
  });
  tearDown(() {
    c.dispose();
    debugApiMode = false;
  });

  List<String> names(Iterable<MdLocationNode> l) => [for (final n in l) n.name];

  group('location tree from the separate lists', () {
    test('Country → Site → Building → Floor → Zone → Room, skipping deleted / orphan rows', () async {
      final tree = await c.read(mdLocationTreeProvider.future);
      expect(names(tree.roots), ['UAE', 'Oman'], reason: 'countries come from the sites country names');
      final uae = tree.roots.first;
      expect(names(uae.children), ['Main Site'], reason: 'the isDeleted site is dropped');
      final building = tree.byId('bld-1')!;
      expect(names(uae.children.first.children), ['Block A'], reason: 'Deleted and orphan buildings are dropped');
      expect(names(building.children), [
        'Ground',
        'First',
        'Lobby',
      ], reason: 'orphan floor dropped; Lobby has no floor');
      expect(tree.byId('fl-orphan'), isNull);
      expect(names(tree.byId('fl-1')!.children), ['Zone 1']);
      expect(names(tree.byId('zone-1')!.children), [
        'Room 101',
      ], reason: 'rooms attach by zone / floor / building name');
      expect(names(tree.byId('zone-lobby')!.children), ['Store'], reason: 'a room under a floor-less zone');
      expect(tree.byId('fl-1')!.mapImage, '/uploads/floors/fl-1.png');
      expect(backend.calls('GET', '/api/master-data/locations/tree'), isEmpty, reason: 'no fallback needed');
    });

    test('ids and names are the Master Data record ids the web saves; skipped levels stay empty', () async {
      final tree = await c.read(mdLocationTreeProvider.future);
      final room = tree.pathTo(tree.byId('room-101'));
      final l = AssetLocation();
      room.writeTo(l);
      expect(
        [l.countryId, l.siteId, l.buildingId, l.floorId, l.zoneId, l.roomId],
        ['UAE', 'site-1', 'bld-1', 'fl-1', 'zone-1', 'room-101'],
        reason: 'floorId is the floor record id = the floor-plan locationId',
      );
      expect(l.serverLocationPath, 'UAE > Main Site > Block A > Ground > Zone 1 > Room 101');

      final lobby = AssetLocation();
      tree.pathTo(tree.byId('zone-lobby')).writeTo(lobby);
      expect([lobby.buildingId, lobby.floorId, lobby.zoneId], ['bld-1', null, 'zone-lobby']);
      expect(lobby.serverLocationPath, 'UAE > Main Site > Block A > Lobby');

      final yard = AssetLocation();
      tree.pathTo(tree.byId('zone-yard')).writeTo(yard);
      expect([yard.siteId, yard.buildingId, yard.floorId, yard.zoneId], ['site-2', null, null, 'zone-yard']);

      // A saved location resolves back through ids, and through names when only names are stored.
      expect(tree.pathOf(lobby).deepest?.id, 'zone-lobby');
      expect(
        tree.pathOf(AssetLocation(siteName: 'Muscat Yard', zoneName: 'Yard', countryName: 'Oman')).deepest?.id,
        'zone-yard',
      );
    });

    test('falls back to /locations/tree only when sites, buildings, floors and zones are all empty', () async {
      for (final p in ['sites', 'buildings', 'floors', 'zones']) {
        backend.on('GET', '/api/master-data/$p', (_) => const FakeReply(200, []));
      }
      final tree = await c.read(mdLocationTreeProvider.future);
      expect(backend.calls('GET', '/api/master-data/locations/tree'), hasLength(1));
      expect(tree.byId('loc-gf')?.level, MdLocationLevel.floor, reason: 'levels by depth in the fallback');
    });

    test('a failing list counts as empty (like the web) instead of failing the tree', () async {
      backend.on('GET', '/api/master-data/locations', (_) => const FakeReply(500, {'message': 'down'}));
      final tree = await c.read(mdLocationTreeProvider.future);
      expect(tree.byId('zone-1'), isNotNull);
      expect(tree.byId('room-101'), isNull);
    });

    testWidgets('the selector offers the nearest filled level: a zone straight under a site or building', (
      tester,
    ) async {
      var path = MdLocationPath.empty;
      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: c,
          child: MaterialApp(
            theme: AppTheme.current,
            home: Scaffold(
              body: StatefulBuilder(
                builder: (context, set) => SingleChildScrollView(
                  child: MasterLocationSelector(value: path, onChanged: (p) => set(() => path = p)),
                ),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      // Outdoor zone: Oman → Muscat Yard has no buildings; Zone is offered straight away.
      await tester.tap(find.byType(DropdownButtonFormField<String>).first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Oman').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byType(DropdownButtonFormField<String>).at(1));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Muscat Yard').last);
      await tester.pumpAndSettle();
      expect(find.text('Building'), findsNothing);
      expect(find.text('Floor'), findsNothing);
      expect(find.text('Zone'), findsOneWidget);
      await tester.tap(find.byType(DropdownButtonFormField<String>).at(2));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Yard').last);
      await tester.pumpAndSettle();
      expect(path.label, 'Oman > Muscat Yard > Yard');
      expect(path.level, MdLocationLevel.zone);
    });
  });

  group('Sub Category is always required', () {
    test('even when the category says subcategoryRequired: false (the backend validator requires it)', () {
      final r = AssetRegistration(assetRegistrationId: 'AR1');
      r.classification.category = 'HVAC'; // sample category with details.subcategoryRequired == false
      final errors = RegistrationValidator.validateStep(r, 1, requireMandatory: true);
      expect(errors['SubCategory'], 'Sub Category is required.');
      r.classification.subCategory = 'Chillers';
      expect(RegistrationValidator.validateStep(r, 1, requireMandatory: true), isNot(contains('SubCategory')));
    });
  });

  group('employees follow the department', () {
    Future<void> pumpField(WidgetTester tester, {String? department}) async {
      final production = {
        ...mdEmployees.first,
        'id': '6650a1b2c3d4e5f600000e02',
        'employeeName': 'Omar Production',
        'employeeId': 'EMP-0099',
        'department': {'id': 'md-dep-2', 'name': 'Production'},
      };
      backend.on('GET', '/api/master-data/employees', (_) => FakeReply(200, [...mdEmployees, production]));
      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: c,
          child: MaterialApp(
            theme: AppTheme.current,
            home: Scaffold(
              body: PersonField(personId: null, onChanged: (_) {}, department: department, hint: 'Pick someone'),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Pick someone'));
      await tester.pumpAndSettle();
    }

    testWidgets('a department lists only its employees; "Show all employees" lists everyone', (tester) async {
      await pumpField(tester, department: 'Production');
      expect(find.text('Omar Production'), findsOneWidget);
      expect(find.text('Sara Ali'), findsNothing, reason: 'belongs to Facilities Management');
      expect(find.text('Show all employees'), findsOneWidget);
      await tester.tap(find.text('Show all employees'));
      await tester.pumpAndSettle();
      expect(find.text('Omar Production'), findsOneWidget);
      expect(find.text('Sara Ali'), findsOneWidget);
    });

    testWidgets('no department: everyone, and no toggle', (tester) async {
      await pumpField(tester);
      expect(find.text('Omar Production'), findsOneWidget);
      expect(find.text('Sara Ali'), findsOneWidget);
      expect(find.text('Show all employees'), findsNothing);
    });
  });
}
