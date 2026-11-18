import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/live_assets_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_registry.dart';
import 'package:asset360_mobile/features/assets/live/live_filter_sheet.dart';
import 'package:asset360_mobile/features/assets/live/live_list_logic.dart';
import 'package:asset360_mobile/features/home/dash_ui.dart' show dashTimeAgo;
import 'package:asset360_mobile/features/more/tracking/live/live_tracking_hub.dart' show LiveCounter;
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

/// Tracked / Not tracked quick filter of the loaded rows.
enum TrackingScope { all, tracked, notTracked }

/// Tracking → Asset List (API mode): EVERY asset of the Asset service (GET /api/asset-registrations,
/// paged 20, server search + filters), each with its tracking facts when a Tracking service record
/// matches it (registration id / asset tag): zone, last seen, battery, online / offline; else
/// "Not tracked". Tracking records that match no registered asset are only counted
/// ("Unregistered tracking records: N", read-only list).
class TrackingAssetsTab extends ConsumerStatefulWidget {
  const TrackingAssetsTab({super.key, this.counter});

  /// The hub counter tile in use (Online / Offline / Low Battery): filters the loaded rows.
  final LiveCounter? counter;

  @override
  ConsumerState<TrackingAssetsTab> createState() => _TrackingAssetsTabState();
}

class _TrackingAssetsTabState extends ConsumerState<TrackingAssetsTab> {
  static const _debounce = Duration(milliseconds: 300);

  final _search = TextEditingController();
  Timer? _timer;
  AssetListQuery _q = const AssetListQuery();
  TrackingScope _scope = TrackingScope.all;

  @override
  void dispose() {
    _timer?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _onSearch(String v) {
    _timer?.cancel();
    _timer = Timer(_debounce, () {
      final t = v.trim();
      if (mounted && t != (_q.search ?? '')) setState(() => _q = _q.copyWith(search: () => t.isEmpty ? null : t));
    });
  }

  Future<void> _openFilters() async {
    final next = await showLiveFilterSheet(context, query: _q);
    if (next != null && mounted) setState(() => _q = next.copyWith(search: () => _q.search));
  }

  bool _keep(TrackedAsset? t, DateTime now) {
    final scopeOk = switch (_scope) {
      TrackingScope.all => true,
      TrackingScope.tracked => t != null,
      TrackingScope.notTracked => t == null,
    };
    final counterOk = switch (widget.counter) {
      LiveCounter.online => t != null && t.isOnline(now),
      LiveCounter.offline => t != null && !t.isOnline(now),
      LiveCounter.lowBattery => t != null && t.lowBattery,
      _ => true,
    };
    return scopeOk && counterOk;
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(liveAssetListProvider(_q));
    final snapshot = ref.watch(trkLiveProvider.select((s) => s.snapshot));
    final registry = ref.watch(trackingRegistryProvider).value;
    final now = trackingNow();
    final filters = _q.filterCount;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: SearchFilterBar(
                controller: _search,
                hint: 'Search by Asset ID, Name, Tag...',
                onChanged: _onSearch,
              ),
            ),
            const SizedBox(width: 8),
            IconButton.filledTonal(
              tooltip: filters == 0 ? 'Filter assets' : 'Filter assets ($filters)',
              onPressed: _openFilters,
              icon: Badge(
                isLabelVisible: filters > 0,
                label: Text('$filters'),
                child: const Icon(Icons.filter_alt_outlined),
              ),
            ),
          ],
        ),
        const SizedBox(height: 10),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final (scope, label) in const [
                (TrackingScope.all, 'All assets'),
                (TrackingScope.tracked, 'Tracked'),
                (TrackingScope.notTracked, 'Not tracked'),
              ])
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: IconFilterChip(
                    label: label,
                    selected: _scope == scope,
                    onTap: () => setState(() => _scope = scope),
                  ),
                ),
              if (filters > 0)
                ActionChip(
                  label: const Text('Clear filters'),
                  visualDensity: VisualDensity.compact,
                  onPressed: () => setState(() => _q = withoutFilters(_q)),
                ),
            ],
          ),
        ),
        if (registry != null && registry.unregistered.isNotEmpty) ...[
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              style: TextButton.styleFrom(visualDensity: VisualDensity.compact, padding: EdgeInsets.zero),
              onPressed: () => showUnregisteredTrackingSheet(context, registry.unregistered),
              icon: Icon(Icons.info_outline_rounded, size: 16, color: AppColors.textMuted),
              label: Text(
                'Unregistered tracking records: ${registry.unregistered.length}',
                style: AppText.caption.copyWith(decoration: TextDecoration.underline),
              ),
            ),
          ),
        ],
        const SizedBox(height: 10),
        if (async.value case final list?)
          ..._rows(list, snapshot, registry, now)
        else if (async.hasError)
          AppCard(
            child: LoadErrorState(
              title: 'Could not load assets',
              error: async.error!,
              onRetry: () => ref.invalidate(liveAssetListProvider(_q)),
            ),
          )
        else
          const ListSkeleton(rows: 4, card: false),
      ],
    );
  }

  List<Widget> _rows(LiveAssetList list, TrackingSnapshot? s, TrackingRegistry? registry, DateTime now) {
    if (list.items.isEmpty) {
      return [
        AppCard(
          child: list.query.filterCount > 0 || list.query.search != null
              ? const EmptyState(icon: Icons.search_off_rounded, title: 'No assets match')
              : const EmptyState(icon: Icons.inventory_2_outlined, title: 'No assets registered yet'),
        ),
      ];
    }
    TrackedAsset? recordOf(AssetListItem i) => s == null
        ? null
        : (registry?.recordFor(s, i.assetRegistrationId, i.assetTag) ??
              s.forRegistered(i.assetRegistrationId, i.assetTag));
    final rows = [
      for (final i in list.items)
        if (recordOf(i) case final t when _keep(t, now)) (i, t),
    ];
    final narrowed = _scope != TrackingScope.all || widget.counter != null;
    return [
      if (rows.isEmpty)
        AppCard(
          child: EmptyState(
            icon: Icons.sensors_off_rounded,
            title: 'No loaded assets match',
            message: list.hasMore ? 'Load more assets to search further.' : null,
          ),
        )
      else
        AppCard(
          padding: EdgeInsets.zero,
          child: Column(
            children: [
              for (var k = 0; k < rows.length; k++) ...[
                if (k > 0) const Divider(height: 1, indent: 90),
                TrackingAssetRow(item: rows[k].$1, record: rows[k].$2, now: now, trackingLoaded: s != null),
              ],
            ],
          ),
        ),
      const SizedBox(height: 8),
      Text(
        narrowed
            ? 'Showing ${rows.length} of ${list.items.length} loaded · ${formatLiveCount(list.totalCount)} assets'
            : 'Showing ${list.items.length} of ${formatLiveCount(list.totalCount)} assets',
        textAlign: TextAlign.center,
        style: AppText.caption,
      ),
      if (list.hasMore) ...[
        const SizedBox(height: 6),
        Center(
          child: list.loadingMore
              ? const Padding(
                  padding: EdgeInsets.all(8),
                  child: SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2)),
                )
              : TextButton(
                  onPressed: () => ref.read(liveAssetListProvider(_q).notifier).loadMore(),
                  child: Text(list.loadMoreError == null ? 'Load more' : 'Could not load more — Retry'),
                ),
        ),
      ],
    ];
  }
}

