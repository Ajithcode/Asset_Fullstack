import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/repositories/asset_map_repository.dart';
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/home/dash_ui.dart' show DashStatusChip, dashTimeAgo;
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_location_tree.dart';
import 'package:asset360_mobile/features/more/tracking/live/outdoor_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

enum LiveMapView { outdoor, floor }

/// A Site → Building → Floor choice. [locationId] = the deepest chosen (a published plan can
/// belong to a site, a building or a floor).
class PlanLocation {
  const PlanLocation({this.siteId, this.buildingId, this.floorId});

  final String? siteId, buildingId, floorId;

  String? get locationId => floorId ?? buildingId ?? siteId;

  /// Resolves a Master Data id (floor, building or site) to its chain.
  static PlanLocation resolve(String id, List<MdSite> sites, List<MdSpace> buildings, List<MdSpace> floors) {
    final f = floors.where((f) => f.id == id).firstOrNull;
    if (f != null) {
      final b = buildings.where((b) => b.id == f.buildingId).firstOrNull;
      return PlanLocation(siteId: f.site?.id ?? b?.site?.id, buildingId: f.buildingId, floorId: f.id);
    }
    final b = buildings.where((b) => b.id == id).firstOrNull;
    if (b != null) return PlanLocation(siteId: b.site?.id, buildingId: b.id);
    return PlanLocation(siteId: sites.any((s) => s.id == id) ? id : null);
  }
}

/// The Live Map tab of the API mode hub: an OUTDOOR map (OpenStreetMap: sites / buildings,
/// GPS geofences) and a FLOOR PLAN map (published plan, zones, plotted assets / devices) sharing
/// [visibleAssets] (the hub's search / counters / filters).
class LiveMapTab extends ConsumerStatefulWidget {
  const LiveMapTab({
    super.key,
    this.initialView,
    this.initialLocation,
    this.focusZone,
    this.focusGeofence,
    required this.visibleAssets,
    this.tree,
    this.view,
    this.onViewChanged,
    this.focusAsset,
    this.focusPlot,
  });

  final String? initialView, initialLocation, focusZone, focusGeofence;

  /// Controlled view (the Live Map / Floor plans toggle above): no Outdoor / Floor plan tabs here,
  /// and a pin that opens a floor asks [onViewChanged].
  final LiveMapView? view;
  final ValueChanged<LiveMapView>? onViewChanged;

  /// View on Map: the asset (registration id) to centre / highlight and its plot key.
  final String? focusAsset, focusPlot;

  /// The live location tree: pin badges and each zone's live count.
  final LocationTree? tree;

  /// Null while the first live round loads.
  final List<TrackedAsset>? visibleAssets;

  @override
  ConsumerState<LiveMapTab> createState() => _LiveMapTabState();
}

class _LiveMapTabState extends ConsumerState<LiveMapTab> {
  late LiveMapView _view =
      (widget.initialView == 'floor' || (widget.initialLocation != null && widget.initialView != 'outdoor'))
      ? LiveMapView.floor
      : LiveMapView.outdoor;
  PlanLocation _loc = const PlanLocation();
  bool _resolved = false;
  MapLayerToggles? _layers;
  late String? _selectedPlot = widget.focusPlot;

  /// The outdoor marker whose card is open (View on Map opens it).
  late String? _geoSelected = widget.focusAsset;

  LiveMapView get _v => widget.view ?? _view;

  void _setView(LiveMapView v) {
    if (widget.view != null) {
      widget.onViewChanged?.call(v);
    } else {
      setState(() => _view = v);
    }
  }

  /// Opens a floor's plan view.
  void _openFloor(PlanLocation loc, {String? plot}) {
    setState(() {
      _loc = loc;
      _selectedPlot = plot;
      if (widget.view == null) _view = LiveMapView.floor;
    });
    if (widget.view != null) widget.onViewChanged?.call(LiveMapView.floor);
  }

  @override
  Widget build(BuildContext context) {
    final sites = ref.watch(mdSitesProvider);
    final buildings = ref.watch(mdBuildingsProvider);
    final floors = ref.watch(mdFloorsProvider);
    if (!_resolved && widget.initialLocation != null && sites.hasValue && buildings.hasValue && floors.hasValue) {
      _resolved = true;
      _loc = PlanLocation.resolve(
        widget.initialLocation!,
        sites.requireValue,
        buildings.requireValue,
        floors.requireValue,
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (widget.view == null) ...[
          SegmentTabs(
            tabs: const [SegmentTab('Outdoor'), SegmentTab('Floor plan')],
            selected: _view.index,
            onChanged: (i) => _setView(LiveMapView.values[i]),
          ),
          const SizedBox(height: 12),
        ],
        if (_v == LiveMapView.outdoor) _outdoor(sites, buildings, floors) else ..._floor(sites, buildings, floors),
        const SizedBox(height: 14),
        const _RecentEvents(),
      ],
    );
  }

  // ---------------------------------------------------------------- outdoor

