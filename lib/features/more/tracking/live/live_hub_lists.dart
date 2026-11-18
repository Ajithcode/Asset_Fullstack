import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/home/dash_ui.dart' show dashTimeAgo, dashNumber;
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

// ---------------------------------------------------------------- Asset List

/// One Tracking service record (map zone lists). Tap → Asset Tracking.
class TrackedAssetRow extends StatelessWidget {
  const TrackedAssetRow({super.key, required this.asset, required this.now});

  final TrackedAsset asset;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final a = asset;
    final where = a.whereLabel;
    return AssetListTile(
      title: a.title,
      subtitle: a.subtitle,
      location: where.isEmpty ? null : where,
      fallbackIcon: a.devices.isNotEmpty ? Icons.sensors_rounded : Icons.nfc_rounded,
      titleTrailing: FreshnessChip(freshness: a.freshness(now)),
      details: [TrackedFactsLine(asset: a, now: now)],
      onTap: () => context.push(Routes.trackAsset(a.key)),
    );
  }
}

// ---------------------------------------------------------------- Sensors

/// GET /api/sensors with type / status chips (option lists from /sensors/types and
/// /sensors/statuses). Tap → sensor detail.
class LiveSensorsTab extends ConsumerStatefulWidget {
  const LiveSensorsTab({super.key});

  @override
  ConsumerState<LiveSensorsTab> createState() => _LiveSensorsTabState();
}

class _LiveSensorsTabState extends ConsumerState<LiveSensorsTab> {
  String? _type;
  String? _status;
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final sensors = ref.watch(trkSensorsProvider);
    final types = ref.watch(trkSensorTypesProvider).value ?? const <TrkSensorType>[];
    final statuses = ref.watch(trkSensorStatusesProvider).value ?? TrkSensorStatus.all;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SearchFilterBar(hint: 'Search sensor, device or asset...', onChanged: (v) => setState(() => _query = v)),
        const SizedBox(height: 10),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              IconFilterChip(label: 'All types', selected: _type == null, onTap: () => setState(() => _type = null)),
              for (final t in types)
                Padding(
                  padding: const EdgeInsets.only(left: 8),
                  child: IconFilterChip(
                    label: t.sensorType,
                    selected: _type == t.sensorType,
                    onTap: () => setState(() => _type = _type == t.sensorType ? null : t.sensorType),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 8),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              IconFilterChip(
                label: 'Any status',
                selected: _status == null,
                onTap: () => setState(() => _status = null),
              ),
              for (final s in statuses)
                Padding(
                  padding: const EdgeInsets.only(left: 8),
                  child: IconFilterChip(
                    label: s,
                    selected: _status == s,
                    onTap: () => setState(() => _status = _status == s ? null : s),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        asyncBody(
          sensors,
          onRetry: () => ref.invalidate(trkSensorsProvider),
          builder: (all) {
            final q = _query.trim().toLowerCase();
            final list = [
              for (final s in all)
                if ((_type == null || s.sensorType == _type) &&
                    (_status == null || s.status == _status) &&
                    (q.isEmpty ||
                        [
                          s.sensorId,
                          s.name,
                          s.deviceId,
                          s.assetName,
                          s.assetCode,
                          s.measurementPoint,
                        ].any((v) => (v ?? '').toLowerCase().contains(q))))
                  s,
            ];
            if (all.isEmpty) {
              return const EmptyState(icon: Icons.sensors_off_rounded, title: 'No sensors registered yet');
            }
            if (list.isEmpty) return const EmptyState(icon: Icons.search_off_rounded, title: 'No sensors match');
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                AppCard(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: [
                      for (var i = 0; i < list.length; i++) ...[
                        if (i > 0) const Divider(height: 1, indent: 64),
                        SensorRow(sensor: list[i]),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  'Showing ${list.length} of ${all.length} sensors',
                  textAlign: TextAlign.center,
                  style: AppText.caption,
                ),
              ],
            );
          },
        ),
        const SizedBox(height: 16),
        const IotEventsCard(),
      ],
    );
  }
}

class SensorRow extends StatelessWidget {
  const SensorRow({super.key, required this.sensor, this.showAsset = true});

  final TrkSensor sensor;
  final bool showAsset;

