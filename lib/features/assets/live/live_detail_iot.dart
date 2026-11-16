import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/models/asset_registration.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/home/dash_ui.dart' show dashTimeAgo;
import 'package:asset360_mobile/features/more/tracking/live/live_asset_tracking_screen.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

/// API mode Asset detail → IoT tab: the asset's devices / tags and sensors (Tracking service),
/// their latest readings, a 24 h chart, and the last known (zone-level) position with
/// "Show on map". Matches the asset by registration id, or by asset tag where the Tracking
/// service only stores a code.
class LiveIotTab extends ConsumerStatefulWidget {
  const LiveIotTab({super.key, required this.asset});

  final AssetRegistration asset;

  @override
  ConsumerState<LiveIotTab> createState() => _LiveIotTabState();
}

class _LiveIotTabState extends ConsumerState<LiveIotTab> {
  String? _sensorId;
  TrackRange _range = TrackRange.day;

  @override
  Widget build(BuildContext context) {
    final id = widget.asset.assetRegistrationId;
    final tag = widget.asset.basicInformation.assetTag;
    final snapV = ref.watch(trackingSnapshotOnceProvider);
    final sensorsV = ref.watch(trkSensorsProvider);
    if ((snapV.hasError && !snapV.isLoading) || (sensorsV.hasError && !sensorsV.isLoading)) {
      return LoadErrorState(
        error: (snapV.error ?? sensorsV.error)!,
        onRetry: () => ref
          ..invalidate(trackingSnapshotOnceProvider)
          ..invalidate(trkSensorsProvider),
      );
    }
    if (!snapV.hasValue || !sensorsV.hasValue) return const ListSkeleton(rows: 3, card: false);
    final snap = snapV.requireValue;
    final tracked = snap.byKey(id) ?? (tag == null ? null : snap.byKey(tag));
    final keys = {...?tracked?.matchKeys, if (tag != null) tag.toLowerCase()};
    final sensors = sensorsOfAsset(sensorsV.requireValue, assetRegistrationId: id, keys: keys);
    final now = trackingNow();
    final chosen = sensors.where((s) => s.sensorId == _sensorId).firstOrNull ?? sensors.firstOrNull;
    final trackKey = tracked?.key ?? id;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionCard(
          title: 'Last known position',
          actionLabel: 'Show on map',
          onAction: () => context.push(Routes.trackAsset(trackKey)),
          child: tracked == null
              ? Text(
                  'The Tracking service has no tag, device or location event for this asset.',
                  style: AppText.caption,
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Icon(Icons.place_outlined, color: AppColors.primary),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            tracked.zone?.zoneName ??
                                (tracked.whereLabel.isEmpty ? 'Zone unknown' : tracked.whereLabel),
                            style: AppText.bodyStrong,
                          ),
                        ),
                        FreshnessChip(freshness: tracked.freshness(now)),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      [
                        if (tracked.zone != null) '${tracked.zone!.eventType} ${dashTimeAgo(tracked.zone!.since)}',
                        lastSeenText(tracked.lastSeen),
                        if (tracked.battery != null) 'Battery ${tracked.battery}%',
                      ].join(' · '),
                      style: AppText.caption,
                    ),
                    const SizedBox(height: 2),
                    Text('Zone-level position from the latest location event.', style: AppText.caption),
                  ],
                ),
        ),
        const SizedBox(height: 12),
        if (sensors.isEmpty)
          const AppCard(
            child: EmptyState(icon: Icons.sensors_off_rounded, title: 'No sensors linked to this asset'),
          )
        else ...[
          const SectionHeader(title: 'Latest readings'),
          const SizedBox(height: 8),
          SensorTiles(
            sensors: sensors,
            selectedId: chosen?.sensorId,
            onTap: (s) => setState(() => _sensorId = s.sensorId),
          ),
          const SizedBox(height: 12),
          SensorHistoryCard(sensor: chosen!, range: _range, onRange: (r) => setState(() => _range = r)),
        ],
        const SizedBox(height: 12),
        const SectionHeader(title: 'Linked devices & tags'),
        const SizedBox(height: 8),
        if (tracked == null)
          const AppCard(
            child: EmptyState(icon: Icons.link_off_rounded, title: 'No devices or tags linked'),
          )
        else
          LinkedDevicesCard(asset: tracked, now: now),
      ],
    );
  }
}
