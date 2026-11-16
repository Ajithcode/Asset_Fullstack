import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/live_assets_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/home/dash_ui.dart' show dashTimeAgo, openAssetByTag;
import 'package:asset360_mobile/features/more/tracking/live/asset_zone_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_hub_lists.dart' show IotEventsCard, SensorRow;
import 'package:asset360_mobile/features/more/tracking/live/live_map_tab.dart' show LocationEventRow;
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

/// Sensors of an asset: same registration id, or an asset code the asset is known by.
List<TrkSensor> sensorsOfAsset(List<TrkSensor> all, {String? assetRegistrationId, Set<String> keys = const {}}) {
  final id = assetRegistrationId?.toLowerCase();
  return [
    for (final s in all)
      if ((id != null && s.assetRegistrationId?.toLowerCase() == id) ||
          (s.assetCode != null && keys.contains(s.assetCode!.toLowerCase())))
        s,
  ];
}

/// The Master Data id whose published plan shows an asset: its floor, else building, else site.
String? planLocationOf(AssetRegistration? r) {
  final l = r?.locationAssignment.location;
  return l?.floorId ?? l?.buildingId ?? l?.siteId;
}

enum _Tab { live, telemetry, history, details }

/// API mode Asset Tracking (design 06, middle screen) for one tracked asset ([assetKey] =
/// registration id, or the location-event asset text for assets the Tracking service only knows
/// by name). Polls while visible.
class LiveAssetTrackingScreen extends ConsumerStatefulWidget {
  const LiveAssetTrackingScreen({super.key, required this.assetKey, this.initialTab});

  final String assetKey;
  final String? initialTab;

  @override
  ConsumerState<LiveAssetTrackingScreen> createState() => _LiveAssetTrackingScreenState();
}

class _LiveAssetTrackingScreenState extends ConsumerState<LiveAssetTrackingScreen> {
  late _Tab _tab = switch (widget.initialTab) {
    'telemetry' => _Tab.telemetry,
    'history' => _Tab.history,
    'details' => _Tab.details,
    _ => _Tab.live,
  };
  TrackRange _range = TrackRange.day;
  bool _replay = false;
  int _step = 0;
  Timer? _player;

  @override
  void dispose() {
    _player?.cancel();
    super.dispose();
  }