/// One Asset service asset with its tracking facts, or "Not tracked".
class TrackingAssetRow extends StatelessWidget {
  const TrackingAssetRow({
    super.key,
    required this.item,
    required this.record,
    required this.now,
    this.trackingLoaded = true,
  });

  final AssetListItem item;
  final TrackedAsset? record;
  final DateTime now;

  /// False while the first Tracking round loads (no "Not tracked" verdict yet).
  final bool trackingLoaded;

  @override
  Widget build(BuildContext context) {
    final t = record;
    final id = item.assetRegistrationId;
    final zone = t?.zone?.zoneName;
    return AssetListTile(
      title: item.assetTag ?? id,
      subtitle: item.assetName ?? (item.assetTag == null ? null : id),
      location: zone != null ? 'Zone: $zone' : locationLine(item),
      imagePath: item.imageUrl,
      fallbackIcon: t == null ? Icons.inventory_2_outlined : Icons.sensors_rounded,
      titleTrailing: t != null
          ? FreshnessChip(freshness: t.freshness(now))
          : trackingLoaded
          ? const StatusChip('Not tracked', tone: ChipTone.grey)
          : null,
      details: [if (t != null) TrackedFactsLine(asset: t, now: now)],
      onTap: () => context.push(t != null ? Routes.trackAsset(t.key) : Routes.assetDetail(id)),
    );
  }
}

/// Read-only list of tracking records that match no registered asset.
Future<void> showUnregisteredTrackingSheet(BuildContext context, List<TrackedAsset> records) =>
    showModalBottomSheet<void>(
      context: context,
      useRootNavigator: false,
      isScrollControlled: true,
      builder: (ctx) {
        final now = trackingNow();
        return sheetFrame(ctx, [
          SheetTitle(
            'Unregistered tracking records',
            subtitle:
                '${records.length} Tracking service record${records.length == 1 ? '' : 's'} that match no asset in '
                'the Asset service (by registration id, tag, barcode, RFID or NFC). Read-only.',
          ),
          for (final r in records)
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: CircleAvatar(
                backgroundColor: AppColors.tileLavender,
                child: Icon(
                  r.devices.isNotEmpty ? Icons.sensors_rounded : Icons.nfc_rounded,
                  color: AppColors.primary,
                  size: 20,
                ),
              ),
              title: Text(r.title, style: AppText.bodyStrong),
              subtitle: Text(
                [
                  r.subtitle,
                  if (r.whereLabel.isNotEmpty) r.whereLabel,
                  r.lastSeen == null ? 'Never seen' : 'Last seen ${dashTimeAgo(r.lastSeen)}',
                ].join(' · '),
                style: AppText.caption,
              ),
              trailing: FreshnessChip(freshness: r.freshness(now)),
            ),
          const SizedBox(height: 8),
          OutlinedButton(onPressed: () => Navigator.of(ctx).pop(), child: const Text('Close')),
        ]);
      },
    );