  Widget _outdoor(
    AsyncValue<List<MdSite>> sites,
    AsyncValue<List<MdSpace>> buildings,
    AsyncValue<List<MdSpace>> floors,
  ) {
    final fences = ref.watch(trkGeofencesProvider);
    if ((sites.hasError && !sites.isLoading) || (buildings.hasError && !buildings.isLoading)) {
      return LoadErrorState(
        error: (sites.error ?? buildings.error)!,
        onRetry: () => ref
          ..invalidate(mdSitesProvider)
          ..invalidate(mdBuildingsProvider),
      );
    }
    final geoV = ref.watch(geoAssetsProvider);
    if (!sites.hasValue ||
        !buildings.hasValue ||
        !fences.hasValue && !fences.hasError ||
        geoV.isLoading && !geoV.hasValue) {
      return const SizedBox(height: 300, child: ListSkeleton(rows: 3, card: false));
    }
    final geo = geoV.value ?? const GeoAssets();
    final selectedGeo = _geoSelected == null ? null : geo.byId(_geoSelected!);
    final tree = widget.tree;
    final places = geoPlaces(
      sites.requireValue,
      buildings.requireValue,
      widget.visibleAssets ?? const [],
      floors: floors.value ?? const [],
      liveCount: tree == null ? null : (id) => tree.find(id)?.allAssets.length ?? 0,
    );
    final geofences = fences.value ?? const <TrkGeofence>[];
    final layers = _layers ?? const MapLayerToggles();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SizedBox(
          height: 300,
          child: places.isEmpty && geofences.isEmpty && geo.assets.isEmpty
              ? const AppCard(
                  child: EmptyState(
                    icon: Icons.public_off_rounded,
                    title: 'No mapped locations',
                    message: 'No asset, site, building or floor has latitude / longitude yet.',
                  ),
                )
              : OutdoorMap(
                  places: places,
                  assets: geo.assets,
                  center: geo.center,
                  selectedAssetId: _geoSelected,
                  focusAssetId: widget.focusAsset,
                  onAssetTap: (a) => setState(
                    () => _geoSelected = _geoSelected == a.assetRegistrationId ? null : a.assetRegistrationId,
                  ),
                  geofences: geofences,
                  showGeofences: layers.geofences,
                  focusGeofenceId: widget.focusGeofence,
                  onPlaceTap: (p) => _openPlace(p, buildings.requireValue, floors.value ?? const []),
                  onGeofenceTap: (g) => _showGeofence(context, g),
                  onLayers: () => _editLayers(layers),
                ),
        ),
        if (selectedGeo != null) ...[
          const SizedBox(height: 10),
          _GeoAssetCard(
            asset: selectedGeo,
            floorPlan: _floorPlanOf(selectedGeo.assetRegistrationId),
            onFloorPlan: _openFloor,
          ),
        ],
        if (geoV.hasError) ...[
          const SizedBox(height: 6),
          InlineRetry(
            text: 'Asset coordinates could not be loaded',
            error: true,
            onRetry: () => ref.invalidate(geoAssetsProvider),
          ),
        ],
        if (fences.hasError) ...[
          const SizedBox(height: 6),
          InlineRetry(
            text: 'GPS geofences could not be loaded',
            error: true,
            onRetry: () => ref.invalidate(trkGeofencesProvider),
          ),
        ],
        const SizedBox(height: 8),
        const LiveUpdatedBar(),
        const SizedBox(height: 8),
        const _InfoNote(
          'Asset pins use the coordinates the Dashboard and Asset services report (as on Home → Asset '
          'Location); tags without coordinates are never drawn at a guessed spot. Site badges count the live '
          'assets placed at each site; tap a site, or an asset\'s "Floor plan", to open its floor plan.',
        ),
      ],
    );
  }

  Future<void> _openPlace(GeoPlace p, List<MdSpace> buildings, List<MdSpace> floors) async {
    if (p.isFloor) {
      _openFloor(PlanLocation(siteId: p.siteId, buildingId: p.buildingId, floorId: p.id));
      return;
    }
    final chosen = await showModalBottomSheet<PlanLocation>(
      context: context,
      isScrollControlled: true,
      builder: (context) {
        final siteBuildings = p.isSite ? buildings.where((b) => b.site?.id == p.id).toList() : <MdSpace>[];
        List<MdSpace> floorsOf(String buildingId) => floors.where((f) => f.buildingId == buildingId).toList();
        return sheetFrame(context, [
          SheetTitle(p.name, subtitle: p.isSite ? 'Site · ${p.tracked} tracked assets' : 'Building'),
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.map_outlined),
            title: Text(p.isSite ? 'Site map' : 'Building plan', style: AppText.bodyStrong),
            subtitle: const Text('Published plan of this location, if any'),
            onTap: () => Navigator.pop(
              context,
              p.isSite ? PlanLocation(siteId: p.id) : PlanLocation(siteId: p.siteId, buildingId: p.id),
            ),
          ),
          if (p.isSite && siteBuildings.isEmpty) const Text('No buildings in Master Data for this site.'),
          for (final b in p.isSite ? siteBuildings : buildings.where((b) => b.id == p.id)) ...[
            if (p.isSite)
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Text(b.name, style: AppText.sectionTitle),
              ),
            if (floorsOf(b.id).isEmpty) Text('No floors', style: AppText.caption),
            for (final f in floorsOf(b.id))
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.layers_outlined),
                title: Text(f.name),
                trailing: const Icon(Icons.chevron_right_rounded),
                onTap: () => Navigator.pop(context, PlanLocation(siteId: b.site?.id, buildingId: b.id, floorId: f.id)),
              ),
          ],
        ]);
      },
    );
    if (chosen == null || !mounted) return;
    _openFloor(chosen);
  }

  /// The floor plan an outdoor asset can open: its live zone's plan (tag → plotted device), else a
  /// plan where it is plotted. Null = none.
  ({PlanLocation loc, String? plot})? _floorPlanOf(String assetId) {
    final id = assetId.toLowerCase();
    final plans = ref.watch(planDataProvider).value ?? const <PlanData>[];
    final floors = ref.watch(mdFloorsProvider).value ?? const <MdSpace>[];
    PlanLocation locOf(String floorId) {
      final f = floors.where((f) => f.id == floorId).firstOrNull;
      return PlanLocation(siteId: f?.site?.id, buildingId: f?.buildingId, floorId: floorId);
    }

    final live = widget.tree?.all.where((a) => a.assetRegistrationId?.toLowerCase() == id && a.near != null);
    final near = live?.firstOrNull?.near;
    if (near != null) {
      final plot = plans
          .where((d) => d.plan.floorPlanId == near.plan.floorPlanId)
          .expand((d) => d.plots)
          .where((p) => p.assetId?.toLowerCase() == id)
          .firstOrNull;
      return (loc: locOf(near.plan.locationId), plot: plot == null ? null : plotKey(plot));
    }
    for (final d in plans) {
      final plot = d.plots.where((p) => p.assetId?.toLowerCase() == id).firstOrNull;
      if (plot != null) return (loc: locOf(d.plan.locationId), plot: plotKey(plot));
    }
    return null;
  }

  // ---------------------------------------------------------------- floor plan

  List<Widget> _floor(
    AsyncValue<List<MdSite>> sites,
    AsyncValue<List<MdSpace>> buildings,
    AsyncValue<List<MdSpace>> floors,
  ) {
    final s = sites.value ?? const <MdSite>[];
    final b = buildings.value ?? const <MdSpace>[];
    final f = floors.value ?? const <MdSpace>[];
    final siteBuildings = b.where((x) => x.site?.id == _loc.siteId).toList();
    final buildingFloors = f.where((x) => x.buildingId == _loc.buildingId).toList();
    String nameOf(String? id, List<MdEntry> list) => list.where((e) => e.id == id).firstOrNull?.name ?? '';
    final pickers = Row(
      children: [
        Expanded(
          child: _PickField(
            label: 'Site',
            value: nameOf(_loc.siteId, s),
            loading: !sites.hasValue,
            onTap: () => _pick('Site', s, (id) => PlanLocation(siteId: id)),
          ),
        ),
        const SizedBox(width: 6),
        Expanded(
          child: _PickField(
            label: 'Building',
            value: nameOf(_loc.buildingId, b),
            enabled: _loc.siteId != null,
            onTap: () => _pick('Building', siteBuildings, (id) => PlanLocation(siteId: _loc.siteId, buildingId: id)),
          ),
        ),
        const SizedBox(width: 6),
        Expanded(
          child: _PickField(
            label: 'Floor',
            value: nameOf(_loc.floorId, f),
            enabled: _loc.buildingId != null,
            onTap: () => _pick(
              'Floor',
              buildingFloors,
              withPlan: {
                for (final p in ref.read(visibleFloorPlansProvider).value ?? const <MapFloorPlan>[]) p.locationId,
              },
              (id) => PlanLocation(siteId: _loc.siteId, buildingId: _loc.buildingId, floorId: id),
            ),
          ),
        ),
      ],
    );
    final id = _loc.locationId;
    if ((sites.hasError && !sites.isLoading)) {
      return [LoadErrorState(error: sites.error!, onRetry: () => ref.invalidate(mdSitesProvider))];
    }
    return [
      pickers,
      const SizedBox(height: 10),
      if (id == null)
        const AppCard(
          child: EmptyState(
            icon: Icons.layers_outlined,
            title: 'Choose a site, building and floor',
            message: 'Floors with a map icon have a floor plan: it opens here with zones and live assets.',
          ),
        )
      else
        _PlanView(
          key: ValueKey(id),
          locationId: id,
          focusZone: widget.focusZone,
          visibleAssets: widget.visibleAssets,
          floor: f.where((x) => x.id == id).firstOrNull,
          liveNode: widget.tree?.find(id),
          tree: widget.tree,
          focusAsset: widget.focusAsset,
          layers: _layers,
          selectedPlot: _selectedPlot,
          onSelectPlot: (k) => setState(() => _selectedPlot = _selectedPlot == k ? null : k),
          onLayers: _editLayers,
        ),
    ];
  }

  /// [withPlan] = ids that have a floor plan (map icon).
  Future<void> _pick(
    String what,
    List<MdEntry> options,
    PlanLocation Function(String id) apply, {
    Set<String> withPlan = const {},
  }) async {
    final id = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (context) => sheetFrame(context, [
        SheetTitle('Choose $what'),
        if (options.isEmpty) Text('No ${what.toLowerCase()}s in Master Data here.', style: AppText.secondary),
        for (final o in options)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(o.name),
            subtitle: o.code.isEmpty ? null : Text(o.code),
            trailing: withPlan.contains(o.id)
                ? Tooltip(
                    message: 'Has a floor plan',
                    child: Icon(Icons.map_rounded, color: AppColors.primary),
                  )
                : null,
            onTap: () => Navigator.pop(context, o.id),
          ),
      ]),
    );
    if (id == null || !mounted) return;
    setState(() {
      _loc = apply(id);
      _selectedPlot = null;
    });
  }

  Future<void> _editLayers(MapLayerToggles current) async {
    final next = await showLiveLayersSheet(context, current);
    if (next != null && mounted) setState(() => _layers = next);
  }
}

