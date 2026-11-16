import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/app/theme/app_theme.dart';
import 'package:asset360_mobile/core/demo_clock.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/models/tracking.dart';

/// Icon and colour of a telemetry metric tile (design 06, right screen).
(IconData, Color) metricLook(String key) => switch (key) {
  'battery' => (Icons.battery_5_bar_rounded, AppColors.iconGreen),
  'hours' || 'runtime' => (Icons.schedule_rounded, AppColors.purple),
  'motorTemp' || 'temperature' => (Icons.device_thermostat_rounded, AppColors.red),
  'voltage' || 'energy' => (Icons.bolt_rounded, AppColors.iconOrange),
  'current' || 'vibration' => (Icons.monitor_heart_outlined, AppColors.purple),
  'load' => (Icons.scale_outlined, AppColors.iconBlue),
  'speed' || 'spindle' || 'feedRate' => (Icons.speed_rounded, AppColors.iconGreen),
  'location' => (Icons.location_on_rounded, AppColors.iconBlue),
  'signal' => (Icons.signal_cellular_alt_rounded, AppColors.iconGreen),
  'door' => (Icons.sensor_door_outlined, AppColors.iconOrange),
  _ => (Icons.sensors_rounded, AppColors.grey),
};

(IconData, Color) sensorLook(SensorKind k) => switch (k) {
  SensorKind.temperature => metricLook('temperature'),
  SensorKind.vibration => metricLook('vibration'),
  SensorKind.door => metricLook('door'),
  SensorKind.battery => metricLook('battery'),
};

/// Online / Offline chip with a dot.
class OnlineChip extends StatelessWidget {
  const OnlineChip({super.key, required this.online});

  final bool online;

  @override
  Widget build(BuildContext context) =>
      StatusChip(online ? 'Online' : 'Offline', tone: online ? ChipTone.green : ChipTone.red);
}

/// "Forklift FL-012 / Moved to Warehouse - Zone B / 1 min ago"; opens the asset when it has one.
class TrackActivityRow extends StatelessWidget {
  const TrackActivityRow({super.key, required this.activity});

  final TrackActivity activity;

  @override
  Widget build(BuildContext context) {
    final a = activity;
    final (icon, color) = switch (a.title) {
      final t when t.startsWith('Forklift') => (Icons.forklift, AppColors.iconOrange),
      final t when t.startsWith('Pallet') => (Icons.inventory_2_rounded, AppColors.iconBlue),
      final t when t.startsWith('Tool') => (Icons.warning_rounded, AppColors.red),
      _ => (Icons.person_rounded, AppColors.iconBlue),
    };
    return InkWell(
      onTap: () => a.assetId == null
          ? context.showSnack('${a.title}: ${a.detail} (${DemoClock.formatTime(a.at)})')
          : context.push(Routes.trackAsset(a.assetId!)),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 10),
        child: Row(
          children: [
            Icon(icon, color: color, size: 30),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    a.title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppText.bodyStrong.copyWith(fontSize: 13.5),
                  ),
                  const SizedBox(height: 2),
                  Text(a.detail, maxLines: 2, overflow: TextOverflow.ellipsis, style: AppText.caption),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Text(DemoClock.timeAgo(a.at), style: AppText.caption),
          ],
        ),
      ),
    );
  }
}

/// Which pin kinds and overlays the map shows.
class MapLayers {
  const MapLayers({
    this.people = true,
    this.equipment = true,
    this.vehicles = true,
    this.alerts = true,
    this.zones = false,
  });

  final bool people;
  final bool equipment;
  final bool vehicles;
  final bool alerts;
  final bool zones;

  bool shows(TrackedKind k) => switch (k) {
    TrackedKind.person => people,
    TrackedKind.equipment => equipment,
    TrackedKind.vehicle => vehicles,
    TrackedKind.alert => alerts,
  };

  MapLayers copyWith({bool? people, bool? equipment, bool? vehicles, bool? alerts, bool? zones}) => MapLayers(
    people: people ?? this.people,
    equipment: equipment ?? this.equipment,
    vehicles: vehicles ?? this.vehicles,
    alerts: alerts ?? this.alerts,
    zones: zones ?? this.zones,
  );
}

