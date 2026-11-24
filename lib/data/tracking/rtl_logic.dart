import 'dart:math' as math;

import 'package:latlong2/latlong.dart';

import 'package:asset360_mobile/data/api/admin/map_models.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_models.dart';
import 'package:asset360_mobile/data/api/tracking/rtl_models.dart';

/// Pure rules of the Real-Time Location screen (no Flutter): floor picker, floor-plan choice,
/// asset placement on gateway plots, marker status, restricted zones. Tests: test/live/rtl_test.dart.

// ------------------------------------------------------------------ floors

/// One entry of the "Site › Building › Floor" picker.
class RtlFloorOption {
  const RtlFloorOption({required this.floor, this.siteId, this.siteName, this.buildingName});

  final MdSpace floor;
  final String? siteId, siteName, buildingName;

  String get floorId => floor.id;

  String get label => [siteName, buildingName, floor.name].nonNulls.where((s) => s.isNotEmpty).join(' › ');
}

/// Every floor as "Site › Building › Floor", sorted by that label. Deleted / inactive rows are
/// already gone (`mdParseList`). A floor's site comes from the floor, else its building.
List<RtlFloorOption> rtlFloorOptions(List<MdSite> sites, List<MdSpace> buildings, List<MdSpace> floors) {
  final siteById = {for (final s in sites) s.id: s};
  final buildingById = {for (final b in buildings) b.id: b};
  final out = [
    for (final f in floors)
      () {
        final b = f.buildingId == null ? null : buildingById[f.buildingId];
        final siteId = f.site?.id ?? b?.site?.id;
        final site = siteId == null ? null : siteById[siteId];
        return RtlFloorOption(
          floor: f,
          siteId: siteId,
          siteName: site?.name ?? f.site?.name ?? b?.site?.name,
          buildingName: b?.name ?? f.buildingName,
        );
      }(),
  ]..sort((a, b) => a.label.toLowerCase().compareTo(b.label.toLowerCase()));
  return out;
}

// ------------------------------------------------------------------ floor plan choice

/// What the map shows for a floor.
sealed class RtlFloorPlanChoice {
  const RtlFloorPlanChoice();
}

/// An Administration floor plan drawing (zones + gateway plots); [draft] = labelled "Draft".
class RtlPlanChoice extends RtlFloorPlanChoice {
  const RtlPlanChoice(this.plan);

  final MapFloorPlan plan;

  bool get draft => plan.isDraft;
}

/// No usable plan: the Master Data floor picture (no zones / gateways can be placed on it).
class RtlPictureChoice extends RtlFloorPlanChoice {
  const RtlPictureChoice(this.url);

  final String url;
}

/// "No floor plan for this floor".
class RtlNoPlan extends RtlFloorPlanChoice {
  const RtlNoPlan();
}

DateTime _newest(MapFloorPlan p) => p.updatedAt ?? p.publishedAt ?? p.createdAt ?? DateTime.utc(0);

/// The web rule, per floor: the Published plan, else the newest non-archived plan with a file
/// ([dev] builds only: Drafts are skipped otherwise). Only plans whose file is an image can be
/// drawn; when none is, the floor's `imageUrl` / `images[0]`; else [RtlNoPlan].
RtlFloorPlanChoice chooseFloorPlan(List<MapFloorPlan> plans, MdSpace floor, {required bool dev}) {
  final mine = [
    for (final p in plans)
      if (p.locationId == floor.id && !p.isArchived && (p.fileUrl?.isNotEmpty ?? false) && (dev || !p.isDraft)) p,
  ]..sort((a, b) {
      final s = (a.isPublished ? 0 : 1).compareTo(b.isPublished ? 0 : 1);
      return s != 0 ? s : _newest(b).compareTo(_newest(a));
    });
  final plan = mine.where((p) => p.isImage).firstOrNull;
  if (plan != null) return RtlPlanChoice(plan);
  final picture = floor.imageUrl ?? floor.images.firstOrNull;
  if (picture != null && picture.trim().isNotEmpty) return RtlPictureChoice(picture.trim());
  return const RtlNoPlan();
}

