import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart' show PlanImage;
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart' show frameFor;
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_animated_markers.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_ui.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart' show hexColor;
import 'package:asset360_mobile/features/more/tracking/tracking_map.dart' show MapRoundButton;

/// Which layers the Real-Time Location map draws.
typedef RtlLayers = ({bool zones, bool gateways, bool assets});

const rtlAllLayers = (zones: true, gateways: true, assets: true);

/// The Real-Time Location floor plan (flutter_map `CrsSimple`, plan % → map points via
/// [PlanFrame], like the Live Map's FloorPlanMap): the drawing, zones read-only (restricted ones
/// red), gateways at their device plots, and each asset at the plot of the gateway that hears it
/// strongest — fanned out round it, or a count bubble when more than [rtlFanLimit] share it. An asset with a zone
/// on this plan sits at its ZONE SPOT instead (the web's stable zone grid: zone presence, not a measured
/// position). Exact positions are never invented. A marker glides 1.2 s when its point changes, and a Moving
/// asset that changed zone leaves a fading trail ([RtlAnimatedMarkerLayer]).
class RtlFloorMap extends StatefulWidget {
  const RtlFloorMap({
    super.key,
    required this.plan,
    required this.image,
    required this.zones,
    required this.placement,
    required this.overview,
    this.assets = const [],
    this.preview = false,
    this.layers = rtlAllLayers,
    this.selectedAssetId,
    this.focusGatewayId,
    this.focusNonce = 0,
    this.onAssetTap,
    this.onClusterTap,
    this.onGatewayTap,
    this.onLayers,
  });

  final MapFloorPlan plan;
  final PlanImage? image;
  final List<MapZone> zones;
  final RtlPlacement placement;
  final RtlOverview? overview;

  /// The assets of the overview (those with a zone spot in [placement] are drawn in their zone).
  final List<RtlAsset> assets;

  /// A small read-only picture (Home's Asset Location card): no gestures, no buttons.
  final bool preview;
  final RtlLayers layers;

  /// The asset with the callout under its marker.
  final String? selectedAssetId;

  /// Zoom to this gateway's plot (where the focused asset is drawn) whenever [focusNonce] changes
  /// (Locate target / Show on map); retried until the plots of a newly shown floor are loaded.
  final String? focusGatewayId;
  final int focusNonce;
  final ValueChanged<RtlAsset>? onAssetTap;
  final void Function(MapPlot gateway, List<RtlAsset> assets)? onClusterTap;
  final ValueChanged<MapPlot>? onGatewayTap;
  final VoidCallback? onLayers;

  @override
  State<RtlFloorMap> createState() => _RtlFloorMapState();
}

class _RtlFloorMapState extends State<RtlFloorMap> {
  final _map = MapController();
  bool _ready = false;
  int _focused = 0;

  PlanFrame get _frame => frameFor(widget.plan, widget.image);

  @override
  void didUpdateWidget(RtlFloorMap old) {
    super.didUpdateWidget(old);
    _focus();
  }

  void _focus() {
    final id = widget.focusGatewayId?.trim().toLowerCase();
    if (id == null || id.isEmpty || !_ready || widget.focusNonce == _focused) return;
    final plot = widget.placement.gatewayPlots.values
        .where((p) => p.deviceKeys.contains(id) || p.itemId.toLowerCase() == id)
        .firstOrNull;
    if (plot == null) return;
    _focused = widget.focusNonce;
    _map.move(_frame.toLatLng(plot.x, plot.y), 3);
  }

  @override
  void dispose() {
    _map.dispose();
    super.dispose();
  }

  void _fit() {
    final f = _frame;
    _map.fitCamera(CameraFit.bounds(bounds: LatLngBounds(f.topLeft, f.bottomRight), padding: const EdgeInsets.all(12)));
  }

  void _zoom(double by) => _map.move(_map.camera.center, (_map.camera.zoom + by).clamp(-1, 6));

