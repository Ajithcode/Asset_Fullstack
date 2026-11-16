import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';
import 'package:asset360_mobile/features/more/tracking/tracking_map.dart' show MapRoundButton;

/// Key of a plot marker: `asset:{registration id}` / `device:{device id}`.
String plotKey(MapPlot p) => '${p.kind}:${p.itemId}';

/// The plan's map frame: width / height from the plan's metres when both are set, else the
/// drawing's pixels.
PlanFrame frameFor(MapFloorPlan plan, PlanImage? image) {
  final w = plan.widthMeters, h = plan.heightMeters;
  if (w != null && h != null && w > 0 && h > 0) return PlanFrame.forAspect(w / h);
  return PlanFrame.forAspect(image?.aspect ?? 1);
}

/// Live state of a plotted item: the tracked asset / device's last seen.
Freshness plotFreshness(MapPlot p, TrackingSnapshot? s, DateTime now) {
  if (s == null) return Freshness.unknown;
  final seen = p.isDevice ? s.device(p.itemId)?.lastSeenAt : s.byKey(p.itemId)?.lastSeen;
  return freshnessOf(seen, now);
}

/// Marker colour: live assets violet (orange on low battery), live devices blue, stale / never
/// seen grey.
Color plotColor(MapPlot p, TrackingSnapshot? s, DateTime now) {
  final f = plotFreshness(p, s, now);
  if (f != Freshness.live) return AppColors.grey;
  if (p.isDevice) return AppColors.iconBlue;
  return (s?.byKey(p.itemId)?.lowBattery ?? false) ? AppColors.orange : AppColors.primary;
}

/// A published floor plan drawn with flutter_map's `CrsSimple`: the drawing as an
/// [OverlayImageLayer], zones as polygons (geofences dashed), plotted assets / devices as markers
/// that glide to a new position when a plot moves. Pinch-zoom keeps everything aligned because
/// all of it lives in the same map coordinates ([PlanFrame]).
class FloorPlanMap extends StatefulWidget {
  const FloorPlanMap({
    super.key,
    required this.plan,
    required this.image,
    required this.zones,
    required this.plots,
    required this.layers,
    required this.snapshot,
    required this.now,
    this.selectedPlot,
    this.highlightZones = const {},
    this.trailZones = const {},
    this.onPlotTap,
    this.onZoneTap,
    this.onLayers,
  });

  final MapFloorPlan plan;
  final PlanImage? image;
  final List<MapZone> zones;
  final List<MapPlot> plots;
  final MapLayerToggles layers;
  final TrackingSnapshot? snapshot;
  final DateTime now;

  /// [plotKey] of the selected marker.
  final String? selectedPlot;

  /// Zone ids drawn highlighted (focused geofence, the zone an asset is in).
  final Set<String> highlightZones;

  /// Zone id → visit numbers of a zone-level trail ("1", "3").
  final Map<String, List<int>> trailZones;
  final ValueChanged<MapPlot>? onPlotTap;
  final ValueChanged<MapZone>? onZoneTap;
  final VoidCallback? onLayers;

  @override
  State<FloorPlanMap> createState() => _FloorPlanMapState();
}

class _FloorPlanMapState extends State<FloorPlanMap> with SingleTickerProviderStateMixin {
  final _map = MapController();
  late final AnimationController _move = AnimationController(vsync: this, duration: const Duration(milliseconds: 900));
  Map<String, LatLng> _from = {}, _to = {};

  PlanFrame get _frame => frameFor(widget.plan, widget.image);

  @override
  void initState() {
    super.initState();
    _to = _targets();
    _from = _to;
    _move.value = 1;
  }

  Map<String, LatLng> _targets() => {for (final p in widget.plots) plotKey(p): _frame.toLatLng(p.x, p.y)};

  @override
  void didUpdateWidget(FloorPlanMap old) {
    super.didUpdateWidget(old);
    final next = _targets();
    if (_sameTargets(next)) return;
    // Start every marker from where it is drawn now.
    _from = {for (final k in next.keys) k: _current(k) ?? next[k]!};
    _to = next;
    _move.forward(from: 0);
  }

  bool _sameTargets(Map<String, LatLng> next) =>
      next.length == _to.length && next.entries.every((e) => _to[e.key] == e.value);

  LatLng? _current(String key) {
    final a = _from[key], b = _to[key];
    if (a == null || b == null) return b ?? a;
    final t = Curves.easeInOut.transform(_move.value);
    return LatLng(a.latitude + (b.latitude - a.latitude) * t, a.longitude + (b.longitude - a.longitude) * t);
  }