  void _togglePlay(int count) {
    if (_player != null) {
      _player!.cancel();
      setState(() => _player = null);
      return;
    }
    if (_step >= count - 1) _step = 0;
    _player = Timer.periodic(const Duration(milliseconds: 1200), (t) {
      if (!mounted) return t.cancel();
      if (_step >= count - 1) {
        t.cancel();
        setState(() => _player = null);
      } else {
        setState(() => _step++);
      }
    });
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final live = ref.watch(trkLiveProvider);
    final s = live.snapshot;
    final a = s?.byKey(widget.assetKey);
    final regId = a?.assetRegistrationId ?? (s != null && a == null ? widget.assetKey : null);
    final record = regId == null ? null : ref.watch(liveAssetProvider(regId)).value;
    final now = trackingNow();
    final trail = a == null
        ? const <TrkLocationEvent>[]
        : s!.eventsOf(a, window: _range.duration, now: now).reversed.toList();
    return LivePollingScope(
      child: AppScaffold(
        bottomActions: Row(
          children: [
            Expanded(
              child: OutlinedButton.icon(
                icon: Icon(_replay ? Icons.my_location_rounded : Icons.history_rounded),
                label: Text(_replay ? 'Live Position' : 'View History'),
                onPressed: () => setState(() {
                  _player?.cancel();
                  _player = null;
                  _replay = !_replay;
                  _tab = _Tab.live;
                  _step = trail.isEmpty ? 0 : trail.length - 1;
                }),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: FilledButton.icon(
                icon: const Icon(Icons.open_in_new_rounded),
                label: const Text('View Asset'),
                onPressed: () => _openAsset(a),
              ),
            ),
          ],
        ),
        body: RefreshIndicator(
          onRefresh: () async => refreshTracking(ref),
          child: ListView(
            padding: context.pagePadding,
            children: [
              PageTitle(
                title: 'Asset Tracking',
                subtitle: 'Real-time location and live telemetry',
                leading: BackButton(onPressed: () => context.pop()),
              ),
              const SizedBox(height: 12),
              if (s == null && live.error != null)
                LoadErrorState(error: live.error!, onRetry: () => ref.read(trkLiveProvider.notifier).refresh())
              else if (s == null)
                const ListSkeleton(rows: 4, card: false)
              else if (a == null)
                AppCard(
                  child: EmptyState(
                    icon: Icons.sensors_off_rounded,
                    title: 'No tracking data for this asset',
                    message: 'No tag, IoT device or location event names ${widget.assetKey}.',
                  ),
                )
              else ...[
                _Header(asset: a, record: record, now: now, onTap: () => _openAsset(a)),
                const SizedBox(height: 12),
                SegmentTabs(
                  tabs: const [
                    SegmentTab('Live Tracking'),
                    SegmentTab('Telemetry'),
                    SegmentTab('History'),
                    SegmentTab('Details'),
                  ],
                  selected: _tab.index,
                  onChanged: (i) => setState(() => _tab = _Tab.values[i]),
                ),
                const SizedBox(height: 12),
                ...switch (_tab) {
                  _Tab.live => _liveTab(a, record, trail, now),
                  _Tab.telemetry => [_TelemetryTab(asset: a)],
                  _Tab.history => [_HistoryTab(asset: a, snapshot: s, now: now)],
                  _Tab.details => [_DetailsTab(asset: a, now: now)],
                },
              ],
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _liveTab(TrackedAsset a, AssetRegistration? record, List<TrkLocationEvent> trail, DateTime now) {
    final step = trail.isEmpty ? null : trail[_step.clamp(0, trail.length - 1)];
    final zoneName = _replay ? (step?.placesAssetInZone ?? false ? step!.zoneName : null) : a.zone?.zoneName;
    return [
      RangeChips(
        value: _range,
        onChanged: (r) => setState(() {
          _range = r;
          _step = 0;
          _player?.cancel();
          _player = null;
        }),
      ),
      const SizedBox(height: 10),
      AssetZoneMap(
        locationId: planLocationOf(record),
        asset: a,
        zoneName: zoneName,
        trail: _replay && step != null ? trail.sublist(0, _step + 1) : trail,
      ),
      const SizedBox(height: 8),
      if (_replay)
        _Replay(
          trail: trail,
          step: _step,
          playing: _player != null,
          onStep: (i) => setState(() => _step = i),
          onPlay: () => _togglePlay(trail.length),
        )
      else
        const LiveUpdatedBar(),
      const SizedBox(height: 10),
      _PositionTiles(asset: a, now: now),
      const SizedBox(height: 14),
      SectionHeader(title: 'Route (${trail.length} zone events, last ${_range.label})'),
      const SizedBox(height: 8),
      if (trail.isEmpty)
        AppCard(
          child: EmptyState(icon: Icons.route_outlined, title: 'No zone events in the last ${_range.label}'),
        )
      else
        AppCard(
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Column(
            children: [
              for (final (i, e) in trail.reversed.indexed)
                Row(
                  children: [
                    CircleAvatar(
                      radius: 11,
                      backgroundColor: AppColors.primary,
                      child: Text('${trail.length - i}', style: const TextStyle(color: Colors.white, fontSize: 10)),
                    ),
                    const SizedBox(width: 6),
                    Expanded(child: LocationEventRow(event: e, showAsset: false)),
                  ],
                ),
            ],
          ),
        ),
    ];
  }

  void _openAsset(TrackedAsset? a) {
    final id = a?.assetRegistrationId;
    if (id != null) {
      context.push(Routes.assetDetail(id));
    } else {
      openAssetByTag(context, ref, a?.code ?? widget.assetKey);
    }
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.asset, required this.record, required this.now, required this.onTap});

  final TrackedAsset asset;
  final AssetRegistration? record;
  final DateTime now;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final a = asset;
    final loc = record?.locationAssignment.location.locationPath;
    return AppCard(
      onTap: onTap,
      child: Row(
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(color: AppColors.tileLavender, borderRadius: BorderRadius.circular(12)),
            child: Icon(Icons.sensors_rounded, color: AppColors.primary, size: 28),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(a.subtitle, style: AppText.bodyStrong.copyWith(fontSize: 16)),
                    FreshnessChip(freshness: a.freshness(now)),
                  ],
                ),
                if (a.name.isNotEmpty) Text(a.name, style: AppText.secondary),
                const SizedBox(height: 4),
                Row(
                  children: [
                    Icon(Icons.location_on_outlined, size: 14, color: AppColors.grey),
                    const SizedBox(width: 3),
                    Expanded(
                      child: Text(
                        a.zone != null
                            ? 'In ${a.zone!.zoneName}'
                            : (loc ?? (a.whereLabel.isEmpty ? 'Zone unknown' : a.whereLabel)),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppText.caption,
                      ),
                    ),
                  ],
                ),
                Text(lastSeenText(a.lastSeen), style: AppText.caption),
              ],
            ),
          ),
          Icon(Icons.chevron_right_rounded, color: AppColors.grey),
        ],
      ),
    );
  }
}

