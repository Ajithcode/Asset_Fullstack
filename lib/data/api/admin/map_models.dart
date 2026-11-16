import 'dart:math' as math;

import 'package:latlong2/latlong.dart';

import 'package:asset360_mobile/data/api/assets/json_read.dart';
import 'package:asset360_mobile/data/api/tracking/tracking_models.dart' show jUtc;

/// Typed replies of the Administration map system (ADMIN_BASE_URL, port 5460,
/// /api/administration/maps/*). Fields: docs/backend_ref/Services/Administration/Api/
/// A360.Administration.Api/Contracts/MapContracts.cs (`FloorPlanResponse`, `ZoneResponse`,
/// `PlotResponse`, `LayerResponse`, `MapSettingsResponse`). Every position / size is PERCENT of the
/// plan (0–100, origin top-left, y downwards); a circle radius is percent of the plan WIDTH.

String _s(Map<String, dynamic> j, String key) => jStr(j[key])?.trim() ?? '';
String? _o(Map<String, dynamic> j, String key) => jStr(j[key])?.trim();

List<T> _list<T>(Object? body, T Function(Map<String, dynamic>) parse) => [
  for (final e in jItems(body))
    if (e is Map) parse(jMap(e)),
];

/// A point in plan percent.
typedef PlanPoint = ({double x, double y});

/// GET /maps/floor-plans?locationId=&status=Published (`FloorPlanResponse`). [locationId] is the
/// Master Data id of the site / building / floor; [fileUrl] is the Media URL of the drawing.
class MapFloorPlan {
  const MapFloorPlan({
    required this.floorPlanId,
    required this.locationId,
    this.locationType = '',
    this.locationPath,
    this.name = '',
    this.description,
    this.type = '',
    this.format = '',
    this.fileUrl,
    this.fileName = '',
    this.widthMeters,
    this.heightMeters,
    this.scale,
    this.currentVersion = '',
    this.status,
    this.publishedAt,
    this.zoneCount = 0,
    this.plotCount = 0,
  });

  final String floorPlanId, locationId, locationType, name, type, format, fileName, currentVersion;
  final String? locationPath, description, fileUrl, scale, status;
  final double? widthMeters, heightMeters;
  final DateTime? publishedAt;
  final int zoneCount, plotCount;

  bool get isPublished => status == 'Published';

  /// PNG / JPG can be drawn as the map; PDF / SVG / DWG / DXF cannot (no renderer on the phone).
  bool get isImage =>
      const {'PNG', 'JPG', 'JPEG', 'WEBP'}.contains(format.toUpperCase()) ||
      RegExp(r'\.(png|jpe?g|webp)$', caseSensitive: false).hasMatch(fileUrl ?? fileName);

  factory MapFloorPlan.fromJson(Map<String, dynamic> j) => MapFloorPlan(
    floorPlanId: _s(j, 'floorPlanId'),
    locationId: _s(j, 'locationId'),
    locationType: _s(j, 'locationType'),
    locationPath: _o(j, 'locationPath'),
    name: _s(j, 'name'),
    description: _o(j, 'description'),
    type: _s(j, 'type'),
    format: _s(j, 'format'),
    fileUrl: _o(j, 'fileUrl'),
    fileName: _s(j, 'fileName'),
    widthMeters: jNum(j['widthMeters']),
    heightMeters: jNum(j['heightMeters']),
    scale: _o(j, 'scale'),
    currentVersion: _s(j, 'currentVersion'),
    status: _o(j, 'status'),
    publishedAt: jUtc(j['publishedAt']),
    zoneCount: jInt(j['zoneCount']) ?? 0,
    plotCount: jInt(j['plotCount']) ?? 0,
  );

  static List<MapFloorPlan> parseList(Object? body) =>
      _list(body, MapFloorPlan.fromJson).where((p) => p.floorPlanId.isNotEmpty).toList();
}

/// GET /maps/floor-plans/{id}/zones (`ZoneResponse`). [shape] = Polygon / Rectangle / Circle /
/// Custom. [x] / [y] / [w] / [h] = bounding box (always sent); [points] = outline of Polygon /
/// Custom; [radius] = circle radius in percent of the plan width.
class MapZone {
  const MapZone({
    required this.zoneId,
    this.floorPlanId = '',
    this.code = '',
    this.name = '',
    this.type = '',
    this.color,
    this.shape = '',
    this.x = 0,
    this.y = 0,
    this.w = 0,
    this.h = 0,
    this.points = const [],
    this.radius,
    this.areaM2,
    this.description,
    this.accessRule,
    this.active = true,
    this.geofence = false,
    this.entryAlert = false,
    this.exitAlert = false,
    this.dwellAlert = false,
    this.dwellMinutes = 0,
  });

