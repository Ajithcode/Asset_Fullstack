import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_asset_tracking_screen.dart' show AlertRow;
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

// ---------------------------------------------------------------- sensor detail

/// API mode sensor detail: GET /api/sensors/{sensorId} + its readings
/// (GET /api/telemetry-readings?sensorId=&days=) for 1 h / 24 h / 7 d.
class LiveSensorScreen extends ConsumerStatefulWidget {
  const LiveSensorScreen({super.key, required this.sensorId});

  final String sensorId;

  @override
  ConsumerState<LiveSensorScreen> createState() => _LiveSensorScreenState();
}

class _LiveSensorScreenState extends ConsumerState<LiveSensorScreen> {
  TrackRange _range = TrackRange.day;

  @override
  Widget build(BuildContext context) {
    if (!apiMode) {
      return const AppScaffold(
        body: EmptyState(
          icon: Icons.sensors_rounded,
          title: 'Sensor details need the backend',
          message: 'Run with USE_API=true to open Tracking service sensors.',
        ),
      );
    }
    final v = ref.watch(trkSensorProvider(widget.sensorId));
    final q = (sensorId: widget.sensorId, deviceId: null, range: _range);
    final readings = ref.watch(trkReadingsProvider(q));
    return AppScaffold(
      body: RefreshIndicator(
        onRefresh: () async => ref
          ..invalidate(trkSensorProvider(widget.sensorId))
          ..invalidate(trkReadingsProvider(q)),
        child: ListView(
          padding: context.pagePadding,
          children: [
            PageTitle(
              title: 'Telemetry & IoT Sensors',
              subtitle: 'Real-time sensor data and condition monitoring',
              leading: BackButton(onPressed: () => context.pop()),
            ),
            const SizedBox(height: 12),
            asyncBody(
              v,
              onRetry: () => ref.invalidate(trkSensorProvider(widget.sensorId)),
              builder: (s) => Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _SensorHeader(sensor: s),
                  const SizedBox(height: 12),
                  SectionCard(
                    title: 'Sensor History',
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        RangeChips(value: _range, onChanged: (r) => setState(() => _range = r)),
                        const SizedBox(height: 10),
                        asyncBody(
                          readings,
                          rows: 2,
                          onRetry: () => ref.invalidate(trkReadingsProvider(q)),
                          builder: (rows) => Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              ReadingsChart(readings: rows, range: _range, unit: s.unit ?? '', height: 220),
                              if (rows.isNotEmpty) ...[const SizedBox(height: 10), _Stats(rows: rows, sensor: s)],
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  _Facts(sensor: s),
                  const SizedBox(height: 12),
                  _LatestReadings(readings: readings.value ?? const [], unit: s.unit),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SensorHeader extends StatelessWidget {
  const _SensorHeader({required this.sensor});

  final TrkSensor sensor;

  @override
  Widget build(BuildContext context) {
    final s = sensor;
    return AppCard(
      child: Row(
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(color: AppColors.tileLavender, borderRadius: BorderRadius.circular(12)),
            child: Icon(sensorTypeIcon(s.sensorType), color: AppColors.primary, size: 28),
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
                    Text(s.sensorId, style: AppText.bodyStrong.copyWith(fontSize: 16)),
                    StatusChip(s.status ?? '—', tone: sensorStatusTone(s.status)),
                  ],
                ),
                Text('${s.title} · ${s.measuredParameter}', style: AppText.secondary),
                if (s.measurementPoint != null) Text(s.measurementPoint!, style: AppText.caption),
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                readingText(s.liveReading, s.unit),
                style: AppText.bodyStrong.copyWith(fontSize: 18, color: s.outOfRange ? AppColors.red : AppColors.text),
              ),
              Text(s.outOfRange ? 'Out of range' : 'Live reading', style: AppText.caption),
            ],
          ),
        ],
      ),
    );
  }
}

class _Stats extends StatelessWidget {
  const _Stats({required this.rows, required this.sensor});

  final List<TrkReading> rows;
  final TrkSensor sensor;

  @override
  Widget build(BuildContext context) {
    final values = rows.map((r) => r.value);
    final min = values.reduce(math.min), max = values.reduce(math.max);
    final avg = values.reduce((a, b) => a + b) / rows.length;
    final cells = [
      ('Min', readingText(min, sensor.unit)),
      ('Avg', readingText(avg, sensor.unit)),
      ('Max', readingText(max, sensor.unit)),
      ('Readings', '${rows.length}'),
    ];
    return Row(
      children: [
        for (final (label, value) in cells)
          Expanded(
            child: Column(
              children: [
                Text(label, style: AppText.caption),
                Text(value, style: AppText.bodyStrong.copyWith(fontSize: 13)),
              ],
            ),
          ),
      ],
    );
  }
}

class _Facts extends StatelessWidget {
  const _Facts({required this.sensor});

  final TrkSensor sensor;

  @override
  Widget build(BuildContext context) {
    final s = sensor;
    final asset = s.assetName ?? s.assetCode;
    return SectionCard(
      title: 'Details',
      child: Column(
        children: [
          InfoRow(label: 'Sensor type', value: s.sensorType),
          InfoRow(label: 'Unit', value: s.unit ?? '—'),
          if (s.serialNumber != null) InfoRow(label: 'Serial number', value: s.serialNumber!),
          InfoRow(
            label: 'Linked device',
            value: [?s.deviceId, ?s.deviceModel, if (s.channel != null) 'ch ${s.channel}'].join(' · ').ifBlank,
          ),
          InfoRow(
            label: 'Sampling',
            value: s.samplingIntervalSeconds == null ? '—' : 'Every ${s.samplingIntervalSeconds} s',
          ),
          InfoRow(
            label: 'Alarm limits',
            value: s.lowAlarm == null && s.highAlarm == null
                ? '—'
                : '${readingText(s.lowAlarm, null).replaceAll('No data', '—')} – ${readingText(s.highAlarm, s.unit).replaceAll('No data', '—')}',
          ),
          InfoRow(label: 'Site', value: s.siteName ?? '—'),
          InfoRow(
            label: 'Calibration due',
            value: s.calibrationDueDate == null ? '—' : DateFormat('d MMM yyyy').format(s.calibrationDueDate!),
          ),
          if (asset != null || s.assetRegistrationId != null) ...[
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(child: Text('Asset: ${asset ?? s.assetRegistrationId}', style: AppText.bodyStrong)),
                if (s.assetRegistrationId != null) ...[
                  TextButton(
                    onPressed: () => context.push(Routes.trackAsset(s.assetRegistrationId!)),
                    child: const Text('Track'),
                  ),
                  TextButton(
                    onPressed: () => context.push(Routes.assetDetail(s.assetRegistrationId!)),
                    child: const Text('View asset'),
                  ),
                ],
              ],
            ),
          ],
        ],
      ),
    );
  }
}

extension on String {
  String get ifBlank => isEmpty ? '—' : this;
}

class _LatestReadings extends StatelessWidget {
  const _LatestReadings({required this.readings, required this.unit});