/// Zone / since / battery / signal / devices tiles.
class _PositionTiles extends StatelessWidget {
  const _PositionTiles({required this.asset, required this.now});

  final TrackedAsset asset;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final a = asset;
    final sig = a.signal;
    final tiles = [
      (Icons.place_outlined, 'Zone', a.zone?.zoneName ?? 'Unknown'),
      (Icons.schedule_rounded, 'In zone since', a.zone == null ? '—' : dashTimeAgo(a.zone!.since)),
      (Icons.battery_5_bar_rounded, 'Battery', a.battery == null ? '—' : '${a.battery}%'),
      (
        Icons.signal_cellular_alt_rounded,
        'Signal',
        sig == null ? '—' : (sig.signalStrengthDbm != null ? '${sig.signalStrengthDbm} dBm' : sig.signalQuality ?? '—'),
      ),
      (Icons.router_outlined, 'Devices / tags', '${a.devices.length} / ${a.tags.length}'),
      (Icons.wifi_tethering_rounded, 'Last seen', a.lastSeen == null ? 'Never' : dashTimeAgo(a.lastSeen)),
    ];
    return GridView.count(
      crossAxisCount: 3,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 8,
      crossAxisSpacing: 8,
      childAspectRatio: 1.1,
      children: [
        for (final (icon, label, value) in tiles)
          AppCard(
            padding: const EdgeInsets.all(10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(icon, size: 20, color: AppColors.primary),
                const SizedBox(height: 4),
                Text(label, style: AppText.caption.copyWith(fontSize: 10.5)),
                Text(
                  value,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppText.bodyStrong.copyWith(fontSize: 13),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

/// Time slider over the trail (oldest → newest) with play / pause.
class _Replay extends StatelessWidget {
  const _Replay({
    required this.trail,
    required this.step,
    required this.playing,
    required this.onStep,
    required this.onPlay,
  });

  final List<TrkLocationEvent> trail;
  final int step;
  final bool playing;
  final ValueChanged<int> onStep;
  final VoidCallback onPlay;

  @override
  Widget build(BuildContext context) {
    if (trail.isEmpty) {
      return Text('No zone events to replay in this period.', style: AppText.caption);
    }
    final e = trail[step.clamp(0, trail.length - 1)];
    final fmt = DateFormat('d MMM, HH:mm');
    return AppCard(
      padding: const EdgeInsets.fromLTRB(6, 6, 12, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              IconButton(
                tooltip: playing ? 'Pause' : 'Play',
                onPressed: trail.length < 2 ? null : onPlay,
                icon: Icon(
                  playing ? Icons.pause_circle_filled_rounded : Icons.play_circle_fill_rounded,
                  color: AppColors.primary,
                  size: 32,
                ),
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${step + 1} of ${trail.length} · ${fmt.format(e.eventTime.toLocal())}',
                      style: AppText.bodyStrong,
                    ),
                    Text('${e.eventType} · ${e.zoneName}', style: AppText.caption),
                  ],
                ),
              ),
            ],
          ),
          if (trail.length > 1)
            Slider(
              value: step.clamp(0, trail.length - 1).toDouble(),
              max: (trail.length - 1).toDouble(),
              divisions: trail.length - 1,
              label: fmt.format(e.eventTime.toLocal()),
              onChanged: (v) => onStep(v.round()),
            ),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(fmt.format(trail.first.eventTime.toLocal()), style: AppText.caption),
              Text(fmt.format(trail.last.eventTime.toLocal()), style: AppText.caption),
            ],
          ),
        ],
      ),
    );
  }
}

