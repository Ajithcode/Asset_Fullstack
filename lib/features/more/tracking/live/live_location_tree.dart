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
import 'package:asset360_mobile/features/more/tracking/live/live_map_tab.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

enum LocationMode { tree, map }

/// Tracking → Live Map / Geofencing (API mode): the LIVE LOCATION TREE (Site → Building → Floor →
/// Zone, from location-updates + gateways) while no location has coordinates; the map
/// ([LiveMapTab]) when Master Data gives any site / building / floor a latitude / longitude, with a
/// Tree / Map toggle. Location updates poll every 10 s while this view is visible.
class LiveLocationView extends ConsumerStatefulWidget {
  const LiveLocationView({
    super.key,
    this.initialView,
    this.initialLocation,
    this.focusZone,
    this.focusGeofence,
    required this.visibleAssets,
    this.query = '',
  });

  /// Map deep link (see [Routes.trackingMap]); any of them opens the map.
  final String? initialView, initialLocation, focusZone, focusGeofence;
  final List<TrackedAsset>? visibleAssets;

  /// The hub's search text.
  final String query;

  @override
  ConsumerState<LiveLocationView> createState() => _LiveLocationViewState();
}

class _LiveLocationViewState extends ConsumerState<LiveLocationView> {
  LocationMode? _mode;

  /// A floor opened from the tree ("Floor plan").
  String? _floor;

  bool get _deepLink =>
      widget.initialView != null ||
      widget.initialLocation != null ||
      widget.focusZone != null ||
      widget.focusGeofence != null;

  @override
  Widget build(BuildContext context) {
    final data = ref.watch(locationTreeProvider(widget.query.trim()));
    final mode = _mode ?? (_deepLink || data.mapPossible ? LocationMode.map : LocationMode.tree);
    final toggle = data.mapPossible || mode == LocationMode.map;
    return LivePollingScope(
      poller: (ref) => ref.read(liveLocProvider.notifier),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (toggle) ...[
            SegmentTabs(
              tabs: const [SegmentTab('Tree'), SegmentTab('Map')],
              selected: mode.index,
              onChanged: (i) => setState(() {
                _mode = LocationMode.values[i];
                if (_mode == LocationMode.tree) _floor = null;
              }),
            ),
            const SizedBox(height: 12),
          ],
          if (mode == LocationMode.tree)
            LocationTreeList(
              data: data,
              onOpenFloor: (id) => setState(() {
                _floor = id;
                _mode = LocationMode.map;
              }),
            )
          else
            LiveMapTab(
              key: ValueKey('map-${_floor ?? widget.initialLocation}-${widget.focusGeofence}-${widget.focusZone}'),
              initialView: _floor != null ? 'floor' : widget.initialView,
              initialLocation: _floor ?? widget.initialLocation,
              focusZone: widget.focusZone,
              focusGeofence: widget.focusGeofence,
              visibleAssets: widget.visibleAssets,
              tree: data.tree,
            ),
        ],
      ),
    );
  }
}

/// The tree itself: totals, one card per site, then the unplaced tags.
class LocationTreeList extends ConsumerWidget {
  const LocationTreeList({super.key, required this.data, required this.onOpenFloor});