/// One floor plan (Published; Draft too in developer builds): the drawing at its real pixel size,
/// zones with their LIVE counts, plotted devices / assets with live status, live assets without
/// a plot clustered on the device that heard them (KEY RULE), the legend and the live zone list.
class _PlanView extends ConsumerWidget {
  const _PlanView({
    super.key,
    required this.locationId,
    required this.focusZone,
    required this.visibleAssets,
    required this.layers,
    required this.selectedPlot,
    required this.onSelectPlot,
    required this.onLayers,
    this.floor,
    this.liveNode,
    this.tree,
    this.focusAsset,
  });

  /// View on Map: the asset whose plot / live zone the plan zooms to.
  final String? focusAsset;

  /// The Master Data floor (its picture when no plan exists).
  final MdSpace? floor;

  /// This location in the live tree (zone live counts).
  final LocNode? liveNode;

  /// The whole live tree (live assets heard by this plan's devices).
  final LocationTree? tree;

  final String locationId;
  final String? focusZone, selectedPlot;
  final List<TrackedAsset>? visibleAssets;
  final MapLayerToggles? layers;
  final ValueChanged<String> onSelectPlot;
  final ValueChanged<MapLayerToggles> onLayers;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final planV = ref.watch(floorPlanForProvider(locationId));
    return asyncBody(
      planV,
      onRetry: () => ref.invalidate(visibleFloorPlansProvider),
      builder: (plan) {
        final picture = floor?.imageUrl ?? floor?.images.firstOrNull;
        if (plan == null && picture != null && picture.isNotEmpty) {
          return FloorPictureView(floor: floor!, imageUrl: picture, liveNode: liveNode);
        }
        if (plan == null) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const AppCard(
                child: EmptyState(icon: Icons.map_outlined, title: 'No floor plan published for this floor'),
              ),
              if (liveNode != null) ...[const SizedBox(height: 12), LiveZoneCounts(node: liveNode!)],
            ],
          );
        }
        final zonesV = ref.watch(planZonesProvider(plan.floorPlanId));
        final plotsV = ref.watch(planPlotsProvider(plan.floorPlanId));
        final layersV = ref.watch(planLayersProvider(plan.floorPlanId));
        final url = plan.fileUrl;
        final imageV = plan.isImage && url != null ? ref.watch(planImageProvider(url)) : null;
        if (zonesV.hasError && !zonesV.hasValue) {
          return LoadErrorState(
            error: zonesV.error!,
            onRetry: () => ref.invalidate(planZonesProvider(plan.floorPlanId)),
          );
        }
        if (imageV != null && imageV.hasError && !imageV.hasValue && !imageV.isLoading) {
          return AppCard(
            child: LoadErrorState(
              title: 'The floor plan image could not be loaded',
              error: imageV.error!,
              onRetry: () => ref.invalidate(planImageProvider(url!)),
            ),
          );
        }
        if (!zonesV.hasValue ||
            (!plotsV.hasValue && !plotsV.hasError) ||
            (imageV != null && imageV.isLoading && !imageV.hasValue)) {
          return const SizedBox(
            height: 340,
            child: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [CircularProgressIndicator(), SizedBox(height: 10), Text('Loading floor plan…')],
              ),
            ),
          );
        }
        final snapshot = ref.watch(trkLiveProvider).snapshot;
        final now = trackingNow();
        final zones = zonesV.requireValue;
        final allowed = visibleAssets == null ? null : {for (final a in visibleAssets!) a.key.toLowerCase()};
        final allPlots = plotsV.value ?? const <MapPlot>[];
        final plots = [
          for (final p in allPlots)
            if (p.isDevice || allowed == null || allowed.contains(p.itemId.toLowerCase())) p,
        ];
        final live = buildFloorLive(plan: plan, plots: allPlots, assets: tree?.all ?? const []);
        final devices = <String, TrkDevice>{
          for (final d in snapshot?.devices ?? const <TrkDevice>[]) ...{
            d.deviceId.toLowerCase(): d,
            if (d.assetCode != null) d.assetCode!.toLowerCase(): d,
          },
        };
        bool devOnline(MapPlot p) => deviceOnline(p.deviceKeys.map((k) => devices[k]).nonNulls.firstOrNull, now);
        final toggles = layers ?? MapLayerToggles.fromServer(layersV.value ?? const []);
        final focus = zones.where((z) => z.zoneId == focusZone).firstOrNull;
        final selected = plots.where((p) => plotKey(p) == selectedPlot).firstOrNull;
        // View on Map zooms to the asset's plot, else its live / focused zone.
        final PlanPoint? focusPoint = focusAsset == null
            ? null
            : (selected != null && plotKeyFocus(focusAsset, selected) != null
                  ? (x: selected.x, y: selected.y)
                  : focus?.center);
        void openZone(MapZone z) => showLiveZoneSheet(context, z, plan, live, allPlots, devOnline);
        final liveZones = [
          for (final z in zones)
            if ((live.byZone[z.zoneId] ?? const []).isNotEmpty) z,
        ];
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    [plan.name, plan.locationPath ?? '', plan.currentVersion].where((s) => s.isNotEmpty).join(' · '),
                    style: AppText.caption,
                  ),
                ),
                if (plan.isDraft) const StatusChip('Draft', tone: ChipTone.amber),
              ],
            ),
            const SizedBox(height: 6),
            SizedBox(
              height: 340,
              child: FloorPlanMap(
                plan: plan,
                image: imageV?.value,
                zones: zones,
                plots: plots,
                layers: toggles,
                snapshot: snapshot,
                now: now,
                live: live,
                deviceOnline: devOnline,
                focus: focusPoint,
                selectedPlot: selectedPlot,
                highlightZones: {if (focus != null) focus.zoneId, if (selected?.zoneId != null) selected!.zoneId!},
                onPlotTap: (p) => onSelectPlot(plotKey(p)),
                onClusterTap: (p, list) => showLiveAssetsSheet(context, 'Near ${p.label}', list),
                onZoneTap: openZone,
                onLayers: () => onLayers(toggles),
              ),
            ),
            if (!plan.isImage || url == null) ...[
              const SizedBox(height: 6),
              _InfoNote(
                url == null
                    ? 'This plan has no drawing file; zones and plots are shown on a blank plan.'
                    : 'The plan file is ${plan.format.isEmpty ? 'not an image' : plan.format}; the phone shows PNG / JPG '
                          'drawings only, so zones and plots are drawn on a blank plan.',
              ),
            ],
            if (plotsV.hasError) ...[
              const SizedBox(height: 6),
              InlineRetry(
                text: 'Plotted assets could not be loaded',
                error: true,
                onRetry: () => ref.invalidate(planPlotsProvider(plan.floorPlanId)),
              ),
            ],
            const SizedBox(height: 8),
            const LiveUpdatedAgo(),
            const SizedBox(height: 8),
            const FloorPlanLegend(),
            if (selected != null) ...[
              const SizedBox(height: 10),
              _PlotCard(
                plot: selected,
                zones: zones,
                snapshot: snapshot,
                now: now,
                live: live.plotted[plotKey(selected)],
                moved: live.moved.contains(plotKey(selected)),
                nearby: live.clusters[plotKey(selected)] ?? const [],
                deviceIsOnline: selected.isDevice && devOnline(selected),
              ),
            ],
            if (focus != null) ...[
              const SizedBox(height: 10),
              AppCard(
                onTap: () => openZone(focus),
                child: _ZoneFacts(zone: focus, counts: LocCounts.of(live.byZone[focus.zoneId] ?? const [], now)),
              ),
            ],
            const SizedBox(height: 14),
            SectionHeader(title: 'Live assets by zone (${liveZones.length})'),
            const SizedBox(height: 8),
            if (live.live.isEmpty)
              Text(
                'No tag on this floor right now: no location update names a device plotted on this plan.',
                style: AppText.caption,
              )
            else
              AppCard(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    for (final (i, z) in liveZones.indexed) ...[
                      if (i > 0) const Divider(height: 1, indent: 16),
                      ListTile(
                        leading: Icon(
                          z.geofence ? Icons.fence_rounded : Icons.crop_square_rounded,
                          color: hexColor(z.color, AppColors.primary),
                        ),
                        title: Text(z.label, style: AppText.bodyStrong),
                        subtitle: Padding(
                          padding: const EdgeInsets.only(top: 4),
                          child: LocCountsLine(counts: LocCounts.of(live.byZone[z.zoneId]!, now)),
                        ),
                        onTap: () => openZone(z),
                      ),
                    ],
                  ],
                ),
              ),
          ],
        );
      },
    );
  }
}

