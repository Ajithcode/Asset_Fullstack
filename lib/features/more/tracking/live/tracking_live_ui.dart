import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/models/common.dart' show TrendPoint;
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/home/dash_ui.dart' show dashTimeAgo;

/// Shared pieces of the API mode Tracking screens.

/// "Now" for live data: the device clock (server data is real time; DemoClock drives mock data only).
DateTime trackingNow() => DateTime.now().toUtc();

/// Keeps [trkLiveProvider] polling while this widget is on screen (its route on top and its
/// tab selected: [TickerMode]) and the app is in the foreground; stops it otherwise and on dispose.
class LivePollingScope extends ConsumerStatefulWidget {
  const LivePollingScope({super.key, required this.child});

  final Widget child;

  @override
  ConsumerState<LivePollingScope> createState() => _LivePollingScopeState();
}

class _LivePollingScopeState extends ConsumerState<LivePollingScope> {
  late final TrkLiveNotifier _live = ref.read(trkLiveProvider.notifier);
  late final AppLifecycleListener _lifecycle;
  ValueListenable<bool>? _ticker;
  bool _foreground = true;
  bool? _active;

  @override
  void initState() {
    super.initState();
    final s = WidgetsBinding.instance.lifecycleState;
    _foreground = s == null || s == AppLifecycleState.resumed;
    _lifecycle = AppLifecycleListener(
      onStateChange: (s) {
        _foreground = s == AppLifecycleState.resumed;
        _sync();
      },
    );
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final t = TickerMode.getNotifier(context);
    if (!identical(t, _ticker)) {
      _ticker?.removeListener(_sync);
      _ticker = t..addListener(_sync);
    }
    _sync();
  }

  void _sync() {
    final active = mounted && (_ticker?.value ?? true) && _foreground;
    if (active == _active) return;
    _active = active;
    // After the frame: providers must not change while widgets build.
    scheduleMicrotask(() => active ? _live.attach(this) : _live.detach(this));
  }

  @override
  void dispose() {
    _ticker?.removeListener(_sync);
    _lifecycle.dispose();
    final live = _live;
    scheduleMicrotask(() => live.detach(this));
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}

/// "Last seen 3 mins ago" (or "Never seen").
String lastSeenText(DateTime? at) => at == null ? 'Never seen' : 'Last seen ${dashTimeAgo(at)}';

/// Green = heard from in the last 10 min, grey = older, grey outline = never.
Color freshnessColor(Freshness f) => switch (f) {
  Freshness.live => AppColors.green,
  Freshness.stale => AppColors.grey,
  Freshness.unknown => AppColors.border,
};

/// Online (live) / Offline (stale or never seen) chip.
class FreshnessChip extends StatelessWidget {
  const FreshnessChip({super.key, required this.freshness});

  final Freshness freshness;

  @override
  Widget build(BuildContext context) => switch (freshness) {
    Freshness.live => const StatusChip('Online', tone: ChipTone.green),
    Freshness.stale => const StatusChip('Offline', tone: ChipTone.grey),
    Freshness.unknown => const StatusChip('No signal yet', tone: ChipTone.grey),
  };
}

/// The small "● Online · 🔋 78% · -62 dBm · 3 mins ago" line under a tracked asset.
class TrackedFactsLine extends StatelessWidget {
  const TrackedFactsLine({super.key, required this.asset, required this.now});

  final TrackedAsset asset;
  final DateTime now;

  @override
  Widget build(BuildContext context) {
    final f = asset.freshness(now);
    final battery = asset.battery;
    final signal = asset.signal;
    return Wrap(
      spacing: 10,
      runSpacing: 2,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.circle, size: 8, color: freshnessColor(f)),
            const SizedBox(width: 4),
            Text(f == Freshness.live ? 'Online' : 'Offline', style: AppText.caption),
          ],
        ),
        if (battery != null) BatteryLabel(percent: battery),
        if (signal != null) SignalLabel(health: signal),
        Text(asset.lastSeen == null ? 'Never seen' : dashTimeAgo(asset.lastSeen), style: AppText.caption),
      ],
    );
  }
}

class BatteryLabel extends StatelessWidget {
  const BatteryLabel({super.key, required this.percent});

  final int percent;

  @override
  Widget build(BuildContext context) {
    final low = percent <= lowBatteryPct;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          low ? Icons.battery_alert_rounded : Icons.battery_5_bar_rounded,
          size: 13,
          color: low ? AppColors.red : AppColors.green,
        ),
        Text(' $percent%', style: AppText.caption),
      ],
    );
  }
}

/// "-62 dBm" or the server's quality word ("Good").
class SignalLabel extends StatelessWidget {
  const SignalLabel({super.key, required this.health});

  final TrkDeviceHealth health;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      const Icon(Icons.signal_cellular_alt_rounded, size: 13, color: AppColors.iconGreen),
      Text(
        ' ${health.signalStrengthDbm != null ? '${health.signalStrengthDbm} dBm' : health.signalQuality}',
        style: AppText.caption,
      ),
    ],
  );
}