  final LocationTreeData data;
  final ValueChanged<String> onOpenFloor;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tree = data.tree;
    final now = trackingNow();
    if (tree == null) {
      if (data.error != null) {
        return AppCard(
          child: LoadErrorState(
            title: 'Could not load live locations',
            error: data.error!,
            onRetry: () => ref.read(liveLocProvider.notifier).refresh(),
          ),
        );
      }
      return const ListSkeleton(rows: 4, card: false);
    }
    final total = LocCounts.of(tree.all, now);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _UpdatedLine(data: data),
        const SizedBox(height: 10),
        if (tree.sites.isEmpty && tree.unplaced.isEmpty)
          const AppCard(
            child: EmptyState(
              icon: Icons.location_searching_rounded,
              title: 'No location updates',
              message: 'No tag has been heard by a gateway yet.',
            ),
          )
        else ...[
          LocCountsLine(counts: total, prefix: 'All'),
          const SizedBox(height: 10),
          for (final site in tree.sites) ...[
            AppCard(
              padding: EdgeInsets.zero,
              child: _NodeTile(node: site, now: now, onOpenFloor: onOpenFloor),
            ),
            const SizedBox(height: 10),
          ],
          if (tree.unplaced.isNotEmpty) ...[
            SectionHeader(title: 'Unplaced (${tree.unplaced.length})'),
            const SizedBox(height: 6),
            if (tree.unknownGateways.isNotEmpty) ...[
              Text(
                'Not in the gateways list: ${(tree.unknownGateways.toList()..sort()).map((g) => 'Unknown gateway $g').join(', ')}',
                style: AppText.caption,
              ),
              const SizedBox(height: 6),
            ],
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final (i, a) in tree.unplaced.indexed) ...[
                    if (i > 0) const Divider(height: 1, indent: 56),
                    LiveTagAssetRow(asset: a, now: now),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 10),
          ],
        ],
        const _Note(
          'Each asset is placed at the site of the gateway that last heard its tag. Building, floor and zone '
          'are the asset\'s registered location, shown only when it is in that same site. Online = the tag '
          'reported within the last 10 minutes.',
        ),
      ],
    );
  }
}

class _UpdatedLine extends ConsumerWidget {
  const _UpdatedLine({required this.data});

  final LocationTreeData data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final polling = ref.watch(liveLocProvider.select((s) => s.polling));
    final at = data.fetchedAt;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(color: AppColors.tileLavender, borderRadius: BorderRadius.circular(8)),
      child: Row(
        children: [
          Icon(Icons.history_rounded, size: 16, color: AppColors.primary),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              at == null
                  ? 'Loading live locations…'
                  : 'Locations updated ${dashTimeAgo(at).toLowerCase()} · every 10 s',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppText.caption.copyWith(color: AppColors.text),
            ),
          ),
          if (data.error != null)
            InkWell(
              onTap: () => ref.read(liveLocProvider.notifier).refresh(),
              child: Text(
                'Retry',
                style: AppText.caption.copyWith(color: AppColors.red, fontWeight: FontWeight.w600),
              ),
            )
          else
            Text(
              polling ? 'Live' : 'Paused',
              style: AppText.caption.copyWith(
                color: polling ? AppColors.primary : AppColors.grey,
                fontWeight: FontWeight.w600,
              ),
            ),
        ],
      ),
    );
  }
}

/// "3 assets · 2 online · 1 offline · 1 alert".
class LocCountsLine extends StatelessWidget {
  const LocCountsLine({super.key, required this.counts, this.prefix});

  final LocCounts counts;
  final String? prefix;

  @override
  Widget build(BuildContext context) {
    final c = counts;
    return Wrap(
      spacing: 6,
      runSpacing: 4,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        if (prefix != null) Text(prefix!, style: AppText.caption),
        StatusChip('${c.assets} asset${c.assets == 1 ? '' : 's'}', tone: ChipTone.purple),
        StatusChip('${c.online} online', tone: ChipTone.green),
        StatusChip('${c.offline} offline', tone: ChipTone.grey),
        StatusChip('${c.alerts} alert${c.alerts == 1 ? '' : 's'}', tone: c.alerts > 0 ? ChipTone.red : ChipTone.grey),
      ],
    );
  }
}

class _NodeTile extends StatelessWidget {
  const _NodeTile({required this.node, required this.now, required this.onOpenFloor});

  final LocNode node;
  final DateTime now;
  final ValueChanged<String> onOpenFloor;

