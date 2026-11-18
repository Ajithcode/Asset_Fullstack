import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/platform/app_launcher.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_models.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/home/dash_map.dart' show DashAssetMap;
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';
import 'package:asset360_mobile/features/more/tracking/tracking_map.dart' show MapRoundButton;

/// A Master Data place with coordinates: a site or a building.
class GeoPlace {
  const GeoPlace({
    required this.id,
    required this.name,
    required this.point,
    required this.isSite,
    this.siteId,
    this.tracked = 0,
    this.isFloor = false,
    this.buildingId,
  });

  final String id, name;
  final LatLng point;
  final bool isSite;

  /// A floor pin (Master Data floor with coordinates); [buildingId] = its building.
  final bool isFloor;
  final String? buildingId;

  /// The building's site.
  final String? siteId;

  /// Tracked assets at this site (Tracking device / tag `siteId` or `siteName`); buildings 0 —
  /// the Tracking service names no building.
  final int tracked;
}

/// Sites, buildings and floors with latitude / longitude (both set and not 0, 0). Badges: the
/// LIVE asset count of each place when [liveCount] is given (location tree), else sites count the
/// Tracking service's assets at the site.
List<GeoPlace> geoPlaces(
  List<MdSite> sites,
  List<MdSpace> buildings,
  List<TrackedAsset> assets, {
  List<MdSpace> floors = const [],
  int Function(String id)? liveCount,
}) {
  bool ok(double? lat, double? lng) => lat != null && lng != null && !(lat == 0 && lng == 0);
  int trackedAt(MdSite s) => assets
      .where(
        (a) =>
            (a.siteId != null && a.siteId == s.id) ||
            (a.siteName != null && a.siteName!.toLowerCase() == s.name.toLowerCase()),
      )
      .length;
  return [
    for (final s in sites)
      if (ok(s.latitude, s.longitude))
        GeoPlace(
          id: s.id,
          name: s.name,
          point: LatLng(s.latitude!, s.longitude!),
          isSite: true,
          tracked: liveCount?.call(s.id) ?? trackedAt(s),
        ),
    for (final b in buildings)
      if (ok(b.latitude, b.longitude))
        GeoPlace(
          id: b.id,
          name: b.name,
          point: LatLng(b.latitude!, b.longitude!),
          isSite: false,
          siteId: b.site?.id,
          tracked: liveCount?.call(b.id) ?? 0,
        ),
    for (final f in floors)
      if (ok(f.latitude, f.longitude))
        GeoPlace(
          id: f.id,
          name: f.name,
          point: LatLng(f.latitude!, f.longitude!),
          isSite: false,
          isFloor: true,
          siteId: f.site?.id,
          buildingId: f.buildingId,
          tracked: liveCount?.call(f.id) ?? 0,
        ),
  ];
}

/// The outline of a GPS geofence: a polygon's points; a rectangle from its 4 corners, or from 2
/// opposite corners when only 2 are stored. Circles are drawn by [CircleLayer] instead.
List<LatLng> geofenceOutline(TrkGeofence g) {
  final pts = [for (final c in g.coordinates) LatLng(c.latitude, c.longitude)];
  if (g.shape.toLowerCase() == 'rectangle' && pts.length == 2) {
    final a = pts[0], b = pts[1];
    return [a, LatLng(a.latitude, b.longitude), b, LatLng(b.latitude, a.longitude)];
  }
  return pts;
}

/// OpenStreetMap view of the Live Map: site / building pins (Master Data coordinates, sites with
/// a tracked-asset count badge), and the Tracking service's GPS geofences. There are no asset GPS
/// positions on the server, so no asset dots are drawn here.
class OutdoorMap extends StatefulWidget {
  const OutdoorMap({
    super.key,
    required this.places,
    required this.geofences,
    required this.showGeofences,
    this.focusGeofenceId,
    this.onPlaceTap,
    this.onGeofenceTap,
    this.onLayers,
  });

  final List<GeoPlace> places;
  final List<TrkGeofence> geofences;
  final bool showGeofences;
  final String? focusGeofenceId;
  final ValueChanged<GeoPlace>? onPlaceTap;
  final ValueChanged<TrkGeofence>? onGeofenceTap;
  final VoidCallback? onLayers;

  @override
  State<OutdoorMap> createState() => _OutdoorMapState();
}

class _OutdoorMapState extends State<OutdoorMap> {
  final _map = MapController();

  List<LatLng> get _focusPoints {
    final g = widget.geofences.where((g) => g.id == widget.focusGeofenceId).firstOrNull;
    if (g != null && g.coordinates.isNotEmpty) return geofenceOutline(g);
    return [
      for (final p in widget.places) p.point,
      if (widget.showGeofences)
        for (final g in widget.geofences) ...geofenceOutline(g),
    ];
  }

  CameraFit? _fit(List<LatLng> pts) =>
      pts.length > 1 ? CameraFit.coordinates(coordinates: pts, padding: const EdgeInsets.all(40), maxZoom: 17) : null;

  @override
  void didUpdateWidget(OutdoorMap old) {
    super.didUpdateWidget(old);
    if (old.focusGeofenceId != widget.focusGeofenceId) _recenter();
  }

  void _recenter() {
    final pts = _focusPoints;
    final fit = _fit(pts);
    if (fit != null) {
      _map.fitCamera(fit);
    } else if (pts.isNotEmpty) {
      _map.move(pts.first, 16);
    }
  }