/// `asset:{id}` when [plot] is [assetId]'s plot (View on Map focus), else null.
String? plotKeyFocus(String? assetId, MapPlot plot) =>
    assetId != null && plot.assetId?.toLowerCase() == assetId.toLowerCase() ? plotKey(plot) : null;

/// The card of a tapped outdoor asset pin: name, status, location; Floor plan (when one exists),
/// Track and View Details.
class _GeoAssetCard extends StatelessWidget {
  const _GeoAssetCard({required this.asset, required this.floorPlan, required this.onFloorPlan});

  final DashLocatedAsset asset;
  final ({PlanLocation loc, String? plot})? floorPlan;
  final void Function(PlanLocation loc, {String? plot}) onFloorPlan;

  @override
  Widget build(BuildContext context) {
    final a = asset;
    final plan = floorPlan;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.location_on_rounded, color: AppColors.primary),
              const SizedBox(width: 8),
              Expanded(child: Text(a.assetName ?? a.assetTag ?? a.assetRegistrationId, style: AppText.bodyStrong)),
              if (a.status != null) DashStatusChip(label: a.status!, groupKey: a.statusGroup),
            ],
          ),
          const SizedBox(height: 8),
          InfoRow(label: 'Asset', value: [a.assetRegistrationId, ?a.assetTag].join(' · ')),
          if (a.locationLabel != null) InfoRow(label: 'Location', value: a.locationLabel!),
          InfoRow(label: 'Position', value: '${a.latitude.toStringAsFixed(5)}, ${a.longitude.toStringAsFixed(5)}'),
          const SizedBox(height: 10),
          Row(
            children: [
              if (plan != null) ...[
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => onFloorPlan(plan.loc, plot: plan.plot),
                    child: const Text('Floor plan'),
                  ),
                ),
                const SizedBox(width: 8),
              ],
              Expanded(
                child: OutlinedButton(
                  onPressed: () => context.push(Routes.trackAsset(a.assetRegistrationId)),
                  child: const Text('Track'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: FilledButton(
                  onPressed: () => context.push(Routes.assetDetail(a.assetRegistrationId)),
                  child: const Text('View Details'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The card under a tapped marker. Assets: plotted zone, live status, "Now near DEVICE in ZONE"
/// when the tag was last heard elsewhere (moved). Devices: online state and the live assets near it.
class _PlotCard extends StatelessWidget {
  const _PlotCard({
    required this.plot,
    required this.zones,
    required this.snapshot,
    required this.now,
    this.live,
    this.moved = false,
    this.nearby = const [],
    this.deviceIsOnline = false,
  });

  final MapPlot plot;
  final List<MapZone> zones;
  final TrackingSnapshot? snapshot;
  final DateTime now;
  final LiveTagAsset? live;
  final bool moved, deviceIsOnline;
  final List<LiveTagAsset> nearby;

  @override
  Widget build(BuildContext context) {
    final p = plot;
    final device = p.isDevice ? snapshot?.device(p.itemId) : null;
    final assetId = p.isAsset ? p.assetId : device?.assetRegistrationId;
    final plottedZone = zones.where((z) => z.zoneId == p.zoneId).firstOrNull?.label ?? p.zoneName;
    final l = live;
    final online = p.isDevice ? deviceIsOnline : (l?.isOnline(now) ?? false);
    final near = l?.near;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(p.isDevice ? Icons.router_rounded : Icons.inventory_2_rounded, color: AppColors.primary),
              const SizedBox(width: 8),
              Expanded(child: Text(p.label, style: AppText.bodyStrong)),
              if (moved) ...[const StatusChip('Moved', tone: ChipTone.orange), const SizedBox(width: 6)],
              StatusChip(
                p.isAsset && l == null ? 'No live tag' : (online ? 'Online' : 'Offline'),
                tone: online ? ChipTone.green : ChipTone.grey,
              ),
            ],
          ),
          const SizedBox(height: 8),
          InfoRow(label: p.isDevice ? 'Device ID' : 'Asset', value: p.code ?? p.itemId),
          if (p.isDevice && device?.assetName != null) InfoRow(label: 'Linked asset', value: device!.assetName!),
          InfoRow(label: 'Plotted zone', value: plottedZone ?? '—'),
          if (l != null) ...[
            InfoRow(label: 'Tag', value: l.tagId),
            InfoRow(
              label: 'Live',
              value: moved && near != null
                  ? 'Now near ${near.deviceLabel} in ${near.zoneName ?? near.floorName ?? near.plan.name}'
                  : l.nearLabel,
            ),
            InfoRow(label: 'Last update', value: l.lastChangedAt == null ? '—' : dashTimeAgo(l.lastChangedAt)),
            if (l.battery != null) InfoRow(label: 'Battery', value: '${l.battery}%'),
          ],
          if (p.isDevice) InfoRow(label: 'Live assets near it', value: '${nearby.length}'),
          const InfoRow(label: 'Marker', value: 'Plotted position (Administration → Maps)'),
          const SizedBox(height: 10),
          Row(
            children: [
              if (p.isDevice && nearby.isNotEmpty)
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => showLiveAssetsSheet(context, 'Near ${p.label}', nearby),
                    child: const Text('Nearby assets'),
                  ),
                ),
              if (assetId != null) ...[
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => context.push(Routes.trackAsset(assetId)),
                    child: const Text('Track'),
                  ),
                ),
                const SizedBox(width: 8),
              ],
              if (assetId != null || nearby.isEmpty)
                Expanded(
                  child: FilledButton(
                    onPressed: assetId == null
                        ? () => context.showSnack('Device ${p.itemId} has no linked asset')
                        : () => context.push(Routes.assetDetail(assetId)),
                    child: const Text('View Details'),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ZoneFacts extends StatelessWidget {
  const _ZoneFacts({required this.zone, required this.counts});

  final MapZone zone;
  final LocCounts counts;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Row(
        children: [
          Icon(
            zone.geofence ? Icons.fence_rounded : Icons.crop_square_rounded,
            color: hexColor(zone.color, AppColors.primary),
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(zone.label, style: AppText.bodyStrong)),
        ],
      ),
      if (zone.location?.locationPath case final path?) ...[
        const SizedBox(height: 4),
        Text(path, style: AppText.caption),
      ],
      const SizedBox(height: 6),
      LocCountsLine(counts: counts),
      const SizedBox(height: 6),
      Wrap(
        spacing: 6,
        runSpacing: 4,
        children: [
          if (zone.type.isNotEmpty) StatusChip(zone.type, tone: ChipTone.blue),
          if (zone.geofence) const StatusChip('Geofence', tone: ChipTone.red),
          if (zone.entryAlert) const StatusChip('Entry alert', tone: ChipTone.blue),
          if (zone.exitAlert) const StatusChip('Exit alert', tone: ChipTone.orange),
          if (zone.dwellAlert) StatusChip('Dwell ${zone.dwellMinutes} min', tone: ChipTone.purple),
          if (zone.accessRule != null) StatusChip(zone.accessRule!, tone: ChipTone.grey),
        ],
      ),
      if (zone.description != null) ...[const SizedBox(height: 6), Text(zone.description!, style: AppText.caption)],
    ],
  );
}

/// A zone: name, location path, the LIVE assets inside (tag last heard by a device plotted in the
/// zone) and the devices plotted in it. Read-only.
Future<void> showLiveZoneSheet(
  BuildContext context,
  MapZone zone,
  MapFloorPlan plan,
  FloorLive live,
  List<MapPlot> plots,
  bool Function(MapPlot) deviceIsOnline,
) {
  final assets = live.byZone[zone.zoneId] ?? const <LiveTagAsset>[];
  final devices = [
    for (final p in plots)
      if (p.isDevice && p.zoneId == zone.zoneId) p,
  ];
  final now = trackingNow();
  return showModalBottomSheet<void>(
    context: context,
    useRootNavigator: false,
    isScrollControlled: true,
    builder: (ctx) => sheetFrame(ctx, [
      SheetTitle(zone.label, subtitle: zone.location?.locationPath ?? plan.locationPath),
      _ZoneFacts(zone: zone, counts: LocCounts.of(assets, now)),
      const SizedBox(height: 12),
      Text('Live assets (${assets.length})', style: AppText.sectionTitle),
      if (assets.isEmpty)
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Text('No tag was last heard by a device in this zone.', style: AppText.caption),
        )
      else
        for (final a in assets) LiveTagAssetRow(asset: a, now: now),
      const SizedBox(height: 8),
      Text('Plotted devices (${devices.length})', style: AppText.sectionTitle),
      for (final d in devices)
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: Icon(Icons.router_rounded, color: deviceIsOnline(d) ? AppColors.iconBlue : AppColors.grey),
          title: Text(d.label, style: AppText.bodyStrong),
          subtitle: Text(
            [
              d.code ?? d.itemId,
              '${(live.clusters[plotKey(d)] ?? const []).length} unplotted live assets near it',
            ].join(' · '),
            style: AppText.caption,
          ),
          trailing: StatusChip(
            deviceIsOnline(d) ? 'Online' : 'Offline',
            tone: deviceIsOnline(d) ? ChipTone.green : ChipTone.grey,
          ),
        ),
      const SizedBox(height: 8),
      OutlinedButton(onPressed: () => Navigator.of(ctx).pop(), child: const Text('Close')),
    ]),
  );
}

/// Details of a GPS geofence.
Future<void> _showGeofence(BuildContext context, TrkGeofence g) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  builder: (context) => sheetFrame(context, [
    SheetTitle(g.name, subtitle: [g.siteName, g.geofenceType].where((s) => s.isNotEmpty).join(' · ')),
    InfoRow(label: 'Status', value: g.status ?? '—'),
    InfoRow(label: 'Shape', value: g.isCircle ? 'Circle · ${g.radiusMeters?.round() ?? 0} m' : g.shape),
    InfoRow(label: 'Schedule', value: g.schedule.isEmpty ? '—' : g.schedule),
    InfoRow(label: 'Asset groups', value: g.assetGroups.isEmpty ? '—' : g.assetGroups.join(', ')),
    InfoRow(label: 'Monitored assets', value: '${g.monitoredAssetCount}'),
    InfoRow(label: 'Alert rules', value: '${g.ruleCount}'),
    InfoRow(
      label: 'Alerts on',
      value: [
        if (g.monitorEntry) 'Entry',
        if (g.monitorExit) 'Exit',
        if (g.dwellAlert) 'Dwell ${g.dwellAlertMinutes} min',
      ].join(', ').ifEmpty('None'),
    ),
    if (g.lastEventAt != null) InfoRow(label: 'Last event', value: dashTimeAgo(g.lastEventAt)),
  ]),
);

