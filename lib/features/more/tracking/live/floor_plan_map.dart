import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';
import 'package:asset360_mobile/features/more/tracking/tracking_map.dart' show MapRoundButton;

/// Key of a plot marker: `asset:{registration id}` / `device:{device id}`.
String plotKey(MapPlot p) => plotKeyOf(p);

/// The plan's map frame: the drawing's REAL pixel size (zone / plot % are % of the image), else
/// the plan's metres when both are set, else square.
PlanFrame frameFor(MapFloorPlan plan, PlanImage? image) {
  if (image != null && image.width > 0 && image.height > 0) return PlanFrame.forAspect(image.aspect);
  final w = plan.widthMeters, h = plan.heightMeters;
  if (w != null && h != null && w > 0 && h > 0) return PlanFrame.forAspect(w / h);
  return PlanFrame.forAspect(1);
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
    this.live,
    this.deviceOnline,
    this.onClusterTap,
    this.focus,
  });

  /// View on Map: start zoomed in on this plan point (the asset's plot or zone centre).
  final PlanPoint? focus;

  /// LIVE mode (Live Map): zone counts, asset status, moved badges and device clusters come from
  /// location updates (KEY RULE). Null = the Tracking snapshot's zone events (asset zone map).
  final FloorLive? live;

  /// Online state of a plotted device (iot-devices status / last seen).
  final bool Function(MapPlot device)? deviceOnline;

  /// A device with unplotted live assets near it was tapped.
  final void Function(MapPlot device, List<LiveTagAsset> assets)? onClusterTap;

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
              initialCenter: widget.focus == null ? f.toLatLng(50, 50) : f.point(widget.focus!),
              initialZoom: widget.focus == null ? 0 : 2,
              initialCameraFit: widget.focus == null
                  ? CameraFit.bounds(bounds: bounds, padding: const EdgeInsets.all(12))
                  : null,
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
                      ).withValues(alpha: widget.highlightZones.contains(z.zoneId) ? .4 : .25),
                      borderColor: widget.live == null && z.geofence
                          ? AppColors.red
                          : hexColor(z.color, AppColors.primary),
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
                      width: 150,
                      height: widget.live == null ? 34 : 44,
                      child: _ZoneLabel(
                        zone: z,
                        count: widget.live != null
                            ? (widget.live!.byZone[z.zoneId] ?? const []).length
                            : widget.snapshot?.inZone(name: z.name, code: z.code).length ?? 0,
                        alerts: widget.live == null
                            ? null
                            : (widget.live!.byZone[z.zoneId] ?? const []).fold<int>(0, (n, a) => n + a.alerts),
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
                        width: 44,
                        height: 44,
                        child: _PlotDot(
                          key: ValueKey('plot-${plotKey(p)}'),
                          plot: p,
                          color: _color(p),
                          selected: widget.selectedPlot == plotKey(p),
                          moved: widget.live?.moved.contains(plotKey(p)) ?? false,
                          cluster: widget.live?.clusters[plotKey(p)]?.length ?? 0,
                          onTap: _onPlotTap(p),
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

  /// Live mode: assets green online / grey offline (no live tag: violet = plotted only);
  /// devices blue online / grey offline. Snapshot mode: [plotColor].
  Color _color(MapPlot p) {
    final live = widget.live;
    if (live == null) return plotColor(p, widget.snapshot, widget.now);
    if (p.isDevice) return (widget.deviceOnline?.call(p) ?? false) ? AppColors.iconBlue : AppColors.grey;
    final a = live.plotted[plotKey(p)];
    if (a == null) return AppColors.primary;
    return a.isOnline(widget.now) ? AppColors.green : AppColors.grey;
  }

  VoidCallback? _onPlotTap(MapPlot p) {
    final cluster = widget.live?.clusters[plotKey(p)];
    if (p.isDevice && cluster != null && cluster.isNotEmpty && widget.onClusterTap != null) {
      return () => widget.onClusterTap!(p, cluster);
    }
    return widget.onPlotTap == null ? null : () => widget.onPlotTap!(p);
  }

  void _zoom(double by) => _map.move(_map.camera.center, (_map.camera.zoom + by).clamp(-1, 6));
}

class _ZoneLabel extends StatelessWidget {
  const _ZoneLabel({required this.zone, required this.count, required this.trail, this.alerts, this.onTap});

  final MapZone zone;
  final int count;

  /// Live mode: open alerts of the assets in the zone (shows the "3 assets · 1 alert" badge).
  final int? alerts;
  final List<int> trail;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final live = alerts != null;
    final text = live ? zone.label : [zone.label, if (count > 0) '· $count'].join(' ');
    final badge = live
        ? [
            '$count asset${count == 1 ? '' : 's'}',
            if (alerts! > 0) '$alerts alert${alerts == 1 ? '' : 's'}',
          ].join(' · ')
        : null;
    return Semantics(
      button: onTap != null,
      label: live
          ? '${zone.label}, $badge${zone.geofence ? ', geofence' : ''}'
          : '${zone.label}, $count tracked assets${zone.geofence ? ', geofence' : ''}',
      child: GestureDetector(
        onTap: onTap,
        child: ExcludeSemantics(
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
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          text,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: AppText.caption.copyWith(
                            fontSize: 10,
                            color: AppColors.text,
                            fontWeight: count > 0 ? FontWeight.w700 : FontWeight.w500,
                          ),
                        ),
                        if (badge != null)
                          // Count changes fade + scale in.
                          AnimatedSwitcher(
                            duration: const Duration(milliseconds: 350),
                            transitionBuilder: (c, a) => ScaleTransition(
                              scale: a,
                              child: FadeTransition(opacity: a, child: c),
                            ),
                            child: Text(
                              badge,
                              key: ValueKey(badge),
                              maxLines: 1,
                              style: AppText.caption.copyWith(
                                fontSize: 9.5,
                                fontWeight: FontWeight.w700,
                                color: alerts! > 0 ? AppColors.red : (count > 0 ? AppColors.primary : AppColors.grey),
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _PlotDot extends StatelessWidget {
  const _PlotDot({
    super.key,
    required this.plot,
    required this.color,
    required this.selected,
    this.moved = false,
    this.cluster = 0,
    this.onTap,
  });

  final MapPlot plot;
  final Color color;
  final bool selected, moved;

  /// Live assets without a plot near this device (badge).
  final int cluster;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Semantics(
    button: onTap != null,
    label: [
      '${plot.isDevice ? 'Device' : 'Asset'} ${plot.label}',
      if (moved) 'moved',
      if (cluster > 0) '$cluster live nearby',
    ].join(', '),
    child: GestureDetector(
      onTap: onTap,
      child: Center(
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            AnimatedContainer(
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
            if (moved)
              Positioned(
                right: -6,
                top: -6,
                child: Container(
                  padding: const EdgeInsets.all(2),
                  decoration: const BoxDecoration(color: AppColors.orange, shape: BoxShape.circle),
                  child: const Icon(Icons.swap_horiz_rounded, size: 10, color: Colors.white),
                ),
              ),
            if (cluster > 0)
              Positioned(
                right: -8,
                top: -8,
                child: AnimatedSwitcher(
                  duration: const Duration(milliseconds: 350),
                  transitionBuilder: (c, a) => ScaleTransition(scale: a, child: c),
                  child: Container(
                    key: ValueKey(cluster),
                    padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                    decoration: BoxDecoration(color: AppColors.primary, borderRadius: BorderRadius.circular(9)),
                    child: Text(
                      '$cluster',
                      style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.w700),
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

/// Status colours of the live floor plan.
class FloorPlanLegend extends StatelessWidget {
  const FloorPlanLegend({super.key});

  @override
  Widget build(BuildContext context) {
    Widget item(Color c, String label, {bool square = false, IconData? icon}) => Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(
            color: c,
            shape: square ? BoxShape.rectangle : BoxShape.circle,
            borderRadius: square ? BorderRadius.circular(3) : null,
          ),
          child: icon == null ? null : Icon(icon, size: 9, color: Colors.white),
        ),
        const SizedBox(width: 4),
        Text(label, style: AppText.caption.copyWith(fontSize: 11)),
      ],
    );
    return Wrap(
      spacing: 12,
      runSpacing: 6,
      children: [
        item(AppColors.green, 'Asset online'),
        item(AppColors.grey, 'Asset offline'),
        item(AppColors.primary, 'Plotted, no live tag'),
        item(AppColors.orange, 'Moved', icon: Icons.swap_horiz_rounded),
        item(AppColors.iconBlue, 'Device online', square: true),
        item(AppColors.grey, 'Device offline', square: true),
        item(AppColors.primary, 'N = live assets near a device', square: true),
      ],
    );
  }
}

/// "Updated 5 s ago" for the live location updates; ticks every second while visible.
class LiveUpdatedAgo extends ConsumerStatefulWidget {
  const LiveUpdatedAgo({super.key});

  @override
  ConsumerState<LiveUpdatedAgo> createState() => _LiveUpdatedAgoState();
}

class _LiveUpdatedAgoState extends ConsumerState<LiveUpdatedAgo> {
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _tick = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted && TickerMode.of(context)) setState(() {});
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final s = ref.watch(liveLocProvider);
    final at = s.fetchedAt;
    final secs = at == null ? null : DateTime.now().toUtc().difference(at).inSeconds.clamp(0, 1 << 30);
    final text = secs == null
        ? 'Loading live locations…'
        : 'Updated ${secs < 60 ? '$secs s' : '${secs ~/ 60} min'} ago';
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(color: AppColors.tileLavender, borderRadius: BorderRadius.circular(8)),
      child: Row(
        children: [
          Icon(Icons.history_rounded, size: 16, color: AppColors.primary),
          const SizedBox(width: 6),
          Expanded(
            child: Text(text, style: AppText.caption.copyWith(color: AppColors.text)),
          ),
          if (s.error != null)
            InkWell(
              onTap: () => ref.read(liveLocProvider.notifier).refresh(),
              child: Text(
                'Retry',
                style: AppText.caption.copyWith(color: AppColors.red, fontWeight: FontWeight.w600),
              ),
            )
          else
            Text(
              s.polling ? 'Live · every 10 s' : 'Paused',
              style: AppText.caption.copyWith(
                color: s.polling ? AppColors.primary : AppColors.grey,
                fontWeight: FontWeight.w600,
              ),
            ),
        ],
      ),
    );
  }
}
