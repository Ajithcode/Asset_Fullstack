import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_config.dart' show showServerSwitcher;
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart' show planImageProvider;
import 'package:asset360_mobile/features/more/tracking/live/rtl/locate_panel.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_asset_sheet.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_events.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_ui.dart';
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart' show frameFor;
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart' show LivePollingScope;
import 'package:asset360_mobile/features/more/tracking/tracking_widgets.dart' show CounterTiles;

/// Counter filters of the assets list (In view clears them).
enum RtlCounter { inView, moving, stationary, alerts }

/// REAL-TIME LOCATION (Tracking & IoT → Live Map and Geofencing tabs, API mode): header (Live /
/// Polling / Paused, site, Updated N s ago, Locate Asset, refresh), 4 counters, the facility map of one
/// floor, the assets list and recent location events. The tags/live socket + overview (30 s) run while visible
/// ([rtlProvider] through [LivePollingScope]). Contract: docs/api/MOBILE-API-HANDOFF.md section 1.
class RealTimeLocationView extends ConsumerStatefulWidget {
  const RealTimeLocationView({super.key, this.focusAsset, this.focusFloorPlan});

  /// View on Map: the asset (registration id) to show on its floor.
  final String? focusAsset;

  /// Home's Asset Location card: open on the floor of this floor plan id.
  final String? focusFloorPlan;

  @override
  ConsumerState<RealTimeLocationView> createState() => _RealTimeLocationViewState();
}

class _RealTimeLocationViewState extends ConsumerState<RealTimeLocationView> {
  String? _siteId;
  String? _floorId;
  RtlLayers _layers = rtlAllLayers;
  String? _selected;
  String? _focusAsset;
  int _focusNonce = 0;
  String _query = '';
  FilterSelection _filters = const {};
  RtlCounter? _counter;
  bool _deepLinkDone = false;

  @override
  void initState() {
    super.initState();
    if (widget.focusAsset != null) unawaited(_openDeepLink(widget.focusAsset!));
    if (widget.focusFloorPlan != null) unawaited(_openFloorOfPlan(widget.focusFloorPlan!));
  }

  /// Show the floor that the floor plan [planId] belongs to.
  Future<void> _openFloorOfPlan(String planId) async {
    try {
      final plans = await ref.read(rtlFloorPlansProvider.future);
      final floor = plans.where((p) => p.floorPlanId == planId).firstOrNull?.locationId;
      if (mounted && floor != null) setState(() => _floorId = floor);
    } catch (_) {
      // The default floor stays.
    }
  }

  /// View on Map: find the asset on any floor, then show it.
  Future<void> _openDeepLink(String id) async {
    RtlAsset? a;
    try {
      a = await ref.read(rtlAssetProvider(id).future);
    } catch (_) {
      a = null;
    }
    if (!mounted || _deepLinkDone) return;
    _deepLinkDone = true;
    if (a == null) {
      context.showSnack('No location: no tag linked, or its tag has not been heard by a gateway on a floor plan yet');
      return;
    }
    await _show(a, openCard: false);
  }

  /// The Master Data floor of [a] (via its location's floor plan), or null.
  Future<String?> _floorOf(RtlAsset a) async {
    final planId = a.location?.floorPlanId;
    if (planId == null) return null;
    try {
      final plans = await ref.read(rtlFloorPlansProvider.future);
      return plans.where((p) => p.floorPlanId == planId).firstOrNull?.locationId;
    } catch (_) {
      return null;
    }
  }

