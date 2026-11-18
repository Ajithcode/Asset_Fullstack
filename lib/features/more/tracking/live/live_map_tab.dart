import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/home/dash_ui.dart' show dashTimeAgo;
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_location_tree.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_hub_lists.dart' show TrackedAssetRow;
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
  });

  final String? initialView, initialLocation, focusZone, focusGeofence;

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
  String? _selectedPlot;

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
        SegmentTabs(
          tabs: const [SegmentTab('Outdoor'), SegmentTab('Floor plan')],
          selected: _view.index,
          onChanged: (i) => setState(() => _view = LiveMapView.values[i]),
        ),
        const SizedBox(height: 12),
        if (_view == LiveMapView.outdoor) _outdoor(sites, buildings, floors) else ..._floor(sites, buildings, floors),
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
    if (!sites.hasValue || !buildings.hasValue || !fences.hasValue && !fences.hasError) {
      return const SizedBox(height: 300, child: ListSkeleton(rows: 3, card: false));
    }
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
          child: places.isEmpty && geofences.isEmpty
              ? const AppCard(
                  child: EmptyState(
                    icon: Icons.public_off_rounded,
                    title: 'No mapped locations',
                    message: 'Sites, buildings or floors need latitude / longitude in Master Data to appear here.',
                  ),
                )
              : OutdoorMap(
                  places: places,
                  geofences: geofences,
                  showGeofences: layers.geofences,
                  focusGeofenceId: widget.focusGeofence,
                  onPlaceTap: (p) => _openPlace(p, buildings.requireValue, floors.value ?? const []),
                  onGeofenceTap: (g) => _showGeofence(context, g),
                  onLayers: () => _editLayers(layers),
                ),
        ),
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
          'Tags and gateways have no GPS position, so assets are never drawn as dots here. Badges count the '
          'assets placed at each site (the site of the gateway that last heard their tag); tap a pin to open '
          'its floors.',
        ),
      ],
    );
  }

  Future<void> _openPlace(GeoPlace p, List<MdSpace> buildings, List<MdSpace> floors) async {
    if (p.isFloor) {
      setState(() {
        _loc = PlanLocation(siteId: p.siteId, buildingId: p.buildingId, floorId: p.id);
        _view = LiveMapView.floor;
        _selectedPlot = null;
      });
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
    setState(() {
      _loc = chosen;
      _view = LiveMapView.floor;
      _selectedPlot = null;
    });
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
            message: 'Its published floor plan opens here with zones and plotted assets.',
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
          layers: _layers,
          selectedPlot: _selectedPlot,
          onSelectPlot: (k) => setState(() => _selectedPlot = _selectedPlot == k ? null : k),
          onLayers: _editLayers,
        ),
    ];
  }

  Future<void> _pick(String what, List<MdEntry> options, PlanLocation Function(String id) apply) async {
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

/// One plan: loads the published plan of [locationId], its drawing, zones, plots and layer
/// defaults, and draws them.
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
  });

  /// The Master Data floor (its picture when no plan is published).
  final MdSpace? floor;

  /// This location in the live tree (zone live counts).
  final LocNode? liveNode;

  final String locationId;
  final String? focusZone, selectedPlot;
  final List<TrackedAsset>? visibleAssets;
  final MapLayerToggles? layers;
  final ValueChanged<String> onSelectPlot;
  final ValueChanged<MapLayerToggles> onLayers;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final planV = ref.watch(publishedPlanProvider(locationId));
    return asyncBody(
      planV,
      onRetry: () => ref.invalidate(publishedPlanProvider(locationId)),
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
        if (!zonesV.hasValue ||
            (!plotsV.hasValue && !plotsV.hasError) ||
            (imageV != null && imageV.isLoading && !imageV.hasValue)) {
          return const SizedBox(height: 340, child: ListSkeleton(rows: 3, card: false));
        }
        final live = ref.watch(trkLiveProvider);
        final snapshot = live.snapshot;
        final now = trackingNow();
        final zones = zonesV.requireValue;
        final allowed = visibleAssets == null ? null : {for (final a in visibleAssets!) a.key.toLowerCase()};
        final plots = [
          for (final p in plotsV.value ?? const <MapPlot>[])
            if (p.isDevice || allowed == null || allowed.contains(p.itemId.toLowerCase())) p,
        ];
        final toggles = layers ?? MapLayerToggles.fromServer(layersV.value ?? const []);
        final focus = zones.where((z) => z.zoneId == focusZone).firstOrNull;
        final occupied = [
          for (final z in zones)
            if ((snapshot?.inZone(name: z.name, code: z.code) ?? const []).isNotEmpty) z,
        ];
        final selected = plots.where((p) => plotKey(p) == selectedPlot).firstOrNull;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              [plan.name, plan.locationPath ?? '', plan.currentVersion].where((s) => s.isNotEmpty).join(' · '),
              style: AppText.caption,
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
                selectedPlot: selectedPlot,
                highlightZones: {if (focus != null) focus.zoneId, if (selected?.zoneId != null) selected!.zoneId!},
                onPlotTap: (p) => onSelectPlot(plotKey(p)),
                onZoneTap: (z) => showZoneSheet(context, z, snapshot),
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
            if (imageV != null && imageV.hasError) ...[
              const SizedBox(height: 6),
              InlineRetry(
                text: 'The plan drawing could not be loaded',
                error: true,
                onRetry: () => ref.invalidate(planImageProvider(url!)),
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
            const LiveUpdatedBar(),
            if (selected != null) ...[
              const SizedBox(height: 10),
              _PlotCard(plot: selected, zones: zones, snapshot: snapshot, now: now),
            ],
            if (focus != null) ...[const SizedBox(height: 10), _ZoneCard(zone: focus, snapshot: snapshot)],
            if (liveNode != null) ...[const SizedBox(height: 14), LiveZoneCounts(node: liveNode!)],
            const SizedBox(height: 14),
            SectionHeader(title: 'Tracked assets by zone (${occupied.length})'),
            const SizedBox(height: 8),
            if (plots.where((p) => p.isAsset).isEmpty && occupied.isEmpty)
              const AppCard(
                child: EmptyState(icon: Icons.location_off_outlined, title: 'No tracked assets here'),
              )
            else if (occupied.isEmpty)
              Text('No tracked asset\'s latest zone event names a zone of this plan.', style: AppText.caption)
            else
              AppCard(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    for (final (i, z) in occupied.indexed) ...[
                      if (i > 0) const Divider(height: 1, indent: 16),
                      ListTile(
                        leading: Icon(
                          z.geofence ? Icons.fence_rounded : Icons.crop_square_rounded,
                          color: hexColor(z.color, AppColors.primary),
                        ),
                        title: Text(z.label, style: AppText.bodyStrong),
                        subtitle: Text(z.type),
                        trailing: StatusChip(
                          '${snapshot!.inZone(name: z.name, code: z.code).length} assets',
                          tone: ChipTone.purple,
                        ),
                        onTap: () => showZoneSheet(context, z, snapshot),
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

/// The card under a tapped marker: asset / device, zone, last seen, battery; View Details opens
/// the live Asset detail (or the device's asset).
class _PlotCard extends StatelessWidget {
  const _PlotCard({required this.plot, required this.zones, required this.snapshot, required this.now});

  final MapPlot plot;
  final List<MapZone> zones;
  final TrackingSnapshot? snapshot;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final p = plot;
    final device = p.isDevice ? snapshot?.device(p.itemId) : null;
    final asset = p.isAsset ? snapshot?.byKey(p.itemId) : null;
    final assetId = p.isAsset ? p.itemId : device?.assetRegistrationId;
    final zone = asset?.zone?.zoneName ?? zones.where((z) => z.zoneId == p.zoneId).firstOrNull?.label;
    final lastSeen = p.isDevice ? device?.lastSeenAt : asset?.lastSeen;
    final battery = p.isDevice ? device?.batteryLevel : asset?.battery;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(p.isDevice ? Icons.router_rounded : Icons.inventory_2_rounded, color: plotColor(p, snapshot, now)),
              const SizedBox(width: 8),
              Expanded(child: Text(p.label, style: AppText.bodyStrong)),
              FreshnessChip(freshness: freshnessOf(lastSeen, now)),
            ],
          ),
          const SizedBox(height: 8),
          InfoRow(label: p.isDevice ? 'Device ID' : 'Asset', value: p.code ?? p.itemId),
          if (p.isDevice && device?.assetName != null) InfoRow(label: 'Linked asset', value: device!.assetName!),
          InfoRow(label: 'Zone', value: zone ?? '—'),
          InfoRow(label: 'Last seen', value: lastSeen == null ? 'Never' : dashTimeAgo(lastSeen)),
          if (battery != null) InfoRow(label: 'Battery', value: '$battery%'),
          const InfoRow(label: 'Marker', value: 'Plotted position (Administration → Maps)'),
          const SizedBox(height: 10),
          Row(
            children: [
              if (assetId != null)
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => context.push(Routes.trackAsset(assetId)),
                    child: const Text('Track'),
                  ),
                ),
              if (assetId != null) const SizedBox(width: 8),
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

class _ZoneCard extends StatelessWidget {
  const _ZoneCard({required this.zone, required this.snapshot});

  final MapZone zone;
  final TrackingSnapshot? snapshot;

  @override
  Widget build(BuildContext context) => AppCard(
    onTap: () => showZoneSheet(context, zone, snapshot),
    child: _ZoneFacts(
      zone: zone,
      count: snapshot?.inZone(name: zone.name, code: zone.code).length ?? 0,
    ),
  );
}

class _ZoneFacts extends StatelessWidget {
  const _ZoneFacts({required this.zone, required this.count});

  final MapZone zone;
  final int count;

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
          StatusChip('$count assets', tone: ChipTone.purple),
        ],
      ),
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

/// A zone and the tracked assets whose latest location event put them in it (zone-level: the
/// server has no position inside the zone).
Future<void> showZoneSheet(BuildContext context, MapZone zone, TrackingSnapshot? snapshot) {
  final assets = snapshot?.inZone(name: zone.name, code: zone.code) ?? const <TrackedAsset>[];
  final now = trackingNow();
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (context) => sheetFrame(context, [
      _ZoneFacts(zone: zone, count: assets.length),
      const SizedBox(height: 12),
      if (assets.isEmpty)
        const EmptyState(icon: Icons.location_off_outlined, title: 'No tracked assets in this zone')
      else
        for (final a in assets) TrackedAssetRow(asset: a, now: now),
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