/// The floor plan with the most located assets (Home's Asset Location card): assets are counted per
/// `location.floorPlanId`; only plans the Real-Time Location map can draw count (not archived, an image
/// file, Published — or Draft in [dev] builds). Ties go to a Published plan, then the newest.
/// Null = nothing is located on a drawable plan.
({MapFloorPlan plan, int assets})? busiestFloorPlan(List<RtlAsset> assets, List<MapFloorPlan> plans, {required bool dev}) {
  final counts = <String, int>{};
  for (final a in assets) {
    final id = a.location?.floorPlanId?.trim().toLowerCase();
    if (id != null && id.isNotEmpty) counts[id] = (counts[id] ?? 0) + 1;
  }
  final candidates = [
    for (final p in plans)
      if (!p.isArchived && (p.fileUrl?.isNotEmpty ?? false) && p.isImage && (dev || !p.isDraft) && (counts[p.floorPlanId.toLowerCase()] ?? 0) > 0) p,
  ]..sort((a, b) {
      final c = counts[b.floorPlanId.toLowerCase()]!.compareTo(counts[a.floorPlanId.toLowerCase()]!);
      if (c != 0) return c;
      final s = (a.isPublished ? 0 : 1).compareTo(b.isPublished ? 0 : 1);
      return s != 0 ? s : _newest(b).compareTo(_newest(a));
    });
  if (candidates.isEmpty) return null;
  final best = candidates.first;
  return (plan: best, assets: counts[best.floorPlanId.toLowerCase()]!);
}

// ------------------------------------------------------------------ marker status

/// Marker colour classes (legend order).
enum RtlStatus {
  liveMoving('Live (moving)'),
  liveStationary('Live (stationary)'),
  stale('Stale'),
  lowConfidence('Low confidence'),
  offline('Offline');

  const RtlStatus(this.label);
  final String label;
}

/// Green Live + Moving, blue Live + Stationary, orange Stale, red Offline, grey Unknown / low
/// confidence (also a live fix with Low / Unknown confidence).
RtlStatus rtlStatusOf(RtlAsset a) {
  final f = a.freshness.toLowerCase(), c = a.confidence.toLowerCase();
  if (f == 'offline') return RtlStatus.offline;
  if (f == 'stale') return RtlStatus.stale;
  if (f != 'live' || c == 'low' || c == 'unknown') return RtlStatus.lowConfidence;
  return a.movement.toLowerCase() == 'moving' ? RtlStatus.liveMoving : RtlStatus.liveStationary;
}

/// "Moving" / "Stationary" / "Unknown".
String rtlMovementLabel(RtlAsset a) => switch (a.movement.toLowerCase()) {
  'moving' => 'Moving',
  'stationary' => 'Stationary',
  _ => 'Unknown',
};

// ------------------------------------------------------------------ placement

/// Where the overview's assets go on one floor plan.
class RtlPlacement {
  const RtlPlacement({
    this.byGateway = const {},
    this.gatewayPlots = const {},
    this.notOnPlan = const [],
    this.zoneSpots = const {},
  });

  /// Device plot itemId → the assets WITHOUT a zone spot that the gateway hears strongest (they share its point).
  final Map<String, List<RtlAsset>> byGateway;

  /// Device plot itemId → the plot.
  final Map<String, MapPlot> gatewayPlots;

  /// Assets with neither a zone on this plan nor a plotted gateway ("Not on this floor plan").
  final List<RtlAsset> notOnPlan;

  /// Asset id → its spot inside its zone, in map units ([zoneSpotsFor]; the web's `zoneSpots()` layout).
  /// ZONE PRESENCE, not a measured position.
  final Map<String, LatLng> zoneSpots;