  /// Locate / Show on map / View on Map: switch to [a]'s floor, fit its ZONE and pulse ITS marker red, select it
  /// (and open its card). Without a location the map stays where it is and a message says why ("No location"):
  /// no tag linked / tag never heard / gateway not plotted / its floor has no floor plan.
  Future<void> _show(RtlAsset a, {bool openCard = true}) async {
    final who = a.name.isEmpty ? a.tagLabel : a.name;
    Future<void> noLocation(String why) async {
      if (mounted) context.showSnack('$who: No location ($why)');
      if (openCard && mounted) await showRtlAssetSheet(context, a, onShowOnMap: () => _show(a, openCard: false));
    }

    final why = rtlNoLocationReason(a);
    if (why != null) return noLocation(why);
    final floor = await _floorOf(a);
    RtlFloorPlanChoice? choice;
    if (floor != null) {
      try {
        choice = await ref.read(rtlFloorChoiceProvider(floor).future);
      } catch (_) {
        choice = null;
      }
    }
    if (!mounted) return;
    if (floor == null || choice is! RtlPlanChoice) return noLocation(rtlNoFloorPlanReason);
    // Is it drawn on that plan (zone of the plan, else a plotted gateway)?
    var zones = const <MapZone>[];
    var plots = const <MapPlot>[];
    try {
      zones = await ref.read(rtlZonesProvider(choice.plan.floorPlanId).future);
      plots = await ref.read(rtlDevicePlotsProvider(choice.plan.floorPlanId).future);
    } catch (_) {
      // Unloaded zones / plots: the asset then counts as not drawn.
    }
    if (!mounted) return;
    final drawn = placeAssets([a], plots, zones: zones, frame: PlanFrame.forAspect(1)).notOnPlan.isEmpty;
    setState(() {
      _floorId = floor;
      _selected = a.assetId;
      if (drawn) {
        _focusAsset = a.assetId;
        _focusNonce++;
      }
    });
    if (!drawn) {
      context.showSnack('$who: No location (${rtlUnplacedReason(a)})');
    }
    if (openCard) await showRtlAssetSheet(context, a, onShowOnMap: () => _show(a, openCard: false));
  }

  void _onCounter(RtlCounter c) =>
      setState(() => _counter = c == RtlCounter.inView || _counter == c ? null : c);

  Future<void> _locateAsset() async {
    final c = ProviderScope.containerOf(context, listen: false);
    List<RtlAsset> assets;
    try {
      assets = await c.read(rtlAllAssetsProvider(_siteId).future);
    } catch (_) {
      assets = c.read(rtlProvider).overview?.assets ?? const [];
    }
    if (!mounted) return;
    await showLocateAssetSheet(context, assets: assets, onTarget: (a) => _show(a));
  }

  Future<void> _pickLayers() async {
    await showModalBottomSheet<void>(
      context: context,
      useRootNavigator: false,
      showDragHandle: true,
      builder: (sheet) => StatefulBuilder(
        builder: (context, set) {
          void toggle(RtlLayers l) {
            setState(() => _layers = l);
            set(() {});
          }

          return sheetFrame(context, [
            const SheetTitle('Layers'),
            SwitchListTile(
              key: const ValueKey('layer-zones'),
              title: const Text('Zones'),
              value: _layers.zones,
              onChanged: (v) => toggle((zones: v, gateways: _layers.gateways, assets: _layers.assets)),
            ),
            SwitchListTile(
              key: const ValueKey('layer-gateways'),
              title: const Text('Gateways'),
              value: _layers.gateways,
              onChanged: (v) => toggle((zones: _layers.zones, gateways: v, assets: _layers.assets)),
            ),
            SwitchListTile(
              key: const ValueKey('layer-assets'),
              title: const Text('Assets'),
              value: _layers.assets,
              onChanged: (v) => toggle((zones: _layers.zones, gateways: _layers.gateways, assets: v)),
            ),
          ]);
        },
      ),
    );
  }

  /// The assets the counters / search / filters let through.
  List<RtlAsset> _filtered(List<RtlAsset> all) {
    final types = _filters['Asset Type'] ?? const {};
    final statuses = _filters['Status'] ?? const {};
    final zones = _filters['Zone'] ?? const {};
    return [
      for (final a in all)
        if (a.matches(_query) &&
            switch (_counter) {
              RtlCounter.moving => a.movement.toLowerCase() == 'moving',
              RtlCounter.stationary => a.movement.toLowerCase() == 'stationary',
              RtlCounter.alerts => a.activeLocationAlerts > 0,
              _ => true,
            } &&
            (types.isEmpty || types.contains(a.assetType ?? 'Other')) &&
            (statuses.isEmpty || statuses.contains(rtlStatusOf(a).label)) &&
            (zones.isEmpty || zones.contains(a.location?.zoneName ?? 'Unknown zone')))
          a,
    ];
  }