  @override
  Widget build(BuildContext context) {
    final f = _frame;
    final bounds = LatLngBounds(f.topLeft, f.bottomRight);
    final image = widget.image;
    final zones = [for (final z in widget.zones) if (z.active) z];
    final gateways = widget.placement.gatewayPlots.values.toList();
    final markers = <Marker>[];
    final assetMarkers = <RtlMarkerSpec>[];
    if (widget.layers.gateways) {
      for (final g in gateways) {
        final gw = widget.overview?.gateway(g.itemId) ?? widget.overview?.gateway(g.code);
        markers.add(
          Marker(
            point: f.toLatLng(g.x, g.y),
            width: 96,
            height: 46,
            child: _GatewayMark(
              key: ValueKey('rtl-gw-${g.itemId}'),
              plot: g,
              color: gatewayColor(gatewayStateOf(gw)),
              onTap: widget.onGatewayTap == null ? null : () => widget.onGatewayTap!(g),
            ),
          ),
        );
      }
    }
    if (widget.layers.assets) {
      // Assets inside a zone: one stable spot each (the web's zone grid); they glide to the new spot on a zone change.
      for (final a in widget.assets) {
        final spot = widget.placement.zoneSpots[a.assetId];
        if (spot == null) continue;
        assetMarkers.add(
          RtlMarkerSpec(
            id: a.assetId,
            point: spot,
            width: 120,
            height: 120,
            zoneKey: a.location?.zoneId?.toLowerCase(),
            moving: a.movement == 'Moving',
            child: _FannedAsset(
              key: ValueKey('rtl-asset-${a.assetId}'),
              asset: a,
              dx: 0,
              dy: 0,
              selected: a.assetId == widget.selectedAssetId,
              onTap: widget.onAssetTap == null ? null : () => widget.onAssetTap!(a),
            ),
          ),
        );
      }
      for (final e in widget.placement.byGateway.entries) {
        final g = widget.placement.gatewayPlots[e.key];
        if (g == null) continue;
        final point = f.toLatLng(g.x, g.y);
        final list = e.value;
        if (list.length > rtlFanLimit) {
          markers.add(
            Marker(
              point: point,
              width: 44,
              height: 44,
              child: _CountBubble(
                key: ValueKey('rtl-cluster-${g.itemId}'),
                count: list.length,
                alerts: list.any((a) => a.activeLocationAlerts > 0),
                onTap: () => widget.onClusterTap?.call(g, list),
              ),
            ),
          );
          continue;
        }
        final offsets = fanOffsets(list.length);
        for (final (i, a) in list.indexed) {
          final o = offsets[i];
          final selected = a.assetId == widget.selectedAssetId;
          // The newest live row's x / y (else the gateway's plot), same plan → map conversion as the web.
          final at = widget.placement.pointOf(a) ?? (x: g.x, y: g.y);
          assetMarkers.add(
            RtlMarkerSpec(
              id: a.assetId,
              point: f.toLatLng(at.x, at.y),
              width: 120,
              height: 120,
              child: _FannedAsset(
                key: ValueKey('rtl-asset-${a.assetId}'),
                asset: a,
                dx: o.dx,
                dy: o.dy,
                selected: selected,
                onTap: widget.onAssetTap == null ? null : () => widget.onAssetTap!(a),
              ),
            ),
          );
        }
      }
    }
    return ClipRRect(
      borderRadius: BorderRadius.circular(14),
      child: Stack(
        children: [
          FlutterMap(
            mapController: _map,
            options: MapOptions(
              crs: const CrsSimple(),
              backgroundColor: AppColors.surface,
              initialCenter: f.toLatLng(50, 50),
              initialZoom: 0,
              initialCameraFit: CameraFit.bounds(bounds: bounds, padding: const EdgeInsets.all(12)),
              cameraConstraint: CameraConstraint.containCenter(bounds: bounds),
              minZoom: -1,
              maxZoom: 6,
              interactionOptions: InteractionOptions(
                flags: widget.preview ? InteractiveFlag.none : InteractiveFlag.all & ~InteractiveFlag.rotate,
              ),
              onMapReady: () {
                _ready = true;
                _focus();
              },
            ),
            children: [
              if (image != null)
                OverlayImageLayer(
                  overlayImages: [OverlayImage(bounds: bounds, imageProvider: MemoryImage(image.bytes))],
                ),
              if (widget.layers.zones) ...[
                PolygonLayer(
                  polygons: [
                    for (final z in zones)
                      () {
                        final color = isRestrictedZone(z) ? rtlRestrictedColor : hexColor(z.color, AppColors.primary);
                        return Polygon(
                          points: f.zoneOutline(z),
                          color: color.withValues(alpha: isRestrictedZone(z) ? .2 : .16),
                          borderColor: color,
                          borderStrokeWidth: isRestrictedZone(z) ? 2.2 : 1.4,
                          pattern: isRestrictedZone(z)
                              ? StrokePattern.dashed(segments: const [8, 5])
                              : const StrokePattern.solid(),
                        );
                      }(),
                  ],
                ),
                MarkerLayer(
                  markers: [
                    for (final z in zones)
                      Marker(
                        point: f.point(z.center),
                        width: 140,
                        height: isRestrictedZone(z) ? 48 : 32,
                        child: IgnorePointer(child: _ZoneTag(zone: z)),
                      ),
                  ],
                ),
              ],
              MarkerLayer(markers: markers),
              RtlAnimatedMarkerLayer(markers: assetMarkers),
            ],
          ),
          if (!widget.preview) ...[
          Positioned(
            right: 8,
            top: 8,
            child: Column(
              children: [
                if (widget.onLayers != null) ...[
                  MapRoundButton(icon: Icons.layers_outlined, tooltip: 'Layers', onTap: widget.onLayers!),
                  const SizedBox(height: 8),
                ],
                MapRoundButton(icon: Icons.fit_screen_rounded, tooltip: 'Fit plan', onTap: _fit),
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
        ],
      ),
    );
  }
}

class _ZoneTag extends StatelessWidget {
  const _ZoneTag({required this.zone});

  final MapZone zone;

  @override
  Widget build(BuildContext context) {
    final restricted = isRestrictedZone(zone);
    return Center(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: .9),
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: restricted ? rtlRestrictedColor : AppColors.border),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              zone.label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppText.caption.copyWith(fontSize: 10, color: AppColors.text, fontWeight: FontWeight.w600),
            ),
            if (restricted)
              Text(
                'Restricted Zone',
                style: AppText.caption.copyWith(fontSize: 9, color: rtlRestrictedColor, fontWeight: FontWeight.w700),
              ),
          ],
        ),
      ),
    );
  }
}