  /// Where [asset] is drawn when it has no zone spot, in % of the plan: the x / y of its newest live tag row
  /// (the web's `TagLocation` X / Y = the plot of the gateway hearing the tag), else the plot of its gateway
  /// from the overview. Null = no gateway plot (never invented).
  ({double x, double y})? pointOf(RtlAsset asset) {
    final plot = plotOf(asset);
    if (plot == null) return null;
    final x = asset.x, y = asset.y;
    return x != null && y != null ? (x: x, y: y) : (x: plot.x, y: plot.y);
  }

  /// The device plot [asset] sits on, or null.
  MapPlot? plotOf(RtlAsset asset) {
    for (final e in byGateway.entries) {
      if (e.value.any((a) => a.assetId == asset.assetId)) return gatewayPlots[e.key];
    }
    return null;
  }
}

/// Puts each asset on one floor plan, like the web's `updateAssets()`:
/// 1. an asset whose `location.zoneId` is a zone of this plan sits at its ZONE SPOT (a stable grid inside the
///    zone, assets sorted by id: [zoneSpotsFor]);
/// 2. else at its `location.gatewayId`'s DEVICE plot (itemId / code / deviceId, case-insensitive);
/// 3. else [RtlPlacement.notOnPlan]. Exact positions are never invented.
/// [zones] and [frame] are needed for step 1 (without them every asset uses its gateway).
RtlPlacement placeAssets(List<RtlAsset> assets, List<MapPlot> plots, {List<MapZone> zones = const [], PlanFrame? frame}) {
  final byKey = <String, MapPlot>{};
  for (final p in plots) {
    if (!p.isDevice) continue;
    for (final k in [p.itemId, p.code, p.deviceId]) {
      final key = k?.trim().toLowerCase();
      if (key != null && key.isNotEmpty) byKey.putIfAbsent(key, () => p);
    }
  }
  final zoneById = {
    if (frame != null)
      for (final z in zones)
        if (z.active) z.zoneId.toLowerCase(): z,
  };
  final inZone = <String, List<RtlAsset>>{};
  final byGateway = <String, List<RtlAsset>>{};
  final notOnPlan = <RtlAsset>[];
  for (final a in assets) {
    final zone = zoneById[a.location?.zoneId?.trim().toLowerCase() ?? ''];
    if (zone != null) {
      (inZone[zone.zoneId.toLowerCase()] ??= []).add(a);
      continue;
    }
    final plot = byKey[a.location?.gatewayId.trim().toLowerCase() ?? ''];
    if (plot == null) {
      notOnPlan.add(a);
    } else {
      (byGateway[plot.itemId] ??= []).add(a);
    }
  }
  final spots = <String, LatLng>{};
  for (final e in inZone.entries) {
    final list = [...e.value]..sort((x, y) => compareAssetIds(x.assetId, y.assetId));
    final points = zoneSpotsFor(zoneById[e.key]!, list.length, frame!);
    for (final (i, a) in list.indexed) {
      spots[a.assetId] = points[i];
    }
  }
  return RtlPlacement(
    byGateway: byGateway,
    gatewayPlots: {for (final p in plots) if (p.isDevice) p.itemId: p},
    notOnPlan: notOnPlan,
    zoneSpots: spots,
  );
}

/// Order of asset ids inside a zone: the web sorts with `localeCompare` (case-insensitive first, lower case
/// before upper case on a tie).
int compareAssetIds(String a, String b) {
  final c = a.toLowerCase().compareTo(b.toLowerCase());
  return c != 0 ? c : b.compareTo(a);
}

/// A zone as the web's `zoneGeom()` sees it, in map units (lat = y axis, lng = x axis).
class ZoneGeom {
  const ZoneGeom({this.pts, this.center, this.radius = 0, required this.south, required this.north, required this.west, required this.east});

  /// Polygon / rectangle corners (null for a circle).
  final List<LatLng>? pts;