  @override
  Widget build(BuildContext context) {
    final rtl = ref.watch(rtlProvider);
    final overview = rtl.overview;
    final floors = ref.watch(rtlFloorOptionsProvider);
    final plans = ref.watch(rtlFloorPlansProvider);
    final siteFloors = [
      for (final f in floors.value ?? const <RtlFloorOption>[])
        if (_siteId == null || f.siteId == _siteId) f,
    ];
    // Default floor: the first with a drawable plan, else the first.
    final floor =
        siteFloors.where((f) => f.floorId == _floorId).firstOrNull ??
        siteFloors.where((f) {
          final p = plans.value;
          return p != null && chooseFloorPlan(p, f.floor, dev: showServerSwitcher) is RtlPlanChoice;
        }).firstOrNull ??
        siteFloors.firstOrNull;
    final choice = floor == null ? null : ref.watch(rtlFloorChoiceProvider(floor.floorId)).value;
    final planId = choice is RtlPlanChoice ? choice.plan.floorPlanId : null;
    // Only while this screen is the one on show: Home's Asset Location card shares rtlProvider and sets its own
    // scope while Home is visible.
    if (TickerMode.of(context) && (rtl.siteId != _siteId || rtl.floorPlanId != planId)) {
      // Poll the scope that is on screen (after this frame: providers can't change while building).
      scheduleMicrotask(() {
        if (mounted) ref.read(rtlProvider.notifier).setScope(siteId: _siteId, floorPlanId: planId);
      });
    }
    final assets = overview?.assets ?? const <RtlAsset>[];
    final shown = _filtered(assets);
    final k = overview?.kpis ?? const RtlKpis();
    return LivePollingScope(
      poller: (ref) => ref.read(rtlProvider.notifier),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _Header(
            state: rtl,
            siteId: _siteId,
            onSite: (s) => setState(() {
              _siteId = s;
              _floorId = null;
            }),
            onLocate: _locateAsset,
            onRefresh: () => refreshRtl(ref),
          ),
          const SizedBox(height: 12),
          CounterTiles<RtlCounter>(
            items: [
              (RtlCounter.inView, Icons.visibility_rounded, k.assetsInView, 'Assets in View', TileTone.lavender),
              (RtlCounter.moving, Icons.directions_run_rounded, k.moving, 'Moving', TileTone.mint),
              (RtlCounter.stationary, Icons.pause_circle_rounded, k.stationary, 'Stationary', TileTone.sky),
              (RtlCounter.alerts, Icons.warning_rounded, k.locationAlerts, 'Location Alerts', TileTone.pink),
            ],
            selected: _counter,
            onTap: _onCounter,
          ),
          if (rtl.error != null && overview != null) ...[
            const SizedBox(height: 10),
            _ErrorBanner(error: rtl.error!, onRetry: () => ref.read(rtlProvider.notifier).refresh()),
          ],
          const SizedBox(height: 12),
          _FloorPicker(
            floors: floors,
            options: siteFloors,
            selected: floor,
            choice: choice,
            onChanged: (id) => setState(() {
              _floorId = id;
              _selected = null;
            }),
          ),
          const SizedBox(height: 10),
          _MapArea(
            floor: floor,
            choice: choice,
            overview: overview,
            layers: _layers,
            selected: _selected,
            focusAsset: _focusAsset,
            focusNonce: _focusNonce,
            onLayers: _pickLayers,
            onAsset: (a) {
              setState(() => _selected = a.assetId);
              showRtlAssetSheet(context, a, onShowOnMap: () => _show(a, openCard: false));
            },
            onCluster: (g, list) => showLocateAssetSheet(context, assets: list, onTarget: (a) => _show(a)),
          ),
          const SizedBox(height: 8),
          const RtlLegend(),
          const SizedBox(height: 16),
          Text('Assets', style: AppText.sectionTitle),
          const SizedBox(height: 8),
          if (overview == null && rtl.error != null)
            LoadErrorState(error: rtl.error!, onRetry: () => ref.read(rtlProvider.notifier).refresh())
          else if (overview == null)
            const ListSkeleton(rows: 3)
          else if (assets.isEmpty)
            const EmptyState(
              icon: Icons.sensors_off_rounded,
              title: 'No assets in view yet',
              message:
                  'Tracked tags appear here when gateways report them. Link a BLE tag to an asset and plot its '
                  'gateway on a floor plan in Administration › Maps.',
            )
          else ...[
            SearchFilterBar(
              hint: 'Search asset tag, name or tag ID',
              onChanged: (v) => setState(() => _query = v),
              filterGroups: [
                FilterGroup(title: 'Asset Type', options: ({for (final a in assets) a.assetType ?? 'Other'}.toList()..sort())),
                FilterGroup(title: 'Status', options: [for (final s in RtlStatus.values) s.label]),
                FilterGroup(
                  title: 'Zone',
                  options: ({for (final a in assets) a.location?.zoneName ?? 'Unknown zone'}.toList()..sort()),
                ),
              ],
              selection: _filters,
              onFiltersChanged: (f) => setState(() => _filters = f),
            ),
            const SizedBox(height: 8),
            if (shown.isEmpty)
              const EmptyState(icon: Icons.search_off_rounded, title: 'No asset matches the filters')
            else
              AppCard(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 2),
                child: Column(
                  children: [
                    for (final (i, a) in shown.indexed) ...[
                      if (i > 0) Divider(height: 1, color: AppColors.divider),
                      _AssetRow(asset: a, selected: a.assetId == _selected, onTap: () => _show(a)),
                    ],
                  ],
                ),
              ),
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text('Showing ${shown.length} of ${assets.length}', style: AppText.caption),
            ),
          ],
          const SizedBox(height: 16),
          RtlEventsSection(
            siteId: _siteId,
            onShowOnMap: (id) async {
              final a = await ref.read(rtlAssetProvider(id).future);
              if (a != null) await _show(a, openCard: false);
            },
          ),
        ],
      ),
    );
  }
}