/// Layers sheet: toggles for People / Equipment / Vehicles / Alerts / Zones. Returns the new layers.
Future<MapLayers?> showLayersSheet(BuildContext context, MapLayers current) {
  return showModalBottomSheet<MapLayers>(
    context: context,
    isScrollControlled: true,
    builder: (context) {
      var layers = current;
      return StatefulBuilder(
        builder: (context, setState) {
          Widget row(IconData icon, Color color, String label, bool value, MapLayers Function(bool) apply) =>
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                secondary: Icon(icon, color: color),
                title: Text(label, style: AppText.bodyStrong),
                value: value,
                onChanged: (v) => setState(() => layers = apply(v)),
              );
          return sheetFrame(context, [
            const SheetTitle('Map Layers', subtitle: 'Choose what the floor plan shows'),
            row(Icons.person_rounded, AppColors.iconBlue, 'People', layers.people, (v) => layers.copyWith(people: v)),
            row(
              Icons.shopping_cart_rounded,
              AppColors.iconGreen,
              'Equipment',
              layers.equipment,
              (v) => layers.copyWith(equipment: v),
            ),
            row(Icons.forklift, AppColors.iconOrange, 'Vehicles', layers.vehicles, (v) => layers.copyWith(vehicles: v)),
            row(Icons.warning_rounded, AppColors.red, 'Alerts', layers.alerts, (v) => layers.copyWith(alerts: v)),
            row(
              Icons.fence_rounded,
              AppColors.primary,
              'Zones (geofences)',
              layers.zones,
              (v) => layers.copyWith(zones: v),
            ),
            const SizedBox(height: 12),
            FilledButton(onPressed: () => Navigator.pop(context, layers), child: const Text('Apply')),
          ]);
        },
      );
    },
  );
}

/// Signal bars for an RSSI value (4 = strong).
class SignalBars extends StatelessWidget {
  const SignalBars({super.key, required this.rssi, this.height = 18});

  final int rssi;
  final double height;

  int get bars => rssi >= -55
      ? 4
      : rssi >= -65
      ? 3
      : rssi >= -75
      ? 2
      : 1;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Signal $bars of 4',
      child: Row(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          for (var i = 1; i <= 6; i++)
            Container(
              width: 4,
              height: height * (.35 + .65 * i / 6),
              margin: const EdgeInsets.only(right: 3),
              decoration: BoxDecoration(
                color: i <= (bars * 1.5).round() ? AppColors.iconGreen : AppColors.border,
                borderRadius: BorderRadius.circular(1),
              ),
            ),
        ],
      ),
    );
  }
}

/// The big icon tabs of the Tracking hub (design 06, left screen): one tile per (value, label, icon).
class TrackingHubTabs<T> extends StatelessWidget {
  const TrackingHubTabs({super.key, required this.tabs, required this.selected, required this.onChanged});

  final List<(T, String, IconData)> tabs;
  final T selected;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        for (final (i, (value, label, icon)) in tabs.indexed) ...[
          if (i > 0) const SizedBox(width: 8),
          Expanded(
            child: Material(
              color: value == selected ? AppColors.tileLavender : AppColors.tileSky.withValues(alpha: .6),
              borderRadius: BorderRadius.circular(AppRadius.tile),
              child: InkWell(
                onTap: () => onChanged(value),
                borderRadius: BorderRadius.circular(AppRadius.tile),
                child: Container(
                  height: 64,
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(AppRadius.tile),
                    border: Border(
                      bottom: BorderSide(color: value == selected ? AppColors.primary : Colors.transparent, width: 2.5),
                    ),
                  ),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(icon, color: AppColors.primary, size: 24),
                      const SizedBox(height: 4),
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        child: Text(
                          label,
                          style: TextStyle(
                            fontSize: 11.5,
                            color: AppColors.text,
                            fontWeight: value == selected ? FontWeight.w600 : FontWeight.w500,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

/// A row of counter tiles (Online / Offline / Low Battery / Alerts): (value, icon, count, label,
/// tone). Tapping one selects it (outlined).
class CounterTiles<T> extends StatelessWidget {
  const CounterTiles({super.key, required this.items, required this.selected, required this.onTap});

  final List<(T, IconData, int, String, TileTone)> items;
  final T? selected;
  final ValueChanged<T> onTap;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        for (final (i, (c, icon, n, label, tone)) in items.indexed) ...[
          if (i > 0) const SizedBox(width: 6),
          Expanded(
            child: Material(
              color: tone.background,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
                side: BorderSide(color: selected == c ? tone.foreground : Colors.transparent, width: 1.5),
              ),
              child: InkWell(
                onTap: () => onTap(c),
                borderRadius: BorderRadius.circular(10),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 8),
                  child: Row(
                    children: [
                      Icon(icon, color: tone.foreground, size: 20),
                      const SizedBox(width: 4),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('$n', style: AppText.bodyStrong.copyWith(fontSize: 15, color: tone.foreground)),
                            FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Text(label, style: AppText.caption.copyWith(fontSize: 10.5)),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}