  /// Oldest first.
  final List<TrkReading> readings;
  final String? unit;

  @override
  Widget build(BuildContext context) {
    final latest = readings.reversed.take(10).toList();
    return SectionCard(
      title: 'Latest readings',
      child: latest.isEmpty
          ? Text('No readings in this period.', style: AppText.caption)
          : Column(
              children: [
                for (final r in latest)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            DateFormat('d MMM, HH:mm:ss').format(r.recordedAt.toLocal()),
                            style: AppText.body,
                          ),
                        ),
                        Text(readingText(r.value, r.unit ?? unit), style: AppText.bodyStrong),
                      ],
                    ),
                  ),
              ],
            ),
    );
  }
}

// ---------------------------------------------------------------- alerts

/// Alerts raised in this many days: the same window as GET /iot-alerts/count's default, so the
/// hub's badge and this list agree.
const alertWindowDays = 7;

/// API mode IoT alerts (Tracking service), read-only: Open (not Resolved) / All, severity chips.
/// Rows open the asset. The badge = /iot-alerts/count (unresolved, last 7 days).
class LiveTrackingAlertsScreen extends ConsumerStatefulWidget {
  const LiveTrackingAlertsScreen({super.key});

  @override
  ConsumerState<LiveTrackingAlertsScreen> createState() => _LiveTrackingAlertsScreenState();
}