/// Latest reading tiles of the asset's sensors + the history chart of the chosen one.
class _TelemetryTab extends ConsumerStatefulWidget {
  const _TelemetryTab({required this.asset});

  final TrackedAsset asset;

  @override
  ConsumerState<_TelemetryTab> createState() => _TelemetryTabState();
}

class _TelemetryTabState extends ConsumerState<_TelemetryTab> {
  String? _sensorId;
  TrackRange _range = TrackRange.day;

  @override
  Widget build(BuildContext context) {
    final a = widget.asset;
    return asyncBody(
      ref.watch(trkSensorsProvider),
      onRetry: () => ref.invalidate(trkSensorsProvider),
      builder: (all) {
        final sensors = sensorsOfAsset(all, assetRegistrationId: a.assetRegistrationId, keys: a.matchKeys);
        if (sensors.isEmpty) {
          return const AppCard(
            child: EmptyState(icon: Icons.sensors_off_rounded, title: 'No sensors linked to this asset'),
          );
        }
        final chosen = sensors.where((s) => s.sensorId == _sensorId).firstOrNull ?? sensors.first;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SensorTiles(
              sensors: sensors,
              selectedId: chosen.sensorId,
              onTap: (s) => setState(() => _sensorId = s.sensorId),
            ),
            const SizedBox(height: 14),
            SensorHistoryCard(sensor: chosen, range: _range, onRange: (r) => setState(() => _range = r)),
          ],
        );
      },
    );
  }
}

/// 3-column tiles: sensor name, latest value, status (red when outside the alarm limits).
class SensorTiles extends StatelessWidget {
  const SensorTiles({super.key, required this.sensors, this.selectedId, required this.onTap});

  final List<TrkSensor> sensors;
  final String? selectedId;
  final ValueChanged<TrkSensor> onTap;

  @override
  Widget build(BuildContext context) => GridView.count(
    crossAxisCount: 3,
    shrinkWrap: true,
    physics: const NeverScrollableScrollPhysics(),
    mainAxisSpacing: 8,
    crossAxisSpacing: 8,
    childAspectRatio: 1.05,
    children: [
      for (final s in sensors)
        Material(
          color: AppColors.surface,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
            side: BorderSide(
              color: s.sensorId == selectedId ? AppColors.primary : AppColors.border,
              width: s.sensorId == selectedId ? 1.5 : 1,
            ),
          ),
          child: InkWell(
            borderRadius: BorderRadius.circular(12),
            onTap: () => onTap(s),
            child: Padding(
              padding: const EdgeInsets.all(10),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(
                        sensorTypeIcon(s.sensorType),
                        size: 18,
                        color: s.outOfRange ? AppColors.red : AppColors.primary,
                      ),
                      const SizedBox(width: 4),
                      Expanded(
                        child: Text(
                          s.measuredParameter.isEmpty ? s.title : s.measuredParameter,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: AppText.caption.copyWith(fontSize: 10.5),
                        ),
                      ),
                    ],
                  ),
                  const Spacer(),
                  Text(
                    readingText(s.liveReading, s.unit),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppText.bodyStrong.copyWith(
                      fontSize: 15,
                      color: s.outOfRange ? AppColors.red : AppColors.text,
                    ),
                  ),
                  Text(s.status ?? '—', style: AppText.caption.copyWith(fontSize: 10)),
                ],
              ),
            ),
          ),
        ),
    ],
  );
}