// ------------------------------------------------------------------ header

class _Header extends ConsumerWidget {
  const _Header({
    required this.state,
    required this.siteId,
    required this.onSite,
    required this.onLocate,
    required this.onRefresh,
  });

  final RtlState state;
  final String? siteId;
  final ValueChanged<String?> onSite;
  final VoidCallback onLocate, onRefresh;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sites = ref.watch(mdSitesProvider).value ?? const <MdSite>[];
    final live = state.live;
    // Live = socket connected; Polling = the /tags fallback; Paused = screen hidden.
    final label = live ? 'Live' : (state.polling ? 'Polling' : 'Paused');
    final tone = live ? AppColors.green : (state.polling ? AppColors.orange : AppColors.red);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Container(
              key: const ValueKey('rtl-live'),
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: tone.withValues(alpha: .12),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.circle, size: 9, color: tone),
                  const SizedBox(width: 5),
                  _BadgeText(
                    label: label,
                    since: live ? state.lastFrameAt : null,
                    style: AppText.caption.copyWith(
                      fontWeight: FontWeight.w700,
                      color: tone,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Expanded(child: _UpdatedAgo(at: state.fetchedAt)),
            IconButton(
              key: const ValueKey('rtl-refresh'),
              tooltip: 'Refresh',
              onPressed: onRefresh,
              icon: const Icon(Icons.refresh_rounded),
            ),
          ],
        ),
        Row(
          children: [
            Expanded(
              child: DropdownButtonFormField<String?>(
                key: const ValueKey('rtl-site'),
                initialValue: sites.any((s) => s.id == siteId) ? siteId : null,
                isExpanded: true,
                decoration: const InputDecoration(
                  isDense: true,
                  prefixIcon: Icon(Icons.apartment_rounded, size: 18),
                  contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 10),
                ),
                items: [
                  const DropdownMenuItem<String?>(value: null, child: Text('All sites')),
                  for (final s in sites)
                    DropdownMenuItem<String?>(value: s.id, child: Text(s.name, overflow: TextOverflow.ellipsis)),
                ],
                onChanged: onSite,
              ),
            ),
            const SizedBox(width: 8),
            FilledButton.icon(
              key: const ValueKey('rtl-locate'),
              onPressed: onLocate,
              icon: const Icon(Icons.gps_fixed_rounded, size: 18),
              label: const Text('Locate Asset'),
            ),
          ],
        ),
      ],
    );
  }
}

/// "Updated 12 s ago", ticking every second while visible.
/// "Live · 3 s ago": the badge text; with [since] (the last socket frame) it counts up every second.
class _BadgeText extends StatefulWidget {
  const _BadgeText({required this.label, required this.since, required this.style});

  final String label;
  final DateTime? since;
  final TextStyle style;

  @override
  State<_BadgeText> createState() => _BadgeTextState();
}

class _BadgeTextState extends State<_BadgeText> {
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _tick = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted && widget.since != null && TickerMode.of(context)) setState(() {});
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final since = widget.since;
    return Text(
      since == null ? widget.label : '${widget.label} · ${rtlAgo(since, rtlNow())}',
      key: const ValueKey('rtl-live-text'),
      style: widget.style,
    );
  }
}

class _UpdatedAgo extends StatefulWidget {
  const _UpdatedAgo({required this.at});

  final DateTime? at;