class _LiveTrackingAlertsScreenState extends ConsumerState<LiveTrackingAlertsScreen> {
  int _tab = 0;
  String? _severity;

  @override
  Widget build(BuildContext context) {
    if (!apiMode) {
      return const AppScaffold(
        body: EmptyState(
          icon: Icons.warning_amber_rounded,
          title: 'IoT alerts need the backend',
          message: 'Run with USE_API=true to see Tracking service alerts.',
        ),
      );
    }
    final v = ref.watch(trkAlertsProvider(alertWindowDays));
    final count = ref.watch(trkLiveProvider.select((s) => s.alertCount));
    return AppScaffold(
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(trkAlertsProvider(alertWindowDays)),
        child: ListView(
          padding: context.pagePadding,
          children: [
            PageTitle(
              title: 'IoT Alerts',
              subtitle: 'Tracking alerts raised in the last $alertWindowDays days',
              leading: BackButton(onPressed: () => context.pop()),
            ),
            const SizedBox(height: 12),
            asyncBody(
              v,
              onRetry: () => ref.invalidate(trkAlertsProvider(alertWindowDays)),
              builder: (all) {
                final open = all.where((a) => a.isOpen).toList();
                final source = _tab == 0 ? open : all;
                final list = [
                  for (final a in source)
                    if (_severity == null || a.severity == _severity) a,
                ];
                final severities = {for (final a in all) a.severity}.where((s) => s.isNotEmpty).toList();
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    SegmentTabs(
                      tabs: [
                        SegmentTab('Open', count: open.length),
                        SegmentTab('All', count: all.length),
                      ],
                      selected: _tab,
                      onChanged: (i) => setState(() => _tab = i),
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 8,
                      children: [
                        IconFilterChip(
                          label: 'All severities',
                          selected: _severity == null,
                          onTap: () => setState(() => _severity = null),
                        ),
                        for (final s in severities)
                          IconFilterChip(
                            label: '$s (${source.where((a) => a.severity == s).length})',
                            selected: _severity == s,
                            onTap: () => setState(() => _severity = _severity == s ? null : s),
                          ),
                      ],
                    ),
                    if (count != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        '${count.total} raised in the last ${count.days} days'
                        '${count.changePercent == null ? '' : ' (${count.changePercent! >= 0 ? '+' : ''}${count.changePercent}% vs the previous period)'}',
                        style: AppText.caption,
                      ),
                    ],
                    const SizedBox(height: 10),
                    if (list.isEmpty)
                      AppCard(
                        child: EmptyState(
                          icon: Icons.check_circle_outline_rounded,
                          title: _tab == 0 ? 'No open alerts' : 'No alerts in the last $alertWindowDays days',
                        ),
                      )
                    else
                      AppCard(
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        child: Column(
                          children: [
                            for (final (i, a) in list.indexed) ...[
                              if (i > 0) const Divider(height: 1),
                              AlertRow(alert: a),
                            ],
                          ],
                        ),
                      ),
                    const SizedBox(height: 8),
                    Text(
                      'Read-only here; acknowledge / resolve alerts in the web IoT dashboard.',
                      textAlign: TextAlign.center,
                      style: AppText.caption,
                    ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
