import 'package:asset360_mobile/data/api/admin/map_models.dart';
import 'package:asset360_mobile/data/api/assets/json_read.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_models.dart';
import 'package:asset360_mobile/data/api/tracking/tracking_models.dart';
import 'package:asset360_mobile/data/tracking/tracked_assets.dart' show staleAfter;

/// Pure logic of the LIVE LOCATION TREE (Tracking → Live Map / Geofencing): places every tag of
/// Asset GET /api/asset-dashboard/location-updates at the SITE of the gateway that last heard it
/// (tag → lastToGatewayId → Tracking gateway → siteId). The servers send no coordinates for tags
/// or gateways today, so nothing here invents a position: the tree is site-level, plus the
/// asset's REGISTERED building / floor / zone when that registration is in the same site.
/// No Flutter, no I/O.

String _norm(String? s) => (s ?? '').trim().toLowerCase();

String? _o(Map<String, dynamic> j, String key) {
  final s = jStr(j[key])?.trim();
  return s == null || s.isEmpty ? null : s;
}

/// One tag of GET /api/asset-dashboard/location-updates (`tags[]`).
class TagLocationUpdate {
  const TagLocationUpdate({
    required this.tagId,
    this.count = 0,
    this.lastChangedAt,
    this.lastFromGatewayId,
    this.lastToGatewayId,
    this.lastToLatitude,
    this.lastToLongitude,
  });

  final String tagId;

  /// Gateway changes in the period.
  final int count;
  final DateTime? lastChangedAt;
  final String? lastFromGatewayId, lastToGatewayId;

  /// Null today; kept so a later server can send real positions.
  final double? lastToLatitude, lastToLongitude;

  factory TagLocationUpdate.fromJson(Map<String, dynamic> j) => TagLocationUpdate(
    tagId: _o(j, 'tagId') ?? '',
    count: jInt(j['count']) ?? 0,
    lastChangedAt: jUtc(j['lastChangedAt']),
    lastFromGatewayId: _o(j, 'lastFromGatewayId'),
    lastToGatewayId: _o(j, 'lastToGatewayId'),
    lastToLatitude: jNum(j['lastToLatitude']),
    lastToLongitude: jNum(j['lastToLongitude']),
  );

  /// `{tags: [...]}` (the documented shape) or a bare list.
  static List<TagLocationUpdate> parseList(Object? body) {
    final list = body is Map ? (body['tags'] ?? body['items']) : body;
    return [
      for (final e in jItems(list))
        if (e is Map) TagLocationUpdate.fromJson(jMap(e)),
    ].where((t) => t.tagId.isNotEmpty).toList();
  }
}

/// One gateway of Tracking GET /api/iot-devices/gateways (site only: no zone / floor / position).
class TrkGateway {
  const TrkGateway({required this.deviceId, this.deviceName = '', this.siteId, this.siteName});

  final String deviceId, deviceName;
  final String? siteId, siteName;

  String get label => deviceName.isNotEmpty ? deviceName : deviceId;

  factory TrkGateway.fromJson(Map<String, dynamic> j) => TrkGateway(
    deviceId: _o(j, 'deviceId') ?? '',
    deviceName: _o(j, 'deviceName') ?? '',
    siteId: _o(j, 'siteId'),
    siteName: _o(j, 'siteName'),
  );

  static List<TrkGateway> parseList(Object? body) => [
    for (final e in jItems(body))
      if (e is Map) TrkGateway.fromJson(jMap(e)),
  ].where((g) => g.deviceId.isNotEmpty).toList();
}

/// A tag ↔ asset link (Tracking iot-devices: `assetCode` = tag id; Dashboard network-status/tags).
class TagLink {
  const TagLink({required this.tagId, this.assetRegistrationId, this.assetName, this.battery});

  final String tagId;
  final String? assetRegistrationId, assetName;
  final int? battery;
}

