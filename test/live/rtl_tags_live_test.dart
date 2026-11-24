import 'dart:async';
import 'dart:convert';

import 'package:asset360_mobile/data/api/auth_models.dart';
import 'package:asset360_mobile/data/api/dashboard/network_status_live.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/auth/auth_diagnostics_screen.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_animated_markers.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_asset_backend.dart';
import 'rtl_samples.dart';

class _AuthIn extends AuthNotifier {
  @override
  AuthState build() => AuthSignedIn(
    user: const CurrentUser(fullName: 'Demo User', clientId: 'CLT-1001', tenantId: 'TENANT-DEFAULT'),
    tokens: TokenPair(accessToken: 'tok-1', refreshToken: 'r-1', expiresAt: DateTime.now().add(const Duration(hours: 1))),
    rememberMe: false,
  );
}

/// Timers the notifier / socket schedule, fired by hand.
class _Timers {
  final pending = <(Duration, void Function(), _FakeTimer)>[];

  Timer schedule(Duration d, void Function() cb) {
    final t = _FakeTimer();
    pending.add((d, cb, t));
    return t;
  }

  void fire(Duration d) {
    final due = [for (final p in pending) if (!p.$3.cancelled && p.$1 == d) p];
    pending.removeWhere(due.contains);
    for (final p in due) {
      p.$3.cancelled = true;
      p.$2();
    }
  }

  int live(Duration d) => pending.where((p) => !p.$3.cancelled && p.$1 == d).length;
}

class _FakeTimer implements Timer {
  bool cancelled = false;

  @override
  void cancel() => cancelled = true;

  @override
  bool get isActive => !cancelled;

  @override
  int get tick => 0;
}

/// A tags/live connection the test drives.
class _FakeSocket implements LiveSocket, LiveSocketCloseInfo {
  _FakeSocket(this.uri, this.bearer, {this.fail, int? closeCode, String? closeReason}) : _code = closeCode, _reason = closeReason;

  final int? _code;
  final String? _reason;

  @override
  int? get closeCode => closeCodeOverride ?? _code;

  @override
  String? get closeReason => closeReasonOverride ?? _reason;

  final Uri uri;
  final String? bearer;
  final Object? fail;
  final _in = StreamController<Object?>();
  bool closed = false;

  @override
  Future<void> get ready => fail == null ? Future.value() : Future.error(fail!);

  @override
  Stream<Object?> get messages => _in.stream;

  void send(Map<String, Object?> frame) => _in.add(jsonEncode(frame));

  void sendRaw(Object? data) => _in.add(data);

  /// The server closes the connection with [code] / [reason].
  void dropWith(int code, String reason) {
    closeCodeOverride = code;
    closeReasonOverride = reason;
    _in.close();
  }

  int? closeCodeOverride;
  String? closeReasonOverride;

  /// The server / network ends the connection.
  Future<void> drop() => _in.close();

  @override
  Future<void> close() async {
    closed = true;
    if (!_in.isClosed) await _in.close();
  }
}

Map<String, Object?> _snapshot(List<Map<String, Object?>> tags) => {
  'type': 'snapshot',
  'total': tags.length,
  'online': tags.length,
  'plotted': tags.length,
  'withAsset': tags.length,
  'tags': tags,
};

Map<String, Object?> _update({
  List<Map<String, Object?>> changed = const [],
  List<Map<String, Object?>> seen = const [],
  List<String> removed = const [],
}) => {'type': 'update', 'changed': changed, 'seen': seen, 'removed': removed};

Map<String, Object?> _seen(String tagId, String gatewayId, {String connection = 'Online', double rssi = -50, double battery = 70}) => {
  'tagId': tagId,
  'gatewayId': gatewayId,
  'timestamp': '2026-11-19T10:15:30Z',
  'status': 'online',
  'connection': connection,
  'rssi': rssi,
  'battery': battery,
};