  @override
  Widget build(BuildContext context) {
    final n = node;
    final (icon, label) = switch (n.level) {
      LocLevel.site => (Icons.location_city_rounded, 'Site'),
      LocLevel.building => (Icons.apartment_rounded, 'Building'),
      LocLevel.floor => (Icons.layers_rounded, 'Floor'),
      LocLevel.zone => (Icons.crop_square_rounded, 'Zone'),
    };
    return Theme(
      data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
      child: ExpansionTile(
        key: PageStorageKey('loc-${n.level.name}-${n.id}'),
        initiallyExpanded: n.level == LocLevel.site,
        tilePadding: EdgeInsets.only(left: 12.0 + 12 * n.level.index, right: 8),
        childrenPadding: EdgeInsets.zero,
        leading: Icon(icon, color: AppColors.primary),
        title: Text('${n.name} · $label', style: AppText.bodyStrong),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 4),
          child: LocCountsLine(counts: n.counts(now)),
        ),
        children: [
          if (n.level == LocLevel.floor && n.hasPlan)
            Align(
              alignment: Alignment.centerLeft,
              child: Padding(
                padding: EdgeInsets.only(left: 12.0 + 12 * n.level.index),
                child: TextButton.icon(
                  onPressed: () => onOpenFloor(n.id),
                  icon: const Icon(Icons.map_outlined, size: 18),
                  label: const Text('Floor plan'),
                ),
              ),
            ),
          for (final c in n.children) _NodeTile(node: c, now: now, onOpenFloor: onOpenFloor),
          for (final a in n.assets)
            Padding(
              padding: EdgeInsets.only(left: 12.0 * n.level.index),
              child: LiveTagAssetRow(asset: a, now: now),
            ),
        ],
      ),
    );
  }
}

/// One asset: name, tag, "near GATEWAY", last update, battery; tap → Asset Tracking (tracked)
/// or the Asset detail.
class LiveTagAssetRow extends ConsumerWidget {
  const LiveTagAssetRow({super.key, required this.asset, required this.now});

  final LiveTagAsset asset;
  final DateTime now;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final a = asset;
    final online = a.isOnline(now);
    return ListTile(
      dense: true,
      leading: Stack(
        clipBehavior: Clip.none,
        children: [
          Icon(Icons.sell_outlined, color: AppColors.primary),
          Positioned(
            right: -2,
            bottom: -2,
            child: Container(
              width: 10,
              height: 10,
              decoration: BoxDecoration(
                color: online ? AppColors.green : AppColors.grey,
                shape: BoxShape.circle,
                border: Border.all(color: AppColors.surface, width: 1.5),
              ),
            ),
          ),
        ],
      ),
      title: Text(a.title, style: AppText.bodyStrong, maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            [
              'Tag ${a.tagId}',
              a.nearLabel,
              a.lastChangedAt == null ? 'No update time' : dashTimeAgo(a.lastChangedAt),
              if (a.battery != null) 'Battery ${a.battery}%',
            ].join(' · '),
            style: AppText.caption,
          ),
          if (a.registered != null)
            Text(
              'Registered location',
              style: AppText.caption.copyWith(color: AppColors.iconBlue, fontStyle: FontStyle.italic),
            ),
        ],
      ),
      trailing: a.alerts > 0
          ? StatusChip('${a.alerts} alert${a.alerts == 1 ? '' : 's'}', tone: ChipTone.red)
          : StatusChip(online ? 'Online' : 'Offline', tone: online ? ChipTone.green : ChipTone.grey),
      onTap: () {
        final id = a.assetRegistrationId;
        if (id == null) {
          context.showSnack('Tag ${a.tagId} is not linked to an asset');
          return;
        }
        final tracked = ref.read(trkLiveProvider).snapshot?.byKey(id) != null;
        context.push(tracked ? Routes.trackAsset(id) : Routes.assetDetail(id));
      },
    );
  }
}

/// The live count of every zone (child) of [node], plus the assets placed at [node] itself.
class LiveZoneCounts extends StatelessWidget {
  const LiveZoneCounts({super.key, required this.node, this.zones = const []});

  final LocNode node;

  /// Master Data zones of the floor (listed with 0 when nothing is there).
  final List<MdSpace> zones;