/// Tag links from the Tracking devices (assetCode or deviceId = tag id) and the Dashboard
/// network-status tags; the device wins (it carries the battery).
Map<String, TagLink> tagLinks(
  List<TrkDevice> devices,
  List<({String tagId, String? assetRegistrationId, String? assetName})> networkTags,
) {
  final out = <String, TagLink>{};
  for (final t in networkTags) {
    if (t.tagId.trim().isEmpty) continue;
    out[_norm(t.tagId)] = TagLink(tagId: t.tagId, assetRegistrationId: t.assetRegistrationId, assetName: t.assetName);
  }
  for (final d in devices) {
    if (d.isGateway) continue;
    for (final id in {?d.assetCode, d.deviceId}) {
      final old = out[_norm(id)];
      out[_norm(id)] = TagLink(
        tagId: id,
        assetRegistrationId: d.assetRegistrationId ?? old?.assetRegistrationId,
        assetName: d.assetName ?? old?.assetName,
        battery: d.batteryLevel ?? old?.battery,
      );
    }
  }
  return out;
}

/// The asset's registered location (Asset service `locationAssignment.location`).
class RegisteredPlace {
  const RegisteredPlace({
    this.siteId,
    this.siteName,
    this.buildingId,
    this.buildingName,
    this.floorId,
    this.floorName,
    this.zoneId,
    this.zoneName,
  });

  final String? siteId, siteName, buildingId, buildingName, floorId, floorName, zoneId, zoneName;
}

/// Why a tag could not be placed at a site.
enum UnplacedReason { noGateway, unknownGateway, gatewayWithoutSite }

/// One tag / asset in the tree.
class LiveTagAsset {
  const LiveTagAsset({
    required this.tagId,
    this.assetRegistrationId,
    this.assetName,
    this.gatewayId,
    this.gateway,
    this.lastChangedAt,
    this.battery,
    this.alerts = 0,
    this.registered,
    this.unplaced,
    this.moves = 0,
    this.near,
  });

  /// The PLOTTED device that last heard the tag (KEY RULE: lastToGatewayId = a device plot):
  /// gives the live floor / zone and "near" position. Null = placed by the gateways list only.
  final DevicePlace? near;

  final String tagId;
  final String? assetRegistrationId, assetName;

  /// lastToGatewayId; [gateway] null = not in the gateways list.
  final String? gatewayId;
  final TrkGateway? gateway;
  final DateTime? lastChangedAt;
  final int? battery;

  /// Open IoT alerts of the asset.
  final int alerts;

  /// The registered building / floor / zone — set only when it is in the live site.
  final RegisteredPlace? registered;
  final UnplacedReason? unplaced;
  final int moves;

  String get title => assetName ?? assetRegistrationId ?? 'Tag $tagId';

  /// "near Gate 1" / "Unknown gateway GW-9" / "No gateway reported".
  String get nearLabel => near != null
      ? 'near ${near!.deviceLabel}'
      : gatewayId == null
      ? 'No gateway reported'
      : gateway == null
      ? 'Unknown gateway $gatewayId'
      : 'near ${gateway!.label}';

  /// Online = the tag changed gateway within [staleAfter] (10 min).
  bool isOnline(DateTime now) => lastChangedAt != null && now.difference(lastChangedAt!) <= staleAfter;

  bool matches(String query) {
    final q = _norm(query);
    if (q.isEmpty) return true;
    return [
      tagId,
      assetRegistrationId,
      assetName,
      gatewayId,
      gateway?.deviceName,
      near?.deviceLabel,
    ].any((s) => _norm(s).contains(q));
  }
}

enum LocLevel { site, building, floor, zone }

/// One Site / Building / Floor / Zone of the tree: its children and the assets placed directly at
/// it (a site's direct assets have no registered building there).
class LocNode {
  LocNode({required this.level, required this.id, required this.name});

  final LocLevel level;
  final String id;
  final String name;
  final children = <LocNode>[];
  final assets = <LiveTagAsset>[];