extension on String {
  String ifEmpty(String other) => isEmpty ? other : this;
}

/// Zones / Geofences / Assets / Devices switches.
Future<MapLayerToggles?> showLiveLayersSheet(BuildContext context, MapLayerToggles current) =>
    showModalBottomSheet<MapLayerToggles>(
      context: context,
      isScrollControlled: true,
      builder: (context) {
        var l = current;
        return StatefulBuilder(
          builder: (context, setState) {
            Widget row(IconData icon, String label, bool value, MapLayerToggles Function(bool) apply) => SwitchListTile(
              contentPadding: EdgeInsets.zero,
              secondary: Icon(icon, color: AppColors.primary),
              title: Text(label, style: AppText.bodyStrong),
              value: value,
              onChanged: (v) => setState(() => l = apply(v)),
            );
            return sheetFrame(context, [
              const SheetTitle('Map layers', subtitle: 'Defaults come from the floor plan\'s layer settings'),
              row(Icons.crop_square_rounded, 'Zones', l.zones, (v) => l.copyWith(zones: v)),
              row(Icons.fence_rounded, 'Geofences', l.geofences, (v) => l.copyWith(geofences: v)),
              row(Icons.inventory_2_outlined, 'Assets', l.assets, (v) => l.copyWith(assets: v)),
              row(Icons.router_outlined, 'Devices', l.devices, (v) => l.copyWith(devices: v)),
              const SizedBox(height: 12),
              FilledButton(onPressed: () => Navigator.pop(context, l), child: const Text('Apply')),
            ]);
          },
        );
      },
    );