  final String zoneId, floorPlanId, code, name, type, shape;
  final String? color, description, accessRule;
  final double x, y, w, h;
  final List<PlanPoint> points;
  final double? radius, areaM2;
  final bool active, geofence, entryAlert, exitAlert, dwellAlert;
  final int dwellMinutes;

  String get label => name.isNotEmpty ? name : code;

  /// Centre of the bounding box (a circle's centre: the server keeps no separate centre, so
  /// the box centre is used; a zero-size box means [x] / [y] IS the centre).
  PlanPoint get center => (w == 0 && h == 0) ? (x: x, y: y) : (x: x + w / 2, y: y + h / 2);

  /// The outline in plan percent: polygon points; a rectangle's 4 corners; a circle as a
  /// [segments]-sided polygon (radius = percent of the plan WIDTH, so it is scaled by
  /// [aspect] = width / height on the y axis to stay round).
  List<PlanPoint> outline({double aspect = 1, int segments = 48}) {
    final s = shape.toLowerCase();
    if ((s == 'polygon' || s == 'custom') && points.length >= 3) return points;
    if (s == 'circle' && (radius ?? 0) > 0) {
      final c = center, r = radius!;
      return [
        for (var i = 0; i < segments; i++)
          (
            x: c.x + r * math.cos(2 * math.pi * i / segments),
            y: c.y + r * aspect * math.sin(2 * math.pi * i / segments),
          ),
      ];
    }
    return [(x: x, y: y), (x: x + w, y: y), (x: x + w, y: y + h), (x: x, y: y + h)];
  }

  factory MapZone.fromJson(Map<String, dynamic> j) => MapZone(
    zoneId: _s(j, 'zoneId'),
    floorPlanId: _s(j, 'floorPlanId'),
    code: _s(j, 'code'),
    name: _s(j, 'name'),
    type: _s(j, 'type'),
    color: _o(j, 'color'),
    shape: _s(j, 'shape'),
    x: jNum(j['x']) ?? 0,
    y: jNum(j['y']) ?? 0,
    w: jNum(j['w']) ?? 0,
    h: jNum(j['h']) ?? 0,
    points: [
      for (final p in jList(j['points']))
        if (p is Map && jNum(p['x']) != null && jNum(p['y']) != null) (x: jNum(p['x'])!, y: jNum(p['y'])!),
    ],
    radius: jNum(j['radius']),
    areaM2: jNum(j['areaM2']),
    description: _o(j, 'description'),
    accessRule: _o(j, 'accessRule'),
    active: jBool(j['active'], orElse: true),
    geofence: jBool(j['geofence']),
    entryAlert: jBool(j['entryAlert']),
    exitAlert: jBool(j['exitAlert']),
    dwellAlert: jBool(j['dwellAlert']),
    dwellMinutes: jInt(j['dwellMinutes']) ?? 0,
  );

  static List<MapZone> parseList(Object? body) =>
      _list(body, MapZone.fromJson).where((z) => z.zoneId.isNotEmpty).toList();
}

/// GET /maps/floor-plans/{id}/plots (`PlotResponse`): an asset or device PLACED on the plan by an
/// administrator ([kind] = asset | device; [itemId] = asset registration id or IoT device id).
/// This is where it was plotted, not a live position.
class MapPlot {
  const MapPlot({
    required this.kind,
    required this.itemId,
    required this.x,
    required this.y,
    this.floorPlanId = '',
    this.code,
    this.name,
    this.type,
    this.assetType,
    this.model,
    this.serial,
    this.status,
    this.orientation = 0,
    this.zoneId,
    this.updatedAt,
  });

  final String kind, itemId, floorPlanId;
  final double x, y, orientation;
  final String? code, name, type, assetType, model, serial, status, zoneId;
  final DateTime? updatedAt;

  bool get isAsset => kind == 'asset';
  bool get isDevice => kind == 'device';
  String get label => name ?? code ?? itemId;

  factory MapPlot.fromJson(Map<String, dynamic> j) => MapPlot(
    kind: _s(j, 'kind').toLowerCase(),
    itemId: _s(j, 'itemId'),
    x: jNum(j['x']) ?? 0,
    y: jNum(j['y']) ?? 0,
    floorPlanId: _s(j, 'floorPlanId'),
    code: _o(j, 'code'),
    name: _o(j, 'name'),
    type: _o(j, 'type'),
    assetType: _o(j, 'assetType'),
    model: _o(j, 'model'),
    serial: _o(j, 'serial'),
    status: _o(j, 'status'),
    orientation: jNum(j['orientation']) ?? 0,
    zoneId: _o(j, 'zoneId'),
    updatedAt: jUtc(j['updatedAt']),
  );

  static List<MapPlot> parseList(Object? body) =>
      _list(body, MapPlot.fromJson).where((p) => p.itemId.isNotEmpty).toList();
}