  /// Floors: a picture (Master Data imageUrl) or a published plan exists.
  bool hasPlan = false;

  Iterable<LiveTagAsset> get allAssets sync* {
    yield* assets;
    for (final c in children) {
      yield* c.allAssets;
    }
  }

  LocCounts counts(DateTime now) => LocCounts.of(allAssets, now);

  LocNode? find(String id) {
    if (this.id == id) return this;
    for (final c in children) {
      final f = c.find(id);
      if (f != null) return f;
    }
    return null;
  }
}

class LocCounts {
  const LocCounts({this.assets = 0, this.online = 0, this.offline = 0, this.alerts = 0});

  final int assets, online, offline, alerts;

  static LocCounts of(Iterable<LiveTagAsset> list, DateTime now) {
    var n = 0, on = 0, al = 0;
    for (final a in list) {
      n++;
      if (a.isOnline(now)) on++;
      al += a.alerts;
    }
    return LocCounts(assets: n, online: on, offline: n - on, alerts: al);
  }
}

/// The whole tree: sites with assets, and the tags that could not be placed.
class LocationTree {
  const LocationTree({required this.sites, required this.unplaced, required this.unknownGateways});

  final List<LocNode> sites;
  final List<LiveTagAsset> unplaced;

  /// Gateway ids heard by a tag but missing from the gateways list.
  final Set<String> unknownGateways;

  Iterable<LiveTagAsset> get all sync* {
    for (final s in sites) {
      yield* s.allAssets;
    }
    yield* unplaced;
  }

  LocNode? find(String id) {
    for (final s in sites) {
      final f = s.find(id);
      if (f != null) return f;
    }
    return null;
  }

  /// Live asset count per site id (map pins).
  Map<String, int> get siteCounts => {for (final s in sites) s.id: s.allAssets.length};
}

/// Master Data used to name the levels and to mark floors that have a picture.
class LocationMasterData {
  const LocationMasterData({
    this.sites = const [],
    this.buildings = const [],
    this.floors = const [],
    this.zones = const [],
    this.planLocationIds = const {},
  });

  final List<MdSite> sites;
  final List<MdSpace> buildings, floors, zones;

  /// Location ids with a PUBLISHED Administration floor plan.
  final Set<String> planLocationIds;

  bool floorHasPlan(String floorId) =>
      planLocationIds.contains(floorId) ||
      floors.any((f) => f.id == floorId && ((f.imageUrl ?? '').isNotEmpty || f.images.isNotEmpty));
}