class _PickField extends StatelessWidget {
  const _PickField({
    required this.label,
    required this.value,
    required this.onTap,
    this.enabled = true,
    this.loading = false,
  });

  final String label, value;
  final VoidCallback onTap;
  final bool enabled, loading;

  @override
  Widget build(BuildContext context) => Material(
    color: enabled ? AppColors.surface : AppColors.border.withValues(alpha: .3),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(8),
      side: BorderSide(color: AppColors.border),
    ),
    child: InkWell(
      borderRadius: BorderRadius.circular(8),
      onTap: enabled && !loading ? onTap : null,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(8, 6, 4, 6),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(label, style: AppText.caption.copyWith(fontSize: 10.5)),
                  Text(
                    loading ? 'Loading…' : (value.isEmpty ? 'Select' : value),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppText.bodyStrong.copyWith(
                      fontSize: 12.5,
                      color: value.isEmpty ? AppColors.grey : AppColors.text,
                    ),
                  ),
                ],
              ),
            ),
            Icon(Icons.keyboard_arrow_down_rounded, size: 18, color: AppColors.grey),
          ],
        ),
      ),
    ),
  );
}

class _InfoNote extends StatelessWidget {
  const _InfoNote(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Icon(Icons.info_outline_rounded, size: 15, color: AppColors.grey),
      const SizedBox(width: 6),
      Expanded(child: Text(text, style: AppText.caption)),
    ],
  );
}

