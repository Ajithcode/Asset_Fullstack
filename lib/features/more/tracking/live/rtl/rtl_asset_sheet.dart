import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/locate_panel.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_ui.dart';

/// The asset card (bottom sheet): where the asset is, how sure the backend is, detected vs
/// registered location, the zone trail, and Locate / View Asset / Show on map.
Future<void> showRtlAssetSheet(BuildContext context, RtlAsset asset, {VoidCallback? onShowOnMap}) {
  return showModalBottomSheet<void>(
    context: context,
    useRootNavigator: false,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (sheet) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: .75,
      maxChildSize: .95,
      minChildSize: .4,
      builder: (_, scroll) => RtlAssetCard(
        asset: asset,
        scroll: scroll,
        onShowOnMap: onShowOnMap == null
            ? null
            : () {
                Navigator.pop(sheet);
                onShowOnMap();
              },
        onViewAsset: () {
          Navigator.pop(sheet);
          context.push(Routes.assetDetail(asset.assetId));
        },
      ),
    ),
  );
}

/// Opens the card of [assetId]: from the polled overview, else one overview fetch; a SnackBar when
/// the asset is not tracked.
Future<void> openRtlAssetById(BuildContext context, String? assetId, {VoidCallback? onShowOnMap}) async {
  if (assetId == null || assetId.isEmpty) {
    context.showSnack('This event has no asset');
    return;
  }
  final c = ProviderScope.containerOf(context, listen: false);
  RtlAsset? a = c.read(rtlProvider).overview?.asset(assetId);
  if (a == null) {
    try {
      a = await c.read(rtlAssetProvider(assetId).future);
    } catch (_) {
      a = null;
    }
  }
  if (!context.mounted) return;
  if (a == null) {
    context.showSnack('$assetId: No location (no tag linked, or its tag has not been heard by a gateway on a floor plan yet)');
    return;
  }
  await showRtlAssetSheet(context, a, onShowOnMap: onShowOnMap);
}

class RtlAssetCard extends ConsumerStatefulWidget {
  const RtlAssetCard({super.key, required this.asset, this.scroll, this.onShowOnMap, this.onViewAsset});

  final RtlAsset asset;
  final ScrollController? scroll;
  final VoidCallback? onShowOnMap, onViewAsset;

  @override
  ConsumerState<RtlAssetCard> createState() => _RtlAssetCardState();
}

class _RtlAssetCardState extends ConsumerState<RtlAssetCard> {
  String _range = '30m';

  @override
  Widget build(BuildContext context) {
    // Follow the polled overview so the card stays current.
    final a = ref.watch(rtlProvider.select((s) => s.overview?.asset(widget.asset.assetId))) ?? widget.asset;
    final now = rtlNow();
    final status = rtlStatusOf(a);
    final loc = a.location;
    final last = a.lastSeen;
    final exact = last == null ? null : DateFormat('d MMM yyyy, HH:mm:ss').format(last.toLocal());
    return ListView(
      controller: widget.scroll,
      padding: EdgeInsets.fromLTRB(20, 0, 20, 24 + MediaQuery.paddingOf(context).bottom),
      children: [
        Row(
          children: [
            RtlAssetAvatar(asset: a, size: 46),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(a.tagLabel, style: AppText.sectionTitle),
                  Text(a.name.isEmpty ? (a.assetType ?? '') : a.name, style: AppText.secondary),
                ],
              ),
            ),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                RtlStatusDot(status: status),
                const SizedBox(width: 4),
                Text(status.label, style: AppText.caption),
              ],
            ),
          ],
        ),
        if (a.activeLocationAlerts > 0) ...[
          const SizedBox(height: 10),
          StatusChip(
            '${a.activeLocationAlerts} open location alert${a.activeLocationAlerts == 1 ? '' : 's'}',
            tone: ChipTone.red,
            icon: Icons.warning_rounded,
          ),
        ],
        const SizedBox(height: 12),
        InfoRow(label: 'Current Zone', value: loc == null || loc.path.isEmpty ? 'Unknown' : loc.path),
        InfoRow(label: 'Movement', value: rtlMovementLabel(a)),
        InfoRow(label: 'Last Update', value: last == null ? 'Never' : '${rtlAgo(last, now)} · $exact'),
        InfoRow(
          label: 'Location Confidence',
          value: a.confidence,
          valueWidget: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              StatusChip(a.confidence, tone: rtlConfidenceTone(a.confidence)),
              if (a.confidenceBasis.isNotEmpty) ...[
                const SizedBox(height: 2),
                Text(a.confidenceBasis, style: AppText.caption),
              ],
            ],
          ),
        ),
        InfoRow(
          label: 'Location Source',
          value: [a.locationSource, if (loc != null && loc.basis.isNotEmpty) loc.basis]
              .where((s) => s.isNotEmpty)
              .join(' · ')
              .ifEmpty('Unknown'),
        ),
        InfoRow(label: 'Accuracy', value: a.accuracy),
        InfoRow(
          label: 'Assigned Device',
          value: [if (a.tracker != null) a.tracker!, ...a.tags.where((t) => t != a.tracker)].join(', ').ifEmpty('No tag'),
        ),
        InfoRow(label: 'RSSI', value: a.rssi == null ? '—' : '${a.rssi!.round()} dBm'),
        InfoRow(
          label: 'Gateways hearing it',
          value: [
            '${a.gatewaysHearing}',
            if (loc?.gatewayName != null || (loc?.gatewayId.isNotEmpty ?? false))
              'strongest ${loc!.gatewayName ?? loc.gatewayId}',
          ].join(' · '),
        ),
        const SizedBox(height: 14),
        _Comparison(asset: a),
        const SizedBox(height: 16),
        _Trail(assetId: a.assetId, range: _range, onRange: (r) => setState(() => _range = r)),
        const SizedBox(height: 18),
        Row(
          children: [
            Expanded(
              child: FilledButton.icon(
                onPressed: a.hasTags ? () => showLocatePanel(context, a) : null,
                icon: const Icon(Icons.wb_twilight_rounded, size: 18),
                label: Text(a.hasTags ? 'Locate' : 'No tag linked'),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: widget.onViewAsset,
                icon: const Icon(Icons.open_in_new_rounded, size: 18),
                label: const Text('View Asset'),
              ),
            ),
          ],
        ),
        if (widget.onShowOnMap != null) ...[
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: widget.onShowOnMap,
            icon: const Icon(Icons.my_location_rounded, size: 18),
            label: const Text('Show on map'),
          ),
        ],
      ],
    );
  }
}