  /// Circle centre and radius (null centre for other shapes).
  final LatLng? center;
  final double radius;
  final double south, north, west, east;
}

ZoneGeom zoneGeom(MapZone z, PlanFrame f) {
  List<LatLng>? pts;
  LatLng? c;
  var r = 0.0;
  final shape = z.shape.toLowerCase();
  if (shape == 'circle') {
    final ctr = z.center;
    c = f.toLatLng(ctr.x, ctr.y);
    // Radius = % of the plan WIDTH (as the web: radius / 100 · W).
    r = ((z.radius ?? z.w / 2) / 100) * f.width;
  } else if ((shape == 'polygon' || shape == 'custom') && z.points.length >= 3) {
    pts = [for (final p in z.points) f.toLatLng(p.x, p.y)];
  } else {
    pts = [f.toLatLng(z.x, z.y), f.toLatLng(z.x + z.w, z.y), f.toLatLng(z.x + z.w, z.y + z.h), f.toLatLng(z.x, z.y + z.h)];
  }
  final lat = pts != null ? [for (final p in pts) p.latitude] : [c!.latitude - r, c.latitude + r];
  final lng = pts != null ? [for (final p in pts) p.longitude] : [c!.longitude - r, c.longitude + r];
  return ZoneGeom(
    pts: pts,
    center: c,
    radius: r,
    south: lat.reduce(math.min),
    north: lat.reduce(math.max),
    west: lng.reduce(math.min),
    east: lng.reduce(math.max),
  );
}

/// Stable spots for [n] assets inside zone [z] — a port of the web's `zoneSpots()` (floor-plan-map.ts): a grid over
/// the zone's bounding box inset by 10 % on each side, cells outside a polygon / circle (92 % of its radius)
/// skipped, filled in row order from the top-left. The cell size starts at sqrt(area / n) and shrinks by 15 %
/// (up to 40 times) until at least [n] cells fit. Returns ALL cells; the i-th asset (sorted by id) takes the
/// i-th. The same zone and asset ids always give the same spots, so markers never jump between refreshes.
List<LatLng> zoneSpotsFor(MapZone z, int n, PlanFrame f) {
  final g = zoneGeom(z, f);
  bool inside(double lat, double lng) {
    final c = g.center;
    if (c != null) return math.sqrt(math.pow(lat - c.latitude, 2) + math.pow(lng - c.longitude, 2)) <= g.radius * 0.92;
    final p = g.pts!;
    var on = false;
    for (var i = 0, j = p.length - 1; i < p.length; j = i++) {
      if ((p[i].latitude > lat) != (p[j].latitude > lat) &&
          lng < ((p[j].longitude - p[i].longitude) * (lat - p[i].latitude)) / (p[j].latitude - p[i].latitude) + p[i].longitude) {
        on = !on;
      }
    }
    return on;
  }

  final padY = (g.north - g.south) * 0.1;
  final padX = (g.east - g.west) * 0.1;
  final s = g.south + padY, n2 = g.north - padY, w = g.west + padX, e = g.east - padX;
  var d = math.sqrt(((n2 - s) * (e - w)) / math.max(1, n));
  for (var tries = 0; tries < 40; tries++) {
    final cells = <LatLng>[];
    final rows = math.max(1, ((n2 - s) / d).floor());
    final cols = math.max(1, ((e - w) / d).floor());
    for (var r = 0; r < rows; r++) {
      for (var c = 0; c < cols; c++) {
        final lat = n2 - ((r + 0.5) * (n2 - s)) / rows;
        final lng = w + ((c + 0.5) * (e - w)) / cols;
        if (inside(lat, lng)) cells.add(LatLng(lat, lng));
      }
    }
    if (cells.length >= n) return cells;
    d *= 0.85;
  }
  return List.filled(n, LatLng((g.south + g.north) / 2, (g.west + g.east) / 2));
}

/// More assets than this on one gateway show a count bubble (tap → list) instead of a fan.
const rtlFanLimit = 6;