/// The newest location events (zone entries / exits / geofence alerts) with View All.
class _RecentEvents extends ConsumerWidget {
  const _RecentEvents();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final events = ref.watch(trkLiveProvider.select((s) => s.snapshot?.events));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(
          title: 'Recent Activity',
          actionLabel: 'View All',
          actionIcon: null,
          onAction: () => showLocationEventsSheet(context, events ?? const []),
        ),
        const SizedBox(height: 8),
        if (events == null)
          const ListSkeleton(rows: 2, card: false)
        else if (events.isEmpty)
          const AppCard(
            child: EmptyState(icon: Icons.timeline_rounded, title: 'No location events in the last 7 days'),
          )
        else
          for (final e in events.take(3))
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: AppCard(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 2),
                child: LocationEventRow(event: e),
              ),
            ),
      ],
    );
  }
}

/// "FL-008 Forklift · Entered Zone · Warehouse A · 3 mins ago"; tap → Asset Tracking.
class LocationEventRow extends StatelessWidget {
  const LocationEventRow({super.key, required this.event, this.showAsset = true});

  final TrkLocationEvent event;
  final bool showAsset;

  @override
  Widget build(BuildContext context) {
    final e = event;
    final (icon, color) = switch (e.eventType) {
      TrkEventType.enteredZone => (Icons.login_rounded, AppColors.green),
      TrkEventType.departedZone => (Icons.logout_rounded, AppColors.iconBlue),
      TrkEventType.geofenceAlert => (Icons.fence_rounded, AppColors.red),
      TrkEventType.dwellExceeded => (Icons.timer_outlined, AppColors.orange),
      _ => (Icons.place_outlined, AppColors.grey),
    };
    return ListTile(
      contentPadding: EdgeInsets.zero,
      onTap: showAsset ? () => context.push(Routes.trackAsset(e.assetId)) : null,
      leading: Icon(icon, color: color),
      title: Text(
        showAsset ? [e.assetId, if (e.assetName.isNotEmpty) e.assetName].join(' · ') : e.eventType,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: AppText.bodyStrong,
      ),
      subtitle: Text(
        [
          if (showAsset) e.eventType,
          e.zoneName,
          if (e.details != null) e.details!,
        ].where((s) => s.isNotEmpty).join(' · '),
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
        style: AppText.caption,
      ),
      trailing: Text(dashTimeAgo(e.eventTime), style: AppText.caption),
    );
  }
}

Future<void> showLocationEventsSheet(BuildContext context, List<TrkLocationEvent> events) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  builder: (context) => DraggableScrollableSheet(
    expand: false,
    initialChildSize: .75,
    builder: (context, scroll) => Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 4, 20, 0),
          child: SheetTitle('Location events', subtitle: '${events.length} in the last 7 days'),
        ),
        Expanded(
          child: events.isEmpty
              ? const EmptyState(icon: Icons.timeline_rounded, title: 'No location events')
              : ListView.builder(
                  controller: scroll,
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  itemCount: events.length,
                  itemBuilder: (_, i) => LocationEventRow(event: events[i]),
                ),
        ),
      ],
    ),
  ),
);