  @override
  void dispose() {
    _map.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final pts = _focusPoints;
    final fences = widget.showGeofences
        ? widget.geofences.where((g) => g.coordinates.isNotEmpty).toList()
        : <TrkGeofence>[];
    return ClipRRect(
      borderRadius: BorderRadius.circular(14),
      child: Stack(
        children: [
          FlutterMap(
            mapController: _map,
            options: MapOptions(
              initialCenter: pts.isEmpty ? const LatLng(25.2048, 55.2708) : pts.first,
              initialZoom: pts.length == 1 ? 16 : 11,
              initialCameraFit: _fit(pts),
              interactionOptions: const InteractionOptions(flags: InteractiveFlag.all & ~InteractiveFlag.rotate),
            ),
            children: [
              TileLayer(urlTemplate: DashAssetMap.tileUrl, userAgentPackageName: DashAssetMap.userAgent),
              PolygonLayer(
                polygons: [
                  for (final g in fences)
                    if (!g.isCircle)
                      Polygon(
                        points: geofenceOutline(g),
                        color: hexColor(
                          g.mapColour,
                          AppColors.primary,
                        ).withValues(alpha: g.id == widget.focusGeofenceId ? .35 : .18),
                        borderColor: hexColor(g.mapColour, AppColors.primary),
                        borderStrokeWidth: g.id == widget.focusGeofenceId ? 3 : 2,
                        pattern: g.isActive
                            ? const StrokePattern.solid()
                            : StrokePattern.dashed(segments: const [6, 4]),
                      ),
                ],
              ),
              CircleLayer(
                circles: [
                  for (final g in fences)
                    if (g.isCircle && (g.radiusMeters ?? 0) > 0)
                      CircleMarker(
                        point: LatLng(g.coordinates.first.latitude, g.coordinates.first.longitude),
                        radius: g.radiusMeters!,
                        useRadiusInMeter: true,
                        color: hexColor(
                          g.mapColour,
                          AppColors.primary,
                        ).withValues(alpha: g.id == widget.focusGeofenceId ? .35 : .18),
                        borderColor: hexColor(g.mapColour, AppColors.primary),
                        borderStrokeWidth: 2,
                      ),
                ],
              ),
              MarkerLayer(
                markers: [
                  for (final g in fences)
                    Marker(
                      point: _labelPoint(g),
                      width: 130,
                      height: 26,
                      child: _FenceLabel(
                        fence: g,
                        onTap: widget.onGeofenceTap == null ? null : () => widget.onGeofenceTap!(g),
                      ),
                    ),
                  for (final p in widget.places)
                    Marker(
                      point: p.point,
                      width: 44,
                      height: 48,
                      alignment: Alignment.topCenter,
                      child: _PlacePin(place: p, onTap: widget.onPlaceTap == null ? null : () => widget.onPlaceTap!(p)),
                    ),
                ],
              ),
              RichAttributionWidget(
                attributions: [
                  TextSourceAttribution(
                    'OpenStreetMap contributors',
                    onTap: () => AppLauncher.openUrl('https://openstreetmap.org/copyright'),
                  ),
                ],
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
                MapRoundButton(icon: Icons.my_location_rounded, tooltip: 'Recenter', onTap: _recenter),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static LatLng _labelPoint(TrkGeofence g) {
    final pts = geofenceOutline(g);
    if (g.isCircle || pts.length == 1) return pts.first;
    final lat = pts.map((p) => p.latitude).reduce((a, b) => a + b) / pts.length;
    final lng = pts.map((p) => p.longitude).reduce((a, b) => a + b) / pts.length;
    return LatLng(lat, lng);
  }
}

class _PlacePin extends StatelessWidget {
  const _PlacePin({required this.place, this.onTap});

  final GeoPlace place;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Semantics(
    button: onTap != null,
    label:
        '${place.isSite ? 'Site' : (place.isFloor ? 'Floor' : 'Building')} ${place.name}${place.tracked > 0 ? ', ${place.tracked} tracked' : ''}',
    child: GestureDetector(
      onTap: onTap,
      // The label above already says the count; the badge text would repeat it.
      child: ExcludeSemantics(
        child: Stack(
          clipBehavior: Clip.none,
          alignment: Alignment.topCenter,
          children: [
            Icon(
              place.isSite
                  ? Icons.location_city_rounded
                  : (place.isFloor ? Icons.layers_rounded : Icons.apartment_rounded),
              size: place.isSite ? 38 : 30,
              color: place.isSite ? AppColors.primary : AppColors.iconBlue,
              shadows: [Shadow(color: AppColors.text.withValues(alpha: .35), blurRadius: 4)],
            ),
            if (place.tracked > 0)
              Positioned(
                right: -2,
                top: -4,
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                  decoration: BoxDecoration(color: AppColors.green, borderRadius: BorderRadius.circular(9)),
                  child: Text(
                    '${place.tracked}',
                    style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.w700),
                  ),
                ),
              ),
          ],
        ),
      ),
    ),
  );
}

class _FenceLabel extends StatelessWidget {
  const _FenceLabel({required this.fence, this.onTap});

  final TrkGeofence fence;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => GestureDetector(
    onTap: onTap,
    child: Center(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: .9),
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: hexColor(fence.mapColour, AppColors.primary)),
        ),
        child: Text(
          fence.name,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: AppText.caption.copyWith(fontSize: 10, color: AppColors.text, fontWeight: FontWeight.w600),
        ),
      ),
    ),
  );
}