  @override
  Widget build(BuildContext context) {
    final s = sensor;
    final bad = s.outOfRange;
    return ListTile(
      onTap: () => context.push(Routes.trackSensor(s.sensorId)),
      leading: CircleAvatar(
        backgroundColor: AppColors.tileLavender,
        child: Icon(sensorTypeIcon(s.sensorType), color: AppColors.primary, size: 20),
      ),
      title: Text(
        '${s.title} · ${s.sensorId}',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: AppText.bodyStrong,
      ),
      subtitle: Text(
        [
          s.measuredParameter,
          if (s.measurementPoint != null) s.measurementPoint!,
          if (showAsset && (s.assetName ?? s.assetCode) != null) (s.assetName ?? s.assetCode)!,
          if (s.deviceId != null) s.deviceId!,
        ].join(' · '),
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
        style: AppText.caption,
      ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(
            readingText(s.liveReading, s.unit),
            style: AppText.bodyStrong.copyWith(color: bad ? AppColors.red : AppColors.text),
          ),
          const SizedBox(height: 4),
          StatusChip(s.status ?? '—', tone: sensorStatusTone(s.status)),
        ],
      ),
    );
  }
}

/// GET /api/iot-events/timeline: org-wide IoT activity per day (the server has no site / asset
/// filter for it).
class IotEventsCard extends ConsumerStatefulWidget {
  const IotEventsCard({super.key});

  @override
  ConsumerState<IotEventsCard> createState() => _IotEventsCardState();
}

class _IotEventsCardState extends ConsumerState<IotEventsCard> {
  int _days = 7;