  @override
  void dispose() {
    _move.dispose();
    _map.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final f = _frame;
    final bounds = LatLngBounds(f.topLeft, f.bottomRight);
    final image = widget.image;
    final zones = [
      for (final z in widget.zones)
        if (z.active && (z.geofence ? widget.layers.geofences || widget.layers.zones : widget.layers.zones)) z,
    ];
    final plots = [
      for (final p in widget.plots)
        if (p.isAsset ? widget.layers.assets : widget.layers.devices) p,
    ];
    return ClipRRect(
      borderRadius: BorderRadius.circular(14),
      child: Stack(
        children: [
          FlutterMap(
            mapController: _map,
            options: MapOptions(
              crs: const CrsSimple(),
              backgroundColor: AppColors.surface,
              // Start inside the plan: the default centre (50, 0) is outside a plan under 50 units high.
              initialCenter: f.toLatLng(50, 50),
              initialZoom: 0,
              initialCameraFit: CameraFit.bounds(bounds: bounds, padding: const EdgeInsets.all(12)),
              cameraConstraint: CameraConstraint.containCenter(bounds: bounds),
              minZoom: -1,
              maxZoom: 6,
              interactionOptions: const InteractionOptions(flags: InteractiveFlag.all & ~InteractiveFlag.rotate),
            ),
            children: [
              if (image != null)
                OverlayImageLayer(
                  overlayImages: [OverlayImage(bounds: bounds, imageProvider: MemoryImage(image.bytes))],
                ),
              PolygonLayer(
                polygons: [
                  for (final z in zones)
                    Polygon(
                      points: f.zoneOutline(z),
                      color: hexColor(
                        z.color,
                        AppColors.primary,
                      ).withValues(alpha: widget.highlightZones.contains(z.zoneId) ? .38 : .16),
                      borderColor: z.geofence ? AppColors.red : hexColor(z.color, AppColors.primary),
                      borderStrokeWidth: widget.highlightZones.contains(z.zoneId) ? 3 : 1.5,
                      pattern: z.geofence ? StrokePattern.dashed(segments: const [8, 5]) : const StrokePattern.solid(),
                    ),
                ],
              ),
              MarkerLayer(
                markers: [
                  for (final z in zones)
                    Marker(
                      point: f.point(z.center),
                      width: 120,
                      height: 34,
                      child: _ZoneLabel(
                        zone: z,
                        count: widget.snapshot?.inZone(name: z.name, code: z.code).length ?? 0,
                        trail: widget.trailZones[z.zoneId] ?? const [],
                        onTap: widget.onZoneTap == null ? null : () => widget.onZoneTap!(z),
                      ),
                    ),
                ],
              ),
              AnimatedBuilder(
                animation: _move,
                builder: (_, _) => MarkerLayer(
                  markers: [
                    for (final p in plots)
                      Marker(
                        point: _current(plotKey(p)) ?? f.toLatLng(p.x, p.y),
                        width: 34,
                        height: 34,
                        child: _PlotDot(
                          plot: p,
                          color: plotColor(p, widget.snapshot, widget.now),
                          selected: widget.selectedPlot == plotKey(p),
                          onTap: widget.onPlotTap == null ? null : () => widget.onPlotTap!(p),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
          Positioned(
            right: 8,
            top: 8,
            child: Column(
              children: [
                if (widget.onLayers != null)
                  MapRoundButton(icon: Icons.layers_outlined, tooltip: 'Layers', onTap: widget.onLayers!),
                const SizedBox(height: 8),
                MapRoundButton(
                  icon: Icons.fit_screen_rounded,
                  tooltip: 'Fit plan',
                  onTap: () => _map.fitCamera(CameraFit.bounds(bounds: bounds, padding: const EdgeInsets.all(12))),
                ),
              ],
            ),
          ),
          Positioned(
            right: 8,
            bottom: 8,
            child: Column(
              children: [
                MapRoundButton(icon: Icons.add, tooltip: 'Zoom in', onTap: () => _zoom(1)),
                const SizedBox(height: 8),
                MapRoundButton(icon: Icons.remove, tooltip: 'Zoom out', onTap: () => _zoom(-1)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  void _zoom(double by) => _map.move(_map.camera.center, (_map.camera.zoom + by).clamp(-1, 6));
}

class _ZoneLabel extends StatelessWidget {
  const _ZoneLabel({required this.zone, required this.count, required this.trail, this.onTap});

  final MapZone zone;
  final int count;
  final List<int> trail;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final text = [zone.label, if (count > 0) '· $count'].join(' ');
    return Semantics(
      button: onTap != null,
      label: '${zone.label}, $count tracked assets${zone.geofence ? ', geofence' : ''}',
      child: GestureDetector(
        onTap: onTap,
        child: Center(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final n in trail)
                Container(
                  margin: const EdgeInsets.only(right: 2),
                  padding: const EdgeInsets.all(4),
                  decoration: BoxDecoration(color: AppColors.primary, shape: BoxShape.circle),
                  child: Text(
                    '$n',
                    style: const TextStyle(color: Colors.white, fontSize: 9, fontWeight: FontWeight.w700),
                  ),
                ),
              Flexible(
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: .9),
                    borderRadius: BorderRadius.circular(6),
                    border: Border.all(color: count > 0 ? AppColors.primary : AppColors.border),
                  ),
                  child: Text(
                    text,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppText.caption.copyWith(
                      fontSize: 10,
                      color: AppColors.text,
                      fontWeight: count > 0 ? FontWeight.w700 : FontWeight.w500,
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PlotDot extends StatelessWidget {
  const _PlotDot({required this.plot, required this.color, required this.selected, this.onTap});

  final MapPlot plot;
  final Color color;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Semantics(
    button: onTap != null,
    label: '${plot.isDevice ? 'Device' : 'Asset'} ${plot.label}',
    child: GestureDetector(
      onTap: onTap,
      child: Center(
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          width: selected ? 30 : 24,
          height: selected ? 30 : 24,
          decoration: BoxDecoration(
            color: color,
            shape: plot.isDevice ? BoxShape.rectangle : BoxShape.circle,
            borderRadius: plot.isDevice ? BorderRadius.circular(6) : null,
            border: Border.all(color: Colors.white, width: selected ? 3 : 2),
            boxShadow: [BoxShadow(color: AppColors.text.withValues(alpha: .3), blurRadius: 4)],
          ),
          child: Icon(
            plot.isDevice ? Icons.router_rounded : Icons.inventory_2_rounded,
            size: selected ? 16 : 13,
            color: Colors.white,
          ),
        ),
      ),
    ),
  );
}