/// Screen offsets (logical pixels) of [count] markers fanned out around their gateway point:
/// one marker sits on the point; 2–[rtlFanLimit] go round a circle of [radius]. Above the limit
/// every offset is zero (the count bubble is drawn instead).
List<({double dx, double dy})> fanOffsets(int count, {double radius = 22}) {
  if (count <= 1 || count > rtlFanLimit) return [for (var i = 0; i < count; i++) (dx: 0.0, dy: 0.0)];
  return [
    for (var i = 0; i < count; i++)
      (
        dx: radius * math.cos(-math.pi / 2 + 2 * math.pi * i / count),
        dy: radius * math.sin(-math.pi / 2 + 2 * math.pi * i / count),
      ),
  ];
}

// ------------------------------------------------------------------ "No location" reasons

/// Why [a] has no marker, like the web's `noLocationReason`; null when it has a location on a floor plan.
/// 1. no tag linked (Tag Management);
/// 2. the tag was never heard: no location at all (Offline = "tag offline, …");
/// 3. the gateway that hears it is not plotted on a floor plan (no floor plan for it). A missing ZONE is not a
///    reason: the asset is then drawn at its gateway's plot.
String? rtlNoLocationReason(RtlAsset a) {
  if (!a.hasTags && (a.tracker?.trim().isEmpty ?? true)) return 'no tag linked';
  final l = a.location;
  if (l == null) {
    return a.freshness.toLowerCase() == 'offline'
        ? 'tag offline, not heard by any gateway'
        : 'tag not heard by any gateway yet';
  }
  final gateway = l.gatewayId.isEmpty ? 'its gateway' : 'gateway ${l.gatewayId}';
  if (l.floorPlanId == null || l.floorPlanId!.isEmpty) return '$gateway is not plotted on a floor plan';
  return null;
}

/// "No location · reason" for lists; null when the asset has a location.
String? rtlNoLocationText(RtlAsset a) {
  final r = rtlNoLocationReason(a);
  return r == null ? null : 'No location · $r';
}

/// The floor of the asset has no drawable floor plan.
const rtlNoFloorPlanReason = 'its floor has no floor plan';

/// Why [a] is not drawn on THIS plan although it has a location (its zone and its gateway are not on the
/// plan): shown in "Not on this floor plan".
String rtlUnplacedReason(RtlAsset a) {
  final l = a.location;
  final gateway = l == null || l.gatewayId.isEmpty ? 'its gateway' : 'gateway ${l.gatewayId}';
  return '$gateway is not plotted on this floor plan';
}

// ------------------------------------------------------------------ zones / gateways

/// A zone whose type or access rule contains "restrict" (drawn red, "Restricted Zone").
bool isRestrictedZone(MapZone z) =>
    z.type.toLowerCase().contains('restrict') || (z.accessRule?.toLowerCase().contains('restrict') ?? false);

/// Gateway connectivity: Online | Idle | Offline (unknown → Offline).
enum RtlGatewayState { online, idle, offline }

RtlGatewayState gatewayStateOf(RtlGateway? g) => switch (g?.connectivity.toLowerCase()) {
  'online' => RtlGatewayState.online,
  'idle' => RtlGatewayState.idle,
  _ => RtlGatewayState.offline,
};

// ------------------------------------------------------------------ list filters

/// The assets list's Status filter values.
String rtlStatusFilterLabel(RtlStatus s) => s.label;

/// "12 s ago" / "4 min ago" / "3 h ago" / "2 d ago" (never "-1 s").
String rtlAgo(DateTime? at, DateTime now) {
  if (at == null) return 'Never';
  final d = now.difference(at);
  if (d.inSeconds < 60) return '${d.inSeconds.clamp(0, 59)} s ago';
  if (d.inMinutes < 60) return '${d.inMinutes} min ago';
  if (d.inHours < 24) return '${d.inHours} h ago';
  return '${d.inDays} d ago';
}
