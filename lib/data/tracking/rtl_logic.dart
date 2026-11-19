import 'dart:math' as math;

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
  const RtlPlacement({this.byGateway = const {}, this.gatewayPlots = const {}, this.notOnPlan = const []});

  /// Device plot itemId → the assets heard strongest by that gateway (they share its point).
  final Map<String, List<RtlAsset>> byGateway;

  /// Device plot itemId → the plot.
  final Map<String, MapPlot> gatewayPlots;

  /// Assets whose gateway is not plotted on this plan ("Not on this floor plan").
  final List<RtlAsset> notOnPlan;

  /// The device plot [asset] sits on, or null.
  MapPlot? plotOf(RtlAsset asset) {
    for (final e in byGateway.entries) {
      if (e.value.any((a) => a.assetId == asset.assetId)) return gatewayPlots[e.key];
    }
    return null;
  }
}

/// Puts each asset at its `location.gatewayId`'s DEVICE plot (itemId or code, case-insensitive).
/// Never invents a position: no gateway / unplotted gateway / plot without x,y → [RtlPlacement.notOnPlan].
RtlPlacement placeAssets(List<RtlAsset> assets, List<MapPlot> plots) {
  final byKey = <String, MapPlot>{};
  for (final p in plots) {
    if (!p.isDevice) continue;
    for (final k in [p.itemId, p.code, p.deviceId]) {
      final key = k?.trim().toLowerCase();
      if (key != null && key.isNotEmpty) byKey.putIfAbsent(key, () => p);
    }
  }
  final byGateway = <String, List<RtlAsset>>{};
  final notOnPlan = <RtlAsset>[];
  for (final a in assets) {
    final plot = byKey[a.location?.gatewayId.trim().toLowerCase() ?? ''];
    if (plot == null) {
      notOnPlan.add(a);
    } else {
      (byGateway[plot.itemId] ??= []).add(a);
    }
  }
  return RtlPlacement(
    byGateway: byGateway,
    gatewayPlots: {for (final p in plots) if (p.isDevice) p.itemId: p},
    notOnPlan: notOnPlan,
  );
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