/// "Updated 10:24:36 AM · Live" bar under a live map; "Paused" when polling stopped, the error
/// with Retry when the last round failed.
class LiveUpdatedBar extends ConsumerWidget {
  const LiveUpdatedBar({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final s = ref.watch(trkLiveProvider);
    final at = s.updatedAt;
    final failed = s.error != null;
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
                  ? 'Loading live data…'
                  : 'Last updated: ${DateFormat('d MMM yyyy, hh:mm:ss a').format(at.toLocal())}',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppText.caption.copyWith(color: AppColors.text),
            ),
          ),
          if (failed)
            InkWell(
              onTap: () => ref.read(trkLiveProvider.notifier).refresh(),
              child: Text(
                'Retry',
                style: AppText.caption.copyWith(color: AppColors.red, fontWeight: FontWeight.w600),
              ),
            )
          else ...[
            Icon(Icons.graphic_eq_rounded, size: 16, color: s.polling ? AppColors.primary : AppColors.grey),
            const SizedBox(width: 3),
            Text(
              s.polling ? 'Live' : 'Paused',
              style: AppText.caption.copyWith(
                color: s.polling ? AppColors.primary : AppColors.grey,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// 1 h / 24 h / 7 d chips.
class RangeChips extends StatelessWidget {
  const RangeChips({super.key, required this.value, required this.onChanged});

  final TrackRange value;
  final ValueChanged<TrackRange> onChanged;

  @override
  Widget build(BuildContext context) => Wrap(
    spacing: 8,
    children: [
      for (final r in TrackRange.values)
        IconFilterChip(label: 'Last ${r.label}', selected: r == value, onTap: () => onChanged(r)),
    ],
  );
}

/// fl_chart line of readings (oldest first) over [range] ending now; x = minutes from the start.
class ReadingsChart extends StatelessWidget {
  const ReadingsChart({super.key, required this.readings, required this.range, this.unit = '', this.height = 180});

  final List<TrkReading> readings;
  final TrackRange range;
  final String unit;
  final double height;

  @override
  Widget build(BuildContext context) {
    if (readings.isEmpty) {
      return SizedBox(
        height: height,
        child: const EmptyState(icon: Icons.show_chart_rounded, title: 'No readings in this period'),
      );
    }
    final end = trackingNow();
    final start = end.subtract(range.duration);
    DateTime at(double x) => start.add(Duration(minutes: x.round())).toLocal();
    final fmt = range == TrackRange.week ? DateFormat('d MMM') : DateFormat('HH:mm');
    return SizedBox(
      height: height,
      child: TrendChart(
        points: [for (final r in readings) TrendPoint(r.recordedAt.difference(start).inSeconds / 60, r.value)],
        unit: unit,
        xInterval: range.duration.inMinutes / 4,
        xLabel: (x) => fmt.format(at(x)),
        tooltipTime: (x) => DateFormat('d MMM, HH:mm').format(at(x)),
      ),
    );
  }
}

/// A sensor's value with its unit ("42.1 °C"), or "No data".
String readingText(double? v, String? unit) {
  if (v == null) return 'No data';
  final n = v == v.roundToDouble() ? v.toStringAsFixed(0) : v.toStringAsFixed(v.abs() >= 100 ? 0 : 1);
  return unit == null || unit.isEmpty ? n : '$n $unit';
}

/// A server colour ("#2563EB") or [fallback].
Color hexColor(String? hex, Color fallback) {
  final h = (hex ?? '').replaceFirst('#', '').trim();
  final v = int.tryParse(h.length == 6 ? 'FF$h' : h, radix: 16);
  return v == null || h.length < 6 ? fallback : Color(v);
}

/// Icon + tone of a sensor status (Active / Offline / Attention / Inactive).
ChipTone sensorStatusTone(String? s) => switch (s) {
  TrkSensorStatus.active => ChipTone.green,
  TrkSensorStatus.attention => ChipTone.orange,
  TrkSensorStatus.offline => ChipTone.red,
  _ => ChipTone.grey,
};

ChipTone alertSeverityTone(String s) => switch (s) {
  'Critical' => ChipTone.red,
  'Warning' => ChipTone.orange,
  _ => ChipTone.blue,
};

/// Sensor type → icon.
IconData sensorTypeIcon(String type) => switch (type.toLowerCase()) {
  final t when t.contains('temp') => Icons.device_thermostat_rounded,
  final t when t.contains('humid') => Icons.water_drop_outlined,
  final t when t.contains('vibration') => Icons.vibration_rounded,
  final t when t.contains('pressure') => Icons.speed_rounded,
  final t when t.contains('current') => Icons.bolt_rounded,
  _ => Icons.sensors_rounded,
};

/// The section frame of a live list: shimmer, error + Retry, or [builder].
Widget asyncBody<T>(
  AsyncValue<T> v, {
  required VoidCallback onRetry,
  required Widget Function(T value) builder,
  int rows = 3,
}) {
  if (v.hasError && !v.isLoading) return LoadErrorState(error: v.error!, onRetry: onRetry);
  if (!v.hasValue) return ListSkeleton(rows: rows, card: false);
  return builder(v.requireValue);
}