/// Builds the tree. [alerts] = open alerts; [registered] = registered place per registration id.
LocationTree buildLocationTree({
  required List<TagLocationUpdate> updates,
  required List<TrkGateway> gateways,
  required Map<String, TagLink> links,
  Map<String, RegisteredPlace> registered = const {},
  List<TrkAlert> alerts = const [],
  LocationMasterData md = const LocationMasterData(),
  Map<String, DevicePlace> devicePlaces = const {},
  String query = '',
}) {
  final gw = {for (final g in gateways) _norm(g.deviceId): g};
  final alertsOf = <String, int>{};
  for (final a in alerts) {
    final id = a.assetRegistrationId;
    if (id != null && a.isOpen) alertsOf[_norm(id)] = (alertsOf[_norm(id)] ?? 0) + 1;
  }
  final sites = <String, LocNode>{};
  final unplaced = <LiveTagAsset>[];
  final unknown = <String>{};

  String siteName(String id, String? fallback) => md.sites.where((s) => s.id == id).firstOrNull?.name ?? fallback ?? id;
  String spaceName(List<MdSpace> list, String id, String? fallback) =>
      list.where((s) => s.id == id).firstOrNull?.name ?? fallback ?? id;
  LocNode child(LocNode parent, LocLevel level, String id, String name) {
    final hit = parent.children.where((c) => c.id == id).firstOrNull;
    if (hit != null) return hit;
    final n = LocNode(level: level, id: id, name: name);
    if (level == LocLevel.floor) n.hasPlan = md.floorHasPlan(id);
    parent.children.add(n);
    return n;
  }

  // Newest update per tag (the endpoint can repeat a tag).
  final latest = <String, TagLocationUpdate>{};
  for (final u in updates) {
    final k = _norm(u.tagId);
    final old = latest[k];
    if (old == null ||
        (u.lastChangedAt != null && (old.lastChangedAt == null || u.lastChangedAt!.isAfter(old.lastChangedAt!)))) {
      latest[k] = u;
    }
  }

  for (final u in latest.values) {
    final link = links[_norm(u.tagId)];
    final regId = link?.assetRegistrationId;
    final gwId = u.lastToGatewayId;
    // KEY RULE first: the gateway id is a PLOTTED device (case-insensitive) → its zone / floor / site.
    final dp = gwId == null ? null : devicePlaces[_norm(gwId)];
    if (dp != null && dp.siteId != null) {
      final asset = LiveTagAsset(
        tagId: u.tagId,
        assetRegistrationId: regId,
        assetName: link?.assetName,
        gatewayId: gwId,
        gateway: gw[_norm(gwId)],
        lastChangedAt: u.lastChangedAt,
        battery: link?.battery,
        alerts: regId == null ? 0 : (alertsOf[_norm(regId)] ?? 0),
        moves: u.count,
        near: dp,
      );
      if (!asset.matches(query)) continue;
      var at = sites.putIfAbsent(
        dp.siteId!,
        () => LocNode(level: LocLevel.site, id: dp.siteId!, name: siteName(dp.siteId!, dp.siteName)),
      );
      if (dp.buildingId != null) {
        at = child(at, LocLevel.building, dp.buildingId!, spaceName(md.buildings, dp.buildingId!, dp.buildingName));
        if (dp.floorId != null) {
          at = child(at, LocLevel.floor, dp.floorId!, spaceName(md.floors, dp.floorId!, dp.floorName));
          if (dp.zoneId != null) at = child(at, LocLevel.zone, dp.zoneId!, dp.zoneName ?? dp.zoneId!);
        }
      }
      at.assets.add(asset);
      continue;
    }
    final g = gwId == null ? null : gw[_norm(gwId)];
    final reason = gwId == null
        ? UnplacedReason.noGateway
        : g == null
        ? UnplacedReason.unknownGateway
        : (g.siteId == null ? UnplacedReason.gatewayWithoutSite : null);
    if (reason == UnplacedReason.unknownGateway) unknown.add(gwId!);
    final reg = regId == null ? null : registered[_norm(regId)];
    final sameSite = reason == null && reg?.siteId != null && reg!.siteId == g!.siteId;
    final asset = LiveTagAsset(
      tagId: u.tagId,
      assetRegistrationId: regId,
      assetName: link?.assetName,
      gatewayId: gwId,
      gateway: g,
      lastChangedAt: u.lastChangedAt,
      battery: link?.battery,
      alerts: regId == null ? 0 : (alertsOf[_norm(regId)] ?? 0),
      registered: sameSite ? reg : null,
      unplaced: reason,
      moves: u.count,
    );
    if (!asset.matches(query)) continue;
    if (reason != null) {
      unplaced.add(asset);
      continue;
    }
    final site = sites.putIfAbsent(
      g!.siteId!,
      () => LocNode(level: LocLevel.site, id: g.siteId!, name: siteName(g.siteId!, g.siteName)),
    );
    var at = site;
    if (sameSite && reg.buildingId != null) {
      at = child(at, LocLevel.building, reg.buildingId!, spaceName(md.buildings, reg.buildingId!, reg.buildingName));
      if (reg.floorId != null) {
        at = child(at, LocLevel.floor, reg.floorId!, spaceName(md.floors, reg.floorId!, reg.floorName));
        if (reg.zoneId != null) {
          at = child(at, LocLevel.zone, reg.zoneId!, spaceName(md.zones, reg.zoneId!, reg.zoneName));
        }
      }
    }
    at.assets.add(asset);
  }

  int byName(LocNode a, LocNode b) => a.name.toLowerCase().compareTo(b.name.toLowerCase());
  void sortNode(LocNode n) {
    n.children.sort(byName);
    n.assets.sort((a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()));
    n.children.forEach(sortNode);
  }

  final list = sites.values.toList()..sort(byName);
  list.forEach(sortNode);
  unplaced.sort((a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()));
  return LocationTree(sites: list, unplaced: unplaced, unknownGateways: unknown);
}