  @override
  State<_UpdatedAgo> createState() => _UpdatedAgoState();
}

class _UpdatedAgoState extends State<_UpdatedAgo> {
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
    final at = widget.at;
    return Text(
      at == null ? 'Loading…' : 'Updated ${rtlAgo(at, rtlNow())}',
      key: const ValueKey('rtl-updated'),
      style: AppText.caption,
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final e = ApiException.from(error);
    return Container(
      key: const ValueKey('rtl-error'),
      padding: const EdgeInsets.fromLTRB(12, 6, 6, 6),
      decoration: BoxDecoration(color: AppColors.redBg, borderRadius: BorderRadius.circular(10)),
      child: Row(
        children: [
          const Icon(Icons.cloud_off_rounded, size: 18, color: AppColors.red),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              e.isForbidden ? 'No access' : 'Live updates paused: ${e.message}',
              style: AppText.caption.copyWith(color: AppColors.red),
            ),
          ),
          TextButton(onPressed: onRetry, child: const Text('Retry')),
        ],
      ),
    );
  }
}

// ------------------------------------------------------------------ floor + map

class _FloorPicker extends StatelessWidget {
  const _FloorPicker({
    required this.floors,
    required this.options,
    required this.selected,
    required this.choice,
    required this.onChanged,
  });

  final AsyncValue<List<RtlFloorOption>> floors;
  final List<RtlFloorOption> options;
  final RtlFloorOption? selected;
  final RtlFloorPlanChoice? choice;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    if (floors.isLoading && options.isEmpty) return const ListSkeleton(rows: 1);
    if (floors.hasError && options.isEmpty) {
      return Text('Floors could not be loaded: ${ApiException.from(floors.error!).message}', style: AppText.caption);
    }
    if (options.isEmpty) return Text('No floors in Master Data', style: AppText.secondary);
    final draft = choice is RtlPlanChoice && (choice as RtlPlanChoice).draft;
    return Row(
      children: [
        Expanded(
          child: DropdownButtonFormField<String>(
            key: ValueKey('rtl-floor-${selected?.floorId}'),
            initialValue: selected?.floorId,
            isExpanded: true,
            decoration: const InputDecoration(
              isDense: true,
              prefixIcon: Icon(Icons.layers_rounded, size: 18),
              contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 10),
            ),
            items: [
              for (final f in options)
                DropdownMenuItem(value: f.floorId, child: Text(f.label, overflow: TextOverflow.ellipsis)),
            ],
            onChanged: (v) {
              if (v != null) onChanged(v);
            },
          ),
        ),
        if (draft) ...[const SizedBox(width: 8), const StatusChip('Draft', tone: ChipTone.amber)],
      ],
    );
  }
}

class _MapArea extends ConsumerWidget {
  const _MapArea({
    required this.floor,
    required this.choice,
    required this.overview,
    required this.layers,
    required this.selected,
    required this.focusAsset,
    required this.focusNonce,
    required this.onLayers,
    required this.onAsset,
    required this.onCluster,
  });

  final RtlFloorOption? floor;
  final RtlFloorPlanChoice? choice;
  final RtlOverview? overview;
  final RtlLayers layers;
  final String? selected, focusAsset;
  final int focusNonce;
  final VoidCallback onLayers;
  final ValueChanged<RtlAsset> onAsset;
  final void Function(MapPlot gateway, List<RtlAsset> assets) onCluster;