class _GatewayMark extends StatelessWidget {
  const _GatewayMark({super.key, required this.plot, required this.color, this.onTap});

  final MapPlot plot;
  final Color color;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Semantics(
    button: onTap != null,
    label: 'Gateway ${plot.label}',
    child: GestureDetector(
      onTap: onTap,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 24,
            height: 24,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(6),
              border: Border.all(color: color, width: 2),
            ),
            child: Icon(Icons.router_rounded, size: 14, color: color),
          ),
          const SizedBox(height: 2),
          Text(
            plot.label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppText.caption.copyWith(fontSize: 9.5, color: AppColors.text, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    ),
  );
}

/// One asset dot, shifted by ([dx], [dy]) from its gateway; the selected one shows the callout
/// (assetTag, type, Moving / Stationary) under it.
class _FannedAsset extends StatelessWidget {
  const _FannedAsset({
    super.key,
    required this.asset,
    required this.dx,
    required this.dy,
    required this.selected,
    this.onTap,
  });

  final RtlAsset asset;
  final double dx, dy;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final size = selected ? 26.0 : 20.0;
    final color = rtlStatusColor(rtlStatusOf(asset));
    // The marker box is 120 × 120 with the gateway point at its centre.
    final left = 60 + dx - size / 2, top = 60 + dy - size / 2;
    return Stack(
      clipBehavior: Clip.none,
      children: [
        Positioned(
          left: left,
          top: top,
          child: Semantics(
            button: onTap != null,
            selected: selected,
            label: 'Asset ${asset.tagLabel}, ${rtlStatusOf(asset).label}',
            child: GestureDetector(
              onTap: onTap,
              child: Stack(
                clipBehavior: Clip.none,
                children: [
                  AnimatedContainer(
                    duration: const Duration(milliseconds: 200),
                    width: size,
                    height: size,
                    decoration: BoxDecoration(
                      color: color,
                      shape: BoxShape.circle,
                      border: Border.all(color: Colors.white, width: selected ? 3 : 2),
                      boxShadow: [
                        BoxShadow(
                          color: (selected ? AppColors.primary : AppColors.text).withValues(alpha: selected ? .5 : .3),
                          blurRadius: selected ? 8 : 4,
                        ),
                      ],
                    ),
                    child: Icon(Icons.inventory_2_rounded, size: size * .5, color: Colors.white),
                  ),
                  if (asset.activeLocationAlerts > 0)
                    const Positioned(
                      right: -7,
                      top: -7,
                      child: Icon(Icons.warning_rounded, size: 14, color: rtlAlertColor),
                    ),
                ],
              ),
            ),
          ),
        ),
        if (selected)
          Positioned(
            left: 60 + dx - 60,
            top: top + size + 3,
            width: 120,
            child: IgnorePointer(
              child: Center(
                child: Container(
                  key: const ValueKey('rtl-callout'),
                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(6),
                    boxShadow: [BoxShadow(color: AppColors.text.withValues(alpha: .25), blurRadius: 4)],
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        asset.tagLabel,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppText.caption.copyWith(fontSize: 10, fontWeight: FontWeight.w700, color: AppColors.text),
                      ),
                      Text(
                        [asset.assetType, rtlMovementLabel(asset)].nonNulls.join(' · '),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppText.caption.copyWith(fontSize: 9),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class _CountBubble extends StatelessWidget {
  const _CountBubble({super.key, required this.count, required this.alerts, this.onTap});

  final int count;
  final bool alerts;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Semantics(
    button: true,
    label: '$count assets at this gateway',
    child: GestureDetector(
      onTap: onTap,
      child: Center(
        child: Container(
          width: 34,
          height: 34,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: AppColors.primary,
            shape: BoxShape.circle,
            border: Border.all(color: alerts ? rtlAlertColor : Colors.white, width: 2.5),
          ),
          child: Text(
            '$count',
            style: const TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.w700),
          ),
        ),
      ),
    ),
  );
}