/// "Sensor History" chart of one sensor for 1 h / 24 h / 7 d, with "Open sensor".
class SensorHistoryCard extends ConsumerWidget {
  const SensorHistoryCard({super.key, required this.sensor, required this.range, required this.onRange});

  final TrkSensor sensor;
  final TrackRange range;
  final ValueChanged<TrackRange> onRange;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final q = (sensorId: sensor.sensorId, deviceId: null, range: range);
    return SectionCard(
      title: '${sensor.title} history',
      actionLabel: 'Open sensor',
      onAction: () => context.push(Routes.trackSensor(sensor.sensorId)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          RangeChips(value: range, onChanged: onRange),
          const SizedBox(height: 10),
          asyncBody(
            ref.watch(trkReadingsProvider(q)),
            rows: 2,
            onRetry: () => ref.invalidate(trkReadingsProvider(q)),
            builder: (rows) => ReadingsChart(readings: rows, range: range, unit: sensor.unit ?? ''),
          ),
        ],
      ),
    );
  }
}

/// Activity: the asset's zone events (7 d) and its IoT alerts, newest first; then the org-wide
/// IoT events timeline (the server has no per-asset timeline).
class _HistoryTab extends ConsumerWidget {
  const _HistoryTab({required this.asset, required this.snapshot, required this.now});

  final TrackedAsset asset;
  final TrackingSnapshot snapshot;
  final DateTime now;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final events = snapshot.eventsOf(asset, window: keepEventsFor, now: now);
    final id = asset.assetRegistrationId;
    final alertsV = id == null ? null : ref.watch(trkAssetAlertsProvider(id));
    final alerts = alertsV?.value ?? const <TrkAlert>[];
    final rows = <(DateTime, Widget)>[
      for (final e in events) (e.eventTime, LocationEventRow(event: e, showAsset: false)),
      for (final al in alerts) (al.raisedAt, AlertRow(alert: al, showAsset: false)),
    ]..sort((a, b) => b.$1.compareTo(a.$1));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SectionHeader(title: 'Activity (last 7 days)'),
        const SizedBox(height: 8),
        if (alertsV != null && alertsV.hasError)
          InlineRetry(
            text: 'Alerts could not be loaded',
            error: true,
            onRetry: () => ref.invalidate(trkAssetAlertsProvider(id!)),
          ),
        if (rows.isEmpty)
          const AppCard(
            child: EmptyState(icon: Icons.timeline_rounded, title: 'No activity in the last 7 days'),
          )
        else
          AppCard(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Column(children: [for (final r in rows) r.$2]),
          ),
        const SizedBox(height: 14),
        const IotEventsCard(),
      ],
    );
  }
}

class _DetailsTab extends StatelessWidget {
  const _DetailsTab({required this.asset, required this.now});

  final TrackedAsset asset;
  final DateTime now;

  @override
  Widget build(BuildContext context) => LinkedDevicesCard(asset: asset, now: now);
}

/// Devices, tags and sensors linked to an asset.
class LinkedDevicesCard extends StatelessWidget {
  const LinkedDevicesCard({super.key, required this.asset, required this.now, this.sensors = const []});

  final TrackedAsset asset;
  final DateTime now;
  final List<TrkSensor> sensors;