  static const height = 380.0;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assets = overview?.assets ?? const <RtlAsset>[];
    final c = choice;
    if (floor == null) return const SizedBox.shrink();
    if (c == null) return const SizedBox(height: height, child: ListSkeleton(rows: 3));
    switch (c) {
      case RtlNoPlan():
        return Column(
          children: [
            const AppCard(
              child: EmptyState(
                icon: Icons.map_outlined,
                title: 'No floor plan for this floor',
                message: 'Upload and publish a plan in Administration › Maps & Floor Plans.',
              ),
            ),
            _NotOnPlan(assets: assets, onTap: onAsset),
          ],
        );
      case RtlPictureChoice(:final url):
        final image = ref.watch(planImageProvider(url));
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Floor picture (Master Data) · no gateways plotted', style: AppText.caption),
            const SizedBox(height: 6),
            ClipRRect(
              borderRadius: BorderRadius.circular(14),
              child: switch (image) {
                AsyncData(:final value) => AspectRatio(
                  aspectRatio: value.aspect <= 0 ? 4 / 3 : value.aspect,
                  child: InteractiveViewer(maxScale: 5, child: Image.memory(value.bytes, fit: BoxFit.contain)),
                ),
                AsyncError() => const SizedBox(
                  height: 120,
                  child: EmptyState(icon: Icons.broken_image_outlined, title: 'The floor picture could not be loaded'),
                ),
                _ => const SizedBox(height: 200, child: Center(child: CircularProgressIndicator())),
              },
            ),
            _NotOnPlan(assets: assets, onTap: onAsset),
          ],
        );
      case RtlPlanChoice(:final plan):
        final image = plan.fileUrl == null ? null : ref.watch(planImageProvider(plan.fileUrl!));
        final zones = ref.watch(rtlZonesProvider(plan.floorPlanId)).value ?? const <MapZone>[];
        final plotsAsync = ref.watch(rtlDevicePlotsProvider(plan.floorPlanId));
        final zonesAsync = ref.watch(rtlZonesProvider(plan.floorPlanId));
        final placement = placeAssets(
          assets,
          plotsAsync.value ?? const [],
          zones: zones,
          frame: frameFor(plan, image?.value),
        );
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              height: height,
              child: image != null && image.isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : RtlFloorMap(
                      key: ValueKey('rtl-map-${plan.floorPlanId}'),
                      plan: plan,
                      image: image?.value,
                      zones: zones,
                      placement: placement,
                      overview: overview,
                      assets: assets,
                      layers: layers,
                      selectedAssetId: selected,
                      focusAssetId: focusAsset,
                      focusNonce: focusNonce,
                      onAssetTap: onAsset,
                      onClusterTap: onCluster,
                      onLayers: onLayers,
                    ),
            ),
            if (image?.hasError ?? false)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text('The plan drawing could not be loaded', style: AppText.caption),
              ),
            _NotOnPlan(assets: plotsAsync.hasValue && zonesAsync.hasValue ? placement.notOnPlan : const [], onTap: onAsset),
          ],
        );
    }
  }
}

/// "Not on this floor plan (N)": assets with no zone on this plan and no plotted gateway (never placed).
class _NotOnPlan extends StatelessWidget {
  const _NotOnPlan({required this.assets, required this.onTap});

  final List<RtlAsset> assets;
  final ValueChanged<RtlAsset> onTap;

  @override
  Widget build(BuildContext context) {
    if (assets.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: AppCard(
        padding: EdgeInsets.zero,
        child: ExpansionTile(
          key: const ValueKey('rtl-not-on-plan'),
          shape: const Border(),
          title: Text('Not on this floor plan (${assets.length})', style: AppText.bodyStrong),
          subtitle: Text('Their gateway is not plotted on this plan', style: AppText.caption),
          children: [
            for (final a in assets)
              ListTile(
                dense: true,
                leading: RtlStatusDot(status: rtlStatusOf(a)),
                minLeadingWidth: 12,
                title: Text('${a.tagLabel} ${a.name}', maxLines: 1, overflow: TextOverflow.ellipsis),
                subtitle: Text(
                  'No location · ${rtlNoLocationReason(a) ?? rtlUnplacedReason(a)}',
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                onTap: () => onTap(a),
              ),
          ],
        ),
      ),
    );
  }
}

class _AssetRow extends StatelessWidget {
  const _AssetRow({required this.asset, required this.selected, required this.onTap});

  final RtlAsset asset;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final a = asset;
    return InkWell(
      key: ValueKey('rtl-row-${a.assetId}'),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 10),
        child: Row(
          children: [
            RtlStatusDot(status: rtlStatusOf(a), size: 12),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Flexible(
                        child: Text(
                          a.tagLabel,
                          style: AppText.bodyStrong.copyWith(color: selected ? AppColors.primary : null),
                        ),
                      ),
                      if (a.activeLocationAlerts > 0) ...[
                        const SizedBox(width: 4),
                        const Icon(Icons.warning_rounded, size: 14, color: rtlAlertColor),
                      ],
                    ],
                  ),
                  Text(a.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: AppText.secondary),
                  Text(
                    [a.location?.zoneName ?? 'Unknown zone', a.accuracy].join(' · '),
                    style: AppText.caption,
                  ),
                ],
              ),
            ),
            Text(rtlAgo(a.lastSeen, rtlNow()), style: AppText.caption),
          ],
        ),
      ),
    );
  }
}