/// Map mode is possible when any site / building / floor has latitude + longitude (not 0, 0).
bool hasMapCoordinates(LocationMasterData md) {
  bool ok(double? lat, double? lng) => lat != null && lng != null && !(lat == 0 && lng == 0);
  return md.sites.any((s) => ok(s.latitude, s.longitude)) ||
      md.buildings.any((b) => ok(b.latitude, b.longitude)) ||
      md.floors.any((f) => ok(f.latitude, f.longitude));
}

// ------------------------------------------------------------------ device plots (KEY RULE)

/// `asset:{id}` / `device:{id}`.
String plotKeyOf(MapPlot p) => '${p.kind}:${p.itemId}';

/// One floor plan with its zones and plots.
typedef PlanData = ({MapFloorPlan plan, List<MapZone> zones, List<MapPlot> plots});

/// A PLOTTED device and where it is: plan, map zone, and the Master Data site / building / floor /
/// zone (from the zone's `location`, else the plot's, else the plan's floor).
class DevicePlace {
  const DevicePlace({
    required this.plot,
    required this.plan,
    this.zone,
    this.siteId,
    this.siteName,
    this.buildingId,
    this.buildingName,
    this.floorId,
    this.floorName,
    this.zoneId,
    this.zoneName,
  });

  final MapPlot plot;
  final MapFloorPlan plan;

  /// The map zone the device is plotted in.
  final MapZone? zone;
  final String? siteId, siteName, buildingId, buildingName, floorId, floorName;

  /// Tree zone id: the Master Data zone, else `map:{map zoneId}`.
  final String? zoneId, zoneName;

  String get deviceLabel => plot.label;
  String? get mapZoneId => plot.zoneId;
}

/// Every plotted device of [plans], keyed by each of its ids (itemId / deviceId / code) in lower
/// case, so "c851fbf764c0" finds the plot "C851FBF764C0". Published plans win over drafts.
Map<String, DevicePlace> devicePlaces(List<PlanData> plans, LocationMasterData md) {
  final out = <String, DevicePlace>{};
  final ordered = [...plans]..sort((a, b) => (a.plan.isPublished ? 0 : 1).compareTo(b.plan.isPublished ? 0 : 1));
  for (final d in ordered) {
    final floor = md.floors.where((f) => f.id == d.plan.locationId).firstOrNull;
    final building = md.buildings.where((b) => b.id == floor?.buildingId).firstOrNull;
    for (final p in d.plots) {
      if (!p.isDevice) continue;
      final zone = d.zones.where((z) => z.zoneId == p.zoneId).firstOrNull;
      final loc = zone?.location ?? p.location;
      final place = DevicePlace(
        plot: p,
        plan: d.plan,
        zone: zone,
        siteId: loc?.siteId ?? floor?.site?.id ?? building?.site?.id,
        siteName: loc?.siteName ?? floor?.site?.name ?? building?.site?.name,
        buildingId: loc?.buildingId ?? floor?.buildingId,
        buildingName: loc?.buildingName ?? floor?.buildingName,
        floorId: loc?.floorId ?? floor?.id ?? (d.plan.locationType.toLowerCase() == 'floor' ? d.plan.locationId : null),
        floorName: loc?.floorName ?? floor?.name,
        zoneId: zone == null && p.zoneId == null ? null : (loc?.zoneId ?? 'map:${zone?.zoneId ?? p.zoneId}'),
        zoneName: zone?.label ?? loc?.zoneName ?? p.zoneName,
      );
      for (final k in p.deviceKeys) {
        out.putIfAbsent(k, () => place);
      }
    }
  }
  return out;
}