  @override
  Widget build(BuildContext context) {
    final a = asset;
    if (a.devices.isEmpty && a.tags.isEmpty && sensors.isEmpty) {
      return const AppCard(
        child: EmptyState(icon: Icons.link_off_rounded, title: 'No devices or tags linked'),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final d in a.devices) ...[
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(Icons.router_rounded, color: AppColors.primary),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        '${d.deviceName.isEmpty ? d.deviceType : d.deviceName} · ${d.deviceId}',
                        style: AppText.bodyStrong,
                      ),
                    ),
                    FreshnessChip(freshness: freshnessOf(d.lastSeenAt, now)),
                  ],
                ),
                const SizedBox(height: 6),
                InfoRow(label: 'Type / model', value: [d.deviceType, d.model].where((s) => s.isNotEmpty).join(' · ')),
                InfoRow(label: 'Connectivity', value: d.connectivity.isEmpty ? '—' : d.connectivity),
                if (d.gateway != null) InfoRow(label: 'Gateway', value: d.gateway!),
                InfoRow(
                  label: 'Power',
                  value: [d.powerSource, if (d.batteryLevel != null) '${d.batteryLevel}%'].join(' · '),
                ),
                InfoRow(label: 'Health', value: d.health ?? '—'),
                if (d.firmwareVersion != null) InfoRow(label: 'Firmware', value: d.firmwareVersion!),
                InfoRow(label: 'Last seen', value: d.lastSeenAt == null ? 'Never' : dashTimeAgo(d.lastSeenAt)),
                for (final h in a.health.where((h) => h.deviceId == d.deviceId))
                  InfoRow(
                    label: 'Signal',
                    value: [
                      if (h.signalStrengthDbm != null) '${h.signalStrengthDbm} dBm',
                      if (h.snrDb != null) 'SNR ${h.snrDb} dB',
                      ?h.signalQuality,
                    ].join(' · '),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 8),
        ],
        for (final t in a.tags) ...[
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(Icons.nfc_rounded, color: AppColors.primary),
                    const SizedBox(width: 8),
                    Expanded(child: Text('Tag ${t.tagId}', style: AppText.bodyStrong)),
                    StatusChip(t.status ?? '—', tone: ChipTone.blue),
                  ],
                ),
                const SizedBox(height: 6),
                InfoRow(label: 'Technology', value: [t.technology, t.tagType].where((s) => s.isNotEmpty).join(' · ')),
                if (t.locationName != null || t.siteName != null)
                  InfoRow(label: 'Location', value: [?t.siteName, ?t.locationName].join(' · ')),
                InfoRow(label: 'Last seen', value: t.lastSeenAt == null ? 'Never' : dashTimeAgo(t.lastSeenAt)),
              ],
            ),
          ),
          const SizedBox(height: 8),
        ],
        if (sensors.isNotEmpty)
          AppCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                for (final (i, s) in sensors.indexed) ...[
                  if (i > 0) const Divider(height: 1, indent: 64),
                  SensorRow(sensor: s, showAsset: false),
                ],
              ],
            ),
          ),
      ],
    );
  }
}

/// One IoT alert row; tap → the asset (when the alert names one).
class AlertRow extends StatelessWidget {
  const AlertRow({super.key, required this.alert, this.showAsset = true});

  final TrkAlert alert;
  final bool showAsset;

  @override
  Widget build(BuildContext context) {
    final al = alert;
    final id = al.assetRegistrationId;
    return ListTile(
      contentPadding: EdgeInsets.zero,
      onTap: id == null
          ? () => context.showSnack('Alert ${al.alertId} names no registered asset')
          : () => context.push(Routes.assetDetail(id)),
      leading: Icon(Icons.warning_rounded, color: alertSeverityTone(al.severity).foreground),
      title: Text(
        showAsset ? [al.assetName, al.eventType].where((s) => s.isNotEmpty).join(' · ') : al.eventType,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: AppText.bodyStrong,
      ),
      subtitle: Text(
        [?al.message, ?al.siteName, al.alertId].where((s) => s.isNotEmpty).join(' · '),
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
        style: AppText.caption,
      ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(dashTimeAgo(al.raisedAt), style: AppText.caption),
          const SizedBox(height: 3),
          StatusChip(al.status ?? al.severity, tone: al.isOpen ? alertSeverityTone(al.severity) : ChipTone.green),
        ],
      ),
    );
  }
}