void main() {
  // ------------------------------------------------------------------ pure parts

  group('frames and rows', () {
    test('snapshot / update frames parse; other frames and non-JSON are ignored', () {
      final snap = TagLiveMessage.parse(jsonEncode(_snapshot([rtlTagRow('C3:00:00:AA:BB:01', 'ac233fc0ffee', assetId: 'AR-000123')])));
      expect(snap, isA<TagSnapshot>());
      final row = (snap! as TagSnapshot).tags.single;
      expect(row.key, 'c30000aabb01');
      expect(row.assetId, 'AR-000123');
      expect(row.battery, 80);
      expect(row.timestamp, DateTime.utc(2026, 11, 19, 10, 14, 52));
      final up = TagLiveMessage.parse(
        utf8.encode(jsonEncode(_update(changed: [rtlTagRow('a', 'g1')], seen: [_seen('b', 'g2')], removed: ['c']))),
      );
      expect(up, isA<TagUpdate>());
      final u = up! as TagUpdate;
      expect((u.changed.length, u.seen.single.tagId), (1, 'b'));
      expect(u.removed, ['c']);
      expect(TagLiveMessage.parse('{"type":"ping"}'), isNull);
      expect(TagLiveMessage.parse('not json'), isNull);
    });

    test('the list: snapshot replaces, update changes / seen / removed, moves are reported', () {
      final list = TagLiveList();
      expect(list.replace([TagLocation.tryParse(rtlTagRow('T1', 'gw-a'))!, TagLocation.tryParse(rtlTagRow('T2', 'gw-a'))!]), isEmpty);
      expect(list.length, 2);
      final moved = list.apply(
        TagUpdate(
          changed: [TagLocation.tryParse(rtlTagRow('t1', 'gw-b'))!, TagLocation.tryParse(rtlTagRow('T3', 'gw-a'))!],
          seen: [TagSeen.tryParse(_seen('T2', 'gw-a', rssi: -80, battery: 12))!],
          removed: ['nothing'],
        ),
      );
      expect(moved, {'t1'}, reason: 'new tags are not moves');
      expect(list['T1']!.gatewayId, 'gw-b');
      expect(list['T2']!.rssi, -80, reason: 'seen = reading fields only');
      expect(list['T2']!.gatewayId, 'gw-a');
      expect(list['T2']!.zoneName, 'Hall A');
      list.apply(const TagUpdate(removed: ['T2']));
      expect(list['T2'], isNull);
      expect(list.length, 2);
      // A new snapshot replaces everything; a tag on another gateway is a move.
      expect(list.replace([TagLocation.tryParse(rtlTagRow('T1', 'gw-a'))!]), {'t1'});
      expect(list.length, 1);
    });

    test('socket URL: wss for https, same filters as GET /tags, no token in it', () {
      final u = tagsLiveUri('https://a360demo.purpleiq.ai', siteId: 'site-1', floorPlanId: 'fp-001');
      expect((u.scheme, u.host, u.path), ('wss', 'a360demo.purpleiq.ai', '/api/real-time-location/tags/live'));
      expect(u.queryParameters, {'minutes': '1440', 'siteId': 'site-1', 'floorPlanId': 'fp-001'});
      final z = tagsLiveUri('http://172.16.100.29:5490', zoneId: 'z', zonedOnly: true, minutes: 60);
      expect((z.scheme, z.port), ('ws', 5490));
      expect(z.queryParameters, {'minutes': '60', 'zoneId': 'z', 'zonedOnly': 'true'});
      expect(withAccessToken(u, 'tok').queryParameters['access_token'], 'tok');
    });

    test('overlay: location, freshness, rssi, battery; Moving only for a recent move; counters recomputed', () {
      final base = RtlOverview.fromJson(rtlOverviewJson);
      final now = DateTime.utc(2026, 11, 19, 10, 16);
      final list = TagLiveList()
        ..replace([
          TagLocation.tryParse(rtlTagRow('C30000AABB01', 'ac233fc0beef', zoneId: 'z2', zoneName: 'Hall B', rssi: -55, battery: 40))!,
        ]);
      final moving = overlayTags(base, list, {'c30000aabb01': now.subtract(const Duration(seconds: 30))}, now);
      final a = moving.asset('AR-000123')!;
      expect((a.movement, a.freshness), ('Moving', 'Live'));
      expect((a.location!.gatewayId, a.location!.zoneName, a.rssi, a.battery), ('ac233fc0beef', 'Hall B', -55, 40));
      expect(a.confidence, 'High', reason: 'overview fields stay');
      expect(a.activeLocationAlerts, 1);
      expect(moving.asset('AR-000124'), same(base.asset('AR-000124')), reason: 'no tag row → untouched');
      expect((moving.kpis.moving, moving.kpis.stationary, moving.kpis.unknown, moving.kpis.assetsInView), (1, 1, 1, 3));
      expect(moving.kpis.locationAlerts, 1);
      final still = overlayTags(base, list, {'c30000aabb01': now.subtract(const Duration(seconds: 61))}, now);
      expect(still.asset('AR-000123')!.movement, 'Stationary');
      expect((still.kpis.moving, still.kpis.stationary), (0, 2));
      expect(identical(overlayTags(base, TagLiveList(), {}, now), base), isTrue);
    });
  });

  // ------------------------------------------------------------------ position (the web's conversion)

  group('marker position', () {
    final plots = MapPlot.parseList(rtlPlotsJson);

    test('plan % → map point like the web (CRS.Simple): lat = height · (1 − y/100), lng = width · x/100', () {
      // Web: at(x, y) = [h * (1 - y / 100), W * x / 100] with the image's aspect; mobile keeps the same ratios.
      final f = PlanFrame.forAspect(1.5);
      final p = f.toLatLng(25, 40);
      expect(p.latitude, closeTo(f.height * 0.6, 1e-12));
      expect(p.longitude, closeTo(f.width * 0.25, 1e-12));
      expect(f.toLatLng(0, 0).latitude, f.height, reason: 'y runs down: 0 % is the TOP edge');
      expect(f.toLatLng(100, 100), LatLng(0, f.width));
      final back = f.toPercent(p);
      expect(back.x, closeTo(25, 1e-9));
      expect(back.y, closeTo(40, 1e-9));
    });

    test('the asset is drawn at its newest tag row x / y; without one at its gateway plot; no plot → not on plan', () {
      final base = RtlOverview.fromJson(rtlOverviewJson);
      final list = TagLiveList()
        ..replace([
          // The row says the gateway is plotted at 41 / 52 (e.g. moved since the plots were loaded).
          TagLocation.tryParse(rtlTagRow('C30000AABB01', 'ac233fc0ffee', x: 41, y: 52))!,
        ]);
      final o = overlayTags(base, list, {}, DateTime.utc(2026, 11, 19, 10, 15));
      final placed = placeAssets(o.assets, plots);
      final a = o.asset('AR-000123')!;
      expect((a.x, a.y), (41, 52));
      expect(placed.pointOf(a), (x: 41.0, y: 52.0));
      // Fallback: FORK-02 has no live row → its gateway's plot (GW-02 at 75 / 60), found by the plot code.
      final fork = o.asset('AR-000124')!;
      expect(fork.x, isNull);
      expect(fork.y, isNull);
      expect(placed.pointOf(fork), (x: 75.0, y: 60.0));
      // CART-03's gateway is not plotted: never invented.
      expect(placed.pointOf(o.asset('AR-000125')!), isNull);
      // A row of an unplotted gateway (x / y null) keeps the gateway plot fallback.
      final noXy = overlayTags(
        base,
        TagLiveList()..replace([TagLocation.tryParse(rtlTagRow('C30000AABB01', 'ac233fc0ffee'))!]),
        {},
        DateTime.utc(2026, 11, 19, 10, 15),
      );
      expect(placeAssets(noXy.assets, plots).pointOf(noXy.asset('AR-000123')!), (x: 22.5, y: 30.0));
    });

    test('a reading older than the overview\'s own location time never overrides it', () {
      final base = RtlOverview.fromJson(rtlOverviewJson); // PUMP-01 located at 10:14:52
      final older = TagLiveList()
        ..replace([
          TagLocation.tryParse(rtlTagRow('C30000AABB01', 'ac233fc0beef', timestamp: '2026-11-19T10:14:00Z', x: 75, y: 60))!,
        ]);
      final o = overlayTags(base, older, {}, DateTime.utc(2026, 11, 19, 10, 15));
      expect(identical(o, base), isTrue, reason: 'nothing to lay over');
      expect(o.asset('AR-000123')!.location!.gatewayId, 'AC233FC0FFEE');
      final newer = TagLiveList()
        ..replace([
          TagLocation.tryParse(rtlTagRow('C30000AABB01', 'ac233fc0beef', timestamp: '2026-11-19T10:15:00Z', x: 75, y: 60))!,
        ]);
      expect(overlayTags(base, newer, {}, DateTime.utc(2026, 11, 19, 10, 15)).asset('AR-000123')!.x, 75);
    });
  });

  // ------------------------------------------------------------------ zone grid (the web's zoneSpots)

  group('zone spots (port of floor-plan-map.ts zoneSpots)', () {
    final zones = MapZone.parseList(rtlZonesJson);
    final hallA = zones.firstWhere((z) => z.zoneId == 'zone-a1');
    final store = zones.firstWhere((z) => z.zoneId == 'zone-sr');
    final f = PlanFrame.forAspect(1.5); // width 1, height 2/3 (the web: W 1000, h 667)

    RtlAsset inZone(String id, String zone, {String gw = 'AC233FC0FFEE'}) =>
        RtlAsset(assetId: id, location: RtlLocation(gatewayId: gw, zoneId: zone), freshness: 'Live', movement: 'Stationary');

    test('the grid matches the web by hand: one asset = the zone centre; four assets = a 3 × 2 grid from the top-left', () {
      // Hall A is x 10–40 %, y 15–40 %: box inset 10 % → lat .41667–.55, lng .13–.37.
      final one = zoneSpotsFor(hallA, 1, f);
      expect(one, hasLength(1));
      expect(one.single.latitude, closeTo(.48333, 1e-4));
      expect(one.single.longitude, closeTo(.25, 1e-9));
      // n = 4: cell size sqrt(area / 4) fits 2, then 3 cells (× .85 each try) and only at the third try 3 × 2 = 6 ≥ 4.
      final four = zoneSpotsFor(hallA, 4, f);
      expect(four, hasLength(6), reason: 'all cells are returned; the i-th asset takes the i-th');
      expect(four.first.latitude, closeTo(.51667, 1e-4), reason: 'row order from the top (north) row');
      expect(four.first.longitude, closeTo(.17, 1e-9));
      expect(four[1].longitude, closeTo(.25, 1e-9));
      expect(four[3].latitude, closeTo(.45, 1e-4), reason: 'second row (rows = 2: .55 − 1.5 · .1333 / 2)');
      // A circle (radius = % of the plan WIDTH): one asset sits on its centre (75 % / 60 %).
      final c = zoneSpotsFor(store, 1, f).single;
      expect(c.latitude, closeTo(f.height * .4, 1e-9));
      expect(c.longitude, closeTo(.75, 1e-9));
    });

    test('spots stay inside the zone (circle: 92 % of the radius) and never overlap', () {
      for (final n in [1, 2, 3, 5, 8, 13]) {
        final rect = zoneSpotsFor(hallA, n, f);
        expect(rect.length, greaterThanOrEqualTo(n));
        final g = zoneGeom(hallA, f);
        for (final p in rect) {
          expect(p.latitude, inInclusiveRange(g.south, g.north));
          expect(p.longitude, inInclusiveRange(g.west, g.east));
        }
        final circle = zoneSpotsFor(store, n, f);
        final cg = zoneGeom(store, f);
        for (final p in circle) {
          expect(
            (p.latitude - cg.center!.latitude) * (p.latitude - cg.center!.latitude) +
                (p.longitude - cg.center!.longitude) * (p.longitude - cg.center!.longitude),
            lessThanOrEqualTo(cg.radius * .92 * cg.radius * .92 + 1e-12),
          );
        }
        for (final spots in [rect.take(n).toList(), circle.take(n).toList()]) {
          for (var i = 0; i < spots.length; i++) {
            for (var j = i + 1; j < spots.length; j++) {
              final dLat = spots[i].latitude - spots[j].latitude, dLng = spots[i].longitude - spots[j].longitude;
              expect(dLat * dLat + dLng * dLng, greaterThan(1e-4), reason: 'two assets on one spot (n=$n)');
            }
          }
        }
      }
    });

    test('same zone + same asset ids → same spots, whatever order the assets arrive in; sorted by asset id', () {
      final a = [inZone('AR-3', 'zone-a1'), inZone('AR-1', 'zone-a1'), inZone('AR-2', 'zone-a1')];
      final p1 = placeAssets(a, const [], zones: zones, frame: f);
      final p2 = placeAssets(a.reversed.toList(), const [], zones: zones, frame: f);
      expect(p1.zoneSpots, p2.zoneSpots);
      final cells = zoneSpotsFor(hallA, 3, f);
      expect(p1.zoneSpots['AR-1'], cells[0]);
      expect(p1.zoneSpots['AR-2'], cells[1]);
      expect(p1.zoneSpots['AR-3'], cells[2]);
      expect({for (final s in p1.zoneSpots.values) s}, hasLength(3), reason: 'no two assets on one spot');
      // Locale order of the web's localeCompare: case-insensitive, lower case first on a tie.
      expect(compareAssetIds('a-1', 'B-1'), lessThan(0));
      expect(compareAssetIds('a', 'A'), lessThan(0));
      expect(compareAssetIds('AR-10', 'AR-9'), lessThan(0), reason: 'plain text order like localeCompare without numeric');
    });

    test('placement: zone spot > gateway plot > not on plan; no zones → the gateway as before', () {
      final plots = MapPlot.parseList(rtlPlotsJson);
      final inA = inZone('AR-A', 'zone-a1', gw: 'ac233fc0beef'); // zone wins over its gateway
      final zoneOnly = inZone('AR-B', 'zone-sr', gw: 'unplotted-gw'); // gateway not plotted, zone is
      const gwOnly = RtlAsset(assetId: 'AR-C', location: RtlLocation(gatewayId: 'AC233FC0FFEE', zoneId: 'other-plan-zone'));
      const nowhere = RtlAsset(assetId: 'AR-D', location: RtlLocation(gatewayId: 'unplotted-gw'));
      final placed = placeAssets([inA, zoneOnly, gwOnly, nowhere], plots, zones: zones, frame: f);
      expect(placed.zoneSpots.keys, unorderedEquals(['AR-A', 'AR-B']));
      expect(placed.byGateway.values.expand((l) => l).map((a) => a.assetId), ['AR-C']);
      expect(placed.pointOf(gwOnly), (x: 22.5, y: 30.0), reason: 'fallback: the gateway plot');
      expect(placed.notOnPlan.map((a) => a.assetId), ['AR-D']);
      // An inactive zone is not a zone of the plan; without zones / frame everything uses its gateway.
      const off = [MapZone(zoneId: 'zone-a1', shape: 'Rectangle', x: 10, y: 15, w: 30, h: 25, active: false)];
      expect(placeAssets([inA], plots, zones: off, frame: f).zoneSpots, isEmpty);
      final old = placeAssets([inA, zoneOnly], plots);
      expect(old.zoneSpots, isEmpty);
      expect(old.notOnPlan.map((a) => a.assetId), ['AR-B']);
    });

    test('changed rows move the spot (zone change); seen rows only update rssi / time', () {
      final plots = MapPlot.parseList(rtlPlotsJson);
      final base = RtlOverview.fromJson(rtlOverviewJson);
      final at = DateTime.utc(2026, 11, 19, 10, 16);
      RtlPlacement place(RtlOverview o) => placeAssets(o.assets, plots, zones: zones, frame: f);
      final start = place(base).zoneSpots['AR-000123'];
      expect(start, isNotNull, reason: 'PUMP-01 is in zone-a1');
      // snapshot / changed: the tag is heard in the Store Room → the asset takes a spot there.
      final moved = TagLiveList()
        ..replace([TagLocation.tryParse(rtlTagRow('C30000AABB01', 'ac233fc0beef', zoneId: 'zone-sr', zoneName: 'Store Room', timestamp: '2026-11-19T10:16:00Z'))!]);
      final o1 = overlayTags(base, moved, {'c30000aabb01': at}, at);
      final spot1 = place(o1).zoneSpots['AR-000123'];
      expect(spot1, isNot(start));
      expect(o1.asset('AR-000123')!.movement, 'Moving');
      // seen: same gateway + zone, new reading → the spot stays, rssi / time follow.
      moved.apply(TagUpdate(seen: [TagSeen.tryParse(_seen('C30000AABB01', 'ac233fc0beef', rssi: -44))!]));
      final o2 = overlayTags(base, moved, {'c30000aabb01': at}, at);
      expect(place(o2).zoneSpots['AR-000123'], spot1);
      expect(o2.asset('AR-000123')!.rssi, -44);
    });
  });

  // ------------------------------------------------------------------ gliding markers

  group('RtlAnimatedMarkerLayer', () {
    Widget host(List<RtlMarkerSpec> markers) => MaterialApp(
      home: SizedBox(
        width: 300,
        height: 300,
        child: FlutterMap(
          options: const MapOptions(crs: CrsSimple(), initialCenter: LatLng(0.5, 0.5), initialZoom: 0),
          children: [RtlAnimatedMarkerLayer(markers: markers)],
        ),
      ),
    );

    RtlMarkerSpec spec(String id, double lat, double lng) =>
        RtlMarkerSpec(id: id, point: LatLng(lat, lng), child: SizedBox(key: ValueKey('child-$id'), width: 10, height: 10));

    List<LatLng> points(WidgetTester t) => [for (final m in t.widget<MarkerLayer>(find.byType(MarkerLayer)).markers) m.point];

    testWidgets('a moved marker glides (not a jump) over 1.2 s and lands on the new point', (t) async {
      await t.pumpWidget(host([spec('a', 0.2, 0.2), spec('b', 0.8, 0.8)]));
      expect(points(t), [const LatLng(0.2, 0.2), const LatLng(0.8, 0.8)]);
      await t.pumpWidget(host([spec('a', 0.6, 0.4), spec('b', 0.8, 0.8)]));
      await t.pump(const Duration(milliseconds: 500));
      final mid = points(t).first;
      expect(mid.latitude, inExclusiveRange(0.2, 0.6), reason: 'on its way');
      expect(mid.longitude, inExclusiveRange(0.2, 0.4));
      expect(points(t).last, const LatLng(0.8, 0.8), reason: 'the other marker stays put');
      await t.pump(const Duration(milliseconds: 800));
      expect(points(t).first, const LatLng(0.6, 0.4));
    });

    RtlMarkerSpec zoned(String id, double lat, double lng, String zone, {bool moving = true}) => RtlMarkerSpec(
      id: id,
      point: LatLng(lat, lng),
      zoneKey: zone,
      moving: moving,
      child: SizedBox(key: ValueKey('child-$id'), width: 10, height: 10),
    );

    testWidgets('a Moving asset that changes zone glides AND leaves a fading trail; it is gone after 8 s', (t) async {
      await t.pumpWidget(host([zoned('a', 0.2, 0.2, 'z1')]));
      expect(find.byType(PolylineLayer), findsNothing);
      await t.pumpWidget(host([zoned('a', 0.6, 0.6, 'z2')]));
      await t.pump(const Duration(milliseconds: 600));
      expect(find.byType(PolylineLayer), findsOneWidget);
      final line = t.widget<PolylineLayer>(find.byType(PolylineLayer)).polylines.single;
      expect(line.points, [const LatLng(0.2, 0.2), const LatLng(0.6, 0.6)], reason: 'from the old spot to the new one');
      expect(line.strokeWidth, 3);
      final early = line.color.a;
      expect(early, lessThanOrEqualTo(.8 + 1e-6));
      await t.pump(const Duration(seconds: 4));
      final later = t.widget<PolylineLayer>(find.byType(PolylineLayer)).polylines.single.color.a;
      expect(later, lessThan(early), reason: 'fading out');
      await t.pump(const Duration(seconds: 5));
      expect(find.byType(PolylineLayer), findsNothing, reason: 'removed after the 8 s fade');
      expect(points(t).single, const LatLng(0.6, 0.6));
    });

    testWidgets('no trail when the asset is not Moving, stays in its zone, or has no zone (gateway plot)', (t) async {
      await t.pumpWidget(host([zoned('a', 0.2, 0.2, 'z1', moving: false), zoned('b', 0.3, 0.3, 'z1'), spec('c', 0.1, 0.1)]));
      await t.pumpWidget(host([zoned('a', 0.6, 0.6, 'z2', moving: false), zoned('b', 0.4, 0.4, 'z1'), spec('c', 0.9, 0.9)]));
      await t.pump(const Duration(milliseconds: 300));
      expect(find.byType(PolylineLayer), findsNothing);
      expect(points(t).first.latitude, inExclusiveRange(0.2, 0.6), reason: 'they all still glide');
      await t.pump(const Duration(seconds: 2));
    });

    testWidgets('reduced motion: no glide, no trail', (t) async {
      Widget h(List<RtlMarkerSpec> m) => MediaQuery(
        data: const MediaQueryData(disableAnimations: true),
        child: host(m),
      );
      await t.pumpWidget(h([zoned('a', 0.2, 0.2, 'z1')]));
      await t.pumpWidget(h([zoned('a', 0.6, 0.6, 'z2')]));
      await t.pump();
      expect(points(t).single, const LatLng(0.6, 0.6));
      expect(find.byType(PolylineLayer), findsNothing);
    });

    testWidgets('markers are keyed by id: the same widget survives a move; a new one appears, a gone one leaves', (t) async {
      await t.pumpWidget(host([spec('a', 0.2, 0.2)]));
      final before = t.element(find.byKey(const ValueKey('child-a')));
      await t.pumpWidget(host([spec('a', 0.5, 0.5), spec('c', 0.9, 0.1)]));
      await t.pump(const Duration(milliseconds: 100));
      expect(t.element(find.byKey(const ValueKey('child-a'))), same(before), reason: 'no flicker / rebuild from scratch');
      expect(points(t).last, const LatLng(0.9, 0.1), reason: 'a new marker appears at its point');
      await t.pump(const Duration(seconds: 2));
      await t.pumpWidget(host([spec('c', 0.9, 0.1)]));
      expect(find.byKey(const ValueKey('child-a')), findsNothing);
    });

    testWidgets('a move that starts mid-glide continues from where the marker is drawn', (t) async {
      await t.pumpWidget(host([spec('a', 0, 0)]));
      await t.pumpWidget(host([spec('a', 1, 0)]));
      await t.pump(const Duration(milliseconds: 500));
      final mid = points(t).single.latitude;
      await t.pumpWidget(host([spec('a', 0, 0)]));
      expect(points(t).single.latitude, closeTo(mid, 1e-9), reason: 'no jump back to the old start');
      await t.pump(const Duration(seconds: 2));
      expect(points(t).single, const LatLng(0, 0));
    });
  });

  // ------------------------------------------------------------------ the Diagnostics screen

  group('Diagnostics: Live map socket', () {
    tearDown(() => debugApiMode = false);

    testWidgets('the section lists state, URL (no token), last snapshot / update, frames, last close, last error', (t) async {
      debugApiMode = true;
      final backend = FakeAssetBackend()..seedRtl();
      await t.pumpWidget(
        ProviderScope(
          overrides: backend.overrides,
          child: const MaterialApp(home: AuthDiagnosticsScreen()),
        ),
      );
      await t.pump();
      expect(find.text('Live map socket'), findsOneWidget);
      expect(find.text('Paused'), findsOneWidget, reason: 'not on screen: nothing runs');
      expect(find.text('Not opened yet'), findsOneWidget);
      expect(find.text('Frames received'), findsOneWidget);
      final c = ProviderScope.containerOf(t.element(find.byType(AuthDiagnosticsScreen)));
      c.read(rtlSocketDiagProvider.notifier).change(
        (d) => d.copyWith(
          url: 'wss://a360demo.purpleiq.ai/api/real-time-location/tags/live?minutes=1440',
          frames: 12,
          closed: true,
          closeCode: 1006,
          closeReason: 'abnormal',
          lastError: 'boom',
        ),
      );
      await t.pump();
      expect(find.textContaining('/api/real-time-location/tags/live?minutes=1440'), findsOneWidget);
      expect(find.text('12'), findsOneWidget);
      expect(find.text('Code 1006 · abnormal'), findsOneWidget);
      expect(find.text('boom'), findsOneWidget);
      expect(find.textContaining('access_token'), findsNothing);
    });
  });

  // ------------------------------------------------------------------ the socket client

  group('TagsLiveSocket', () {
    test('token as Bearer header and ?access_token=, backoff 1 → 30 s with a fresh token each time, frames reset it', () async {
      final timers = _Timers();
      final sockets = <_FakeSocket>[];
      var tokens = 0;
      final got = <TagLiveMessage>[];
      final states = <bool>[];
      final s = TagsLiveSocket(
        uri: tagsLiveUri('https://h.example'),
        token: () async => 'tok-${++tokens}',
        onMessage: got.add,
        onConnection: states.add,
        schedule: timers.schedule,
        connect: (uri, token) {
          final f = _FakeSocket(uri, token, fail: sockets.length < 3 ? const FormatException('refused') : null);
          sockets.add(f);
          return f;
        },
      )..start();
      await pumpEventQueue();
      expect(sockets.first.bearer, 'tok-1');
      expect(sockets.first.uri.queryParameters['access_token'], 'tok-1');
      expect(states, isEmpty, reason: 'never connected');
      expect(timers.live(const Duration(seconds: 1)), 1);
      timers.fire(const Duration(seconds: 1));
      await pumpEventQueue();
      expect(sockets[1].bearer, 'tok-2', reason: 'a fresh token for every attempt');
      expect(timers.live(const Duration(seconds: 2)), 1);
      timers.fire(const Duration(seconds: 2));
      await pumpEventQueue();
      expect(timers.live(const Duration(seconds: 4)), 1);
      timers.fire(const Duration(seconds: 4));
      await pumpEventQueue();
      expect(states, [true]);
      expect(s.connected, isTrue);
      sockets.last.send(_snapshot([rtlTagRow('t', 'g')]));
      await pumpEventQueue();
      expect(got.single, isA<TagSnapshot>());
      expect(s.failures, 0);
      await sockets.last.drop();
      await pumpEventQueue();
      expect(states, [true, false]);
      expect(timers.live(const Duration(seconds: 1)), 1, reason: 'backoff restarted after a frame');
      // The ladder tops out at 30 s.
      s.failures = 9;
      expect(s.nextDelay, const Duration(seconds: 30));
      await s.stop();
      expect(timers.live(const Duration(seconds: 1)), 0);
      expect(sockets.last.closed, isTrue);
    });
  });

  // ------------------------------------------------------------------ the notifier

  group('Live Map: socket over the overview', () {
    late FakeAssetBackend backend;
    late _Timers timers;
    late List<_FakeSocket> sockets;
    late DateTime now;

    setUp(() {
      backend = FakeAssetBackend()..seedRtl();
      timers = _Timers();
      sockets = [];
      now = DateTime.utc(2026, 11, 19, 10, 15, 0);
      TrkLiveNotifier.debugSchedule = timers.schedule;
      RtlNotifier.debugNow = () => now;
      RtlNotifier.debugConnector = (uri, token) {
        final s = _FakeSocket(uri, token);
        sockets.add(s);
        return s;
      };
    });

    tearDown(() {
      TrkLiveNotifier.debugSchedule = null;
      RtlNotifier.debugNow = null;
      // flutter_test_config.dart's idle socket again.
      RtlNotifier.debugConnector = (_, _) => _Idle();
    });

    int calls(String path) => backend.requests.where((o) => o.uri.path == path).length;

    Future<(ProviderContainer, RtlNotifier, Object)> attached() async {
      final c = liveContainer(backend, overrides: [authProvider.overrideWith(_AuthIn.new)]);
      addTearDown(c.dispose);
      final n = c.read(rtlProvider.notifier);
      final owner = Object();
      n.attach(owner);
      await pumpEventQueue();
      return (c, n, owner);
    }

    test('connects with the Bearer token + ?access_token= and the map filters; first the /tags list, then Live', () async {
      final (c, n, owner) = await attached();
      expect(calls('/api/real-time-location/tags'), 1, reason: 'initial list from GET /tags before the snapshot');
      expect(sockets, hasLength(1));
      expect(sockets.single.bearer, 'tok-1');
      expect(sockets.single.uri.scheme, 'wss');
      expect(sockets.single.uri.path, '/api/real-time-location/tags/live');
      expect(sockets.single.uri.queryParameters['access_token'], 'tok-1');
      expect(sockets.single.uri.queryParameters['minutes'], '1440');
      expect(c.read(rtlProvider).live, isTrue);
      n.setScope(siteId: 'site-1', floorPlanId: 'fp-001');
      await pumpEventQueue();
      expect(sockets, hasLength(2), reason: 'the scope changed: reconnect with the new filters');
      expect(sockets.first.closed, isTrue);
      final q = sockets.last.uri.queryParameters;
      expect((q['siteId'], q['floorPlanId']), ('site-1', 'fp-001'));
      expect(calls('/api/real-time-location/tags'), 1, reason: 'socket up: no extra poll once connected');
      n.detach(owner);
    });

    test('snapshot replaces the tag list: map data, counters and Moving / Stationary follow it', () async {
      final (c, n, owner) = await attached();
      expect(c.read(rtlProvider).overview!.kpis.moving, 1, reason: 'the overview alone');
      sockets.single.send(
        _snapshot([
          rtlTagRow('C30000AABB01', 'ac233fc0ffee', rssi: -48, battery: 66),
          rtlTagRow('C30000AABB03', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -72),
        ]),
      );
      await pumpEventQueue();
      final o = c.read(rtlProvider).overview!;
      expect(o.asset('AR-000123')!.rssi, -48);
      expect(o.asset('AR-000123')!.battery, 66);
      expect(o.asset('AR-000124')!.location!.zoneName, 'Hall B');
      // No move seen on the socket: Stationary while live (the web's rule), not the server's 5 min Moving.
      expect((o.kpis.moving, o.kpis.stationary, o.kpis.unknown), (0, 2, 1));
      n.detach(owner);
    });

    test('update: changed (full row → Moving for 60 s), seen (reading only), removed (drops back to the overview)', () async {
      final (c, n, owner) = await attached();
      final s = sockets.single;
      s.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee'), rtlTagRow('C30000AABB03', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -72)]));
      await pumpEventQueue();
      // changed: PUMP-01's tag is now heard by the other gateway.
      s.send(_update(changed: [rtlTagRow('C30000AABB01', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -58)]));
      await pumpEventQueue();
      var a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.movement, a.location!.gatewayId, a.location!.zoneName, a.rssi), ('Moving', 'ac233fc0beef', 'Hall B', -58));
      expect(c.read(rtlProvider).overview!.kpis.moving, 1);
      // 61 s later the move expires on its own (no frame needed).
      now = now.add(const Duration(seconds: 61));
      timers.fire(const Duration(seconds: 61));
      a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect(a.movement, 'Stationary');
      expect(c.read(rtlProvider).overview!.kpis.moving, 0);
      // seen: same gateway, only the reading moved on; Idle = Stale, so movement is Unknown.
      s.send(_update(seen: [_seen('C30000AABB03', 'ac233fc0beef', connection: 'Idle', rssi: -80, battery: 9)]));
      await pumpEventQueue();
      final f = c.read(rtlProvider).overview!.asset('AR-000124')!;
      expect((f.freshness, f.movement, f.rssi, f.battery), ('Stale', 'Unknown', -80, 9));
      expect(f.location!.zoneName, 'Hall B', reason: 'seen never changes the place');
      expect(f.location!.at, DateTime.utc(2026, 11, 19, 10, 15, 30));
      // removed: the tag left the window → the asset shows the overview's values again.
      s.send(_update(removed: ['c30000aabb03']));
      await pumpEventQueue();
      final back = c.read(rtlProvider).overview!.asset('AR-000124')!;
      final orig = RtlOverview.fromJson(rtlOverviewJson).asset('AR-000124')!;
      expect((back.freshness, back.movement, back.rssi), ('Live', 'Stationary', orig.rssi));
      expect(back.battery, isNull);
      n.detach(owner);
    });

    test('overview refresh every 30 s keeps alerts / confidence and the live rows over it', () async {
      final (c, n, owner) = await attached();
      sockets.single.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee', rssi: -40)]));
      await pumpEventQueue();
      final before = calls('/api/real-time-location/overview');
      timers.fire(RtlNotifier.interval);
      await pumpEventQueue();
      expect(calls('/api/real-time-location/overview'), before + 1);
      final a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect(a.rssi, -40, reason: 'the live row stays over the fresh overview');
      expect(a.activeLocationAlerts, 1);
      expect(c.read(rtlProvider).overview!.kpis.locationAlerts, 1);
      n.detach(owner);
    });

    test('socket drops → "Polling": /tags every 10 s; reconnect with a fresh token → Live again, polling stops', () async {
      final (c, n, owner) = await attached();
      expect(timers.live(RtlNotifier.tagPollInterval), 0);
      await sockets.single.drop();
      await pumpEventQueue();
      expect(c.read(rtlProvider).socketUp, isFalse);
      expect(c.read(rtlProvider).live, isFalse);
      expect(c.read(rtlProvider).polling, isTrue, reason: 'still attached: "Polling"');
      expect(calls('/api/real-time-location/tags'), 2, reason: 'one /tags round at once');
      expect(timers.live(RtlNotifier.tagPollInterval), 1);
      expect(timers.live(const Duration(seconds: 1)), 1, reason: 'reconnect after 1 s');
      // Polling replaces the rows; a tag on another gateway counts as a move.
      backend.on(
        'GET',
        '/api/real-time-location/tags',
        (_) => FakeReply(200, {
          'tags': [rtlTagRow('c30000aabb01', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', rssi: -77)],
        }),
      );
      timers.fire(RtlNotifier.tagPollInterval);
      await pumpEventQueue();
      expect(calls('/api/real-time-location/tags'), 3);
      var a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.rssi, a.location!.zoneName, a.movement), (-77, 'Hall B', 'Moving'));
      // The first reconnect fails (refused), the next works.
      RtlNotifier.debugConnector = (uri, token) {
        final s = _FakeSocket(uri, token, fail: sockets.length == 1 ? const FormatException('refused') : null);
        sockets.add(s);
        return s;
      };
      timers.fire(const Duration(seconds: 1));
      await pumpEventQueue();
      expect(sockets, hasLength(2));
      expect(c.read(rtlProvider).live, isFalse);
      expect(timers.live(const Duration(seconds: 2)), 1, reason: 'backoff 1 → 2 s');
      timers.fire(const Duration(seconds: 2));
      await pumpEventQueue();
      expect(sockets, hasLength(3));
      expect(sockets.last.bearer, 'tok-1');
      expect(c.read(rtlProvider).live, isTrue);
      expect(timers.live(RtlNotifier.tagPollInterval), 0, reason: 'polling stops once connected');
      sockets.last.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee', rssi: -45)]));
      await pumpEventQueue();
      a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.rssi, a.location!.zoneName), (-45, 'Hall A'));
      n.detach(owner);
    });

    Map<String, Object?> overviewWith(String assetId, {required String at, String? gateway}) {
      final j = jsonDecode(jsonEncode(rtlOverviewJson)) as Map<String, dynamic>;
      final a = (j['assets'] as List).cast<Map<String, dynamic>>().firstWhere((x) => x['assetId'] == assetId);
      (a['location'] as Map<String, dynamic>)['at'] = at;
      if (gateway != null) (a['location'] as Map<String, dynamic>)['gatewayId'] = gateway;
      return j;
    }

    test('the 30 s overview refresh never moves a marker back to an older position; a newer overview wins', () async {
      final (c, n, owner) = await attached();
      sockets.single.send(
        _snapshot([rtlTagRow('C30000AABB01', 'ac233fc0beef', zoneId: 'zone-b1', zoneName: 'Hall B', timestamp: '2026-11-19T10:15:40Z', x: 75, y: 60)]),
      );
      await pumpEventQueue();
      var a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.location!.gatewayId, a.x, a.y), ('ac233fc0beef', 75, 60));
      // The overview still reports the gateway of 10:14:52 (older than the socket row).
      timers.fire(RtlNotifier.interval);
      await pumpEventQueue();
      a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect((a.location!.gatewayId, a.x, a.y), ('ac233fc0beef', 75, 60), reason: 'the newer socket reading wins');
      // An overview with a LATER location time for the old gateway is the newer reading, so it wins.
      backend.on('GET', '/api/real-time-location/overview', (_) => FakeReply(200, overviewWith('AR-000123', at: '2026-11-19T10:16:30Z')));
      timers.fire(RtlNotifier.interval);
      await pumpEventQueue();
      a = c.read(rtlProvider).overview!.asset('AR-000123')!;
      expect(a.location!.gatewayId, 'AC233FC0FFEE');
      expect(a.x, isNull);
      n.detach(owner);
    });

    test('seen-only updates redraw: a new state with the new time / rssi / battery, position unchanged', () async {
      final (c, n, owner) = await attached();
      sockets.single.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee', x: 22.5, y: 30)]));
      await pumpEventQueue();
      final states = <RtlState>[];
      final sub = c.listen(rtlProvider, (_, s) => states.add(s));
      addTearDown(sub.close);
      sockets.single.send(_update(seen: [_seen('C30000AABB01', 'ac233fc0ffee', rssi: -33, battery: 21)]));
      await pumpEventQueue();
      expect(states, isNotEmpty, reason: 'a seen-only frame must notify the map');
      final a = states.last.overview!.asset('AR-000123')!;
      expect((a.rssi, a.battery, a.x, a.y), (-33, 21, 22.5, 30));
      expect(a.location!.at, DateTime.utc(2026, 11, 19, 10, 15, 30));
      expect(a.positionAt, DateTime.utc(2026, 11, 19, 10, 15, 30), reason: 'position time = the row\'s reading time (seen moves it on)');
      n.detach(owner);
    });

    test('header: the last frame time feeds "Live · N s ago"', () async {
      final (c, n, owner) = await attached();
      expect(c.read(rtlProvider).lastFrameAt, isNull, reason: 'no frame yet: just "Live"');
      sockets.single.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee')]));
      await pumpEventQueue();
      expect(c.read(rtlProvider).lastFrameAt, now);
      now = now.add(const Duration(seconds: 7));
      expect(rtlAgo(c.read(rtlProvider).lastFrameAt, now), '7 s ago');
      sockets.single.send(_update(seen: [_seen('C30000AABB01', 'ac233fc0ffee')]));
      await pumpEventQueue();
      expect(c.read(rtlProvider).lastFrameAt, now, reason: 'an update frame counts too');
      n.detach(owner);
    });

    test('diagnostics: state, URL without the token, last snapshot / update, frames, close code, error — never the token', () async {
      final (c, n, owner) = await attached();
      var d = c.read(rtlSocketDiagProvider);
      expect(d.stateOf(c.read(rtlProvider)), 'Live');
      expect(d.url, startsWith('wss://'));
      expect(d.url, contains('/api/real-time-location/tags/live'));
      expect(d.url, isNot(contains('access_token')));
      expect(d.url, isNot(contains('tok-1')));
      expect(d.frames, 0);
      expect(d.lastSnapshotAt, isNull);
      expect(d.lastUpdateAt, isNull);
      sockets.single.send(_snapshot([rtlTagRow('C30000AABB01', 'ac233fc0ffee')]));
      await pumpEventQueue();
      now = now.add(const Duration(seconds: 5));
      sockets.single.send(_update(seen: [_seen('C30000AABB01', 'ac233fc0ffee')]));
      await pumpEventQueue();
      d = c.read(rtlSocketDiagProvider);
      expect(d.frames, 2);
      expect(d.lastSnapshotAt, DateTime.utc(2026, 11, 19, 10, 15, 0));
      expect(d.lastUpdateAt, DateTime.utc(2026, 11, 19, 10, 15, 5));
      // The server closes with 1001 "going away".
      sockets.single.dropWith(1001, 'going away');
      await pumpEventQueue();
      d = c.read(rtlSocketDiagProvider);
      expect((d.closeCode, d.closeReason, d.failures), (1001, 'going away', 1));
      expect(d.stateOf(c.read(rtlProvider)), 'Reconnecting');
      // The next attempt fails with an error that quotes the URL (and token): it is stored without them.
      RtlNotifier.debugConnector = (uri, token) {
        final s = _FakeSocket(uri, token, fail: WebSocketExceptionLike('Connection to \'${uri.toString()}\' was not upgraded; Bearer $token'));
        sockets.add(s);
        return s;
      };
      timers.fire(const Duration(seconds: 1));
      await pumpEventQueue();
      d = c.read(rtlSocketDiagProvider);
      expect(d.lastError, isNotNull);
      expect(d.lastError, isNot(contains('tok-1')));
      expect(d.lastError, isNot(contains('access_token=tok')));
      expect(d.report(c.read(rtlProvider), now), isNot(contains('tok-1')));
      n.detach(owner);
      expect(d.stateOf(c.read(rtlProvider)), anyOf('Paused', 'Reconnecting'));
      expect(c.read(rtlSocketDiagProvider).stateOf(c.read(rtlProvider)), 'Paused');
    });

    test('hidden / backgrounded: the socket is closed and nothing polls; back on screen reconnects', () async {
      final (c, n, owner) = await attached();
      n.detach(owner);
      await pumpEventQueue();
      expect(sockets.single.closed, isTrue);
      expect(c.read(rtlProvider).polling, isFalse);
      expect(c.read(rtlProvider).socketUp, isFalse);
      for (final d in [RtlNotifier.interval, RtlNotifier.tagPollInterval, const Duration(seconds: 1)]) {
        expect(timers.live(d), 0);
      }
      final before = backend.requests.length;
      timers.fire(RtlNotifier.tagPollInterval);
      await pumpEventQueue();
      expect(backend.requests.length, before);
      n.attach(Object());
      await pumpEventQueue();
      expect(sockets, hasLength(2));
      expect(c.read(rtlProvider).live, isTrue);
    });
  });
}

/// Stands in for the platform's WebSocketException (its message quotes the URL).
class WebSocketExceptionLike implements Exception {
  WebSocketExceptionLike(this.message);
  final String message;

  @override
  String toString() => message;
}

class _Idle implements LiveSocket {
  final _m = StreamController<Object?>();

  @override
  Future<void> get ready => Future.value();

  @override
  Stream<Object?> get messages => _m.stream;

  @override
  Future<void> close() => _m.close();
}