  @override
  Widget build(BuildContext context) {
    final v = ref.watch(trkTimelineProvider(_days));
    return SectionCard(
      title: 'IoT Events · all sites',
      actionLabel: '$_days days',
      onAction: () => setState(
        () => _days = switch (_days) {
          7 => 30,
          30 => 90,
          _ => 7,
        },
      ),
      child: asyncBody(
        v,
        rows: 2,
        onRetry: () => ref.invalidate(trkTimelineProvider(_days)),
        builder: (t) {
          final rows = [
            (Icons.sensors_rounded, 'Telemetry', t.totals.telemetryData, t.points.map((p) => p.telemetryData)),
            (
              Icons.place_outlined,
              'Location updates',
              t.totals.locationUpdates,
              t.points.map((p) => p.locationUpdates),
            ),
            (Icons.fence_rounded, 'Geofence events', t.totals.geofenceEvents, t.points.map((p) => p.geofenceEvents)),
            (Icons.warning_amber_rounded, 'Alerts', t.totals.alerts, t.points.map((p) => p.alerts)),
          ];
          return Column(
            children: [
              for (final (icon, label, total, series) in rows)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 5),
                  child: Row(
                    children: [
                      Icon(icon, size: 18, color: AppColors.primary),
                      const SizedBox(width: 8),
                      Expanded(child: Text(label, style: AppText.body)),
                      SizedBox(width: 90, child: Sparkline(values: [for (final n in series) n.toDouble()])),
                      const SizedBox(width: 10),
                      SizedBox(
                        width: 54,
                        child: Text(dashNumber.format(total), textAlign: TextAlign.end, style: AppText.bodyStrong),
                      ),
                    ],
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}

// ---------------------------------------------------------------- Geofencing

/// Not shown at the moment: the Geofencing tab shows the live map for now. Kept for when the
/// geofence list returns.
/// View-only: Tracking GPS geofences and Administration floor-plan zones flagged as geofence,
/// each with its entry / exit / dwell alert flags. Tap → the right map, focused on it.
class LiveGeofencingTab extends ConsumerWidget {
  const LiveGeofencingTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final gps = ref.watch(trkGeofencesProvider);
    final zones = ref.watch(geofenceZonesProvider);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SectionHeader(title: 'Outdoor geofences (GPS)'),
        const SizedBox(height: 8),
        asyncBody(
          gps,
          onRetry: () => ref.invalidate(trkGeofencesProvider),
          builder: (list) => list.isEmpty
              ? const AppCard(
                  child: EmptyState(icon: Icons.fence_rounded, title: 'No GPS geofences'),
                )
              : AppCard(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: [
                      for (var i = 0; i < list.length; i++) ...[
                        if (i > 0) const Divider(height: 1, indent: 16),
                        _FenceTile(
                          title: list[i].name,
                          subtitle: [
                            list[i].siteName,
                            list[i].geofenceType,
                            list[i].shape,
                            if (list[i].monitoredAssetCount > 0) '${list[i].monitoredAssetCount} assets',
                          ].where((s) => s.isNotEmpty).join(' · '),
                          color: hexColor(list[i].mapColour, AppColors.primary),
                          active: list[i].isActive,
                          entry: list[i].monitorEntry,
                          exit: list[i].monitorExit,
                          dwellMinutes: list[i].dwellAlertMinutes ?? 0,
                          lastEvent: list[i].lastEventAt,
                          onTap: () => context.go(Routes.trackingMap(view: 'outdoor', geofence: list[i].id)),
                        ),
                      ],
                    ],
                  ),
                ),
        ),
        const SizedBox(height: 16),
        const SectionHeader(title: 'Floor-plan geofence zones'),
        const SizedBox(height: 8),
        asyncBody(
          zones,
          onRetry: () => ref.invalidate(geofenceZonesProvider),
          builder: (list) => list.isEmpty
              ? const AppCard(
                  child: EmptyState(
                    icon: Icons.layers_clear_outlined,
                    title: 'No geofence zones on published floor plans',
                  ),
                )
              : AppCard(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: [
                      for (var i = 0; i < list.length; i++) ...[
                        if (i > 0) const Divider(height: 1, indent: 16),
                        _FenceTile(
                          title: list[i].zone.label,
                          subtitle: [
                            list[i].plan.locationPath ?? list[i].plan.name,
                            list[i].zone.type,
                            if (list[i].zone.accessRule != null) list[i].zone.accessRule!,
                          ].where((s) => s.isNotEmpty).join(' · '),
                          color: hexColor(list[i].zone.color, AppColors.primary),
                          active: list[i].zone.active,
                          entry: list[i].zone.entryAlert,
                          exit: list[i].zone.exitAlert,
                          dwellMinutes: list[i].zone.dwellAlert ? list[i].zone.dwellMinutes : 0,
                          onTap: () => context.go(
                            Routes.trackingMap(
                              view: 'floor',
                              location: list[i].plan.locationId,
                              zone: list[i].zone.zoneId,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
        ),
      ],
    );
  }
}

class _FenceTile extends StatelessWidget {
  const _FenceTile({
    required this.title,
    required this.subtitle,
    required this.color,
    required this.active,
    required this.entry,
    required this.exit,
    required this.dwellMinutes,
    required this.onTap,
    this.lastEvent,
  });

  final String title, subtitle;
  final Color color;
  final bool active, entry, exit;
  final int dwellMinutes;
  final DateTime? lastEvent;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => InkWell(
    onTap: onTap,
    child: Padding(
      padding: const EdgeInsets.fromLTRB(14, 12, 10, 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 34,
            height: 34,
            decoration: BoxDecoration(color: color.withValues(alpha: .15), borderRadius: BorderRadius.circular(8)),
            child: Icon(Icons.fence_rounded, color: color, size: 20),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: AppText.bodyStrong),
                if (subtitle.isNotEmpty) Text(subtitle, style: AppText.caption),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 6,
                  runSpacing: 4,
                  children: [
                    StatusChip(active ? 'Active' : 'Disabled', tone: active ? ChipTone.green : ChipTone.grey),
                    if (entry) const StatusChip('Entry alert', tone: ChipTone.blue),
                    if (exit) const StatusChip('Exit alert', tone: ChipTone.orange),
                    if (dwellMinutes > 0) StatusChip('Dwell $dwellMinutes min', tone: ChipTone.purple),
                    if (!entry && !exit && dwellMinutes == 0) const StatusChip('No alerts', tone: ChipTone.grey),
                  ],
                ),
                if (lastEvent != null) ...[
                  const SizedBox(height: 4),
                  Text('Last event ${dashTimeAgo(lastEvent)}', style: AppText.caption),
                ],
              ],
            ),
          ),
          Icon(Icons.map_outlined, color: AppColors.grey, size: 20),
        ],
      ),
    ),
  );
}