/// Floor plans the phone shows: Published always; Draft only in developer builds; Archived never.
bool planVisible(MapFloorPlan p, {required bool dev}) => p.isPublished || (dev && p.isDraft);

/// The plan to show for a floor: the newest Published, else (dev builds) the newest Draft.
MapFloorPlan? planForLocation(List<MapFloorPlan> plans, String locationId, {required bool dev}) {
  final mine =
      [
        for (final p in plans)
          if (p.locationId == locationId && planVisible(p, dev: dev)) p,
      ]..sort((a, b) {
        final s = (a.isPublished ? 0 : 1).compareTo(b.isPublished ? 0 : 1);
        return s != 0 ? s : (b.publishedAt ?? DateTime(0)).compareTo(a.publishedAt ?? DateTime(0));
      });
  return mine.firstOrNull;
}

/// Online state of a plotted device: the server's status when it says online / offline, else
/// last seen within [staleAfter].
bool deviceOnline(TrkDevice? d, DateTime now) {
  if (d == null) return false;
  final s = _norm(d.status);
  if (const {'online', 'active', 'connected'}.contains(s)) return true;
  if (const {'offline', 'inactive', 'disconnected'}.contains(s)) return false;
  final seen = d.lastSeenAt;
  return seen != null && now.difference(seen) <= staleAfter;
}

/// What one floor plan shows live.
class FloorLive {
  const FloorLive({
    this.byZone = const {},
    this.clusters = const {},
    this.plotted = const {},
    this.moved = const {},
    this.live = const [],
  });

  /// Map zone id → live assets whose tag was last heard by a device plotted in that zone.
  final Map<String, List<LiveTagAsset>> byZone;

  /// Device [plotKeyOf] → live assets near it that have NO asset plot on this plan.
  final Map<String, List<LiveTagAsset>> clusters;

  /// Asset [plotKeyOf] → its live tag (anywhere).
  final Map<String, LiveTagAsset> plotted;

  /// Asset plot keys whose live zone differs from the plotted zone.
  final Set<String> moved;

  /// Every live asset heard on this plan.
  final List<LiveTagAsset> live;
}

/// Joins the tree's live assets with one plan's plots.
FloorLive buildFloorLive({
  required MapFloorPlan plan,
  required List<MapPlot> plots,
  required Iterable<LiveTagAsset> assets,
}) {
  final all = assets.toList();
  final live = [
    for (final a in all)
      if (a.near?.plan.floorPlanId == plan.floorPlanId) a,
  ];
  final byZone = <String, List<LiveTagAsset>>{};
  for (final a in live) {
    final z = a.near!.mapZoneId;
    if (z != null) (byZone[z] ??= []).add(a);
  }
  final plotted = <String, LiveTagAsset>{};
  final moved = <String>{};
  final plottedIds = <String>{};
  for (final p in plots) {
    final id = p.assetId;
    if (id == null) continue;
    plottedIds.add(_norm(id));
    final a = all.where((a) => _norm(a.assetRegistrationId) == _norm(id)).firstOrNull;
    if (a == null) continue;
    plotted[plotKeyOf(p)] = a;
    final n = a.near;
    if (n != null && (n.plan.floorPlanId != plan.floorPlanId || n.mapZoneId != p.zoneId)) moved.add(plotKeyOf(p));
  }
  final clusters = <String, List<LiveTagAsset>>{};
  for (final a in live) {
    if (a.assetRegistrationId != null && plottedIds.contains(_norm(a.assetRegistrationId))) continue;
    (clusters[plotKeyOf(a.near!.plot)] ??= []).add(a);
  }
  return FloorLive(byZone: byZone, clusters: clusters, plotted: plotted, moved: moved, live: live);
}