extension on String {
  String ifEmpty(String other) => isEmpty ? other : this;
}

class _Comparison extends StatelessWidget {
  const _Comparison({required this.asset});

  final RtlAsset asset;

  @override
  Widget build(BuildContext context) {
    final a = asset;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text('Location Comparison', style: AppText.cardTitle)),
              StatusChip(a.comparisonStatus, tone: rtlComparisonTone(a.comparisonStatus)),
            ],
          ),
          const SizedBox(height: 10),
          InfoRow(label: 'Detected', value: a.location?.path.ifEmpty('Unknown') ?? 'Unknown'),
          InfoRow(label: 'Registered', value: a.expected?.path.ifEmpty('Not set') ?? 'Not set'),
          if (a.comparisonNote.isNotEmpty) Text(a.comparisonNote, style: AppText.caption),
        ],
      ),
    );
  }
}

/// Range chips + zone segments + time-in-zone bars + the backend note.
class _Trail extends ConsumerWidget {
  const _Trail({required this.assetId, required this.range, required this.onRange});

  final String assetId, range;
  final ValueChanged<String> onRange;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(rtlTrailProvider((assetId: assetId, range: range)));
    final time = DateFormat('HH:mm');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Trail', style: AppText.cardTitle),
        const SizedBox(height: 8),
        Wrap(
          spacing: 6,
          children: [
            for (final r in RtlApi.trailRanges)
              ChoiceChip(
                key: ValueKey('trail-$r'),
                label: Text(r),
                selected: r == range,
                onSelected: (_) => onRange(r),
              ),
          ],
        ),
        const SizedBox(height: 10),
        async.when(
          loading: () => const ListSkeleton(rows: 2),
          error: (e, _) {
            final x = ApiException.from(e);
            if (x.kind == ApiErrorKind.notFound) {
              return Text('This asset has no tracked tag', style: AppText.secondary);
            }
            return LoadErrorState(
              error: e,
              onRetry: () => ref.invalidate(rtlTrailProvider((assetId: assetId, range: range))),
            );
          },
          data: (t) {
            final maxMin = t.timeInZones.fold<double>(0, (m, z) => z.minutes > m ? z.minutes : m);
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (t.segments.isEmpty)
                  Text('No zone readings in this range', style: AppText.secondary)
                else
                  for (final s in t.segments)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Icon(Icons.circle, size: 9, color: AppColors.primary),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  [s.zoneName ?? 'Unknown zone', if (s.floorName != null) s.floorName!].join(' · '),
                                  style: AppText.bodyStrong,
                                ),
                                Text(
                                  [
                                    if (s.from != null && s.to != null)
                                      '${time.format(s.from!.toLocal())}–${time.format(s.to!.toLocal())}',
                                    '${s.minutes.toStringAsFixed(s.minutes < 10 ? 1 : 0)} min',
                                    if (s.initial) 'already there',
                                  ].join(' · '),
                                  style: AppText.caption,
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                if (t.timeInZones.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Text('Time in zones', style: AppText.bodyStrong),
                  const SizedBox(height: 6),
                  for (final z in t.timeInZones)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Row(
                        children: [
                          SizedBox(
                            width: 96,
                            child: Text(z.zone, maxLines: 1, overflow: TextOverflow.ellipsis, style: AppText.caption),
                          ),
                          Expanded(
                            child: ClipRRect(
                              borderRadius: BorderRadius.circular(4),
                              child: LinearProgressIndicator(
                                value: maxMin == 0 ? 0 : z.minutes / maxMin,
                                minHeight: 8,
                                backgroundColor: AppColors.tileLavender,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text('${z.minutes.toStringAsFixed(z.minutes < 10 ? 1 : 0)} min', style: AppText.caption),
                        ],
                      ),
                    ),
                ],
                if (t.note.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(t.note, style: AppText.caption.copyWith(fontStyle: FontStyle.italic)),
                ],
              ],
            );
          },
        ),
      ],
    );
  }
}