/// GET /maps/floor-plans/{id}/layers (`LayerResponse`), eight keys: zones, geofences, assets,
/// devices, annotations, rooms, base, heatmap. Shown by default = [enabled] && [visible].
class MapLayer {
  const MapLayer({required this.key, this.name = '', this.enabled = true, this.visible = true, this.opacity = 80});

  final String key, name;
  final bool enabled, visible;

  /// 0–100.
  final int opacity;

  bool get shown => enabled && visible;

  factory MapLayer.fromJson(Map<String, dynamic> j) => MapLayer(
    key: _s(j, 'key'),
    name: _s(j, 'name'),
    enabled: jBool(j['enabled'], orElse: true),
    visible: jBool(j['visible'], orElse: true),
    opacity: jInt(j['opacity']) ?? 80,
  );

  static List<MapLayer> parseList(Object? body) =>
      _list(body, MapLayer.fromJson).where((l) => l.key.isNotEmpty).toList();
}

/// The four layers the phone can toggle. Defaults come from the server's layer list when given.
class MapLayerToggles {
  const MapLayerToggles({this.zones = true, this.geofences = true, this.assets = true, this.devices = true});

  final bool zones, geofences, assets, devices;

  /// Server defaults: a layer the server lists is on when enabled && visible; one it does not
  /// list stays on.
  factory MapLayerToggles.fromServer(List<MapLayer> layers) {
    bool on(String key) => layers.where((l) => l.key == key).map((l) => l.shown).firstOrNull ?? true;
    return MapLayerToggles(
      zones: on('zones'),
      geofences: on('geofences'),
      assets: on('assets'),
      devices: on('devices'),
    );
  }

  MapLayerToggles copyWith({bool? zones, bool? geofences, bool? assets, bool? devices}) => MapLayerToggles(
    zones: zones ?? this.zones,
    geofences: geofences ?? this.geofences,
    assets: assets ?? this.assets,
    devices: devices ?? this.devices,
  );

  @override
  bool operator ==(Object other) =>
      other is MapLayerToggles &&
      other.zones == zones &&
      other.geofences == geofences &&
      other.assets == assets &&
      other.devices == devices;

  @override
  int get hashCode => Object.hash(zones, geofences, assets, devices);
}

/// GET /maps/settings (`MapSettingsResponse`): only the values the phone reads.
class MapSettings {
  const MapSettings({this.defaultView = '', this.defaultZoom, this.plotted = const {}});

  final String defaultView;
  final double? defaultZoom;

  /// e.g. {"assets": true, "devices": true}.
  final Map<String, bool> plotted;

  factory MapSettings.fromJson(Object? body) {
    final j = jMap(body);
    return MapSettings(
      defaultView: _s(j, 'defaultView'),
      defaultZoom: jNum(j['defaultZoom']),
      plotted: {
        for (final e in jMap(j['plotted']).entries)
          if (e.value is bool) e.key: e.value as bool,
      },
    );
  }
}

// ------------------------------------------------------------------ plan → map coordinates

/// Turns plan percent into flutter_map coordinates for `CrsSimple` (1 unit = 256 px at zoom 0,
/// y upwards). The plan is [width] x [height] units with its top-left at (lat [height], lng 0)
/// and bottom-right at (lat 0, lng [width]). The longer side is [span] (1) unit, so the plan
/// fits a phone around zoom 0–1 and stays well inside latlong2's latitude range; the aspect ratio
/// is kept.
class PlanFrame {
  PlanFrame._(this.width, this.height);

  /// [aspect] = plan width / height (from widthMeters / heightMeters, else the image's pixels).
  factory PlanFrame.forAspect(double aspect) {
    final a = aspect.isFinite && aspect > 0 ? aspect : 1.0;
    return a >= 1 ? PlanFrame._(span, span / a) : PlanFrame._(span * a, span);
  }

  /// The longer side in map units.
  static const double span = 1;

  final double width, height;

  double get aspect => width / height;

  /// Plan percent (x right, y down) → map point.
  LatLng toLatLng(double xPct, double yPct) =>
      LatLng(height * (1 - yPct.clamp(0, 100) / 100), width * xPct.clamp(0, 100) / 100);

  LatLng point(PlanPoint p) => toLatLng(p.x, p.y);

  /// Map point → plan percent (the inverse of [toLatLng]).
  PlanPoint toPercent(LatLng p) => (x: p.longitude / width * 100, y: (1 - p.latitude / height) * 100);

  /// Corners of the plan image: top-left, bottom-right.
  LatLng get topLeft => LatLng(height, 0);
  LatLng get bottomRight => LatLng(0, width);

  List<LatLng> zoneOutline(MapZone z) => [for (final p in z.outline(aspect: aspect)) point(p)];
}