  @override
  Widget build(BuildContext context) {
    final now = trackingNow();
    final rows = <(String, LocNode?, List<LiveTagAsset>)>[
      for (final c in node.children) (c.name, c, c.allAssets.toList()),
      for (final z in zones)
        if (!node.children.any((c) => c.id == z.id)) (z.name, null, const <LiveTagAsset>[]),
      if (node.assets.isNotEmpty) ('No registered ${_below(node.level)}', null, node.assets),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionHeader(title: 'Live count by ${_below(node.level)}'),
        const SizedBox(height: 8),
        if (rows.isEmpty)
          Text('No live assets here.', style: AppText.caption)
        else
          AppCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                for (final (i, r) in rows.indexed) ...[
                  if (i > 0) const Divider(height: 1, indent: 16),
                  ListTile(
                    title: Text(r.$1, style: AppText.bodyStrong),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: LocCountsLine(counts: LocCounts.of(r.$3, now)),
                    ),
                    trailing: const Icon(Icons.chevron_right_rounded),
                    onTap: () => showLiveAssetsSheet(context, r.$1, r.$3),
                  ),
                ],
              ],
            ),
          ),
      ],
    );
  }

  static String _below(LocLevel l) => switch (l) {
    LocLevel.site => 'building',
    LocLevel.building => 'floor',
    LocLevel.floor || LocLevel.zone => 'zone',
  };
}

/// Read-only list of the live assets of one place.
Future<void> showLiveAssetsSheet(BuildContext context, String title, List<LiveTagAsset> assets) =>
    showModalBottomSheet<void>(
      context: context,
      useRootNavigator: false,
      isScrollControlled: true,
      builder: (ctx) {
        final now = trackingNow();
        return sheetFrame(ctx, [
          SheetTitle(title, subtitle: '${assets.length} live asset${assets.length == 1 ? '' : 's'}'),
          if (assets.isEmpty)
            const EmptyState(icon: Icons.location_off_outlined, title: 'No live assets here')
          else
            for (final a in assets) LiveTagAssetRow(asset: a, now: now),
          const SizedBox(height: 8),
          OutlinedButton(onPressed: () => Navigator.of(ctx).pop(), child: const Text('Close')),
        ]);
      },
    );

/// A floor's Master Data picture (no published plan): the image (pinch to zoom, read-only) and
/// each zone's live count. Master Data zones have no shape, so they are listed, not drawn.
class FloorPictureView extends ConsumerWidget {
  const FloorPictureView({super.key, required this.floor, required this.imageUrl, this.liveNode});

  final MdSpace floor;
  final String imageUrl;
  final LocNode? liveNode;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final image = ref.watch(planImageProvider(imageUrl));
    final zones = [
      for (final z in ref.watch(mdZonesProvider).value ?? const <MdSpace>[])
        if (z.floorId == floor.id) z,
    ];
    final node = liveNode ?? LocNode(level: LocLevel.floor, id: floor.id, name: floor.name);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('${floor.name} · floor picture (Master Data)', style: AppText.caption),
        const SizedBox(height: 6),
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: switch (image) {
            AsyncData(:final value) => AspectRatio(
              aspectRatio: value.aspect <= 0 ? 4 / 3 : value.aspect,
              child: InteractiveViewer(maxScale: 5, child: Image.memory(value.bytes, fit: BoxFit.contain)),
            ),
            AsyncError(:final error) => AppCard(
              child: LoadErrorState(
                title: 'The floor picture could not be loaded',
                error: error,
                onRetry: () => ref.invalidate(planImageProvider(imageUrl)),
              ),
            ),
            _ => const SizedBox(height: 240, child: ListSkeleton(rows: 3, card: false)),
          },
        ),
        const SizedBox(height: 6),
        const _Note('Zones have no drawn shape in Master Data, so their live counts are listed below.'),
        const SizedBox(height: 12),
        LiveZoneCounts(node: node, zones: zones),
      ],
    );
  }
}

class _Note extends StatelessWidget {
  const _Note(this.text);

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
